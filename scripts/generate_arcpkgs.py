import os
import sys
from urllib.request import urlretrieve
import zipfile

if __name__ == "__main__":
    etoile_release = "v1.0.8"
    etoile_version = "EtoileResurrection-1b6e21c"
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

    paths = ["arcpkgs", "arcpkgs/combine", "arcpkgs/aprilfools", "arcpkgs/aprilfools/combine"]

    for path in paths:
        if not os.path.exists(path):
            os.mkdir(path)

    if sys.platform.startswith("win"):
        os.system(
            f"scripts\\{etoile_version}\\bin\\EtoileResurrection pack fragments-category\\songs\\songlist --songId=.* -re --prefix=lowiro -o arcpkgs"
        )
        os.system(
            f"scripts\\{etoile_version}\\bin\\EtoileResurrection pack fragments-category\\songs\\songlist_aprilfools --songId=.* -re --prefix=lowiro -o arcpkgs\\aprilfools"
        )
        os.system(
            f"scripts\\{etoile_version}\\bin\\EtoileResurrection combine --prefix=lowiro -o arcpkgs\\combine -s.* -re --append-single arcpkgs\\*.arcpkg fragments-category\\songs\\songlist fragments-category\\songs\\packlist"
        )
        os.system(
            f"scripts\\{etoile_version}\\bin\\EtoileResurrection combine --prefix=lowiro -o arcpkgs\\aprilfools\\combine -s.* -re arcpkgs\\aprilfools\\*.arcpkg fragments-category\\songs\\songlist_aprilfools fragments-category\\songs\\packlist_aprilfools"
        )
    else:
        os.system(
            f"scripts/{etoile_version}/bin/EtoileResurrection pack fragments-category/songs/songlist --songId=.* -re --prefix=lowiro -o arcpkgs"
        )
        os.system(
            f"scripts/{etoile_version}/bin/EtoileResurrection pack fragments-category/songs/songlist_aprilfools --songId=.* -re --prefix=lowiro -o arcpkgs/aprilfools"
        )
        os.system(
            f"scripts/{etoile_version}/bin/EtoileResurrection combine --prefix=lowiro -o arcpkgs/combine -s.* -re --append-single arcpkgs/*.arcpkg fragments-category/songs/songlist fragments-category/songs/packlist"
        )
        os.system(
            f"scripts/{etoile_version}/bin/EtoileResurrection combine --prefix=lowiro -o arcpkgs/aprilfools/combine -s.* -re arcpkgs/aprilfools/*.arcpkg fragments-category/songs/songlist_aprilfools fragments-category/songs/packlist_aprilfools"
        )
