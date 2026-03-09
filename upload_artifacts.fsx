#!/usr/bin/env -S dotnet fsi

#r "nuget: AWSSDK.S3, 4.0.18.5"
#r "nuget: DotNetEnv, 3.1.1"
#r "nuget: ShellProgressBar, 5.2.0"

open System.Linq
open System.Text.Json.Nodes
open ShellProgressBar
open System
open System.Diagnostics
open System.IO.Compression
open System.Net.Http
open System.Net.Mime
open System.Runtime.InteropServices
open System.IO
open Amazon.Runtime
open Amazon.S3
open Amazon.S3.Model

(* utility functions *)

let downloadAsStream (httpClient: HttpClient) (url: string) =
    task {
        let! response = httpClient.GetAsync(url)
        response.EnsureSuccessStatusCode() |> ignore
        return! response.Content.ReadAsStreamAsync()
    }
    |> Async.AwaitTask
    |> Async.RunSynchronously

let unzipStreamTo (stream: Stream) (outputDir: string) =
    Directory.CreateDirectory outputDir |> ignore

    use archive = new ZipArchive(stream, ZipArchiveMode.Read)

    for entry in archive.Entries do
        if not (String.IsNullOrEmpty entry.Name) then
            let destPath = Path.Combine(outputDir, entry.FullName)
            Directory.CreateDirectory(Path.GetDirectoryName(destPath)) |> ignore

            use entryStream = entry.Open()
            use fileStream = File.Create(destPath)
            entryStream.CopyTo fileStream

let makeExecutable (filePath: string) =
    File.SetUnixFileMode(
        filePath,
        UnixFileMode.UserRead
        ||| UnixFileMode.UserWrite
        ||| UnixFileMode.UserExecute
        ||| UnixFileMode.GroupRead
        ||| UnixFileMode.GroupExecute
        ||| UnixFileMode.OtherRead
        ||| UnixFileMode.OtherExecute
    )

let runCommand (cmd: string) (args: string) =
    let psi = ProcessStartInfo(cmd, args)
    psi.UseShellExecute <- false
    let proc = Process.Start(psi)
    proc.WaitForExit()

let s3ListObjectAsync (s3: #IAmazonS3) (bucket: string) (prefix: string option) =
    task {
        let req =
            ListObjectsV2Request(BucketName = bucket, Prefix = (prefix |> Option.defaultValue null))

        let! resp = s3.ListObjectsV2Async(req)

        return
            match resp.S3Objects with
            | null -> Seq.empty<S3Object>
            | obj -> obj :> seq<S3Object>
    }

let removeStart (prefix: string) (s: string) =
    if s.StartsWith(prefix) then
        s.Substring(prefix.Length)
    else
        s

let getSongId (filePath: string) =
    Path.GetFileNameWithoutExtension filePath |> removeStart "lowiro."

let getKey (filePath: string) = $"arcpkgs/{getSongId filePath}.arcpkg"

let s3PutObjectAsync (s3: #IAmazonS3) (bucket: string) (filePath: string) =
    task {
        let key = getKey filePath

        use fileStream = File.OpenRead filePath

        let req =
            PutObjectRequest(
                BucketName = bucket,
                Key = key,
                InputStream = fileStream,
                ContentType = MediaTypeNames.Application.Zip,
                DisablePayloadSigning = true
            )

        let! resp = s3.PutObjectAsync req

        let isSuccess httpStatusCode =
            let code = int httpStatusCode
            code >= 200 && code < 300

        if not (isSuccess resp.HttpStatusCode) then
            failwith $"unable to upload '{filePath}' to s3, resp = {resp.HttpStatusCode}"
    }

let createDirectoryIfNotExist (dirPath: string) =
    if not (Path.Exists dirPath) then
        Directory.CreateDirectory(dirPath) |> ignore

(* configurations *)

let etoileConfig =
    {| Release = "v2.1.5"
       Version = "EtoileResurrection.Console-universal-a4dcc68"
       DownloadPath = "EtoileResurrection.zip"
       ExtractPath = "."
       PackagePrefix = "lowiro" |}

let expandFilePath (ext: string) =
    Path.Combine [| "fragments-category"; "songs"; ext |] |> Path.GetFullPath

let filePaths =
    {| Songlist = expandFilePath "songlist"
       SonglistApril = expandFilePath "songlist_aprilfools"
       Packlist = expandFilePath "packlist"
       PacklistApril = expandFilePath "packlist_aprilfools"
       OutputPath = "arcpkgs" |}

DotNetEnv.Env.Load()

let s3Config =
    {| Api = Environment.GetEnvironmentVariable "S3_API"
       AccessKey = Environment.GetEnvironmentVariable "S3_ACCESS_KEY"
       SecretAccessKey = Environment.GetEnvironmentVariable "S3_SECRET_ACCESS_KEY"
       BucketName = Environment.GetEnvironmentVariable "S3_BUCKET" |}

(* validation *)

if
    not (
        Path.Exists filePaths.Songlist
        && Path.Exists filePaths.SonglistApril
        && Path.Exists filePaths.Packlist
        && Path.Exists filePaths.PacklistApril
    )
then
    raise (FileNotFoundException "`fragments-category` not found")

if
    String.IsNullOrWhiteSpace s3Config.Api
    || String.IsNullOrWhiteSpace s3Config.AccessKey
    || String.IsNullOrWhiteSpace s3Config.SecretAccessKey
    || String.IsNullOrWhiteSpace s3Config.BucketName
then
    raise (ArgumentNullException "s3 not configured")

createDirectoryIfNotExist filePaths.OutputPath

(* main *)

let s3 =
    new AmazonS3Client(
        BasicAWSCredentials(accessKey = s3Config.AccessKey, secretKey = s3Config.SecretAccessKey),
        AmazonS3Config(ServiceURL = s3Config.Api, ForcePathStyle = true)
    )

let localSongIds =
    let parseSongList filePath =
        (File.ReadAllText filePath
         |> JsonValue.Parse
         |> fun j -> j["songs"].AsArray().ToArray())

    [ parseSongList "fragments-category/songs/songlist"
      parseSongList "fragments-category/songs/songlist_aprilfools" ]
    |> Array.concat
    |> Array.filter (fun x ->
        not (
            match x["deleted"] with
            | null -> false
            | value ->
                match value.AsValue().TryGetValue<bool>() with
                | true, s -> s
                | _ -> false
        ))
    |> Array.map (fun x -> x["id"].GetValue<string>())
    |> List.ofArray

let remoteSongIds =
    s3ListObjectAsync s3 s3Config.BucketName None
    |> Async.AwaitTask
    |> Async.RunSynchronously
    |> Seq.map (fun x -> getSongId x.Key)
    |> List.ofSeq

let songsToBePacked =
    localSongIds |> List.filter (fun x -> not (remoteSongIds.Contains(x)))

let songIdPattern = "(" + String.Join("|", songsToBePacked) + ")"

if
    Environment.GetEnvironmentVariable("SKIP_PACK") |> String.IsNullOrWhiteSpace
    && not songsToBePacked.IsEmpty
then
    let httpClient = new HttpClient()

    let etoile =
        {| Url = $"https://github.com/freeze-dolphin/EtoileResurrection/releases/download/{etoileConfig.Release}/{etoileConfig.Version}.zip"
           BinPath = $"{etoileConfig.ExtractPath}/{etoileConfig.Version}/bin/EtoileResurrection" |}

    // download étoile
    if not (Path.Exists etoile.BinPath) then
        downloadAsStream httpClient etoile.Url |> unzipStreamTo
        <| etoileConfig.ExtractPath

    // make étoile script runnable on unix
    if not (RuntimeInformation.IsOSPlatform OSPlatform.Windows) then
        makeExecutable etoile.BinPath

    // run étoile
    runCommand
        etoile.BinPath
        $"pack {filePaths.Songlist} --songId={songIdPattern} -re --prefix={etoileConfig.PackagePrefix} -o {filePaths.OutputPath} -j{Environment.ProcessorCount}"

    runCommand
        etoile.BinPath
        $"pack {filePaths.SonglistApril} --songId={songIdPattern} -re --prefix={etoileConfig.PackagePrefix} -o {filePaths.OutputPath} -j1"
else
    printfn "no package is being packed"

let arcpkgPaths =
    songsToBePacked
    |> List.map (fun x -> Path.Combine(filePaths.OutputPath, $"{etoileConfig.PackagePrefix}.{x}.arcpkg"))

if arcpkgPaths.IsEmpty then
    printfn "no package needs to be uploaded"
else
    printfn $"uploading {arcpkgPaths.Length} package(s)"

    let bar = new ProgressBar(arcpkgPaths.Length, String.Empty)

    for arcpkgPath in arcpkgPaths do
        s3PutObjectAsync s3 s3Config.BucketName arcpkgPath
        |> Async.AwaitTask
        |> Async.RunSynchronously

        bar.Tick($"Uploaded: {arcpkgPath}")

    bar.Dispose()
