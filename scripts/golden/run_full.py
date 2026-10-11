"""Snapshot tracked working files, assemble, and invoke the canonical CI shard commands."""
from __future__ import annotations

import argparse
import json
import os
from pathlib import Path
import shutil
import stat
import subprocess
import sys
from time import monotonic, time_ns
import uuid

if not __package__:
    sys.path.insert(0, str(Path(__file__).resolve().parents[2]))

from scripts import verify as verifier
from scripts.ci.baseline_contract import SHARDS, source_commit
from scripts.ci.check_test_shard import check_test_shard
from scripts.golden.golden_pin import (DEFAULT_BASELINE, FixedArgumentParser, ROOT, UNBOUND_PIN, filesystem_path, load_pin,
                                       pin_message, reject_links, require_bound_pin, safe_relative)

FAILURE = "Golden full run failed."


def snapshot_working_tree(source: Path, destination: Path) -> None:
    result = subprocess.run(["git", "-c", f"safe.directory={source.as_posix()}", "-C", str(source),
                             "ls-files", "-z", "--stage"], capture_output=True, check=False)
    if result.returncode:
        raise ValueError(FAILURE)
    names = []
    for entry in result.stdout.split(b"\0"):
        if not entry:
            continue
        metadata, _, name = entry.partition(b"\t")
        if metadata.split(b" ")[0] == b"160000":
            continue  # A submodule pointer is not a file of this repository; CI checks it out empty too.
        names.append(name.decode("utf-8"))
    if "scripts/ci/read_trx.py" not in names:
        raise ValueError("Golden run requires tracked helper files.")
    source, destination = filesystem_path(source), filesystem_path(destination)
    for name in names:
        relative = safe_relative(name)
        file = source.joinpath(*relative.parts)
        reject_links(file)
        if not stat.S_ISREG(file.lstat().st_mode):
            raise ValueError(FAILURE)
        target = destination.joinpath(*relative.parts)
        target.parent.mkdir(parents=True, exist_ok=True)
        shutil.copyfile(file, target, follow_symlinks=False)


def seed_core_feed(source: Path, destination: Path) -> None:
    """Reuse only manifest-hashed build prerequisites; the normal restore still verifies them."""
    source, destination = filesystem_path(source), filesystem_path(destination)
    manifest = json.loads((source / "core-packages.json").read_text(encoding="utf-8"))
    for package in manifest["packages"]:
        file = source / "artifacts/core-packages" / package["asset"]
        if not file.exists():
            continue
        reject_links(file)
        if verifier.sha256_file(file) != package["sha256"]:
            raise ValueError(FAILURE)
        target = destination / "artifacts/core-packages" / package["asset"]
        target.parent.mkdir(parents=True, exist_ok=True)
        shutil.copyfile(file, target, follow_symlinks=False)


def seed_coverlet_adapter(package_cache: Path, destination: Path) -> None:
    """The canonical resolver requires the pinned adapter inside the snapshot boundary."""
    version = verifier.load_baseline()["collection"]["dotnet"]["version"]
    relative = Path("coverlet.collector") / version / "build/netstandard2.0"
    source = filesystem_path(package_cache / relative)
    reject_links(source)
    if not source.exists():
        return  # A normal snapshot-local restore can still provide it.
    expected = verifier.regular_tree_hashes(source, boundary=filesystem_path(package_cache),
                                           description="Coverlet adapter")
    target = filesystem_path(destination / ".packages" / relative)
    reject_links(target)
    target.parent.mkdir(parents=True, exist_ok=True)
    shutil.copytree(source, target)
    if verifier.regular_tree_hashes(target, boundary=filesystem_path(destination),
                                    description="Coverlet adapter") != expected:
        raise ValueError(FAILURE)
    verifier.resolve_coverlet_adapter_path(filesystem_path(destination))


def run_full(*, shard: str, source: Path | None, keep: bool) -> int:
    declaration = os.environ.get("NFC_TEST_AREA_ROOT", "")
    if not declaration:
        raise ValueError("NFC_TEST_AREA_ROOT is not set.")
    area = Path(declaration)
    reject_links(area)
    if not area.is_absolute() or not (area / "temp").is_dir():
        raise ValueError(FAILURE)
    pin = load_pin(DEFAULT_BASELINE)
    if source is not None:
        require_bound_pin(pin)
    run_id = uuid.uuid4().hex[:8]  # short, so long test paths stay within the Windows limit
    # The verifier refuses a repository that overlaps the test area, so the snapshot lives in a sibling folder.
    work_root = area.parent / "NvtFwCombiner-GoldenRun"
    reject_links(work_root)
    work_root.mkdir(exist_ok=True)
    work = work_root / ("golden-full-" + run_id)
    checkout = work / "checkout"
    retained = area / "outputs/golden-full" / run_id
    reject_links(retained)
    work.mkdir()
    checkout.mkdir()
    retained.mkdir(parents=True)
    rows = []
    success = True
    environment = os.environ.copy()
    environment.pop("NFC_TEST_SESSION_ROOT", None)
    for name in ("TEMP", "TMP", "TMPDIR"):
        environment[name] = str(area / "temp")
    commit = source_commit(ROOT)
    status = subprocess.run(["git", "-c", f"safe.directory={ROOT.as_posix()}", "-C", str(ROOT),
                             "status", "--porcelain", "--untracked-files=no"],
                            capture_output=True, text=True, check=False)
    dirty = status.returncode != 0 or bool(status.stdout.strip())
    environment.update(GITHUB_SHA=commit, GITHUB_RUN_ID=str(time_ns()), GITHUB_RUN_ATTEMPT="1")
    # The verifier requires the exact SDK of global.json. CI installs it into the checkout; locally, point
    # NFC_DOTNET_ROOT at a repository-scope install (a folder that holds dotnet.exe) when this tree has none.
    sdk = ROOT / ".dotnet"
    if not (sdk / "dotnet.exe").is_file() and os.environ.get("NFC_DOTNET_ROOT"):
        sdk = Path(os.environ["NFC_DOTNET_ROOT"])
    if (sdk / "dotnet.exe").is_file():
        environment["DOTNET_ROOT"] = str(sdk)
        environment["PATH"] = str(sdk) + os.pathsep + environment.get("PATH", "")
    packages = ROOT / ".packages"
    if packages.is_dir():
        environment["NUGET_PACKAGES"] = str(packages)
    capture = work / "output.log"
    try:
        snapshot_working_tree(ROOT, checkout)
        seed_core_feed(ROOT, checkout)
        seed_coverlet_adapter(Path(environment.get("NUGET_PACKAGES", str(ROOT / ".packages"))), checkout)
        assembly = ["pwsh", "-NoProfile", "-File", str(ROOT / "scripts/golden/assemble-golden.ps1"),
                    "-Root", str(checkout), "-BaselinePath", str(DEFAULT_BASELINE)]
        if source is not None:
            assembly.extend(["-Mode", "FromDirectory", "-Source", str(source)])
        verifier.run(assembly, cwd=checkout, environment=environment, log_path=capture)
        print(pin_message(pin), flush=True)
        for selected in SHARDS if shard == "all" else (shard,):
            capture.unlink(missing_ok=True)
            started = monotonic()
            run_failed = False
            timed_out = False
            try:
                verifier.run([sys.executable, "scripts/verify.py", "--ci-dotnet-test-shard", selected],
                             cwd=checkout, environment=environment, log_path=capture,
                             timeout_seconds=45 * 60)
            except subprocess.CalledProcessError:
                run_failed = True
            except subprocess.TimeoutExpired:
                run_failed = timed_out = True
            evidence = retained / selected
            if keep and run_failed and capture.is_file():
                # Local debugging only (--keep). The log can hold test output, so it never goes to CI artifacts.
                shutil.copyfile(capture, retained / (selected + "-output.log"))
            upload = checkout / "artifacts/ci-dotnet-upload"
            if upload.is_dir():
                shutil.copytree(upload, evidence, symlinks=True)
            else:
                evidence.mkdir()
            results = evidence / "shards" / selected / "results"
            summary = check_test_shard(shard=selected, results_directory=results,
                                       baseline_path=DEFAULT_BASELINE,
                                       summary_path=evidence / "shard-summary.json",
                                       example_data_present=True, commit=commit,
                                       test_run_failed=run_failed)
            rows.append(dict(shard=selected, elapsedSeconds=monotonic() - started,
                             timedOut=timed_out, summary=summary))
            print(f"[golden-full] {selected} " + " ".join(f"{key} {summary[key]}" for key in
                  ("total", "passed", "failed", "skipped", "notExecuted")) +
                  f" outcome {summary['outcome']}", flush=True)
            success &= summary["outcome"] == "passed"
            if timed_out:
                break
    finally:
        try:
            capture.unlink(missing_ok=True)
            if not keep:
                # Only this freshly allocated workspace, checked against its exact parent, is disposable.
                # It holds the golden files, so it goes before any bookkeeping write can fail.
                reject_links(work)
                resolved_work = work.resolve(strict=True)
                if (resolved_work.parent != work_root.resolve(strict=True)
                        or resolved_work.name != "golden-full-" + run_id):
                    raise ValueError(FAILURE)
                shutil.rmtree(filesystem_path(resolved_work))
        finally:
            metadata = dict(mode="FromDirectory" if source else "InTree", commit=commit, dirty=dirty, shards=rows,
                            snapshot=work.name if keep else None,
                            evidence=retained.relative_to(area).as_posix())
            (retained / "run.json").write_text(json.dumps(metadata, indent=2) + "\n", encoding="utf-8")
            pointer = area / "outputs/golden-full/last-run.json"
            pointer.write_text(json.dumps(metadata, indent=2) + "\n", encoding="utf-8")
    return 0 if success else 1


def main() -> int:
    parser = FixedArgumentParser(description=__doc__, allow_abbrev=False)
    parser.add_argument("--shard", choices=(*SHARDS, "all"), default="all")
    parser.add_argument("--source", type=Path)
    parser.add_argument("--keep", action="store_true")
    args = parser.parse_args()
    try:
        return run_full(**vars(args))
    except Exception as error:  # any failure prints one fixed sentence, never a traceback
        fixed = str(error) if str(error) in (UNBOUND_PIN, "NFC_TEST_AREA_ROOT is not set.",
                                            "Golden run requires tracked helper files.") else FAILURE
        print(fixed, file=sys.stderr)
        return 1


if __name__ == "__main__":
    sys.exit(main())
