// organize_dl.fsx
// Sort files from the dl folder into the matching songId folders
// next to this script.
//
// Rules:
//   dl/<songId>          -> <dest>/<songId>/base.ogg
//   dl/<songId>_<digits> -> <dest>/<songId>/<digits>.aff
//   anything else is ignored
//
// Existing target files are overwritten. By default files are MOVED
// (copied first, then the source is deleted). Use --copy to keep the
// originals in dl, or --dry-run to preview without changing anything.
//
// Line endings: after an .aff file is written to its destination, it is
// normalized to LF when it contains CRLF or lone CR line endings
// (dos2unix-style). Binary files such as base.ogg are never touched.
//
// Usage:
//   dotnet fsi organize_dl.fsx -- --help      # help
//   dotnet fsi organize_dl.fsx                # move
//   dotnet fsi organize_dl.fsx -- --copy      # copy only
//   dotnet fsi organize_dl.fsx -- --dry-run   # preview only

open System
open System.IO

try
    Console.OutputEncoding <- Text.Encoding.UTF8
with _ -> ()

(* Command line arguments *)
let argv = fsi.CommandLineArgs |> Array.skip 1 |> List.ofArray

let mutable dlPath = null
let mutable destPath = null
let mutable copyOnly = false
let mutable dryRun = false

let rec parseArgs = function
    | "--dl" :: v :: rest -> dlPath <- v; parseArgs rest
    | "--dest" :: v :: rest -> destPath <- v; parseArgs rest
    | "--copy" :: rest -> copyOnly <- true; parseArgs rest
    | "--dry-run" :: rest -> dryRun <- true; parseArgs rest
    | "--help" :: _ | "-h" :: _ ->
        printfn "Usage: dotnet fsi organize_dl.fsx [--dl <dir>] [--dest <dir>] [--copy] [--dry-run]"
        printfn "  --dl       Path to the `dl` folder"
        printfn "  --dest     Path to the folder containing the songId folders"
        printfn "  --copy     Copy only; keep source files (default is move)"
        printfn "  --dry-run  Preview only; make no changes"
        exit 0
    | unknown :: _ -> failwithf "Unknown argument: %s" unknown
    | [] -> ()

parseArgs argv

if not (Directory.Exists dlPath) then
    failwithf "Error: source folder does not exist: %s" dlPath
if not (Directory.Exists destPath) then
    failwithf "Error: destination folder does not exist: %s" destPath

(* Classification *)
let songIds =
    Directory.EnumerateDirectories destPath
    |> Seq.map Path.GetFileName
    |> Set.ofSeq

/// Returns Some (songId, target file name), or None if it cannot be classified.
let classify (name: string) : (string * string) option =
    let idx = name.LastIndexOf '_'
    if idx > 0 then
        let basePart = name.Substring(0, idx)
        let suffix = name.Substring(idx + 1)
        if suffix.Length > 0 && suffix |> Seq.forall Char.IsDigit && Set.contains basePart songIds then
            Some(basePart, suffix + ".aff")
        elif Set.contains name songIds then
            Some(name, "base.ogg")
        else None
    elif Set.contains name songIds then
        Some(name, "base.ogg")
    else None

(* Line-ending normalization (dos2unix-style) *)

/// True when the file contains a CR byte (CRLF or lone CR line ending).
let containsCR (path: string) : bool =
    use fs = new FileStream(path, FileMode.Open, FileAccess.Read)
    let buf = Array.zeroCreate<byte> 65536
    let mutable found = false
    let mutable reading = true
    while reading && not found do
        let n = fs.Read(buf, 0, buf.Length)
        if n = 0 then
            reading <- false
        else
            let mutable i = 0
            while i < n && not found do
                if buf.[i] = 13uy then found <- true
                i <- i + 1
    found

/// dos2unix-like: rewrite `path`, converting CRLF and lone CR to LF.
/// Returns true if the content was modified.
let toLf (path: string) : bool =
    let bytes = File.ReadAllBytes path
    let mutable changed = false
    use out = new MemoryStream(bytes.Length)
    let mutable i = 0
    while i < bytes.Length do
        if bytes.[i] = 13uy then
            changed <- true
            out.WriteByte 10uy // emit one LF
            if i + 1 < bytes.Length && bytes.[i + 1] = 10uy then
                i <- i + 1 // CRLF: skip the LF; lone CR: nothing to skip
        else
            out.WriteByte bytes.[i]
        i <- i + 1
    if changed then
        File.WriteAllBytes(path, out.ToArray())
    changed

(* Scan and classify *)
let planned = ResizeArray<string * string * string * string * bool>() // name, source, destination, note, needsLf
let skipped = ResizeArray<string * string>()

for srcPath in Directory.EnumerateFiles dlPath |> Seq.sort do
    let name = Path.GetFileName srcPath
    match classify name with
    | Some(song, target) ->
        let dst = Path.Combine(destPath, song, target)
        let note = if File.Exists dst then "overwrite" else "new"
        let needsLf =
            target.EndsWith(".aff", StringComparison.OrdinalIgnoreCase)
            && containsCR srcPath
        planned.Add(name, srcPath, dst, note, needsLf)
    | None ->
        skipped.Add(name, "name does not match any songId folder or hits ignored patterns like _video")

if planned.Count = 0 then
    printfn "No files to classify."
    exit 0

let action = if dryRun then "Preview" elif copyOnly then "Copy" else "Move"
printfn "[%s] %d file(s):" action planned.Count
printfn ""
for name, _, dst, note, _ in planned do
    printfn "  %-36s -> %s  (%s)" name (Path.GetRelativePath(destPath, dst)) note

if dryRun then
    printfn ""
    let lfList =
        planned
        |> Seq.filter (fun (_, _, _, _, needsLf) -> needsLf)
        |> Seq.map (fun (_, _, dst, _, _) -> Path.GetRelativePath(destPath, dst))
        |> Seq.toList
    if lfList.Length > 0 then
        printfn "%d .aff file(s) contain CRLF/CR endings and would be converted to LF:" lfList.Length
        for p in lfList do
            printfn "  %s" p
        printfn ""
    printfn "Preview finished, no changes were made. %d file(s) ignored." skipped.Count
    exit 0

(* Execute *)
let mutable movedCount = 0
let converted = ResizeArray<string>()
for _, src, dst, _, needsLf in planned do
    Directory.CreateDirectory(Path.GetDirectoryName dst) |> ignore
    File.Copy(src, dst, true) // overwrite existing target files
    if needsLf && toLf dst then
        converted.Add(Path.GetRelativePath(destPath, dst))
    if not copyOnly then
        File.Delete src
        movedCount <- movedCount + 1

printfn ""
if copyOnly then
    printfn "Done: %d file(s) copied, originals kept in dl." planned.Count
else
    printfn "Done: %d file(s) moved (copied, then source deleted)." movedCount
if converted.Count > 0 then
    printfn "%d .aff file(s) had non-LF line endings and were normalized to LF:" converted.Count
    for p in converted do
        printfn "  %s" p
if skipped.Count > 0 then
    printfn "%d file(s) ignored:" skipped.Count
    for name, _ in skipped do
        printfn "  %s" name
