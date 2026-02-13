#!/usr/bin/env -S dotnet fsi

#r "nuget: AWSSDK.S3, 4.0.18.5"
#r "nuget: DotNetEnv, 3.1.1"
#r "nuget: ShellProgressBar, 5.2.0"

open ShellProgressBar
open System
open System.Diagnostics
open System.IO.Compression
open System.Net
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

let s3ObjectExistsAsync (s3: #IAmazonS3) (bucket: string) (key: string) =
    task {
        let req = GetObjectMetadataRequest(BucketName = bucket, Key = key)

        try
            let! _ = s3.GetObjectMetadataAsync req
            return true
        with :? AmazonS3Exception as ex when ex.StatusCode = HttpStatusCode.NotFound ->
            return false
    }

let removeStart (prefix: string) (s: string) =
    if s.StartsWith(prefix) then
        s.Substring(prefix.Length)
    else
        s

let s3PutObjectAsync (s3: #IAmazonS3) (bucket: string) (filePath: string) (skipIfExist: bool) =
    task {
        let songId = Path.GetFileNameWithoutExtension filePath |> removeStart "lowiro."

        let key = $"arcpkgs/{songId}"

        if skipIfExist then
            let! exists = s3ObjectExistsAsync s3 bucket key

            if exists then
                return ()

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

let listArcpkgFiles (directoryPath: string) : string seq =
    Directory.EnumerateFiles(directoryPath, "*.arcpkg", SearchOption.TopDirectoryOnly)

let createDirectoryIfNotExist (dirPath: string) =
    if not (Path.Exists dirPath) then
        Directory.CreateDirectory(dirPath) |> ignore

(* configurations *)

let etoileConfig =
    {| Release = "v1.0.9"
       Version = "EtoileResurrection-4c159e3"
       DownloadPath = "EtoileResurrection.zip"
       ExtractPath = "."
       PackagePrefix = "lowiro" |}

let expandFilePath (ext: string) =
    Path.Combine [| ".."; "fragments-category"; "songs"; ext |] |> Path.GetFullPath

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

createDirectoryIfNotExist filePaths.OutputPath

(* main *)

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
if Environment.GetEnvironmentVariable("SKIP_PACK") |> String.IsNullOrWhiteSpace then
    runCommand
        etoile.BinPath
        $"pack {filePaths.Songlist} --songId=.* -re --prefix={etoileConfig.PackagePrefix} -o {filePaths.OutputPath} -j{Environment.ProcessorCount}"

    runCommand
        etoile.BinPath
        $"pack {filePaths.SonglistApril} --songId=.* -re --prefix={etoileConfig.PackagePrefix} -o {filePaths.OutputPath} -j{Environment.ProcessorCount}"

// upload to r2

let s3 =
    new AmazonS3Client(
        BasicAWSCredentials(accessKey = s3Config.AccessKey, secretKey = s3Config.SecretAccessKey),
        AmazonS3Config(ServiceURL = s3Config.Api, ForcePathStyle = true)
    )

let arcpkgPaths = listArcpkgFiles filePaths.OutputPath |> List.ofSeq

printfn $"Total packages: {arcpkgPaths.Length}"

let bar = new ProgressBar(arcpkgPaths.Length, String.Empty)

for arcpkgPath in arcpkgPaths do
    bar.Tick($"Uploading: {arcpkgPath}")

    s3PutObjectAsync s3 s3Config.BucketName arcpkgPath true
    |> Async.AwaitTask
    |> Async.RunSynchronously
