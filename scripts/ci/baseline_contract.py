"""Dependency-free validation of the closed upstream JSON baseline contract."""
import json
from pathlib import Path
import re
import subprocess

ROOT = Path(__file__).resolve().parents[2]
SHARDS = ("bootstrap", "ui", "core")
UPDATE_MODE_VARIABLES = (
    "NFC_UPDATE_SOURCE_REGISTRY_PATH",
    "NFC_UPDATE_CATALOG_TOOL",
    "NFC_UPDATE_SOURCE_REGISTRY_TEMPLATE",
    "NVT_VERSION_UPDATE_SOURCE_LAB",
)


def validate_baseline(document: dict) -> None:
    if (set(document) != {"schemaVersion", "measuredAtCommit", "shards", "pin"}
            or type(document["schemaVersion"]) is not int or document["schemaVersion"] != 2
            or re.fullmatch(r"[0-9a-f]{40}", document["measuredAtCommit"]) is None):
        raise ValueError("Invalid baseline.")
    shards = document["shards"]
    if not isinstance(shards, dict) or not shards or set(shards) - set(SHARDS):
        raise ValueError("Invalid baseline.")
    for row in shards.values():
        if set(row) != {"minTotal", "maxSkipped", "exampleMinTests", "requiredClasses"}:
            raise ValueError("Invalid baseline.")
        if (type(row["minTotal"]) is not int or row["minTotal"] < 1
                or type(row["maxSkipped"]) is not int or row["maxSkipped"] != 0
                or type(row["exampleMinTests"]) is not int or row["exampleMinTests"] < 0):
            raise ValueError("Invalid baseline.")
        classes = row["requiredClasses"]
        if (not isinstance(classes, list) or any(not isinstance(c, str) or
                re.fullmatch(r"[A-Za-z0-9_.]+", c) is None for c in classes)
                or len(set(classes)) != len(classes)):
            raise ValueError("Invalid baseline.")
    pin = document["pin"]
    if (set(pin) != {"assetsRepo", "assetsPath", "assetsCommit", "paths", "fileCount", "treeSha256"}
            or pin["assetsRepo"] != "Dennis40816/nvt-testdata"
            or pin["assetsPath"] != "nfc"
            or pin["paths"] != ["testdata/golden", "docs/references"]
            or re.fullmatch(r"[0-9a-f]{40}", pin["assetsCommit"]) is None
            or re.fullmatch(r"[0-9a-f]{64}", pin["treeSha256"]) is None
            or type(pin["fileCount"]) is not int or pin["fileCount"] < 0):
        raise ValueError("Invalid baseline.")


def load_baseline(path: Path) -> dict:
    document = json.loads(path.read_text(encoding="utf-8-sig"))
    validate_baseline(document)
    return document


def source_commit(root: Path = ROOT) -> str:
    result = subprocess.run(["git", "-c", f"safe.directory={root.as_posix()}",
                             "-C", str(root), "rev-parse", "HEAD"],
                            capture_output=True, text=True, check=False)
    return result.stdout.strip() if result.returncode == 0 else ""


def write_json(path: Path, document: dict) -> None:
    """Upstream encoding: UTF-8 without BOM, two spaces, CRLF, final newline."""
    path.parent.mkdir(parents=True, exist_ok=True)
    path.write_bytes((json.dumps(document, ensure_ascii=False, indent=2) + "\n")
                     .replace("\n", "\r\n").encode("utf-8"))
