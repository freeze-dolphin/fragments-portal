#!/usr/bin/env -S dotnet fsi

// ReSharper disable FSharpRedundantDotInIndexer

#r "nuget: Falco.Markup, 1.4.0"
#r "nuget: LibGit2Sharp, 0.31.0"
#r "nuget: SixLabors.ImageSharp, 3.1.12"
#r "nuget: ShellProgressBar, 5.2.0"

open System
open System.IO
open System.Linq
open System.Text.Json.Nodes
open Falco.Markup
open Falco.Markup.Attr
open LibGit2Sharp
open ShellProgressBar
open SixLabors.ImageSharp
open SixLabors.ImageSharp.Processing

(* utility functions *)

let tryGetLatestVersionMessage (repo: Repository) =
    let mutable found = None

    repo.Commits
    |> Seq.takeWhile (fun _ -> found.IsNone)
    |> Seq.tryPick (fun commit ->
        let msg = commit.Message.TrimStart()

        if msg.StartsWith "#" then
            let trimmed = msg.TrimStart([| '#'; ' '; '\t' |]).Trim()

            if trimmed.Length > 0 then
                found <- Some trimmed
                Some trimmed
            else
                None
        else
            None)
    |> ignore

    found

let getHeadMessage (repo: Repository) =
    match repo.Head with
    | null -> None
    | head ->
        match head.Tip with
        | null -> None
        | commit -> Some(commit.Message.TrimEnd([| '\r'; '\n' |]))

let getCombinedMessage (repoPath: string) =
    use repo = new Repository(repoPath)

    match getHeadMessage repo with
    | None -> ""
    | Some headMsg ->
        match tryGetLatestVersionMessage repo with
        | None -> headMsg
        | Some versionMsg -> headMsg + " [" + versionMsg.TrimStart '#' + "]"

(* page template functions*)

let SimpleAnalyticsEmbedded =
    _p
        [ _align_ "center" ]
        [ _a
              [ _href_ "https://dashboard.simpleanalytics.com/freeze-dolphin.github.io"
                _referrerpolicy_ "origin"
                _target_ "_blank" ]
              [ _img
                    [ _src_ "https://simpleanalyticsbadges.com/freeze-dolphin.github.io?mode=light"
                      _loading_ "lazy"
                      _referrerpolicy_ "no-referrer"
                      _crossorigin_ "anonymous" ] ] ]

let BuildTime (dateTime: DateTime option) =
    _h3
        []
        [ _text (
              "Build Time: "
              + (match dateTime with
                 | None -> DateTime.UtcNow
                 | Some time -> time)
                  .ToString("u")
          ) ]

let CommitMessage repoPath =
    seq {
        _h3 [] [ _text "Latest commit message:" ]
        _blockquote [] [ _text (getCombinedMessage repoPath) ]
    }

let PageTemplate repoPath (songMatrixes: seq<XmlNode>) =
    _html
        []
        [ _head
              []
              [ _meta [ _charset_ "UTF-8" ]
                _meta [ _name_ "viewport"; _content_ "width=device-width, initial-scale=1.0" ]
                _title [] [ _text "fragments-portal" ]
                _script [ _async_; _src_ "https://scripts.simpleanalyticscdn.com/latest.js" ] []
                _style
                    []
                    [ _text "table { table-layout: fixed; border-collapse: collapse; }"
                      _text "td { width: 120px; height: auto; overflow: hidden; white-space: nowrap; text-overflow: clip; }"
                      _text "td table { width: 100%; }"
                      _text ".songs td table tr:nth-child(2) td { font-size: 12px; line-height: 1.2; white-space: nowrap; }"
                      _text "img { max-width: 100%; height: auto; }" ] ]
          _body
              []
              [ _h1 [] [ _text "fragments-portal" ]
                BuildTime None
                yield! CommitMessage repoPath
                _div [ _class_ "songs" ] [ yield! songMatrixes ]
                SimpleAnalyticsEmbedded ] ]

type SongInfo =
    { JacketUrl: string
      Title: string
      DownloadUrl: string }

let SongCell songInfo =
    _a
        [ href songInfo.DownloadUrl ]
        [ _table
              []
              [ _tbody
                    [ align "center" ]
                    [ _tr [] [ _td [] [ _img [ src songInfo.JacketUrl; width "90"; decoding "async"; loading "lazy" ] ] ]
                      _tr [] [ _td [] [ _text songInfo.Title ] ] ] ] ]

let SongMatrix (matrixWidth: int) fillByEmpty groupTitle (songs: list<SongInfo>) =
    seq {
        _table
            [ width "100%"; border "0" ]
            [ _tr
                  []
                  [ _td [ width "40"; align "left" ] [ _h3 [ style "padding-left: 8px" ] [ _text groupTitle ] ]
                    _td [ align "center" ] [ _h3 [ style "color:#999999" ] [ _text groupTitle ] ]
                    _td [ width "40"; align "right" ] [ _h3 [ style "padding-right: 8px" ] [ _text groupTitle ] ] ] ]

        _table
            [ border "1"; align "center" ]
            [ _tbody
                  []
                  [ for chunk in List.chunkBySize matrixWidth songs do
                        _tr
                            []
                            [ for song in chunk do
                                  _td [] [ SongCell song ]

                              if fillByEmpty then
                                  for _ in chunk.Length .. matrixWidth - 1 do
                                      _td [] [] ] ] ]
    }

(* main *)

let songMetaList =
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
    |> Array.map (fun x ->
        {| Title = x.["title_localized"].["en"].GetValue<string>()
           Id = x.["id"].GetValue<string>() |})
    |> List.ofArray

let songMatrixes width =
    songMetaList
    |> List.groupBy (fun x ->
        let cap = x.Title.ToUpper()[0]
        if 'A' <= cap && cap <= 'Z' then cap else '#')
    |> List.sortBy (fun (cap, _) ->
        match cap with
        | '#' -> 1000
        | c -> int c)
    |> List.map (fun (cap, songMeta) ->
        let songInfos =
            songMeta
            |> List.map (fun y ->
                { JacketUrl = $"https://freeze-dolphin.github.io/fragments-portal/thumbnails/{y.Id}.jpg"
                  Title = y.Title
                  DownloadUrl = $"https://pub-748f36e6cae345198861f65a9a8f5218.r2.dev/arcpkgs/{y.Id}.arcpkg" })

        SongMatrix width false $"{cap}" songInfos)
    |> Seq.collect (fun x -> x)

if not (Path.Exists "songdoc/thumbnails") then
    Directory.CreateDirectory "songdoc/thumbnails" |> ignore

// generate index.html
PageTemplate "fragments-category" (songMatrixes 7)
|> renderHtml
|> (fun x -> File.WriteAllText("songdoc/index.html", x))

let getJacketPath songId =
    if (Path.Exists $"fragments-category/songs/{songId}/1080_base.jpg") then
        "1080_base.jpg"
    else if (Path.Exists $"fragments-category/songs/{songId}/base.jpg") then
        "base.jpg"
    else
        failwith $"unable to detect jacket path for {songId}"

// generate thumbnails
let thumbnailMetaList =
    songMetaList
    |> List.map (fun x ->
        {| JacketPath = Path.GetFullPath($"fragments-category/songs/{x.Id}/{getJacketPath x.Id}")
           ThumbnailPath = Path.GetFullPath($"songdoc/thumbnails/{x.Id}.jpg") |})

let bar = new ProgressBar(thumbnailMetaList.Length, String.Empty)

for thumbnailMeta in thumbnailMetaList do
    use jacket = Image.Load thumbnailMeta.JacketPath

    jacket.Mutate(fun ctx -> ctx.Resize(110, 110) |> ignore)
    jacket.Save(thumbnailMeta.ThumbnailPath)

    bar.Tick("Generated thumbnail: " + Path.GetRelativePath(".", thumbnailMeta.ThumbnailPath))

bar.Dispose()