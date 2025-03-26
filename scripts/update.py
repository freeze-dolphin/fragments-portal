import json
import os
from urllib.request import urlretrieve
import zipfile

# TOKEN = os.environ.get("CATEGORY_TOKEN")

PACKER_ACTION_CONTENT = """name: packer

on:
  workflow_dispatch:

jobs:
  pack:
    runs-on: ubuntu-latest
    steps:
      - uses: actions/checkout@v4

      - uses: actions/checkout@v4
        with: 
          repository: freeze-dolphin/fragments-category
          path: fragments-category
          ref: master
          token: ${{ secrets.CATEGORY_TOKEN }}

      - uses: actions/upload-artifact@v4
        with:
          name: metadata
          path: |
            fragments-category/songs/songlist
            fragments-category/songs/packlist
            fragments-category/songs/unlocks
          overwrite: true"""

PACKER_ARCPKG_ACTION_CONTENT = """name: packer-arcpkg

on:
  workflow_dispatch:

jobs:
  pack:
    runs-on: ubuntu-latest
    steps:
      - uses: actions/checkout@v4

      - uses: actions/checkout@v4
        with: 
          repository: freeze-dolphin/fragments-category
          path: fragments-category
          ref: master
          token: ${{ secrets.CATEGORY_TOKEN }}"""


def song(song_id: str) -> str:
    return f"""
      - uses: actions/upload-artifact@v4
        with:
          name: {song_id}
          path: fragments-category/songs/{song_id}
          overwrite: true"""


def song_arcpkg(song_id: str) -> str:
    return f"""
      - uses: actions/upload-artifact@v4
        with:
          name: lowiro.{song_id}.arcpkg
          path: fragments-category/{song_id}.arcpkg
          overwrite: true"""


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

        PACKER_ACTION_CONTENT += song(song_id)
        PACKER_ARCPKG_ACTION_CONTENT += song_arcpkg(song_id)
        os.system(
            f"scripts/{etoile_version}/bin/EtoileResurrection pack fragments-category/songs --songId={song_id} --prefix=lowiro -o=./"
        )
        print(f"- {song_id}")

    with open(".github/workflows/packer.yml", "w") as packer_f:
        packer_f.write(PACKER_ACTION_CONTENT)

    with open(".github/workflows/packer_arcpkg.yml", "w") as packer_arcpkg_f:
        packer_arcpkg_f.write(PACKER_ARCPKG_ACTION_CONTENT)
