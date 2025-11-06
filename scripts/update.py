import json
from git import Repo

# TOKEN = os.environ.get("CATEGORY_TOKEN")

PACKER_ACTION_CONTENT = """name: packer

on:
  workflow_dispatch:

jobs:
  pack:
    runs-on: ubuntu-latest
    steps:
      - uses: actions/checkout@v4

      - name: Checkout category repo
        uses: actions/checkout@v4
        with: 
          repository: freeze-dolphin/fragments-category
          path: fragments-category
          ref: master
          token: ${{ secrets.CATEGORY_TOKEN }}

      - name: Upload metadata
        uses: actions/upload-artifact@v4
        with:
          name: metadata
          path: |
            fragments-category/songs/songlist
            fragments-category/songs/packlist
            fragments-category/songs/unlocks
          overwrite: true"""

PACKER_ARCPKG_CONTENT = """name: packer-arcpkg

on:
  workflow_dispatch:

permissions:
  contents: read
  pages: write
  id-token: write

concurrency:
  group: "pages"
  cancel-in-progress: false

jobs:
  pack_arcpkg:
    environment:
      name: github-pages
      url: ${{ steps.deployment.outputs.page_url }}

    runs-on: ubuntu-latest

    steps:
      - uses: actions/checkout@v4
        with:
          path: fragments-portal

      - name: Checkout fragments-category
        uses: actions/checkout@v4
        with: 
          repository: freeze-dolphin/fragments-category
          path: fragments-category
          ref: master
          token: ${{ secrets.CATEGORY_TOKEN }}
          
      - name: Setup Pages
        uses: actions/configure-pages@v5

      - name: Setup Temurin JDK 17
        uses: actions/setup-java@v4
        with:
          distribution: 'temurin'
          java-version: '17'
      
      - name: Setup Python 3.13
        uses: actions/setup-python@v6.0.0
        with:
          python-version: '3.13' 
      
      - name: Generate ArcCreate Packages
        run: cd fragments-portal && python scripts/generate_arcpkgs.py && cd ..

      - name: Generate Page Content
        run: |
          pip install beautifulsoup4 lxml pillow pytz gitpython
          cd fragments-category && python scripts/generate_songdoc.py && cd ..

      - name: Upload artifact
        uses: actions/upload-pages-artifact@v3
        with:
          path: 'fragments-category/songdoc/pages'

      - name: Deploy assets
        uses: s0/git-publish-subdir-action@develop
        env:
          REPO: self
          BRANCH: %target_branch%
          FOLDER: fragments-category/songdoc/assets
          GITHUB_TOKEN: ${{ secrets.PAT }}

      - name: Deploy to GitHub Pages
        id: deployment
        uses: actions/deploy-pages@v4"""

CLEANER_CONTENT = """name: cleaner

on:
  schedule:
    - cron: '0 0 * * *'
  workflow_dispatch:

jobs:
  cleanup:
    runs-on: ubuntu-latest
    permissions:
      contents: write
    steps:
      - uses: freeze-dolphin/branch-cleanup-bot@main
        with:
          protected_branches: %protected_branches%
          github_token: ${{ secrets.GITHUB_TOKEN }}
          delete_stale: true
          stale_days: 1"""


def song(song_id: str) -> str:
    return f"""
      - name: Upload artifact '{song_id}'
        uses: actions/upload-artifact@v4
        with:
          name: {song_id}
          path: fragments-category/songs/{song_id}
          overwrite: true"""


def get_latest_commit_hash(repo_path: str) -> str:
    repo = Repo(repo_path)
    return repo.head.commit.hexsha


if __name__ == "__main__":
    with open(
        "../fragments-category/songs/songlist", "r", encoding="utf-8"
    ) as songlist_f:
        songlist = json.loads(songlist_f.read())["songs"]

    with open(
        "../fragments-category/songs/songlist_aprilfools", "r", encoding="utf-8"
    ) as songlist_f:
        songlist += json.loads(songlist_f.read())["songs"]

    for song_info in songlist:
        if "deleted" in song_info and song_info["deleted"]:
            continue

        song_id = song_info["id"]

        PACKER_ACTION_CONTENT += song(song_id)
        print(f"- {song_id}")

    with open(".github/workflows/packer.yml", "w") as packer_f:
        packer_f.write(PACKER_ACTION_CONTENT)

    category_last_commit_hash = get_latest_commit_hash("../fragments-category")[:7]

    with open(".github/workflows/packer_arcpkg.yml", "w") as packer_arcpkg_f:
        packer_arcpkg_f.write(
            PACKER_ARCPKG_CONTENT.replace(
                "%target_branch%",
                f"assets_{category_last_commit_hash}",
            )
        )

    with open(".github/workflows/cleaner.yml", "w") as cleaner_f:
        cleaner_f.write(CLEANER_CONTENT.replace("%protected_branches%", f"master,pages,assets_{category_last_commit_hash}"))
