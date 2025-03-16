import os
import json

TOKEN = os.environ.get("CATEGORY_TOKEN")

PACKER_ACTION_CONTENT = """name: packer

on:
  workflow_dispatch:

jobs:
  build:
    runs-on: ubuntu-latest
    steps:
      - uses: actions/checkout@v4

      - uses: actions/checkout@v4
        with: 
          repository: freeze-dolphin/fragments-category
          ref: master
          token: ${{ secrets.CATEGORY_TOKEN }}

      - uses: actions/upload-artifact@v4
        with:
          name: metadata
          path: |
            fragments-category/songlist
            fragments-category/packlist
            fragments-category/unlocks
          overwrite: true"""


def song(song_id: str) -> str:
    return f"""
      - uses: actions/upload-artifact@v4
        with:
          name: {song_id}
          path: fragments-category/{song_id}
          overwrite: true"""


if __name__ == "__main__":
    with open("fragments-category/songlist", "r", encoding="utf-8") as songlist_f:
        songlist = json.loads(songlist_f.read())["songs"]

    for song_info in songlist:
        PACKER_ACTION_CONTENT += song(song_info["id"])

    with open(".github/workflows/packer.yml", "w") as packer_f:
        packer_f.write(PACKER_ACTION_CONTENT)
