import os
import json
import sys
from urllib.request import urlretrieve
import zipfile


if __name__ == "__main__":
    with open("fragments-category/songs/songlist", "r", encoding="utf-8") as songlist_f:
        songlist = json.loads(songlist_f.read())["songs"]

    with open("fragments-category/songs/songlist_aprilfools", "r", encoding="utf-8") as songlist_f:
        songlist += json.loads(songlist_f.read())["songs"]

    etoile_release = "v0.1.0"
    etoile_version = "EtoileResurrection-c2d303d"
    etoile_zip_file = "EtoileResurrection.zip"
    urlretrieve(
        f"https://github.com/freeze-dolphin/EtoileResurrection/releases/download/{etoile_release}/{etoile_version}.zip",
        etoile_zip_file,
    )

    with zipfile.ZipFile(etoile_zip_file, "r") as zip_ref:
        zip_ref.extractall("scripts/")

    if not sys.platform.startswith("win"):
        os.system(f"chmod +x scripts/{etoile_version}/bin/EtoileResurrection")

    print("EtoileResurrection Extracted")

    os.mkdir("arcpkgs")

    for song_info in songlist:
        if "deleted" in song_info and song_info["deleted"]:
            continue

        song_id = song_info["id"]

        if sys.platform.startswith("win"):
            os.system(
                f"scripts\\{etoile_version}\\bin\\EtoileResurrection pack fragments-category\\songs --songId={song_id} --prefix=lowiro -o arcpkgs"
            )
        else:
            os.system(
                f"scripts/{etoile_version}/bin/EtoileResurrection pack fragments-category/songs --songId={song_id} --prefix=lowiro -o arcpkgs"
            )
