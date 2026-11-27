"""Capture G0 output hashes through the existing CLI; compare later captures.

Usage (build this checkout's CLI with dotnet build --no-restore first;
the header commit identifies the checkout, not an attestation of an old binary):
  python scripts/output_baseline.py run
  python scripts/output_baseline.py run --route <exact-route-id> --output <baseline.json>
  python scripts/output_baseline.py compare <g0.json> <candidate.json>
  python scripts/output_baseline.py list
  python scripts/output_baseline.py kinds --output docs/references/g0-output-baseline/input-kinds.md

Work directories and CLI TEMP/TMP/TMPDIR use NFC_TEST_AREA_ROOT/temp. Only the
requested baseline is written outside that area; --output - writes to stdout.
The default is reference evidence, not an independent Golden or support claim.
Generated input paths in captures are stable recipe identities; their files
are materialized only under the per-run test-area work directory and removed.

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

Semantic owners / admission (G0-3 phase A, base a047980, R2; recipes.json R1):
* extend-owner: CanonicalInputs.prepare owns baseline input selection for
  collect/main. Canonical routeEvidence with caseId wins; recipes.json supplies
  only baseline inputs for the 27 routes that need no owner decision, never support,
  parity or Golden evidence. Exact policy fingerprints gate recipe use.
* reuse: open_golden_example.cases/artifact and the canonical manifest own
  repository case identity, artifact sizes and SHA-256. Existing parity input
  bindings supply the declared CtrlRAM slots; CLI number_choices owns tokens.
* reuse: GeneralMergeCliCommandTests, ReplaceCliCommandTests.General and
  Nt51950Nt51951V2StandardMergeGoldenTests supply public synthetic input shapes.
  The recipe declares patterns/overlays as data, not firmware transformations.
* reject-duplicate: MergeCliCommandHandler.ManualMappings and
  ReplaceCliCommandHandler.General already dispatch manual General mappings
  through Application. Translate recipe options only; do not execute mappings,
  initialization, CRC/header or naming here. No production/profile/testdata
  authority changes. Single writer owns this script, its test and recipes.json.
* gates: focused Python tests and scoped review of the uncommitted patch;
  host owns CLI smoke/full capture and integration verification. No commit.

Semantic owners / admission (G0-3 phase B, base 1391b76, local patch, R2):
* extend-owner: CanonicalInputs and recipes.json own all 94 baseline input kinds
  and the portable inventory dispositions projected from G03 input-gaps.json/md.
  Owner decisions D2/D3/D4 (2026-11-27) authorize 6 source-view/cross-case,
  10 AB-reference and 13 behavior-only synthetic recipes. D1 stays unavailable.
* reuse: canonical case/artifact validation owns source identity, size and hash;
  AbCtrlRamReferencePlanTests, AbCtrlRamAuthoringTests and the inventory own the
  declared members/counts/banks and byte construction. The generic interpreter
  applies only ordered copy/fill/literal data; no firmware-field interpretation.
* reject-duplicate: CLI/Application still own Merge/Replace, bank planning,
  CRC/header, integrity, naming and route selection. Input kind is provenance,
  never parity, support or a change to canonical routeEvidence/Golden authority.
  Kinds classify captured source artifacts: the 15 existing CLI Standard Merge
  intermediate bases retain native DP/TP sources and separate base_build evidence.
* single writer: script, focused Python tests, recipes and generated kind list.
  Gates: focused offline Python tests and scoped uncommitted-diff review;
  host owns dotnet tests, real CLI execution, full capture and independent review.

Semantic owners / admission (phase B round 2, Commander 2026-11-27 13:20):
* reuse: profile output.allowOverride declares whether the baseline may send
  --output; the CLI resolves automatic names in its working directory and its
  committed report owns the actual filename. All other CLI arguments stay intact.
* extend-owner: CanonicalInputs accepts a reason-only unavailable generated
  recipe without changing its kind. Six failed D4 bases become same-IC derived
  recipes; NT51919 has no direct same-IC payload and remains unavailable.
* round 3 (Commander 2026-11-27 14:18): the CRC processor refused the derived
  NT51929 cascade and NT51932 single bases. Those 2 routes and the 4 NT51925
  routes without a real base keep their kind and become reason-only unavailable.
* reuse: canonical provenance/artifact hashes and the existing runtime fixture
  catalog establish source identity and bounded backup placement. All input
  copy/fill/literal operations remain recipe data. No processor, timeout, range,
  profile, naming rule, Golden output or product behavior changes are authorized.
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
RECIPES = REPO / "docs/references/g0-output-baseline/recipes.json"
INPUT_KINDS_MARKDOWN = RECIPES.with_name("input-kinds.md")
DEFAULT_CLI = REPO / "src/NvtFwCombiner.Cli/bin/Debug/net10.0/NvtFwCombiner.Cli.dll"
TOOL_VERSION = "4"
STATUSES = ("ok", "input-unavailable", "failed")
INPUT_KINDS = ("native", "derived", "synthetic", "private-native")


def load_routes() -> list[dict]:
    """The exact policy inventory materialized by the Support Matrix source."""
    routes = json.loads((REPO / POLICY).read_bytes())["routes"]
    unique_routes(routes)
    return sorted(routes, key=lambda row: row["routeId"])


def unique_routes(routes: list[dict]) -> None:
    ids = [row["routeId"] for row in routes]
    if len(ids) != len(set(ids)):
        raise ValueError("Duplicate route IDs")


def load_recipes() -> dict[str, dict]:
    document = json.loads(RECIPES.read_bytes())
    if document["schemaVersion"] != 2:
        raise ValueError("Unsupported baseline recipe schema version")
    rows = document["recipes"]
    unique_routes(rows)
    if rows != sorted(rows, key=lambda row: row["routeId"]):
        raise ValueError("Baseline recipes must be sorted by routeId")
    for row in rows:
        kind = row["kind"]
        if kind not in INPUT_KINDS:
            raise ValueError("Unknown baseline input kind")
        if kind == "private-native":
            if any(key in row for key in ("caseId", "bindings", "inputs", "source")):
                raise ValueError("Private-native recipe cannot have repository inputs")
        elif kind == "native":
            if "inputs" in row or not (row.get("source") == "route-evidence" or "caseId" in row):
                raise ValueError("Native recipe requires unchanged repository artifacts")
        else:
            if "missingInputs" in row:
                missing = row["missingInputs"]
                if (not isinstance(missing, list) or not missing
                        or any(not isinstance(reason, str) or not reason.strip() for reason in missing)
                        or any(key in row for key in ("caseId", "source", "inputs", "bindings"))):
                    raise ValueError("Unavailable generated recipe requires reasons and no input declarations")
                continue
            if "caseId" in row or "source" in row or not row.get("inputs"):
                raise ValueError("Generated recipe requires input declarations, not a repository case")
            for spec in row["inputs"]:
                if ("operations" in spec) != (kind == "derived"):
                    raise ValueError("Input kind differs from byte generator recipe")
                if kind == "synthetic" and any(key in spec for key in ("caseId", "artifactId", "source")):
                    raise ValueError("Synthetic recipe cannot read repository artifacts")
    return {row["routeId"]: row for row in rows}


def input_size(size: int) -> int:
    if type(size) is not int or not 0 < size <= 16 * 1024 * 1024:
        raise ValueError("Prepared input size must be in [1, 16777216]")
    return size


def derived_bytes(spec: dict, read_source) -> bytes:
    """Apply declared byte operations only; all offsets are input-file relative."""
    size = input_size(spec["size"])
    data, covered = bytearray(size), bytearray(size)
    if not spec["operations"]:
        raise ValueError("Derived input requires declared operations")
    for operation in spec["operations"]:
        kind = operation["kind"]
        if kind == "copy":
            source = read_source(operation["source"])
            view = operation["sourceRange"]
            start, end = view["start"], view["endExclusive"]
            if (view["addressSpace"] != "repository-artifact" or type(start) is not int
                    or type(end) is not int or not 0 <= start < end <= len(source)):
                raise ValueError("Derived operation exceeds source bounds or address space")
            payload = source[start:end]
        elif kind == "fill":
            length, value = input_size(operation["length"]), operation["value"]
            if type(value) is not int or not 0 <= value <= 255:
                raise ValueError("Derived fill must be a byte")
            payload = bytes([value]) * length
        elif kind == "literal":
            payload = bytes.fromhex(operation["hex"])
            if not payload:
                raise ValueError("Derived literal must not be empty")
        else:
            raise ValueError("Unknown derived byte operation")
        targets = operation["targetOrder"]
        if (type(operation["copyCount"]) is not int or operation["copyCount"] < 1
                or operation["copyCount"] != len(targets)):
            raise ValueError("Derived copy count differs from target order")
        for target in targets:
            start = target["start"]
            if (target["bank"] not in {"a", "b", "none"} or type(start) is not int
                    or start < 0 or start + len(payload) > size):
                raise ValueError("Derived operation exceeds target bounds or bank declaration")
            data[start:start + len(payload)] = payload
            covered[start:start + len(payload)] = b"\x01" * len(payload)
    if 0 in covered:
        raise ValueError("Derived operations leave undeclared input bytes")
    return bytes(data)


def synthetic_bytes(spec: dict) -> bytes:
    """Generate input data only; patterns and literal overlays are recipe facts."""
    size = input_size(spec["size"])
    if ("fill" in spec) == ("pattern" in spec):
        raise ValueError("Synthetic input requires exactly one fill or pattern")

    def byte(value):
        if type(value) is not int or not 0 <= value <= 255:
            raise ValueError("Synthetic pattern/fill must use byte values")
        return value

    if "fill" in spec:
        data = bytearray([byte(spec["fill"])]) * size
    else:
        pattern = spec["pattern"]
        seed, step = byte(pattern["seed"]), byte(pattern["step"])
        cycle = bytes((seed + index * step) % 256 for index in range(256))
        data = bytearray((cycle * ((size + 255) // 256))[:size])
    for overlay in spec.get("overlays", []):
        offset, payload = overlay["offset"], bytes.fromhex(overlay["hex"])
        if type(offset) is not int or offset < 0 or offset + len(payload) > size:
            raise ValueError("Synthetic overlay exceeds input bounds")
        data[offset:offset + len(payload)] = payload
    return bytes(data)


class CanonicalInputs:
    """Project declared evidence into CLI input options, without firmware logic."""

    def __init__(self, number_choices=None) -> None:
        self.number_choices = number_choices
        manifest = json.loads((examples.GOLDEN / "manifest.json").read_bytes())
        unique_routes(manifest["routeEvidence"])
        self.evidence = {row["routeId"]: row for row in manifest["routeEvidence"]}
        self.manifest_case_ids = {row["caseId"] for row in manifest["cases"]}
        self.cases = examples.cases()
        self.recipes = load_recipes()
        authority = json.loads((REPO / "docs/contracts/v0916-parity-certification-v1.json").read_bytes())
        bindings = authority["canonicalInputAuthority"]
        # Reuse input recipes only. The historical plan's hashes/support claims
        # are not current authority; current routeEvidence and CLI identity are.
        self.bases = {row["routeId"]: row for row in bindings["ctrlRamBaseRoutes"]}
        self.ctrlram = {row["caseId"]: row for row in bindings["ctrlRamExecutionBindings"]}
        self.routes = {row["routeId"]: row for row in load_routes()}
        if set(self.recipes) != set(self.routes):
            raise ValueError("Every policy route requires exactly one input-kind recipe")
        for route_id, recipe in self.recipes.items():
            if recipe["capabilityFingerprint"] != self.routes[route_id]["capabilityFingerprint"]:
                raise ValueError("Baseline recipe fingerprint differs from Support Matrix policy")
            if recipe.get("source") == "route-evidence" and "caseId" not in self.evidence.get(route_id, {}):
                raise ValueError("Native route-evidence recipe requires a case binding")

    def case(self, case_id: str, *, require_manifest: bool = False) -> dict:
        visited = set()
        while case_id not in visited:
            if require_manifest and case_id not in self.manifest_case_ids:
                raise ValueError("Baseline recipe case is not in canonical manifest")
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
            recipe = self.recipes.get(route["routeId"])
            return self.prepare_recipe(route, recipe, inputs) if recipe else ({}, required)
        if evidence["capabilityFingerprint"] != route["capabilityFingerprint"]:
            raise ValueError("Route evidence fingerprint differs from Support Matrix policy")
        try:
            case = self.case(evidence["caseId"])
        except KeyError:
            return {"input_source": "route-evidence", "input_kind": "native"}, required
        artifacts = {row["artifactId"]: row for row in case["artifacts"]}
        args = [workflow, "build", "--profile", route["icId"]]
        request = {"arguments": args, "input_source": "route-evidence", "input_kind": "native"}
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
        return request, self.bind_artifacts(case, bindings, inputs, args, base_args)

    @staticmethod
    def bind_artifacts(case: dict, bindings: list[tuple], inputs: list[dict],
                       args: list[str], base_args: list[str] | None = None) -> list[str]:
        artifacts = {row["artifactId"]: row for row in case["artifacts"]}
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
        return missing

    def prepare_recipe(self, route: dict, recipe: dict, inputs: list[dict]) -> tuple[dict, list[str]]:
        if recipe["capabilityFingerprint"] != route["capabilityFingerprint"]:
            raise ValueError("Baseline recipe fingerprint differs from Support Matrix policy")
        workflow = route["workflowId"]
        args = [workflow, "build", "--profile", route["icId"]]
        kind = recipe["kind"]
        request = {"arguments": args, "input_source": f"recipe-{kind}", "input_kind": kind}
        if kind == "private-native" or (kind in {"derived", "synthetic"} and "missingInputs" in recipe):
            return request, recipe["missingInputs"]
        if kind == "native":
            case = self.case(recipe["caseId"], require_manifest=True)
            if workflow == "ctrlram-replace":
                args += ["--ic-num", self.ic_number_token(route, case)]
            elif workflow == "ab-merge":
                topology = {"1-ic": "single", "2-plus-ic": "cascade"}.get(route["icCountVariant"])
                if topology:
                    args += ["--ab-topology", topology]
            allowed = {"standard-merge": {"--dp", "--tp", "--ldc"},
                       "ab-merge": {"--dp-ab", "--tp-a", "--tp-b"},
                       "ctrlram-replace": {"--base", "--ctrlram"}}.get(workflow, set())
            bindings = []
            for row in recipe["bindings"]:
                if row["option"] not in allowed or (row["option"] == "--ctrlram") != ("slotId" in row):
                    raise ValueError("Invalid repository recipe CLI binding")
                bindings.append((row["artifactId"], row["option"], row.get("slotId")))
            return request, self.bind_artifacts(case, bindings, inputs, args)
        if kind not in {"derived", "synthetic"}:
            raise ValueError("Unknown baseline recipe kind")
        specs = recipe["inputs"]
        names = [spec["name"] for spec in specs]
        if len(names) != len(set(names)) or any(
                re.fullmatch(r"[a-zA-Z0-9][a-zA-Z0-9_.-]*\.bin", name) is None for name in names):
            raise ValueError("Generated inputs require unique plain BIN names")
        request[kind] = []
        for spec in specs:
            if kind == "derived":
                spec = self.resolve_derived(spec, inputs)
                payload = derived_bytes(spec, self.read_derived_source)
            else:
                payload = synthetic_bytes(spec)
            request[kind].append(spec)
            inputs.append({"name": spec["name"], "path": f"{kind}/{route['routeId']}/{spec['name']}",
                           "sha256": hashlib.sha256(payload).hexdigest(), "size": len(payload)})
        if workflow == "general-merge":
            args += ["--size", hex(recipe["size"]), "--fill", f"0x{recipe['fill']:02x}"]
        elif workflow == "general-replace":
            args += ["--ic-num", recipe["icNumber"]]
        elif workflow == "ctrlram-replace":
            args += ["--ic-num", self.ic_number_token(route, {"icCount": recipe["icCount"]})]
            if "bank" in recipe:
                args += ["--bank", recipe["bank"]]
        elif workflow == "ab-merge":
            if "topology" in recipe:
                args += ["--ab-topology", recipe["topology"]]
        elif workflow != "standard-merge":
            raise ValueError("Unsupported generated recipe workflow")
        for row in recipe["bindings"]:
            if kind == "derived" and "artifactId" in row:
                case = self.case(row["caseId"], require_manifest=True)
                if row["option"] != "--ctrlram" or not row.get("slotId", "").startswith("replace-ctrlram-"):
                    raise ValueError("Invalid derived repository binding")
                missing = self.bind_artifacts(case, [(row["artifactId"], "--ctrlram", row["slotId"])], inputs, args)
                if missing:
                    return request, missing
                continue
            if row["input"] not in names:
                raise ValueError("Generated binding names an undeclared input")
            value = f"<{kind}:{row['input']}>"
            option = row["option"]
            if option == "--mapping" and workflow in {"general-merge", "general-replace"}:
                prefix = f"{hex(row['sourceStart'])}+" if workflow == "general-merge" else ""
                value = f"{prefix}{hex(row['targetStart'])}+{hex(row['length'])}={value}"
            elif option == "--ctrlram" and workflow == "ctrlram-replace" and row.get("slotId", "").startswith("replace-ctrlram-"):
                value = f"{row['slotId']}={value}"
            elif option not in {"standard-merge": {"--dp", "--tp"}, "ab-merge": {"--dp-ab", "--tp-a", "--tp-b"},
                                "ctrlram-replace": {"--base"}, "general-replace": {"--base"}}.get(workflow, set()):
                raise ValueError("Invalid generated recipe CLI binding")
            args += [option, value]
        return request, []

    def resolve_derived(self, spec: dict, inputs: list[dict]) -> dict:
        resolved = json.loads(json.dumps(spec))
        for operation in resolved["operations"]:
            if operation["kind"] != "copy":
                continue
            source = operation["source"]
            case = self.case(source["caseId"], require_manifest=True)
            path = examples.artifact(case, source["artifactId"])
            artifact = next(row for row in case["artifacts"] if row["artifactId"] == source["artifactId"])
            source.update(path=path.relative_to(REPO).as_posix(), size=artifact["size"], sha256=artifact["sha256"].lower())
            record = {"name": f"source:{source['caseId']}/{source['artifactId']}",
                      "path": source["path"], "sha256": source["sha256"], "size": source["size"]}
            if record not in inputs:
                inputs.append(record)
        return resolved

    @staticmethod
    def read_derived_source(source: dict) -> bytes:
        path = (REPO / source["path"]).resolve()
        if not path.is_relative_to(examples.GOLDEN.resolve()):
            raise ValueError("Derived source escapes canonical repository artifacts")
        payload = path.read_bytes()
        if len(payload) != source["size"] or hashlib.sha256(payload).hexdigest() != source["sha256"]:
            raise ValueError("Derived source size or SHA-256 changed")
        return payload


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
        # Project the declared profile policy into CLI options; the compiler
        # and CLI still validate the policy and render the actual filename.
        self._automatic_name_maps = set()
        for path in (REPO / "profiles/built-in").glob("*/profiles/*.json"):
            profile = json.loads(path.read_bytes())
            if profile.get("output", {}).get("allowOverride") is False:
                self._automatic_name_maps.update(profile["mapBinding"]["mapIds"])

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
            arguments = list(request["arguments"])
            generated = {}
            for kind in ("synthetic", "derived"):
                for spec in request.get(kind, []):
                    path = work / spec["name"]
                    payload = (synthetic_bytes(spec) if kind == "synthetic" else
                               derived_bytes(spec, CanonicalInputs.read_derived_source))
                    path.write_bytes(payload)
                    generated[f"{kind}/{route['routeId']}/{spec['name']}"] = path
                    token = f"<{kind}:{spec['name']}>"
                    arguments = [arg.replace(token, str(path)) for arg in arguments]
            if "base_build" in request:
                base = request["base_build"]
                outputs = self.build(base["route"], base["arguments"], work / "base")
                captured["base_build"] = {"route_id": base["route"]["routeId"], "outputs": outputs}
                arguments = [str(work / "base/outputs/output.bin") if arg == "<generated-base>" else arg
                             for arg in arguments]
            captured["outputs"] = self.build(route, arguments, work / "final")
            for row in inputs:
                path = generated.get(row["path"], REPO / row["path"])
                with path.open("rb") as stream:
                    if hashlib.file_digest(stream, "sha256").hexdigest() != row["sha256"]:
                        raise ValueError("CLI input changed during execution")
            return captured

    def build(self, route: dict, arguments: list[str], work: Path) -> list[dict]:
        output_dir = work / "outputs"
        output_dir.mkdir(parents=True)
        report_path = work / "report.json"
        automatic_name = route.get("mapVariant") in self._automatic_name_maps
        output_options = [] if automatic_name else ["--output", str(output_dir / "output.bin")]
        command = ["dotnet", str(self.cli), *arguments, *output_options, "--report", str(report_path)]
        environment = dict(os.environ, TEMP=str(self.temp), TMP=str(self.temp), TMPDIR=str(self.temp))
        result = subprocess.run(command, cwd=output_dir if automatic_name else work,
                                env=environment, capture_output=True,
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
    kinds = {route_id: row["kind"] for route_id, row in load_recipes().items()}
    records = []
    for route in sorted(routes, key=lambda row: row["routeId"]):
        record = {"route_id": route["routeId"], "status": "ok", "inputs": [], "outputs": [],
                  "input_source": None, "input_kind": kinds.get(route["routeId"], "native")}
        try:
            request, missing = prepare(route, record["inputs"])
            record["input_source"] = request.get("input_source") if isinstance(request, dict) else None
            if isinstance(request, dict):
                record["input_kind"] = request.get("input_kind", record["input_kind"])
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
    kind_counts = Counter(row["input_kind"] for row in records)
    return {"header": {"schema_version": 1, "commit": commit, "tool_version": TOOL_VERSION,
                       "product_version": product_version, "route_count": len(records),
                       "counts": {status: counts[status] for status in STATUSES},
                       "input_kind_counts": {kind: kind_counts[kind] for kind in INPUT_KINDS}}, "routes": records}


def render_input_kinds(catalog: CanonicalInputs) -> str:
    """Project recipes and their portable G03 inventory dispositions, without CLI execution."""
    counts = Counter(row["kind"] for row in catalog.recipes.values())
    lines = ["# G0 baseline input kinds", "",
             "Generated by `python scripts/output_baseline.py kinds`; edit `recipes.json`, then regenerate.", "",
             "Scope: all 94 policy routes. Inventory dispositions are the portable summaries in",
             "`recipes.json`, transcribed from G03 `input-gaps.json` / `input-gaps.md` and the owner's",
             "2026-11-27 D2/D3/D4 and phase B round 2 decisions. D1 remains input-unavailable",
             "pending private CI inputs; routes without a usable same-IC base also remain unavailable.", "",
             "Input provenance is independent of routeEvidence, support and Golden certification.",
             "Kinds classify captured source artifacts. For 15 native routes the existing CLI",
             "Standard Merge assembles an intermediate base; captures record it separately as",
             "`base_build`. Their original DP/TP files remain unchanged; see the rows below.",
             "Synthetic data proves behavior only, never hardware parity. Derived data also supplies",
             "baseline inputs only; copied expected artifacts are references, not new Golden outputs.", "",
             ", ".join(f"{kind}: {counts[kind]}" for kind in INPUT_KINDS) + ".", "",
             "All recipe source ranges are half-open in `repository-artifact` address space; target",
             "offsets address the prepared input file. Operations and targetOrder execute in list order.", "",
             "| Route ID | Input kind | Source files or recipe | Why it is not native | Native replacement needs |",
             "| --- | --- | --- | --- | --- |"]
    for route_id, recipe in catalog.recipes.items():
        kind = recipe["kind"]
        if recipe.get("source") == "route-evidence":
            source = f"routeEvidence case `{catalog.evidence[route_id]['caseId']}` (unchanged CLI artifacts)"
            if catalog.bases.get(route_id, {}).get("kind") == "standard-merge":
                source += "; CLI Standard Merge intermediate `base_build` from original DP/TP"
        elif kind == "native":
            source = f"case `{recipe['caseId']}`: " + ", ".join(f"`{row['artifactId']}`" for row in recipe["bindings"])
        elif kind == "private-native":
            source = "D1 private repository recipe pending; no repository input"
        elif recipe.get("missingInputs"):
            source = "Input unavailable: " + "; ".join(recipe["missingInputs"])
        else:
            sources = []
            for spec in recipe["inputs"]:
                if kind == "synthetic":
                    sources.append(f"`{spec['name']}`: generator, {spec['size']} bytes")
                else:
                    for op in spec["operations"]:
                        if op["kind"] == "copy":
                            view, member = op["sourceRange"], op["source"]
                            sources.append(f"`{spec['name']}`: `{member['caseId']}/{member['artifactId']}` "
                                           f"[{hex(view['start'])}, {hex(view['endExclusive'])}) x{op['copyCount']}")
                    sources.append(f"`{spec['name']}`: {len(spec['operations'])} ordered byte operations")
            if "bank" in recipe:
                sources.append(f"bank `{recipe['bank']}`, IC count {recipe['icCount']}")
            sources.append("route recipe in `recipes.json` (all bindings and literals)")
            source = "; ".join(sources)
        disposition = recipe["inventory"]
        cells = [f"`{route_id}`", f"`{kind}`", source, disposition["reason"], disposition["nativeReplacement"]]
        lines.append("| " + " | ".join(cell.replace("|", "\\|").replace("\n", " ") for cell in cells) + " |")
    return "\n".join(lines) + "\n"


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
    kinds = sub.add_parser("kinds", help="Generate the per-route input provenance list without executing")
    kinds.add_argument("--output", type=Path, help="Write Markdown here; default is stdout")
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
    if args.mode == "kinds":
        payload = render_input_kinds(CanonicalInputs()).encode("utf-8")
        if args.output:
            args.output.write_bytes(payload)
        else:
            sys.stdout.buffer.write(payload)
        return 0
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
