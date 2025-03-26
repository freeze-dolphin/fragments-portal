import os
import json
from urllib.request import urlretrieve
import zipfile


if __name__ == "__main__":
    with open("fragments-category/songs/songlist", "r", encoding="utf-8") as songlist_f:
        songlist = json.loads(songlist_f.read())["songs"]

    etoile_release = "v0.1.0"
    etoile_version = "EtoileResurrection-c2d303d"
    etoile_zip_file = "EtoileResurrection.zip"
    urlretrieve(
        f"https://github.com/freeze-dolphin/EtoileResurrection/releases/download/{etoile_release}/{etoile_version}.zip",
        etoile_zip_file,
    )

    with zipfile.ZipFile(etoile_zip_file, "r") as zip_ref:
        zip_ref.extractall("scripts/")

    for song_info in songlist:
        song_id = song_info["id"]
        song_info["difficulties"]

        os.system(
            f"scripts/{etoile_version}/bin/EtoileResurrection pack fragments-category/songs --songId={song_id} --prefix=lowiro -o ."
        )
        print(f"- {song_id}")
