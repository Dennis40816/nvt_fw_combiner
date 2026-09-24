"""Independent full-output candidate oracle for the exact NT51929 AB CtrlRAM case.

This is evidence for firmware-owner review, not a production transform or Golden
promotion. It applies only while the three canonical CtrlRAM sources already
match both banks of the approved AB Merge image at their selected target bytes.
"""

from __future__ import annotations

import argparse
from datetime import datetime, timezone
import hashlib
import json
import os
import stat
import struct
import subprocess
import uuid
from pathlib import Path


REPO = Path(__file__).resolve().parents[2]
CANONICAL = REPO / "testdata" / "golden" / "canonical"
AB_CASE = "nt51929-ab-t05-d06"
CTRL_CASE = "nt51929-fw200-single-auto-prj-594-20260717"
SOURCE = REPO / ".tmp/combiner-1.13-source/extracted/firmware-merge-tool/Combiner/Combiner.c"
SOURCE_SHA256 = "7fb6551894d5a71f7df42b6b7c2bda99f35cbcc13f5c01713dc0ae596ebb5ea8"
AB_BASE_SHA256 = "c7e1e263ac8ca70f83a6f66fa268da4aa9be37c2c822a39d58fa9c153d66abe2"
SOURCE_SHA256_BY_ARTIFACT = {
    "postbuild-nf-ctrlram": "d16f150e172df1b92d5db32472b245f39f4481351304dc197da6f1d3194ab9d2",
    "postbuild-normal-ctrlram": "c0c8058727406b50dce0e209554ed997c09084808babe2f2a537a9a4b75d1d4c",
    "postbuild-vn-ctrlram": "c90ced38fc2cc8e62cd0275050f7bfcc05e707eaaffd9704533f9a26a49de64d",
}
BANK_SIZE = 0x40000
ADDRESS_FIELDS = (0x7164, 0x7168, 0x716C)
SOURCE_TARGETS = (
    ("postbuild-nf-ctrlram", 0x1FC00, None),
    ("postbuild-normal-ctrlram", 0x21B90, 18944),
    ("postbuild-vn-ctrlram", 0x26590, None),
)


def sha256(data: bytes) -> str:
    return hashlib.sha256(data).hexdigest()


def canonical_case(case_id: str) -> dict:
    matches = []
    for path in CANONICAL.glob("**/provenance/case.json"):
        document = json.loads(path.read_text(encoding="utf-8"))
        if document["caseId"] == case_id:
            matches.append(document)
    if len(matches) != 1:
        raise ValueError(f"Expected one canonical case: {case_id}")
    return matches[0]


def artifact(case: dict, artifact_id: str) -> bytes:
    entries = [item for item in case["artifacts"] if item["artifactId"] == artifact_id]
    if len(entries) != 1:
        raise ValueError(f"Expected one artifact: {case['caseId']} / {artifact_id}")
    entry = entries[0]
    path = (CANONICAL / entry["path"]).resolve()
    if not path.is_relative_to(CANONICAL.resolve()):
        raise ValueError(f"Artifact escapes canonical root: {artifact_id}")
    data = path.read_bytes()
    if len(data) != entry["size"] or sha256(data) != entry["sha256"]:
        raise ValueError(f"Canonical artifact size/hash mismatch: {artifact_id}")
    return data


def crc32_mpeg2(data: bytes | bytearray) -> int:
    crc = 0xFFFFFFFF
    for value in data:
        crc ^= value << 24
        for _ in range(8):
            crc = ((crc << 1) ^ (0x04C11DB7 if crc & 0x80000000 else 0)) & 0xFFFFFFFF
    return crc


def read32(data: bytes | bytearray, offset: int) -> int:
    return struct.unpack_from("<I", data, offset)[0]


def write32(data: bytearray, offset: int, value: int) -> None:
    struct.pack_into("<I", data, offset, value)


def difference_ranges(before: bytes, after: bytes) -> list[list[int]]:
    ranges = []
    start = None
    for index, (left, right) in enumerate(zip(before, after)):
        if left != right and start is None:
            start = index
        elif left == right and start is not None:
            ranges.append([start, index])
            start = None
    if start is not None:
        ranges.append([start, len(before)])
    return ranges


def validate_inputs(base: bytes, sources: dict[str, bytes]) -> None:
    if len(base) != 2 * BANK_SIZE:
        raise ValueError("Approved AB base length is not 0x80000")
    expected_addresses = (0x7200, 0x1F200, 0x2D100)
    for field, expected in zip(ADDRESS_FIELDS, expected_addresses):
        if read32(base, field) != expected or read32(base, BANK_SIZE + field) != expected + BANK_SIZE:
            raise ValueError(f"AB bank address does not match canonical A at 0x{field:X}")
    for bank in (0, 1):
        local = base[bank * BANK_SIZE : (bank + 1) * BANK_SIZE]
        for artifact_id, start, maximum in SOURCE_TARGETS:
            source = sources[artifact_id]
            if not source or (maximum is not None and len(source) < maximum):
                raise ValueError(f"{artifact_id} has insufficient source bytes")
            selected = source if maximum is None else source[:maximum]
            if start + len(selected) > BANK_SIZE or local[start : start + len(selected)] != selected:
                raise ValueError(f"{artifact_id} is not byte-identical to bank {bank} target")
        backup = bytearray(local[0x1F200:0x20200])
        backup[-4:] = b"\x00NVT"
        if backup != local[0x2E000:0x2F000]:
            raise ValueError(f"FWConfig Backup is not already current in bank {bank}")


def derive_bank(reference: bytes, bank: int) -> bytes:
    local = bytearray(reference[bank * BANK_SIZE : (bank + 1) * BANK_SIZE])
    if bank:
        for field in ADDRESS_FIELDS:
            write32(local, field, read32(local, field) - BANK_SIZE)
    # Combiner 1.13 NT51932BASED_NORMAL_MODE is called twice. Each invocation
    # pastes the current 512-byte header at 0x27EF0 before recalculating DLM
    # and then header CRC; the second paste therefore contains first-pass CRCs.
    for _ in range(2):
        local[0x27EF0:0x280F0] = local[0x7000:0x7200]
        dlm_start = read32(local, 0x7168)
        dlm_length = read32(local, 0x7114) + 1
        if dlm_start < 0 or dlm_start + dlm_length > BANK_SIZE:
            raise ValueError("DLM CRC read exceeds local bank")
        write32(local, 0x7118, crc32_mpeg2(local[dlm_start : dlm_start + dlm_length]))
        write32(local, 0x7100, crc32_mpeg2(local[0x7104:0x7128]))
    if bank:
        for field in ADDRESS_FIELDS:
            write32(local, field, read32(local, field) + BANK_SIZE)
    return bytes(local)


def fresh_candidates() -> tuple[Path, str]:
    root_value = os.environ.get("NFC_TEST_AREA_ROOT")
    if not root_value:
        raise ValueError("NFC_TEST_AREA_ROOT is required for a fresh candidate run")
    raw_root = Path(root_value)
    if not raw_root.is_absolute():
        raise ValueError("NFC_TEST_AREA_ROOT must be absolute")
    for checked in (raw_root / "temp", raw_root / "evidence"):
        current = checked
        while True:
            if current.exists() or current.is_symlink():
                status = current.lstat()
                if stat.S_ISLNK(status.st_mode) or \
                        getattr(status, "st_file_attributes", 0) & \
                        getattr(stat, "FILE_ATTRIBUTE_REPARSE_POINT", 0x400):
                    raise ValueError(f"Test-area path contains a reparse point: {current}")
            if current == current.parent:
                break
            current = current.parent
    root = raw_root.resolve(strict=True)
    if root == Path(root.anchor) or root == REPO or \
            root.is_relative_to(REPO) or REPO.is_relative_to(root):
        raise ValueError("Test-area root must not overlap the repository")
    temp = (root / "temp").resolve(strict=True)
    for name in ("TEMP", "TMP", "TMPDIR"):
        if Path(os.environ.get(name, "")).resolve() != temp:
            raise ValueError(f"{name} must name the existing test-area temp child")
    run_id = str(uuid.uuid4())
    stamp = datetime.now(timezone.utc).strftime("%Y%m%dT%H%M%SZ")
    evidence_dir = root / "evidence" / f"v1110-ab-full-input-{stamp}-{run_id[:8]}"
    evidence_dir.mkdir(parents=True, exist_ok=False)
    env = os.environ.copy()
    env["NFC_AB_CTRLRAM_FULL_INPUT_DIR"] = str(evidence_dir)
    env["NFC_AB_CTRLRAM_RUN_ID"] = run_id
    command = [
        "dotnet", "test",
        str(REPO / "tests/NvtFwCombiner.Bootstrap.Tests/NvtFwCombiner.Bootstrap.Tests.csproj"),
        "--no-restore", "-p:RestoreLockedMode=false",
        "--filter", "FullyQualifiedName~AbCtrlRamFullInputCandidateTests",
    ]
    print(f"Fresh candidate directory: {evidence_dir}", flush=True)
    subprocess.run(command, cwd=REPO, env=env, check=True)
    return evidence_dir, run_id


def candidate_report(evidence_dir: Path, name: str, candidate: bytes,
                     base_sha: str, run_id: str | None) -> tuple[dict, dict]:
    report = json.loads((evidence_dir / name / "report.json").read_text(encoding="utf-8"))
    if report.get("caseName") != name or report.get("baseSha256") != base_sha or \
            report.get("outputSha256") != sha256(candidate) or \
            report.get("outputLength") != len(candidate):
        raise ValueError(f"{name}: candidate report does not match the output and approved base")
    if not isinstance(report.get("runId"), str) or (run_id is not None and report["runId"] != run_id):
        raise ValueError(f"{name}: report is not bound to this candidate run")
    assembly_hashes = report.get("assemblySha256")
    if not isinstance(assembly_hashes, dict) or \
            "NvtFwCombiner.Bootstrap.Tests.dll" not in assembly_hashes or \
            "NvtFwCombiner.Bootstrap.dll" not in assembly_hashes:
        raise ValueError(f"{name}: incomplete executed-assembly identity")
    assembly_dir = Path(report["assemblyDirectory"]).resolve(strict=True)
    for assembly, expected_hash in assembly_hashes.items():
        if Path(assembly).name != assembly or \
                sha256((assembly_dir / assembly).read_bytes()) != expected_hash:
            raise ValueError(f"{name}: executed assembly changed: {assembly}")
    return report, assembly_hashes


def main() -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    choice = parser.add_mutually_exclusive_group(required=True)
    choice.add_argument("--fresh", action="store_true",
                        help="Build, execute all three cases, then compare their fresh full outputs")
    choice.add_argument("--inspect-existing", type=Path,
                        help="Inspect saved candidates; this does not prove the current build")
    args = parser.parse_args()
    if crc32_mpeg2(b"123456789") != 0x0376E6E7:
        raise ValueError("CRC-32/MPEG-2 reference vector failed")
    if sha256(SOURCE.read_bytes()) != SOURCE_SHA256:
        raise ValueError("Recovered Combiner 1.13 source identity changed")
    ab = canonical_case(AB_CASE)
    ctrl = canonical_case(CTRL_CASE)
    if ab["testDisposition"]["kind"] != "direct-full-output" or \
            ctrl["testDisposition"]["kind"] != "allowed-byte-difference":
        raise ValueError("Canonical evidence dispositions changed")
    base = artifact(ab, "expected-output")
    sources = {name: artifact(ctrl, name) for name, _, _ in SOURCE_TARGETS}
    if sha256(base) != AB_BASE_SHA256 or any(
            sha256(value) != SOURCE_SHA256_BY_ARTIFACT[name] for name, value in sources.items()):
        raise ValueError("Pinned owner AB base or CtrlRAM source identity changed")
    validate_inputs(base, sources)
    evidence_dir, run_id = fresh_candidates() if args.fresh else (args.inspect_existing, None)
    results = {}
    reports = []
    for name, banks in (("a-only", (0,)), ("b-only", (1,)), ("both", (0, 1))):
        expected = bytearray(base)
        for bank in banks:
            expected[bank * BANK_SIZE : (bank + 1) * BANK_SIZE] = derive_bank(base, bank)
        expected_bytes = bytes(expected)
        candidate_path = evidence_dir / name / "candidate-output.bin"
        candidate = candidate_path.read_bytes()
        report, assembly_hashes = candidate_report(evidence_dir, name, candidate, sha256(base), run_id)
        reports.append(report)
        if len(candidate) != len(expected_bytes) or candidate != expected_bytes:
            first = next((index for index, pair in enumerate(zip(candidate, expected_bytes))
                          if pair[0] != pair[1]), None)
            raise ValueError(f"{name}: complete output differs from oracle at {first}")
        results[name] = {
            "completeBytesEqual": True,
            "candidateSha256": sha256(candidate),
            "derivedSha256": sha256(expected_bytes),
            "size": len(candidate),
            "changedRangesFromApprovedAbBase": difference_ranges(base, candidate),
        }
    if len({report["runId"] for report in reports}) != 1 or \
            len({json.dumps(report["assemblySha256"], sort_keys=True) for report in reports}) != 1 or \
            len({report["assemblyDirectory"] for report in reports}) != 1:
        raise ValueError("Candidate reports do not share one execution identity")
    result = {
        "status": "independent candidate oracle passed; firmware-owner review still required",
        "freshExecution": args.fresh,
        "runId": reports[0]["runId"],
        "evidenceDirectory": str(evidence_dir),
        "abBaseSha256": sha256(base),
        "combinerSourceSha256": SOURCE_SHA256,
        "oracleSourceSha256": sha256(Path(__file__).read_bytes()),
        "executedAssemblySha256": assembly_hashes,
        "cases": results,
    }
    if args.fresh:
        result["gitHead"] = subprocess.check_output(
            ["git", "rev-parse", "HEAD"], cwd=REPO, text=True).strip()
        with (evidence_dir / "oracle-report.json").open("x", encoding="utf-8") as output:
            json.dump(result, output, indent=2)
    print(json.dumps(result, indent=2))
    return 0


if __name__ == "__main__":
    try:
        raise SystemExit(main())
    except (OSError, ValueError, KeyError) as error:
        raise SystemExit(f"oracle error: {error}") from error
