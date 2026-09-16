#!/usr/bin/env -S dotnet fsi

// ReSharper disable FSharpRedundantDotInIndexer

#r "nuget: Falco.Markup, 1.4.0"
#r "nuget: LibGit2Sharp, 0.31.0"
#r "nuget: SixLabors.ImageSharp, 3.1.12"
#r "nuget: NUglify, 1.23.0"

open System
open System.IO
open System.Linq
open System.Net
open System.Text.Json.Nodes
open Falco.Markup
open Falco.Markup.Attr
open LibGit2Sharp
open SixLabors.ImageSharp
open SixLabors.ImageSharp.Processing
open NUglify

(* utility functions *)

let themeCss =
    try
        File.ReadAllText(Path.Combine(__SOURCE_DIRECTORY__, "songdoc_theme.css"))
        |> Uglify.Css
        |> _.Code
    with _ ->
        ""

let buildTimeIso = DateTime.UtcNow.ToString("o")

let relativeTimeScript = (* language=javascript *)
    $"""(function(){{
  let iso = '{buildTimeIso}';
  function rel(iso){{
    let d=new Date(iso), now=new Date(), s=Math.floor((now-d)/1000);
    if(s<60) return "just now";
    if(s<3600) return Math.floor(s/60) + " minutes ago";
    if(s<86400) return Math.floor(s/3600) + " hours ago";
    return "previously";
  }}
  function fmtLocal(iso){{
    let d=new Date(iso); return d.toLocaleString();
  }}
  function render(){{
    let root = document.getElementById('build-time-root'); if(!root) return;
    root.innerHTML = '';
    let h3 = document.createElement('h3'); h3.className = 'meta';
    h3.appendChild(document.createTextNode('Built'));
    let strong = document.createElement('strong'); strong.textContent = rel(iso); h3.appendChild(strong);
    h3.appendChild(document.createTextNode(' • '));
    let span = document.createElement('span'); span.className = 'build-time'; span.setAttribute('title', iso); span.textContent = fmtLocal(iso); h3.appendChild(span);
    root.appendChild(h3);
  }}
  function update(){{
    let root = document.getElementById('build-time-root'); if(!root) return;
    let strong = root.querySelector('strong');
    let span = root.querySelector('.build-time');
    if(!strong || !span) {{ render(); return; }}
    strong.textContent = rel(iso);
    span.textContent = fmtLocal(iso);
  }}
  function init(){{ try{{ render(); update(); setInterval(update, 60000); }}catch(e){{}} }}
  if (document.readyState === 'loading') {{ document.addEventListener('DOMContentLoaded', init); }} else {{ init(); }}
}})();
"""
    |> Uglify.Js
    |> _.Code

let tryGetLatestVersionMessage (repo: Repository) =
    let mutable found = None

    repo.Commits
    |> Seq.takeWhile (fun _ -> found.IsNone)
    |> Seq.tryPick (fun commit ->
        let msg = commit.Message.TrimStart()

        if msg.StartsWith "#" then
            let trimmed = msg.TrimStart('#').TrimEnd([| '\r'; '\n' |])

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

let getCombinedMessage (portalRepoPath: string) (categoryRepoPath: string) =
    use portalRepo = new Repository(portalRepoPath)
    use categoryRepo = new Repository(categoryRepoPath)

    match getHeadMessage portalRepo with
    | None -> ""
    | Some headMsg ->
        match tryGetLatestVersionMessage categoryRepo with
        | None -> headMsg
        | Some versionMsg ->
            if (headMsg.TrimStart '#' = versionMsg) then
                versionMsg
            else
                $"<b>[{versionMsg}]</b> " + headMsg

(* page template functions *)

let CommitMessage portalRepoPath categoryRepoPath =
    seq {
        let versionOpt, headOpt =
            use portalRepo = new Repository(portalRepoPath)
            use categoryRepo = new Repository(categoryRepoPath)
            let h = getHeadMessage portalRepo
            let v = tryGetLatestVersionMessage categoryRepo
            v, h

        let versionText =
            match versionOpt with
            | Some v -> v
            | None -> ""

        let headText =
            match headOpt with
            | Some h -> h
            | None -> ""

        let messageText =
            if versionText <> "" && headText.TrimStart '#' = versionText then
                ""
            else
                headText

        _blockquote
            [ _class_ "commit-message" ]
            ((if versionText <> "" then
                  [ _div [ _class_ "commit-head" ] [ _text versionText ] ]
              else
                  [])
             @ (if messageText <> "" then
                    [ _div [ _class_ "commit-body" ] [ _text messageText ] ]
                else
                    []))
    }

let SimpleAnalyticsBadge =
    _a
        [ href "https://dashboard.simpleanalytics.com/freeze-dolphin.github.io"
          _target_ "_blank"
          _referrerpolicy_ "origin" ]
        [ _img
              [ src
                    $"""
https://img.shields.io/badge/dynamic/json
?url={WebUtility.UrlEncode "https://simpleanalytics.com/freeze-dolphin.github.io.json?version=6&fields=visitors&start=today-30d&end=yesterday"}
&query={WebUtility.UrlEncode "$.visitors"}
&logo={WebUtility.UrlEncode "simpleanalytics"}
&label={WebUtility.UrlEncode "Monthly visitors"}
&color={WebUtility.UrlEncode "#FF4F64"}
&labelColor={WebUtility.UrlEncode "#30363D"}
&style={WebUtility.UrlEncode "for-the-badge"}
"""
                // _loading_ "lazy"
                _referrerpolicy_ "no-referrer"
                _crossorigin_ "anonymous" ] ]

let FooterNote =
    _div
        [ _class_ "footer-note" ]
        [ _text "Powered by: "
          _a [ href "https://github.com/freeze-dolphin/Etoile.Lite" ] [ _text "Etoile.Lite" ]
          _text " • "
          _text "Web design by: "
          _a [ href "https://github.com/WhiteNightAWA" ] [ _text "WhiteNightAWA" ] ]

let PageTemplate portalRepoPath categoryRepoPath (songMatrixes: seq<XmlNode>) =
    _html
        []
        [ _head
              []
              [ _meta [ _charset_ "UTF-8" ]
                _meta [ _name_ "viewport"; _content_ "width=device-width, initial-scale=1.0" ]
                _title [] [ _text "fragments-portal" ]
                _script [ _async_; _src_ "https://scripts.simpleanalyticscdn.com/latest.js" ] []
                _script [] [ _text relativeTimeScript ]
                _style [] [ _text themeCss ] ]
          _body
              []
              [ _div
                    [ _class_ "header" ]
                    [ _div
                          [ _class_ "header-main" ]
                          [ _h1
                                []
                                [ _text "fragments-portal "
                                  _a
                                      [ href "https://github.com/freeze-dolphin/fragments-portal" ]
                                      [ _img [ src "https://img.shields.io/github/stars/freeze-dolphin/fragments-portal" ] ] ]
                            _div [ _id_ "build-time-root" ] [] ] ]
                yield! CommitMessage portalRepoPath categoryRepoPath
                _div [ _class_ "songs" ] [ yield! songMatrixes ]
                _footer
                    [ _class_ "site-footer" ]
                    [ _div [ _class_ "footer-left" ] [ SimpleAnalyticsBadge ]
                      _div [ _class_ "footer-right" ] [ FooterNote ] ] ] ]

type SongInfo =
    { JacketUrl: string
      Title: string
      DownloadUrl: string }

let SongCell songInfo =
    _a
        [ href songInfo.DownloadUrl; _class_ "song-card" ]
        [ _img [ src songInfo.JacketUrl; width "90"; decoding "async"; loading "lazy" ]
          _span [ _class_ "song-title" ] [ _span [ _class_ "song-title-inner" ] [ _text songInfo.Title ] ] ]

let SongMatrix groupTitle (songs: list<SongInfo>) =
    seq {
        _div [ _class_ "group-divider" ] [ _div [ _class_ "group-pill" ] [ _text groupTitle ] ]

        _div
            []
            [ _div
                  [ _class_ "songs-grid" ]
                  [ for song in songs do
                        SongCell song ] ]
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
                  DownloadUrl = $"https://pub-748f36e6cae345198861f65a9a8f5218.r2.dev/arcpkgs/lowiro.{y.Id}.arcpkg" })

        SongMatrix $"{cap}" songInfos)
    |> Seq.collect (fun x -> x)

if not (Path.Exists "songdoc/thumbnails") then
    Directory.CreateDirectory "songdoc/thumbnails" |> ignore

// generate index.html
PageTemplate "fragments-portal" "fragments-category" (songMatrixes 7)
|> renderHtml
|> Uglify.Html
|> _.Code
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

for thumbnailMeta in thumbnailMetaList do
    if not (Path.Exists thumbnailMeta.ThumbnailPath) then
        use jacket = Image.Load thumbnailMeta.JacketPath

        jacket.Mutate(fun ctx -> ctx.Resize(110, 110) |> ignore)
        jacket.Save(thumbnailMeta.ThumbnailPath)
