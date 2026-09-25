"""Non-certifying local v0.9.16 comparison for the 1.1.12 WS-PARITY lane.

Owner decision 10 (option A) on the 1.1.12 board: compare the v0.9.16
predecessor with the 1.1.12 candidate for the plan routes that have canonical
input bindings, without changing `scripts/`, workflows, contracts or ADR 0057.

This harness reuses, read-only, the plan and input authority of
`scripts/v0916_parity_certification.py`: the pinned plan and policy loader,
the pinned canonical Golden materializer and validator, the per-route input
resolver and the CLI argument builder. It runs each executor itself with
plain subprocesses. It produces no receipts, package admission, owner
attestation or certification verdict; its results are local observations.

Firmware inputs, outputs and logs stay under `--work-dir`, which must be
outside the repository (use the NFC test area). The path-free table written
with `--table-json` / `--table-md` carries only identities, sizes, hashes,
exit codes, durations and half-open file-offset ranges.
"""

from __future__ import annotations

import argparse
import datetime as dt
import hashlib
import json
import os
import shutil
import subprocess
import sys
import time
from pathlib import Path, PurePosixPath
from typing import Any, Sequence

REPO = Path(__file__).resolve().parents[4]
sys.path.insert(0, str(REPO))

from scripts import v0916_parity_certification as parity  # noqa: E402

PLAN = REPO / "docs/contracts/v0916-parity-certification-v1.json"
RANGE_LIMIT = 32
ADDRESS_SPACE = "output-file-offset"
_REDACTIONS: list[tuple[str, str]] = []
_PAUSE_FLAG: list[Path] = []


def redact(text: str) -> str:
    """Replace local absolute roots so recorded text stays path-free."""

    for needle, token in _REDACTIONS:
        for variant in {needle, needle.replace("\\", "/"), needle.replace("/", "\\")}:
            text = text.replace(variant, token)
    return text


def wait_while_flag(flag: Path | None) -> None:
    """Machine coordination: do not start work while another lane measures."""

    while flag is not None and flag.exists():
        print("pause flag present; waiting 60 s", flush=True)
        time.sleep(60)


def sha256(data: bytes) -> str:
    return hashlib.sha256(data).hexdigest()


def utc_now() -> str:
    return dt.datetime.now(dt.timezone.utc).strftime("%Y-%m-%dT%H:%M:%SZ")


def differing_ranges(left: bytes, right: bytes) -> list[tuple[int, int]]:
    """Half-open ranges over the common prefix where the bytes differ."""

    ranges: list[tuple[int, int]] = []
    start = None
    for offset in range(min(len(left), len(right))):
        if left[offset] != right[offset]:
            if start is None:
                start = offset
        elif start is not None:
            ranges.append((start, offset))
            start = None
    if start is not None:
        ranges.append((start, min(len(left), len(right))))
    return ranges


def compare_bytes(baseline: bytes, candidate: bytes) -> dict[str, Any]:
    ranges = differing_ranges(baseline, candidate)
    return {
        "addressSpace": ADDRESS_SPACE,
        "equal": baseline == candidate,
        "baselineSize": len(baseline),
        "candidateSize": len(candidate),
        "sizeEqual": len(baseline) == len(candidate),
        "differentByteCount": sum(end - start for start, end in ranges),
        "rangeCount": len(ranges),
        "ranges": [{"start": s, "endExclusive": e} for s, e in ranges[:RANGE_LIMIT]],
        "rangesTruncated": len(ranges) > RANGE_LIMIT,
    }


def require_test_area(work_dir: Path) -> None:
    root_value = os.environ.get("NFC_TEST_AREA_ROOT")
    if not root_value:
        raise SystemExit("NFC_TEST_AREA_ROOT is not set in this process")
    root = Path(root_value).resolve(strict=True)
    temp = (root / "temp").resolve(strict=True)
    for name in ("TEMP", "TMP", "TMPDIR"):
        if Path(os.environ.get(name, "")).resolve() != temp:
            raise SystemExit(f"{name} must name the test-area temp child")
    resolved = work_dir.resolve()
    if not resolved.is_relative_to(root) or resolved.is_relative_to(REPO):
        raise SystemExit("--work-dir must be inside the test area and outside the repository")


class Executor:
    def __init__(self, side: str, cli: Path, label: str):
        self.side = side
        self.cli = cli.resolve(strict=True)
        self.label = label
        self.cli_sha256 = sha256(self.cli.read_bytes())

    def identity(self) -> dict[str, Any]:
        return {"label": self.label, "cliSha256": self.cli_sha256}


def run_invocation(
    executor: Executor,
    request: dict[str, Any],
    role: str,
    inputs: list[tuple[dict[str, Any], Path]],
    stage: Path,
    action: str,
) -> dict[str, Any]:
    wait_while_flag(_PAUSE_FLAG[0] if _PAUSE_FLAG else None)
    stage.mkdir(parents=True)
    output = stage / "output.bin"
    report = stage / "report.json"
    argv = [
        str(executor.cli),
        request["workflowId"],
        action,
        "--profile",
        request["profileId"],
        *parity._cli_arguments(request, role, inputs),
        "--output",
        str(output),
        "--report",
        str(report),
    ]
    before = {str(path): sha256(path.read_bytes()) for _, path in inputs}
    started = time.perf_counter()
    try:
        result = subprocess.run(
            argv,
            cwd=stage,
            capture_output=True,
            text=True,
            encoding="utf-8",
            errors="replace",
            timeout=900,
        )
        exit_code, stdout, stderr = result.returncode, result.stdout, result.stderr
    except subprocess.TimeoutExpired as error:
        exit_code, stdout, stderr = "timeout", str(error.stdout or ""), str(error.stderr or "")
    duration = round(time.perf_counter() - started, 3)
    (stage / "argv.json").write_text(json.dumps(argv, indent=1), encoding="utf-8")
    (stage / "stdout.txt").write_text(stdout, encoding="utf-8")
    (stage / "stderr.txt").write_text(stderr, encoding="utf-8")
    after = {str(path): sha256(path.read_bytes()) for _, path in inputs}
    record: dict[str, Any] = {
        "action": action,
        "exitCode": exit_code,
        "durationSeconds": duration,
        "inputsUnchanged": before == after,
        "stderrFirstLine": redact(
            next((line.strip() for line in stderr.splitlines() if line.strip()), "")
        ),
    }
    if report.is_file():
        payload = report.read_bytes()
        record["report"] = {"size": len(payload), "sha256": sha256(payload)}
        try:
            raw = json.loads(payload.decode("utf-8-sig"))
            record["reportMapId"] = raw.get("MapId")
            record["reportIssueCount"] = len(raw.get("Issues") or [])
            record["reportOperationCount"] = len(raw.get("Operations") or [])
        except (ValueError, AttributeError):
            record["reportParse"] = "failed"
    if output.is_file():
        payload = output.read_bytes()
        record["output"] = {"size": len(payload), "sha256": sha256(payload)}
    extra = sorted(
        path.name
        for path in stage.iterdir()
        if path.name not in {"output.bin", "report.json", "argv.json", "stdout.txt", "stderr.txt"}
    )
    if extra:
        record["extraFiles"] = extra
    return record


def run_pair(
    executor: Executor,
    request: dict[str, Any],
    role: str,
    inputs: list[tuple[dict[str, Any], Path]],
    root: Path,
) -> tuple[list[dict[str, Any]], bytes | None]:
    records = []
    output: bytes | None = None
    for index, action in enumerate(("preview", "build")):
        stage = root / f"{index:02d}-{action}"
        record = run_invocation(executor, request, role, inputs, stage, action)
        records.append(record)
        if record["exitCode"] != 0:
            return records, None
        if action == "build":
            path = stage / "output.bin"
            output = path.read_bytes() if path.is_file() else None
    return records, output


def execute_route(
    executor: Executor,
    verified: parity.VerifiedCanonicalInputs,
    root: Path,
) -> dict[str, Any]:
    """Run one executor for one route; returns the local observation."""

    request = json.loads(json.dumps(verified.request))
    role = verified.execution_role
    rows = request["orderedInputs"]
    observation: dict[str, Any] = {"executor": executor.identity(), "invocations": []}
    started = time.perf_counter()
    base_payload: bytes | None = None
    if "baseRecipe" in request:
        recipe = request["baseRecipe"]
        precursor_request = {
            **{k: v for k, v in request.items() if k not in {"baseRecipe", "orderedInputs"}},
            "routeId": recipe["routeId"],
            "capabilityFingerprint": recipe["capabilityFingerprint"],
            "workflowId": "standard-merge",
            "icCountVariant": "selector-free",
            "mapVariant": recipe["mapVariant"],
            "selectionToken": "selector-free",
            "cliSelectionToken": None,
            "orderedInputs": rows[:2],
        }
        records, base_payload = run_pair(
            executor,
            precursor_request,
            role,
            [(row, Path(row["path"])) for row in rows[:2]],
            root / "base-precursor",
        )
        observation["invocations"].extend({"stage": "base-precursor", **r} for r in records)
        if base_payload is None:
            observation["status"] = "error"
            observation["failedStage"] = "base-precursor"
            observation["durationSeconds"] = round(time.perf_counter() - started, 3)
            return observation
        observation["basePrecursorOutput"] = {"size": len(base_payload), "sha256": sha256(base_payload)}
        base_dir = root / "base"
        base_dir.mkdir(parents=True)
        base_path = base_dir / "replace-base.bin"
        base_path.write_bytes(base_payload)
        base_row = {
            "slotId": "replace-base",
            "role": "input",
            "path": str(base_path),
            "size": len(base_payload),
            "sha256": sha256(base_payload),
            "order": 0,
        }
        request.pop("baseRecipe")
        request["orderedInputs"] = [base_row, *rows[2:]]
    inputs = [(row, Path(row["path"])) for row in request["orderedInputs"]]
    records, output = run_pair(executor, request, role, inputs, root / "workflow")
    observation["invocations"].extend({"stage": "workflow", **r} for r in records)
    observation["durationSeconds"] = round(time.perf_counter() - started, 3)
    if output is None:
        observation["status"] = "error"
        observation["failedStage"] = "workflow"
        return observation
    observation["status"] = "success"
    observation["output"] = {"size": len(output), "sha256": sha256(output)}
    observation["_outputBytes"] = output
    observation["_baseBytes"] = base_payload
    return observation


def case_facts(
    authority: parity.MaterializedCanonicalAuthority, route_id: str
) -> dict[str, Any]:
    manifest = parity._canonical_snapshot_json(authority, authority.manifest_relative)
    manifest["__manifestRelative"] = authority.manifest_relative
    evidence = next(row for row in manifest["routeEvidence"] if row.get("routeId") == route_id)
    case = parity._resolve_case(authority, manifest, evidence["caseId"])
    expected = next(
        (row for row in case.get("artifacts", []) if row.get("artifactId") == "expected-output"),
        None,
    )
    return {
        "routeEvidenceCaseId": evidence["caseId"],
        "resolvedCaseId": case["caseId"],
        "goldenExpectedOutput": (
            {"size": expected["size"], "sha256": expected["sha256"]} if expected else None
        ),
    }


def public_observation(observation: dict[str, Any] | None) -> dict[str, Any] | None:
    if observation is None:
        return None
    return {k: v for k, v in observation.items() if not k.startswith("_")}


def main(argv: Sequence[str] | None = None) -> int:
    parser = argparse.ArgumentParser(description=__doc__.splitlines()[0])
    parser.add_argument("--work-dir", type=Path, required=True)
    parser.add_argument("--candidate-cli", type=Path, required=True)
    parser.add_argument("--candidate-label", required=True)
    parser.add_argument("--baseline-cli", type=Path)
    parser.add_argument("--baseline-label")
    parser.add_argument("--baseline-blocker", default="")
    parser.add_argument("--route", action="append", default=[])
    parser.add_argument("--table-json", type=Path, required=True)
    parser.add_argument("--pause-flag", type=Path)
    args = parser.parse_args(argv)

    require_test_area(args.work_dir)
    _REDACTIONS.extend(
        [
            (str(args.work_dir.resolve()), "<work>"),
            (str(Path(os.environ["NFC_TEST_AREA_ROOT"]).resolve()), "<test-area>"),
        ]
    )
    if args.pause_flag is not None:
        _PAUSE_FLAG.append(args.pause_flag)
    if args.work_dir.exists():
        raise SystemExit("--work-dir must not exist yet")
    args.work_dir.mkdir(parents=True)
    run_started = time.perf_counter()
    started_at = utc_now()

    plan = parity.load_and_validate_pinned_plan(PLAN, repository_root=REPO)
    missing = set(plan.raw["canonicalInputAuthority"]["currentlyMissingRouteIds"])
    materialize_started = time.perf_counter()
    authority = parity.materialize_and_validate_canonical_input_authority(
        plan.raw,
        git_reader=parity.PinnedGitReader(REPO),
        destination=args.work_dir / "canonical",
    )
    materialize_seconds = round(time.perf_counter() - materialize_started, 3)

    candidate = Executor("candidate", args.candidate_cli, args.candidate_label)
    baseline = (
        Executor("baseline", args.baseline_cli, args.baseline_label or "baseline")
        if args.baseline_cli
        else None
    )
    selected = set(args.route)
    ordered = sorted(plan.routes, key=lambda r: r.proof_kind != "exact-output")
    observations: dict[str, dict[str, Any]] = {}
    rows: list[dict[str, Any]] = []
    for index, route in enumerate(ordered):
        if selected and route.route_id not in selected:
            continue
        wait_while_flag(args.pause_flag)
        row: dict[str, Any] = {
            "routeId": route.route_id,
            "capabilityFingerprint": route.capability_fingerprint,
            "icId": route.ic_id,
            "workflowId": route.workflow_id,
            "icCountVariant": route.ic_count_variant,
            "mapVariant": route.map_variant,
            "proofKind": route.proof_kind,
        }
        if route.proof_kind == "tp-prefix-transitive":
            row["fullRouteId"] = route.full_route_id
            row["tpLength"] = route.tp_length
        if route.route_id in missing:
            row["runnable"] = False
            row["result"] = "not-covered"
            row["reason"] = "no canonical input (canonicalInputAuthority.currentlyMissingRouteIds)"
            rows.append(row)
            print(f"[{index + 1:02d}] not covered  {route.route_id}", flush=True)
            continue
        row["runnable"] = True
        row.update(case_facts(authority, route.route_id))
        short = f"{index + 1:02d}-{sha256(route.route_id.encode())[:10]}"
        route_root = args.work_dir / "runs" / short
        (route_root).mkdir(parents=True)
        (route_root / "route.txt").write_text(route.route_id, encoding="utf-8")
        candidate_role = (
            "candidate-tp" if route.proof_kind == "tp-prefix-transitive" else "candidate-exact"
        )
        verified_candidate = parity.resolve_canonical_route_input(
            plan,
            authority,
            admitted_input_root=args.work_dir / "admitted",
            route_id=route.route_id,
            execution_role=candidate_role,
        )
        row["inputs"] = [
            {"slotId": item["slotId"], "size": item["size"], "sha256": item["sha256"]}
            for item in verified_candidate.request["orderedInputs"]
        ]
        if "baseRecipe" in verified_candidate.request:
            recipe = verified_candidate.request["baseRecipe"]
            row["baseRecipe"] = {
                "workflowId": recipe["workflowId"],
                "routeId": recipe["routeId"],
                "mapVariant": recipe["mapVariant"],
            }
        cand = execute_route(candidate, verified_candidate, route_root / "candidate")
        observations[f"candidate:{route.route_id}"] = cand
        row["candidate"] = public_observation(cand)
        base_obs = None
        if route.proof_kind == "exact-output":
            if baseline is not None:
                verified_baseline = parity.resolve_canonical_route_input(
                    plan,
                    authority,
                    admitted_input_root=args.work_dir / "admitted",
                    route_id=route.route_id,
                    execution_role="baseline-exact",
                )
                base_obs = execute_route(baseline, verified_baseline, route_root / "baseline")
                observations[f"baseline:{route.route_id}"] = base_obs
                row["baseline"] = public_observation(base_obs)
            else:
                row["baseline"] = {"status": "not-run", "reason": args.baseline_blocker}
        # Informational only: ADR 0057 forbids expected-output as the parity
        # reference. A size mismatch (TP work image) is reported as None.
        expected = row.get("goldenExpectedOutput")
        for key, obs in (("candidate", cand), ("baseline", base_obs)):
            if expected and obs and obs.get("status") == "success":
                row[f"{key}EqualsGoldenExpected"] = (
                    obs["output"] == expected
                    if obs["output"]["size"] == expected["size"]
                    else None
                )

        if route.proof_kind == "exact-output":
            if cand.get("status") != "success" or (base_obs and base_obs.get("status") != "success"):
                row["result"] = "error"
            elif base_obs is None:
                row["result"] = "candidate-only"
            else:
                comparison = compare_bytes(base_obs["_outputBytes"], cand["_outputBytes"])
                row["comparison"] = comparison
                row["result"] = "equal" if comparison["equal"] else "different"
                if base_obs.get("_baseBytes") is not None and cand.get("_baseBytes") is not None:
                    row["basePrecursorComparison"] = compare_bytes(
                        base_obs["_baseBytes"], cand["_baseBytes"]
                    )
                correction = next(
                    (
                        item
                        for item in plan.raw["approvedSemanticCorrections"]
                        if item["routeId"] == route.route_id
                    ),
                    None,
                )
                if correction is not None:
                    declared = [(r["start"], r["endExclusive"]) for r in correction["differentRanges"]]
                    observed = differing_ranges(base_obs["_outputBytes"], cand["_outputBytes"])
                    row["approvedCorrectionCheck"] = {
                        "ownerDecision": correction["ownerDecision"],
                        "baselineHashMatchesDeclared": base_obs["output"]["sha256"]
                        == correction["baselineOutput"]["sha256"],
                        "candidateHashMatchesDeclared": cand["output"]["sha256"]
                        == correction["candidateOutput"]["sha256"],
                        "differentByteCountMatchesDeclared": comparison["differentByteCount"]
                        == correction["differentByteCount"],
                        "rangesMatchDeclared": observed == declared,
                    }
        else:
            full_cand = observations.get(f"candidate:{route.full_route_id}")
            full_base = observations.get(f"baseline:{route.full_route_id}")
            tp_length = route.tp_length
            if cand.get("status") != "success" or not full_cand or full_cand.get("status") != "success":
                row["result"] = "error" if cand.get("status") != "success" else "candidate-only"
            else:
                tp = cand["_outputBytes"]
                cfull = full_cand["_outputBytes"]
                cbase = full_cand.get("_baseBytes") or b""
                checks: dict[str, Any] = {
                    "tpLengthMatches": len(tp) == tp_length,
                    "tpEqualsCandidateFullPrefix": tp == cfull[:tp_length],
                    "candidateFullTailEqualsCandidateBase": cfull[tp_length:] == cbase[tp_length:]
                    and len(cfull) == len(cbase),
                }
                if full_base and full_base.get("status") == "success":
                    bfull = full_base["_outputBytes"]
                    checks["tpEqualsBaselineFullPrefix"] = tp == bfull[:tp_length]
                    prefix = compare_bytes(bfull[:tp_length], tp)
                    row["comparison"] = {**prefix, "basis": "baseline full-route prefix [0, tpLength)"}
                    row["result"] = "equal" if all(checks.values()) else "different"
                else:
                    row["result"] = "candidate-only"
                row["transitiveChecks"] = checks
        rows.append(row)
        print(f"[{index + 1:02d}] {row['result']:<14} {route.route_id}", flush=True)

    counts: dict[str, int] = {}
    for row in rows:
        counts[row["result"]] = counts.get(row["result"], 0) + 1
    table = {
        "kind": "v0916-local-comparison-non-certifying",
        "authority": "1.1.12 board owner decision 10, option A",
        "startedAt": started_at,
        "completedAt": utc_now(),
        "durationSeconds": round(time.perf_counter() - run_started, 3),
        "canonicalMaterializeSeconds": materialize_seconds,
        "plan": {"path": "docs/contracts/v0916-parity-certification-v1.json", "sha256": plan.identity_sha256},
        "candidate": candidate.identity(),
        "baseline": baseline.identity() if baseline else {"status": "not-run", "reason": args.baseline_blocker},
        "addressSpace": ADDRESS_SPACE,
        "rangeLimitPerFile": RANGE_LIMIT,
        "counts": counts,
        "routes": rows,
    }
    args.table_json.parent.mkdir(parents=True, exist_ok=True)
    args.table_json.write_text(json.dumps(table, indent=1) + "\n", encoding="utf-8", newline="\n")
    print(json.dumps(counts), flush=True)
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
