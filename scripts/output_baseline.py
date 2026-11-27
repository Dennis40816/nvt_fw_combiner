"""Capture G0 output hashes through the existing CLI; compare later captures.

Usage (build this checkout's CLI with dotnet build --no-restore first;
the header commit identifies the checkout, not an attestation of an old binary):
  python scripts/output_baseline.py run
  python scripts/output_baseline.py run --route <exact-route-id> --output <baseline.json>
  python scripts/output_baseline.py compare <g0.json> <candidate.json>
  python scripts/output_baseline.py list

Work directories and CLI TEMP/TMP/TMPDIR use NFC_TEST_AREA_ROOT/temp. Only the
requested baseline is written outside that area; --output - writes to stdout.
The default is reference evidence, not an independent Golden or support claim.

Semantic owners / admission (G0-2, base 7cac3ff, local patch, R2):
* reuse: CanonicalCapabilityCatalogSource partitions every policy route into
  fixed/dynamic definitions; CanonicalSupportMatrixQuery unions and sorts them.
  Read that same policy inventory without deriving IDs or filtering support.
* reuse: canonical manifest routeEvidence binds route ID/fingerprint to caseId;
  open_golden_example owns artifact hash validation and example input selection.
  Existing parity contract supplies explicit CtrlRAM TP-base and slot bindings.
* reuse: CLI build -> Application authoring/execution owns all firmware bytes.
  Verify its Capability report so a different selected map cannot pass silently.
* reuse (round 2): CLI workflows list -> CapabilitySelectorPublication and
  WorkflowIcNumberChoiceProjection own CtrlRAM IC-number tokens. Select only
  from those choices using declared route variants and case icCount; cache
  each profile's listing, including errors, once per run.
* extend-owner: this script owns deterministic capture and output comparison;
  no prior general G0 capture exists. No profiles, naming or Golden bytes change.
"""

from __future__ import annotations

import argparse
import hashlib
import json
import os
import re
import subprocess
import sys
import tempfile
import uuid
from collections import Counter
from pathlib import Path

sys.dont_write_bytecode = True

try:
    from scripts import open_golden_example as examples
except ModuleNotFoundError as error:
    if error.name != "scripts":
        raise
    import open_golden_example as examples


REPO = Path(__file__).resolve().parents[1]
POLICY = "docs/contracts/canonical-capability-policy-v1.json"
DEFAULT_BASELINE = REPO / "docs/references/g0-output-baseline/baseline.json"
DEFAULT_CLI = REPO / "src/NvtFwCombiner.Cli/bin/Debug/net10.0/NvtFwCombiner.Cli.dll"
TOOL_VERSION = "1"
STATUSES = ("ok", "input-unavailable", "failed")


def load_routes() -> list[dict]:
    """The exact policy inventory materialized by the Support Matrix source."""
    routes = json.loads((REPO / POLICY).read_bytes())["routes"]
    unique_routes(routes)
    return sorted(routes, key=lambda row: row["routeId"])


def unique_routes(routes: list[dict]) -> None:
    ids = [row["routeId"] for row in routes]
    if len(ids) != len(set(ids)):
        raise ValueError("Duplicate route IDs")


class CanonicalInputs:
    """Project declared evidence into CLI input options, without firmware logic."""

    def __init__(self, number_choices=None) -> None:
        self.number_choices = number_choices
        manifest = json.loads((examples.GOLDEN / "manifest.json").read_bytes())
        unique_routes(manifest["routeEvidence"])
        self.evidence = {row["routeId"]: row for row in manifest["routeEvidence"]}
        self.cases = examples.cases()
        authority = json.loads((REPO / "docs/contracts/v0916-parity-certification-v1.json").read_bytes())
        bindings = authority["canonicalInputAuthority"]
        # Reuse input recipes only. The historical plan's hashes/support claims
        # are not current authority; current routeEvidence and CLI identity are.
        self.bases = {row["routeId"]: row for row in bindings["ctrlRamBaseRoutes"]}
        self.ctrlram = {row["caseId"]: row for row in bindings["ctrlRamExecutionBindings"]}
        self.routes = {row["routeId"]: row for row in load_routes()}

    def case(self, case_id: str) -> dict:
        visited = set()
        while case_id not in visited:
            visited.add(case_id)
            case = self.cases[case_id]
            if "alias" not in case:
                return case
            case_id = case["alias"]["sourceCaseId"]
        raise ValueError("Canonical case alias cycle")

    def ic_number_token(self, route: dict, case: dict) -> str:
        count = case.get("icCount")
        if type(count) is not int or count < 1:
            raise ValueError("CtrlRAM IC number requires a declared positive case icCount")
        if self.number_choices is None:
            raise ValueError("CtrlRAM IC number choices source is required")
        choices = sorted(set(self.number_choices(route["icId"])))
        numeric = [token for token in choices if token.isdecimal() and int(token) == count]
        if len(numeric) == 1:
            return numeric[0]
        variant = route["icCountVariant"]
        if not numeric:
            if variant == "1-ic" and count == 1 and "single" in choices:
                return "single"
            non_single = [token for token in choices if token != "single"]
            if (variant in {"2-ic", "3-ic", "2-plus-ic", "2-8-ic"}
                    and count > 1 and len(non_single) == 1):
                return non_single[0]
        raise ValueError(f"Cannot select CtrlRAM IC number for {route['icId']}: "
                         f"icCountVariant={variant}, icCount={count}, choices=[{', '.join(choices)}]")

    def prepare(self, route: dict, inputs: list[dict]) -> tuple[dict, list[str]]:
        workflow = route["workflowId"]
        required = {
            "standard-merge": ["dp-input", "tp-input"],
            "ab-merge": ["dp-ab-input", "tp-a-input", "tp-b-input"],
            "ctrlram-replace": ["replace-base", "ctrlram"],
            "general-merge": ["saved-rule", "slot-inputs"],
            "general-replace": ["replace-base", "mappings"],
        }[workflow]
        evidence = self.evidence.get(route["routeId"])
        if evidence is None or "caseId" not in evidence:
            return {}, required
        if evidence["capabilityFingerprint"] != route["capabilityFingerprint"]:
            raise ValueError("Route evidence fingerprint differs from Support Matrix policy")
        try:
            case = self.case(evidence["caseId"])
        except KeyError:
            return {}, required
        artifacts = {row["artifactId"]: row for row in case["artifacts"]}
        args = [workflow, "build", "--profile", route["icId"]]
        request = {"arguments": args}
        base_args = None
        if workflow == "standard-merge":
            bindings = [("dp-input", "--dp", None), ("tp-input", "--tp", None)]
            if "ldc-input" in artifacts:
                bindings.append(("ldc-input", "--ldc", None))
        elif workflow == "ab-merge":
            bindings = [("dp-ab-input", "--dp-ab", None),
                        ("tp-a-input", "--tp-a", None), ("tp-b-input", "--tp-b", None)]
            topology = {"1-ic": "single", "2-plus-ic": "cascade"}.get(route["icCountVariant"])
            if topology:
                args += ["--ab-topology", topology]
        elif workflow == "ctrlram-replace":
            args += ["--ic-num", self.ic_number_token(route, case)]
            recipe = self.ctrlram.get(case["caseId"], {})
            base = self.bases.get(route["routeId"], {})
            if base.get("kind") == "tp-input":
                base_id = recipe.get("tpBaseArtifactId", "tp-input")
                bindings = [(base_id, "--base", None)]
            elif base.get("kind") == "standard-merge":
                full_recipe = recipe.get("fullBaseRecipe")
                base_route = self.routes.get(base["standardMergeRouteId"])
                if not full_recipe or base_route is None:
                    return {}, ["standard-merge-base-recipe"]
                if base_route["workflowId"] != "standard-merge" or base_route["icId"] != route["icId"]:
                    raise ValueError("Declared base recipe does not identify the same IC's Standard Merge route")
                base_args = ["standard-merge", "build", "--profile", base_route["icId"]]
                request["base_build"] = {"route": base_route, "arguments": base_args}
                args += ["--base", "<generated-base>"]
                bindings = [(full_recipe["dpArtifactId"], "--dp", None),
                            (full_recipe["tpArtifactId"], "--tp", None)]
            else:
                bindings = [("reference-base", "--base", None)]
            replacements = [(row["artifactId"], row["slotId"]) for row in case["artifacts"]
                            if row.get("slotId", "").startswith("replace-ctrlram-")]
            if not replacements:
                replacements = [(row["artifactId"], row["slotId"])
                                for row in recipe.get("replacements", [])]
            if not replacements:
                return {}, ["ctrlram"]
            bindings += [(artifact_id, "--ctrlram", slot) for artifact_id, slot in replacements]
        else:
            return {}, required
        missing = []
        for artifact_id, option, slot in bindings:
            name = slot or artifact_id
            if artifact_id not in artifacts:
                missing.append(name)
                continue
            try:
                path = examples.artifact(case, artifact_id)
            except FileNotFoundError:
                missing.append(name)
                continue
            repository_path = path.relative_to(REPO).as_posix()
            inputs.append({"name": name, "path": repository_path,
                           "sha256": artifacts[artifact_id]["sha256"].lower()})
            target_args = base_args if base_args is not None and option in {"--dp", "--tp"} else args
            target_args += [option, f"{slot}={path}" if slot else str(path)]
        return request, missing


def first_error(error: Exception | str) -> str:
    """Keep the first diagnostic, without unstable host paths or run identities."""
    message = error.strerror if isinstance(error, OSError) and error.strerror else str(error)
    lines = [line.strip() for line in message.splitlines() if line.strip() and line.strip() != "Issues:"]
    line = lines[0] if lines else "Build failed without a diagnostic"
    line = re.sub(r"(?:[A-Za-z]:[\\/]|/)[^\r\n\"'<>]*", "<path>", line)
    line = re.sub(r"\b[0-9a-fA-F]{8}(?:-[0-9a-fA-F]{4}){3}-[0-9a-fA-F]{12}\b", "<run-id>", line)
    return re.sub(r"\b\d{4}-\d{2}-\d{2}T[0-9:.+-]+Z?\b", "<time>", line)


class CliExecutor:
    def __init__(self, cli: Path, test_root: Path) -> None:
        self.cli = cli.resolve()
        self.temp = test_root / "temp"
        self._number_choices: dict[str, tuple[str, ...] | str] = {}

    def number_choices(self, profile: str) -> tuple[str, ...]:
        if profile not in self._number_choices:
            try:
                command = ["dotnet", str(self.cli), "workflows", "list",
                           "--workflow", "ctrlram-replace", "--profile", profile]
                environment = dict(os.environ, TEMP=str(self.temp), TMP=str(self.temp), TMPDIR=str(self.temp))
                result = subprocess.run(command, cwd=REPO, env=environment, capture_output=True,
                                        text=True, encoding="utf-8", errors="replace", check=False)
                if result.returncode:
                    raise RuntimeError(result.stderr or f"CLI exited with code {result.returncode}")
                choices = set()
                for line in result.stdout.splitlines():
                    line = line.strip()
                    if not line or line == f"profile={profile}":
                        continue
                    match = re.fullmatch(rf"profile={re.escape(profile)} ic-num=(\S+)(?: slot=\S+)?", line)
                    if match is None:
                        raise ValueError("Unexpected CtrlRAM workflows list output")
                    choices.add(match.group(1))
                if not choices:
                    raise ValueError("No CtrlRAM IC number choices listed")
                self._number_choices[profile] = tuple(sorted(choices))
            except (OSError, ValueError, RuntimeError) as error:
                self._number_choices[profile] = f"Cannot list CtrlRAM IC number choices for {profile}: {first_error(error)}"
        cached = self._number_choices[profile]
        if isinstance(cached, str):
            raise ValueError(cached)
        return cached

    def __call__(self, route: dict, request: dict, inputs: list[dict]) -> dict:
        # Windows tempfile retries access-denied mkdirs when os.access reports
        # writable. A direct probe reports sandbox/ACL denial without that loop.
        probe = self.temp / f"g0-baseline-probe-{uuid.uuid4().hex}"
        probe.mkdir()
        probe.rmdir()
        with tempfile.TemporaryDirectory(prefix="g0-baseline-", dir=self.temp) as temporary:
            work = Path(temporary)
            captured = {}
            arguments = request["arguments"]
            if "base_build" in request:
                base = request["base_build"]
                outputs = self.build(base["route"], base["arguments"], work / "base")
                captured["base_build"] = {"route_id": base["route"]["routeId"], "outputs": outputs}
                arguments = [str(work / "base/outputs/output.bin") if arg == "<generated-base>" else arg
                             for arg in arguments]
            captured["outputs"] = self.build(route, arguments, work / "final")
            for row in inputs:
                with (REPO / row["path"]).open("rb") as stream:
                    if hashlib.file_digest(stream, "sha256").hexdigest() != row["sha256"]:
                        raise ValueError("CLI input changed during execution")
            return captured

    def build(self, route: dict, arguments: list[str], work: Path) -> list[dict]:
        output_dir = work / "outputs"
        output_dir.mkdir(parents=True)
        report_path = work / "report.json"
        # Existing CLI override avoids clock-based names. Automatic product
        # naming is outside this byte baseline's comparison contract.
        command = ["dotnet", str(self.cli), *arguments, "--output", str(output_dir / "output.bin"),
                   "--report", str(report_path)]
        environment = dict(os.environ, TEMP=str(self.temp), TMP=str(self.temp), TMPDIR=str(self.temp))
        result = subprocess.run(command, cwd=work, env=environment, capture_output=True,
                                text=True, encoding="utf-8", errors="replace", check=False)
        if result.returncode:
            raise RuntimeError(result.stderr or f"CLI exited with code {result.returncode}")
        report = json.loads(report_path.read_bytes())
        capability = report.get("Capability") or {}
        if (capability.get("RouteId") != route["routeId"] or
                capability.get("CapabilityFingerprint") != route["capabilityFingerprint"]):
            raise ValueError("CLI executed a different route or capability fingerprint")
        outputs = []
        for path in sorted(output_dir.rglob("*")):
            if path.is_file():
                with path.open("rb") as stream:
                    digest = hashlib.file_digest(stream, "sha256").hexdigest()
                outputs.append({"name": path.relative_to(output_dir).as_posix(),
                                "sha256": digest, "size": path.stat().st_size})
        primary = report.get("Output", {})
        expected = {"name": primary.get("FileName"), "sha256": primary.get("Sha256"), "size": primary.get("Size")}
        if not primary.get("Committed") or expected not in outputs:
            raise ValueError("CLI did not commit the reported output bytes")
        return outputs


def collect(routes: list[dict], prepare, execute, commit: str, product_version: str) -> dict:
    unique_routes(routes)
    records = []
    for route in sorted(routes, key=lambda row: row["routeId"]):
        record = {"route_id": route["routeId"], "status": "ok", "inputs": [], "outputs": []}
        try:
            request, missing = prepare(route, record["inputs"])
            if missing:
                record["status"] = "input-unavailable"
                record["missing_inputs"] = sorted(missing)
            else:
                captured = execute(route, request, record["inputs"])
                record["outputs"] = sorted(captured["outputs"], key=lambda row: row["name"])
                if "base_build" in captured:
                    record["base_build"] = captured["base_build"]
        except (OSError, ValueError, KeyError, RuntimeError) as error:
            record["status"] = "failed"
            record["error"] = first_error(error)
        record["inputs"].sort(key=lambda row: (row["name"], row["path"]))
        records.append(record)
    counts = Counter(row["status"] for row in records)
    return {"header": {"schema_version": 1, "commit": commit, "tool_version": TOOL_VERSION,
                       "product_version": product_version, "route_count": len(records),
                       "counts": {status: counts[status] for status in STATUSES}}, "routes": records}


def encode(baseline: dict) -> bytes:
    return (json.dumps(baseline, ensure_ascii=False, indent=2) + "\n").encode("utf-8")


def compare(before: dict, after: dict) -> int:
    def index(value):
        rows = value["routes"]
        ids = [row["route_id"] for row in rows]
        if len(ids) != len(set(ids)):
            raise ValueError("Duplicate route IDs in baseline")
        return {row["route_id"]: (row["status"], sorted(row["outputs"], key=lambda output: output["name"]))
                for row in rows}

    old, new = index(before), index(after)
    changed = False
    for route_id in sorted(old.keys() | new.keys()):
        kind = "added" if route_id not in old else "removed" if route_id not in new else "changed"
        if old.get(route_id) != new.get(route_id):
            print(f"{kind} {route_id}")
            changed = True
    return int(changed)


def test_area() -> Path:
    declaration = os.environ.get("NFC_TEST_AREA_ROOT", "")
    path = Path(declaration)
    if not declaration or not path.is_absolute():
        raise ValueError("Set NFC_TEST_AREA_ROOT to the existing absolute external test area")
    path = path.resolve(strict=True)
    if path == REPO or path.is_relative_to(REPO) or not (path / "temp").is_dir():
        raise ValueError("NFC_TEST_AREA_ROOT must be outside the repository with an existing temp child")
    return path


def main(argv: list[str] | None = None) -> int:
    parser = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    sub = parser.add_subparsers(dest="mode", required=True)
    sub.add_parser("list", help="List exact Support Matrix route IDs without executing")
    run = sub.add_parser("run", help="Capture every route, or a selected smoke subset")
    run.add_argument("--route", action="append", default=[], help="Exact route ID; repeat for a subset")
    run.add_argument("--output", default=str(DEFAULT_BASELINE), help="Baseline JSON path, or - for stdout")
    diff = sub.add_parser("compare", help="Compare output identities, ignoring run header and input metadata")
    diff.add_argument("before", type=Path)
    diff.add_argument("after", type=Path)
    args = parser.parse_args(argv)
    if args.mode == "run" and args.route and Path(args.output).resolve() == DEFAULT_BASELINE:
        parser.error("Subset capture requires --output under NFC_TEST_AREA_ROOT, or --output -")
    if args.mode == "compare":
        return compare(json.loads(args.before.read_bytes()), json.loads(args.after.read_bytes()))
    routes = load_routes()
    if args.mode == "list":
        print("\n".join(row["routeId"] for row in routes))
        return 0
    selected = set(args.route)
    unknown = selected - {row["routeId"] for row in routes}
    if unknown:
        parser.error("Unknown route IDs: " + ", ".join(sorted(unknown)))
    routes = [row for row in routes if not selected or row["routeId"] in selected]
    root = test_area()
    destination = None if args.output == "-" else Path(args.output).resolve()
    if destination is not None and destination != DEFAULT_BASELINE and not destination.is_relative_to(root):
        raise ValueError("Output must be the declared repository baseline or a path under NFC_TEST_AREA_ROOT")
    executor = CliExecutor(DEFAULT_CLI, root)
    catalog = CanonicalInputs(executor.number_choices)
    commit = subprocess.run(["git", "rev-parse", "HEAD"], cwd=REPO, check=True,
                            capture_output=True, text=True).stdout.strip()
    result = collect(routes, catalog.prepare, executor, commit, (REPO / "VERSION").read_text(encoding="utf-8").strip())
    payload = encode(result)
    if args.output == "-":
        sys.stdout.buffer.write(payload)
    else:
        assert destination is not None
        destination.parent.mkdir(parents=True, exist_ok=True)
        destination.write_bytes(payload)
    print(json.dumps(result["header"]["counts"]), file=sys.stderr)
    return int(result["header"]["counts"]["failed"] > 0)


if __name__ == "__main__":
    try:
        sys.exit(main())
    except (OSError, ValueError, KeyError, subprocess.CalledProcessError) as error:
        print(f"error: {first_error(error)}", file=sys.stderr)
        sys.exit(2)
