import os
import sys
from urllib.request import urlretrieve
import zipfile
import multiprocessing
from pathlib import Path

num_cores = multiprocessing.cpu_count()

if __name__ == "__main__":
    etoile_release = "v1.0.9"
    etoile_version = "EtoileResurrection-4c159e3"
    etoile_zip_file = "scripts/EtoileResurrection.zip"
    urlretrieve(
        f"https://github.com/freeze-dolphin/EtoileResurrection/releases/download/{etoile_release}/{etoile_version}.zip",
        etoile_zip_file,
    )

    with zipfile.ZipFile(etoile_zip_file, "r") as zip_ref:
        zip_ref.extractall("scripts/")

    if not sys.platform.startswith("win"):
        os.system(f"chmod +x scripts/{etoile_version}/bin/EtoileResurrection")

    print("EtoileResurrection Extracted")

    paths = [
        "arcpkgs",
        "arcpkgs/combine",
        "arcpkgs/aprilfools",
        "arcpkgs/aprilfools/combine",
    ]

    for path in paths:
        if not os.path.exists(path):
            os.mkdir(path)

    executor_p = Path("scripts") / etoile_version / "bin" / "EtoileResurrection"
    category_p = Path("..") / "fragments-category"
    arcpkgs_p = Path("arcpkgs")

    slst_p = category_p / "songs" / "songlist"
    slst_aprilfools_p = category_p / "songs" / "songlist_aprilfools"
    plst_p = category_p / "songs" / "packlist"
    plst_aprilfools_p = category_p / "songs" / "packlist_aprilfools"

    executor_rslv = str(executor_p.resolve())

    os.system(
        f"{executor_rslv} pack {str(slst_p.resolve())} --songId=.* -re --prefix=lowiro -o arcpkgs -j{num_cores}"
    )
    os.system(
        f"{executor_rslv} pack {str(slst_aprilfools_p.resolve())} --songId=.* -re --prefix=lowiro -o arcpkgs/aprilfools -j1"
    )
    os.system(
        f"{executor_rslv} combine --prefix=lowiro -o arcpkgs/combine -s.* -re --append-single arcpkgs/*.arcpkg {str(slst_p.resolve())} {str(plst_p.resolve())}"
    )
    os.system(
        f"{executor_rslv} combine --prefix=lowiro -o arcpkgs/aprilfools/combine -s.* -re arcpkgs/aprilfools/*.arcpkg {str(slst_aprilfools_p.resolve())} {str(plst_aprilfools_p.resolve())}"
    )
