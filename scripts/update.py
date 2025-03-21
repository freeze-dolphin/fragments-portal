import os
import json

TOKEN = os.environ.get("CATEGORY_TOKEN")

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


def song(song_id: str) -> str:
    return f"""
      - uses: actions/upload-artifact@v4
        with:
          name: {song_id}
          path: fragments-category/songs/{song_id}
          overwrite: true"""


if __name__ == "__main__":
    with open("fragments-category/songs/songlist", "r", encoding="utf-8") as songlist_f:
        songlist = json.loads(songlist_f.read())["songs"]

    for song_info in songlist:
        song_id = song_info["id"]
        PACKER_ACTION_CONTENT += song(song_id)
        print(f"- {song_id}")

    with open(".github/workflows/packer.yml", "w") as packer_f:
        packer_f.write(PACKER_ACTION_CONTENT)
