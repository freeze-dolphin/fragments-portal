import os
import sys
from urllib.request import urlretrieve
import zipfile

if __name__ == "__main__":
    etoile_release = "v1.0.2"
    etoile_version = "EtoileResurrection-edbbe37"
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

    if sys.platform.startswith("win"):
        os.system(
            f"scripts\\{etoile_version}\\bin\\EtoileResurrection pack fragments-category\\songs\\songlist --songId=.* -re --prefix=lowiro -o arcpkgs"
        )
        os.system(
            f"scripts\\{etoile_version}\\bin\\EtoileResurrection pack fragments-category\\songs\\songlist_aprilfools --songId=.* -re --prefix=lowiro -o arcpkgs"
        )
    else:
        os.system(
            f"scripts/{etoile_version}/bin/EtoileResurrection pack fragments-category/songs/songlist --songId=.* -re --prefix=lowiro -o arcpkgs"
        )
        os.system(
            f"scripts/{etoile_version}/bin/EtoileResurrection pack fragments-category/songs/songlist_aprilfools --songId=.* -re --prefix=lowiro -o arcpkgs"
        )
