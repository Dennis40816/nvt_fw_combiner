"""Rolling orchestration with B1 fake hosts and synthetic, local-only payloads."""

from __future__ import annotations

import copy
from dataclasses import asdict
import hashlib
import json
import os
from pathlib import Path
import subprocess
import tempfile
import unittest
from unittest.mock import patch

from scripts import predecessor_comparison as execution
from scripts import predecessor_rolling as rolling
from scripts import predecessor_validation as validation
from scripts import v0916_parity_certification as parity
from tests.scripts.test_predecessor_comparison import FakeGitHost, FakeProcessHost
from tests.scripts.test_predecessor_report_reader import raw_report
from scripts.render_release_notes import REQUIRED_FEATURE_FIELDS


ROOT = Path(__file__).resolve().parents[2]
FIXTURES = Path(__file__).parent / "fixtures/predecessor-comparison"
CANDIDATE, BASELINE, TAG_OBJECT = "1" * 40, "4" * 40, "7" * 40
PAYLOAD = b"x" * 160
CHANGED = b"y" + PAYLOAD[1:]
APPROVAL = {"boardDecision": "1.2.2 board decision 251", "role": "firmware-owner", "date": "2026-10-02"}


def encoded(value):
    return json.dumps(value, sort_keys=True, separators=(",", ":")).encode()


def identity(payload):
    return {"size": len(payload), "sha256": hashlib.sha256(payload).hexdigest()}


def notes(ids):
    fields = "\n".join(f"{field} synthetic." for field in REQUIRED_FEATURE_FIELDS)
    return (f"# Changelog\n\n## [1.2.2]\n\n### Summary\n\nSynthetic.\n\n### Product changes\n\n"
            f"#### 1. Synthetic\n\n{fields}\n- Declared: {' '.join(ids)}.\n\n### Security\n\nNone.\n\n"
            "### Known issues\n\nNone.\n\n### Upgrade and rollback\n\nSynthetic.\n\n"
            "### Downloads and integrity\n\nSynthetic.\n")


def synthetic_world():
    route = {"icId": "NT51950", "workflowId": "standard-merge", "icCountVariant": "selector-free",
             "mapVariant": "synthetic", "routeId": "route-synthetic", "capabilityFingerprint": "a" * 64,
             "authoring": {"value": "available"}, "publication": {"value": "supported"}}
    artifacts = [{"artifactId": slot, "role": "input", "path": slot + ".bin", **identity(PAYLOAD)}
                 for slot in ("dp-input", "tp-input")]
    scenario = {**{key: route[key] for key in ("icId", "workflowId", "icCountVariant", "mapVariant", "routeId")},
                "scenarioId": "NT51950:standard-merge:selector-free:synthetic:synthetic-case",
                "origin": "synthetic", "binding": "route-evidence", "evidenceCaseId": "synthetic-case",
                "inputCaseId": "synthetic-case", "planRouteId": None, "inputRevision": 1,
                "cli": {"profile": "NT51950", "selectionOption": None, "selectionToken": None}, "ctrlRamBase": None,
                "inputs": [{"order": index, "slotId": row["artifactId"], "artifactId": row["artifactId"],
                            "size": row["size"], "sha256": row["sha256"]} for index, row in enumerate(artifacts)]}
    debt = [f"route-debt-{index:02d}" for index in range(27)]
    ledger = {"schemaVersion": "1.0", "kind": "predecessor-comparison-coverage-ledger", "scenarios": [scenario],
              "debtSet": {"source": rolling.PLAN + "#canonicalInputAuthority.currentlyMissingRouteIds",
                          "decisions": ["1.1.12 board decision 12", "1.1.12 board decision 47", "1.1.12 board decision 60"],
                          "routeIds": debt}, "acceptedGaps": [],
              "pendingAcceptedGaps": {"decision": "1.1.12 board decision 60", "status": "awaiting-owner-approval", "routeIds": []},
              "retiredScenarios": []}
    return {"ledger": ledger, "baseline_ledger": copy.deepcopy(ledger), "policy": {"routes": [route]},
            "case": {"caseId": "synthetic-case", "artifacts": artifacts},
            "manifest": {"cases": [{"caseId": "synthetic-case", "manifestPath": "case.json"}],
                         "routeEvidence": [{"routeId": route["routeId"], "caseId": "synthetic-case", "kind": "direct-golden"}]},
            "plan": {"canonicalInputAuthority": {"manifestPath": "testdata/golden/canonical/manifest.json",
                                                  "currentlyMissingRouteIds": debt, "ctrlRamBaseRoutes": [], "ctrlRamExecutionBindings": []}},
            "declaration": {"schemaVersion": "1.0", "kind": "predecessor-comparison-declaration", "candidateVersion": "1.2.2",
                            "baseline": {"tag": "v1.2.1", "tagObject": TAG_OBJECT}, "ledgerSha256": "", "entries": []}}


def add_ctrlram(world, kind="standard-merge"):
    standard = world["ledger"]["scenarios"][0]
    route = {**world["policy"]["routes"][0], "routeId": "route-ctrlram", "workflowId": "ctrlram-replace",
             "icCountVariant": "1-ic", "mapVariant": "synthetic-ctrlram"}
    row = {**copy.deepcopy(standard), **{key: route[key] for key in ("workflowId", "icCountVariant", "mapVariant", "routeId")},
           "scenarioId": "NT51950:ctrlram-replace:1-ic:synthetic-ctrlram:synthetic-case", "planRouteId": "route-plan-ctrlram",
           "binding": "case", "cli": {"profile": "NT51950", "selectionOption": "--ic-num", "selectionToken": "single"},
           "ctrlRamBase": {"kind": kind}}
    replacement = {"artifactId": "replacement", "role": "input", "path": "replacement.bin", **identity(PAYLOAD)}
    world["case"]["artifacts"].append(replacement)
    replacement_input = {"order": 2, "slotId": "replace-ctrlram-master", "artifactId": "replacement", **identity(PAYLOAD)}
    row["inputs"].append(replacement_input)
    if kind == "standard-merge":
        row["ctrlRamBase"]["standardMergeMapVariant"] = standard["mapVariant"]
        # The Standard Merge scenario compares the same case's full ordered inputs.
        standard["inputs"].append({**replacement_input, "slotId": "replacement"})
    else:
        row["inputs"] = [{"order": 0, "slotId": "replace-base", "artifactId": "tp-input", **identity(PAYLOAD)},
                         {**replacement_input, "order": 1}]
        standard["inputs"].append({**replacement_input, "slotId": "replacement"})
    world["policy"]["routes"].append(route)
    world["ledger"]["scenarios"].append(row)
    world["plan"]["canonicalInputAuthority"]["ctrlRamExecutionBindings"] = [{
        "caseId": "synthetic-case", "tpBaseArtifactId": "tp-input",
        "fullBaseRecipe": {"dpArtifactId": "dp-input", "tpArtifactId": "tp-input"},
        "replacements": [{"artifactId": "replacement", "slotId": "replace-ctrlram-master"}]}]
    world["plan"]["canonicalInputAuthority"]["ctrlRamBaseRoutes"] = [{
        "routeId": "route-plan-ctrlram", "kind": kind,
        **({"standardMergeMapVariant": standard["mapVariant"]} if kind == "standard-merge" else {})}]
    world["baseline_ledger"] = copy.deepcopy(world["ledger"])
    return row


def declared_entry(world, *, outcome="different", baseline_payload=PAYLOAD, candidate_payload=None,
                   precursor=None, scenario=None, output_differs=True):
    if candidate_payload is None:
        candidate_payload = CHANGED if outcome == "different" else PAYLOAD
    row = scenario or world["ledger"]["scenarios"][0]
    def side(rejected, payload, base):
        return {"result": "rejected" if rejected else "output", "output": None if rejected else identity(payload),
                "precursor": None if base is None else identity(base), "stage": "preview" if rejected else None,
                "issueCodes": ["synthetic.product-rejection"] if rejected else []}
    def difference(left, right):
        ranges = execution.compare_output_bytes(left, right).ranges
        if not ranges:
            return None
        projection = validation.range_projection(ranges)
        return {key: projection[key] for key in ("differentByteCount", "rangeCount", "rangeListSha256")} | {
            "ranges": ranges, "attribution": [{**span, "mechanism": "writes-different-bytes", "cause": "synthetic",
                                                 "causeVerification": "not-independently-verified", "evidence": []} for span in ranges]}
    bases = precursor or (None, None)
    entry = {"id": f"RP-1.2.2-{len(world['declaration']['entries']) + 1:02d}",
             "kind": validation.OUTCOME_ENTRY_KINDS[outcome], "scenarioIds": [row["scenarioId"]], "routeIds": [row["routeId"]],
             "expected": {"baseline": side(outcome in ("baseline-rejects", "both-reject"), baseline_payload, bases[0]),
                          "candidate": side(outcome in ("candidate-rejects", "both-reject"), candidate_payload, bases[1])},
             "differences": {"output": difference(baseline_payload, candidate_payload) if output_differs and outcome == "different" else None,
                             "precursor": difference(*bases) if bases[0] is not None else None},
             "knownIssue": {"bugId": "BUG-20261002-synthetic"} if outcome in ("candidate-rejects", "both-reject") else None,
             "routeWithdrawal": False, "approval": dict(APPROVAL)}
    if entry["differences"] == {"output": None, "precursor": None}:
        entry["differences"] = None
    world["declaration"]["entries"].append(entry)
    return entry


class SyntheticReader:
    def __init__(self, files):
        self.files = files
    def list_files(self, commit):
        return list(self.files)
    def read_file(self, commit, path):
        return self.files[path]
    def entry(self, path):
        if path == "testdata/golden/canonical":
            return "040000", "tree", "3" * 40
        return "100644", "blob", hashlib.sha1(b"blob " + str(len(self.files[path])).encode() + b"\0" + self.files[path]).hexdigest()


class RollingFakeGit(FakeGitHost):
    def __init__(self, world):
        super().__init__()
        self.world = world
        self.commits = {}
        self.roots = {}
        self.tags = [validation.RollingTag("v1.2.0", "tag", "6" * 40, BASELINE, True, None),
                     validation.RollingTag("v1.2.1", "tag", TAG_OBJECT, BASELINE, True, None)]
        self.refresh()

    def refresh(self):
        world = self.world
        contract = json.loads((ROOT / rolling.CONTRACT).read_bytes())
        files = {**self.files, **{path: b"{}" for path in rolling.APPLIED_CONTRACTS},
                 rolling.CONTRACT: encoded(contract), "VERSION": b"1.2.2\n",
                 rolling.LEDGER: encoded(world["ledger"]), rolling.POLICY: encoded(world["policy"]), rolling.PLAN: encoded(world["plan"]),
                 "testdata/golden/canonical/manifest.json": encoded(world["manifest"]),
                 "testdata/golden/canonical/case.json": encoded(world["case"])}
        for path in ("scripts/predecessor_comparison.py", "scripts/predecessor_rolling.py",
                     "scripts/predecessor_validation.py", "scripts/predecessor_report_reader.py",
                     "scripts/v0916_parity_certification.py", "scripts/render_release_notes.py",
                     "scripts/canonical_golden_validation.py"):
            files[path] = (ROOT / path).read_bytes()
        for row in world["case"]["artifacts"]:
            files["testdata/golden/canonical/" + row["path"]] = PAYLOAD
        world["declaration"]["ledgerSha256"] = rolling.sha256(files[rolling.LEDGER])
        files["docs/contracts/predecessor-comparison-declarations/1.2.2.json"] = encoded(world["declaration"])
        files["CHANGELOG.md"] = notes([row["id"] for row in world["declaration"]["entries"]]).encode()
        self.commits[CANDIDATE] = files
        self.commits[BASELINE] = {**files, rolling.LEDGER: encoded(world["baseline_ledger"])}

    def list_files(self, commit):
        return list(self.commits[commit])
    def read_file(self, commit, path):
        return self.commits[commit][path]
    def snapshot_reader(self, commit):
        return SyntheticReader(self.commits[commit])
    def commit_tree(self, commit):
        return "2" * 40 if commit == CANDIDATE else "5" * 40
    def stable_tags(self, candidate_commit):
        return self.tags
    def git_tag_object(self, ref):
        return next((tag.tag_object for tag in self.tags if ref in (tag.tag, tag.tag_object)), "0" * 40)
    def git_tag_commit(self, tag_object):
        return next((tag.commit for tag in self.tags if tag.tag_object == tag_object), "0" * 40)
    def git_head(self, root):
        return self.roots[Path(root)]
    def git_tree(self, root):
        return self.commit_tree(self.git_head(root))
    def detached_worktree(self, commit, temporary_root, name):
        from contextlib import contextmanager
        @contextmanager
        def detached():
            previous = self.files
            self.files = self.commits[commit]
            try:
                with super(RollingFakeGit, self).detached_worktree(commit, temporary_root, name) as root:
                    self.roots[root] = commit
                    yield root
            finally:
                self.files = previous
        return detached()


class SyntheticProcesses(FakeProcessHost):
    def __init__(self, git, behavior="equal", payload=CHANGED, precursor_payload=None):
        super().__init__(self.respond)
        self.git, self.behavior, self.payload, self.precursor_payload = git, behavior, payload, precursor_payload
        self.invocations = []

    def respond(self, argv, cwd):
        if argv[:2] == ["dotnet", "--version"]:
            return subprocess.CompletedProcess(argv, 0, "10.0.100\n", "")
        if argv[:2] == ["dotnet", "build"]:
            contract = json.loads(self.git.commits[CANDIDATE][rolling.CONTRACT])
            target = cwd / contract["executor"]["cliAssembly"]
            target.parent.mkdir(parents=True, exist_ok=True)
            target.write_bytes(b"candidate" if self.git.git_head(cwd) == CANDIDATE else b"baseline")
        if argv[0] == "dotnet":
            return subprocess.CompletedProcess(argv, 0, "", "")
        side = Path(argv[0]).read_bytes().decode()
        workflow, action = argv[1:3]
        self.invocations.append((side, workflow, action))
        rejection = self.behavior == "both-reject" or self.behavior == side + "-rejects"
        output = self.payload if side == "candidate" and self.behavior in ("different", "error-output", "lying-output") else PAYLOAD
        if self.precursor_payload is not None and side == "candidate" and workflow == "standard-merge":
            output = self.precursor_payload
        if workflow == "standard-merge" and self.precursor_payload is not None:
            rejection = False
        value = raw_report()
        value.update(ProfileId="NT51950", IcId="NT51950", ModeId=workflow, ExperienceId=workflow)
        inputs = []
        index = argv.index("--profile") + 2
        while index < argv.index("--output"):
            option, path = argv[index:index + 2]
            index += 2
            if option in ("--ic-num", "--ab-topology"):
                continue
            if option == "--ctrlram":
                slot, path = path.split("=", 1)
            else:
                slot = {"--dp": "dp-input", "--tp": "tp-input", "--base": "replace-base"}.get(option, option[2:])
            artifact = ("replace-base" if slot == "replace-base" and Path(path).name == "precursor.bin"
                        else Path(path).stem.split("-", 1)[1])
            inputs.append({"AddressSpaceId": slot, "ArtifactId": artifact, **{
                "Size": Path(path).stat().st_size, "Sha256": rolling.sha256(Path(path).read_bytes())}, "OriginalFileName": None})
        value["Inputs"] = inputs
        span = {"Start": 0, "Length": 160, "EndExclusive": 160}
        value["Operations"][0].update(SourceSpaceId=inputs[0]["AddressSpaceId"], SourceRange=dict(span), TargetRange=dict(span))
        value["Mutations"][0].update(TargetRange=dict(span), ChangedByteCount=160)
        value["Output"].update(Size=len(output), Sha256=rolling.sha256(output))
        if action == "preview":
            value.update(Output=None, Mutations=[])
        if rejection:
            value.update(Output=None, Operations=[], Mutations=[], CompilationFingerprint=None)
            value["Issues"] = [{"Code": "synthetic.product-rejection", "Severity": "error"}]
        exit_code = 1 if rejection else 0
        if side == "candidate":
            if self.behavior == "crash":
                raise OSError("synthetic")
            if self.behavior == "timeout":
                raise subprocess.TimeoutExpired(argv, 1800)
            if self.behavior == "missing-report":
                return subprocess.CompletedProcess(argv, 1, "", "synthetic.product-rejection")
            if self.behavior == "missing-output" and action == "build":
                value["Output"] = None
            if self.behavior in ("process-failed", "start-failed"):
                value.update(Output=None, Operations=[], Mutations=[], CompilationFingerprint=None,
                             Issues=[{"Code": "external-tool.process." + ("failed" if self.behavior == "process-failed" else "start-failed"), "Severity": "error"}])
                exit_code = 1
            if action == "build" and self.behavior == "error-output":
                value["Issues"] = [{"Code": "synthetic.product-rejection", "Severity": "error"}]
            if action == "build" and self.behavior == "lying-output":
                value["Output"]["Sha256"] = "0" * 64
            if action == "build" and self.behavior == "self-widened":
                value["Operations"][0]["TargetRange"].update(Length=161, EndExclusive=161)
                value["Mutations"][0]["TargetRange"].update(Length=161, EndExclusive=161)
        Path(argv[argv.index("--report") + 1]).write_bytes(json.dumps(value).encode())
        if value["Output"] is not None:
            Path(argv[argv.index("--output") + 1]).write_bytes(output)
        return subprocess.CompletedProcess(argv, exit_code, "", "")


class RollingTests(unittest.TestCase):
    def setUp(self):
        self.world = synthetic_world()
        self.scratch = tempfile.TemporaryDirectory(prefix="b2a-", dir=os.environ["TEMP"])
        self.root = Path(self.scratch.name)
        self.settings = self.root / "settings"
        self.settings.mkdir()
        self.counter = 0

    def tearDown(self):
        for path in self.root.rglob("*"):
            if path.is_file():
                path.chmod(0o600)
        self.scratch.cleanup()

    def run_world(self, behavior="equal", *, payload=CHANGED, precursor_payload=None, git=None, formal=False):
        git = git or RollingFakeGit(self.world)
        host = SyntheticProcesses(git, behavior, payload, precursor_payload)
        self.counter += 1
        temporary = self.root / f"run-{self.counter}"
        output = self.root / f"report-{self.counter}.json"
        captures = []
        def materialize(plan, *, git_reader, destination):
            captures.append(plan)
            files = git_reader.files
            descriptor = plan["canonicalInputAuthority"]
            self.assertEqual(CANDIDATE, descriptor["repositoryCommit"])
            self.assertEqual(rolling.sha256(files[descriptor["manifestPath"]]), descriptor["manifestRawSha256"])
            self.assertEqual(len(files[descriptor["manifestPath"]]), descriptor["manifestSize"])
            self.assertEqual(git_reader.entry("testdata/golden/canonical")[2], descriptor["canonicalRootTree"])
            self.assertEqual(git_reader.entry(descriptor["manifestPath"])[2], descriptor["manifestBlob"])
            destination.mkdir()
            return parity.MaterializedCanonicalAuthority(destination, descriptor["manifestRawSha256"], descriptor["manifestPath"], files)
        loader = rolling.load_rolling_sources
        def stable_sources(*args, **kwargs):
            sources = loader(*args, **kwargs)
            return sources._replace(authority=sources.authority._replace(comparator_sha256="c" * 64))
        with patch.object(rolling, "load_rolling_sources", side_effect=stable_sources):
            report = rolling.run_rolling(git=git, host=host, candidate_commit=CANDIDATE, baseline_tag="v1.2.1",
                                         output_path=output, temporary_root=temporary, settings_folder=self.settings,
                                         formal=formal, materializer=materialize,
                                         published=type("Published", (), {"is_complete_published": lambda self, tag: True})() if formal else None)
        self.assertEqual(1, len(captures))
        self.assertEqual(parity.canonical_json_bytes(report) + b"\n", output.read_bytes())
        return report, host, git

    def test_equal_declared_difference_and_typed_rejections_complete(self):
        for behavior in ("equal", "different", "baseline-rejects", "candidate-rejects", "both-reject"):
            with self.subTest(behavior=behavior):
                self.world = synthetic_world()
                if behavior != "equal":
                    declared_entry(self.world, outcome=behavior)
                report, _, _ = self.run_world(behavior)
                self.assertEqual("clear", report["gate"]["result"], report["gate"])
                self.assertEqual(behavior, report["scenarios"][0]["outcome"])
                self.assertFalse(report["formal"])

    def test_runner_and_report_share_the_same_contract_admission(self):
        runners = []
        runner_type = execution.ProcessRunner
        def create_runner(*args, **kwargs):
            runner = runner_type(*args, **kwargs)
            runners.append(runner)
            return runner
        with (patch.object(execution, "ProcessRunner", side_effect=create_runner),
              patch.object(rolling, "build_rolling_report", wraps=rolling.build_rolling_report) as builder):
            report, _, _ = self.run_world()
        self.assertEqual(1, len(runners))
        admission = runners[0].admission
        self.assertIs(admission, builder.call_args.kwargs["admission"])
        self.assertEqual(admission.formal, runners[0].formal)
        self.assertEqual(admission.formal, report["formal"])
        self.assertTrue(admission.diagnostic)

    def test_each_scenario_runs_exactly_once_per_side_and_precursors_are_side_local(self):
        add_ctrlram(self.world)
        report, host, git = self.run_world()
        self.assertEqual("clear", report["gate"]["result"])
        for side in ("baseline", "candidate"):
            self.assertEqual(2, host.invocations.count((side, "standard-merge", "preview")))
            self.assertEqual(2, host.invocations.count((side, "standard-merge", "build")))
            self.assertEqual(1, host.invocations.count((side, "ctrlram-replace", "preview")))
            self.assertEqual(1, host.invocations.count((side, "ctrlram-replace", "build")))
        self.assertEqual([BASELINE, CANDIDATE], [commit for commit, _ in git.detached])

    def test_tp_work_is_compared_directly_without_precursor(self):
        add_ctrlram(self.world, "tp-input")
        report, host, _ = self.run_world()
        self.assertEqual("clear", report["gate"]["result"])
        self.assertIsNone(report["scenarios"][1]["candidate"]["precursor"])
        self.assertEqual(8, len(host.invocations))

    def test_undeclared_difference_and_stale_entry_block(self):
        for behavior in ("different", "equal"):
            with self.subTest(behavior=behavior):
                self.world = synthetic_world()
                if behavior == "equal":
                    declared_entry(self.world)
                report, _, _ = self.run_world(behavior)
                code = "PREDECESSOR_UNDECLARED_CHANGE" if behavior == "different" else "PREDECESSOR_STALE_DECLARATION"
                self.assertIn(code, {item["code"] for item in report["gate"]["failures"]})

    def test_changed_ranges_including_beyond_display_limit_are_not_reproduced(self):
        baseline = PAYLOAD
        original = bytearray(baseline)
        for index in range(33):
            original[index * 4] = ord("y")
        for index in (0, 32):
            with self.subTest(index=index):
                self.world = synthetic_world()
                entry = declared_entry(self.world, candidate_payload=bytes(original))
                changed = bytearray(original)
                changed[index * 4], changed[index * 4 + 1] = ord("x"), ord("y")
                entry["expected"]["candidate"]["output"] = identity(bytes(changed))
                report, _, _ = self.run_world("different", payload=bytes(changed))
                comparison = report["scenarios"][0]["comparison"]
                self.assertEqual(33, comparison["rangeCount"])
                self.assertEqual(32, len(comparison["ranges"]))
                self.assertTrue(comparison["rangesTruncated"])
                self.assertIn("PREDECESSOR_STALE_DECLARATION", {item["code"] for item in report["gate"]["failures"]})
                self.assertTrue(any("output difference is not reproduced" in item["detail"] for item in report["gate"]["failures"]))

    def test_union_of_writes_cannot_replace_exact_attribution(self):
        entry = declared_entry(self.world)
        entry["differences"]["output"]["attribution"][0]["endExclusive"] = 160
        report, _, _ = self.run_world("different")
        self.assertIn("PREDECESSOR_STALE_DECLARATION", {item["code"] for item in report["gate"]["failures"]})

    def test_precursor_carried_and_precursor_only_changes_need_exact_both_scope_attributions(self):
        for carried, exact in ((True, True), (True, False), (False, True), (False, False)):
            with self.subTest(carried=carried, exact=exact):
                self.world = synthetic_world()
                ctrl = add_ctrlram(self.world)
                declared_entry(self.world, candidate_payload=CHANGED)
                entry = declared_entry(self.world, scenario=ctrl, candidate_payload=CHANGED if carried else PAYLOAD,
                                       precursor=(PAYLOAD, CHANGED), output_differs=carried)
                if carried:
                    entry["differences"]["output"]["attribution"][0]["mechanism"] = "precursor-carried"
                if not exact:
                    entry["differences"]["precursor"]["attribution"][0]["endExclusive"] = 2
                report, _, _ = self.run_world("different" if carried else "equal", precursor_payload=CHANGED)
                self.assertEqual("clear" if exact else "blocked", report["gate"]["result"], report["gate"])

    def test_stopped_write_attribution_accepts_only_the_observed_range(self):
        entry = declared_entry(self.world)
        entry["differences"]["output"]["attribution"][0]["mechanism"] = "stopped-write-preserved-bytes"
        report, _, _ = self.run_world("different")
        self.assertEqual("clear", report["gate"]["result"])

    def test_removed_scenario_relisted_as_gap_still_requires_retirement(self):
        add_ctrlram(self.world)
        removed = self.world["ledger"]["scenarios"].pop()
        self.world["ledger"]["pendingAcceptedGaps"]["routeIds"].append(removed["routeId"])
        report, _, _ = self.run_world()
        self.assertIn("PREDECESSOR_COVERAGE_UNDISPOSED", {item["code"] for item in report["gate"]["failures"]})
        self.assertIn("PREDECESSOR_UNDECLARED_CHANGE", {item["code"] for item in report["gate"]["failures"]})

    def test_input_revision_without_disposition_blocks(self):
        self.world["ledger"]["scenarios"][0]["inputRevision"] = 2
        report, _, _ = self.run_world()
        self.assertIn("PREDECESSOR_UNDECLARED_CHANGE", {item["code"] for item in report["gate"]["failures"]})

    def test_new_route_and_successor_debt_are_refused(self):
        for successor in (False, True):
            with self.subTest(successor=successor):
                self.world = synthetic_world()
                route = {**self.world["policy"]["routes"][0], "routeId": "route-successor", "mapVariant": "successor"}
                self.world["policy"]["routes"].append(route)
                if successor:
                    self.world["ledger"]["debtSet"]["routeIds"][0] = route["routeId"]
                with self.assertRaises(execution.ExecutionError) as found:
                    self.run_world()
                self.assertEqual("PREDECESSOR_COVERAGE_UNDISPOSED", found.exception.code)

    def test_old_release_gap_approval_does_not_carry_forward(self):
        route = {**self.world["policy"]["routes"][0], "routeId": "route-gap", "mapVariant": "gap"}
        self.world["policy"]["routes"].append(route)
        gap = {"routeId": "route-gap", "approvedInVersion": "1.2.1", "reason": "synthetic",
               "approval": APPROVAL, "declarationEntryId": "RP-1.2.1-01"}
        self.world["ledger"]["acceptedGaps"].append(gap)
        self.world["baseline_ledger"]["acceptedGaps"].append(copy.deepcopy(gap))
        report, _, _ = self.run_world()
        self.assertIn("PREDECESSOR_COVERAGE_UNDISPOSED", {item["code"] for item in report["gate"]["failures"]})

    def test_process_failures_can_never_be_approved_rejections(self):
        for behavior in ("crash", "timeout", "missing-report", "process-failed", "start-failed", "missing-output"):
            with self.subTest(behavior=behavior):
                self.world = synthetic_world()
                declared_entry(self.world, outcome="candidate-rejects")
                report, host, _ = self.run_world(behavior)
                self.assertEqual("invalid", report["scenarios"][0]["outcome"])
                self.assertEqual("PREDECESSOR_PROCESS_FAILED", report["scenarios"][0]["failureCode"])
                self.assertEqual(1, host.invocations.count(("candidate", "standard-merge", "preview")))
                if behavior != "missing-output":
                    self.assertNotIn(("candidate", "standard-merge", "build"), host.invocations)

    def test_error_issue_output_lie_and_self_widening_are_invalid(self):
        for behavior in ("error-output", "lying-output", "self-widened"):
            with self.subTest(behavior=behavior):
                self.world = synthetic_world()
                report, _, _ = self.run_world(behavior)
                self.assertEqual("invalid", report["scenarios"][0]["outcome"])
                self.assertEqual("PREDECESSOR_REPORT_INVALID", report["scenarios"][0]["failureCode"])

    def test_precursor_build_without_output_stops_that_side_and_preserves_invalid_report(self):
        add_ctrlram(self.world)
        report, host, _ = self.run_world("missing-output")
        ctrl = report["scenarios"][1]
        self.assertEqual("invalid", ctrl["outcome"])
        self.assertEqual("PREDECESSOR_PROCESS_FAILED", ctrl["failureCode"])
        self.assertEqual("precursor-build", ctrl["candidate"]["stoppedAt"])
        self.assertNotIn(("candidate", "ctrlram-replace", "preview"), host.invocations)

    def test_rejection_and_cause_dispositions_require_the_contract_approval(self):
        for change in ("missing-known-issue", "null-withdrawal", "string-withdrawal", "both-withdrawal", "role", "cause", "verification", "missing-evidence", "empty-range"):
            with self.subTest(change=change):
                self.world = synthetic_world()
                behavior = "candidate-rejects" if change in ("missing-known-issue", "null-withdrawal", "string-withdrawal") else "both-reject" if change == "both-withdrawal" else "different"
                entry = declared_entry(self.world, outcome=behavior)
                if change in ("missing-known-issue", "null-withdrawal", "string-withdrawal"):
                    entry["knownIssue"] = None
                    if change != "missing-known-issue":
                        entry["routeWithdrawal"] = None if change == "null-withdrawal" else "withdrawn"
                elif change == "both-withdrawal":
                    entry["routeWithdrawal"] = True
                elif change == "role":
                    entry["approval"]["role"] = "release-owner"
                else:
                    attribution = entry["differences"]["output"]["attribution"][0]
                    if change == "cause":
                        attribution["mechanism"] = "union-of-writes"
                    elif change == "verification":
                        attribution["causeVerification"] = "unconditional"
                    elif change == "empty-range":
                        entry["differences"]["output"]["attribution"].append({**attribution, "start": 1, "endExclusive": 1})
                    else:
                        attribution["causeVerification"] = "supported-by-cited-evidence"
                report, _, _ = self.run_world(behavior)
                self.assertIn("PREDECESSOR_STALE_DECLARATION", {item["code"] for item in report["gate"]["failures"]})

    def test_formal_comparator_modules_must_match_candidate_source(self):
        for path in ("scripts/predecessor_comparison.py", "scripts/predecessor_rolling.py",
                     "scripts/predecessor_validation.py", "scripts/predecessor_report_reader.py",
                     "scripts/v0916_parity_certification.py", "scripts/render_release_notes.py",
                     "scripts/canonical_golden_validation.py"):
            with self.subTest(path=path):
                git = RollingFakeGit(self.world)
                git.commits[CANDIDATE][path] += b"\n# synthetic source mismatch\n"
                with patch.object(execution, "admit_execution_contract"), self.assertRaises(execution.ExecutionError) as found:
                    self.run_world(formal=True, git=git)
                self.assertEqual("PREDECESSOR_SOURCE_MISMATCH", found.exception.code)
                self.assertEqual([], git.detached)

    def test_baseline_rules_and_published_host_seam(self):
        git = RollingFakeGit(self.world)
        published = type("Published", (), {"is_complete_published": lambda self, tag: tag == "v1.2.1"})()
        baseline = rolling.resolve_rolling_baseline(git, CANDIDATE, "1.2.2", baseline_tag=None, formal=True, published=published)
        self.assertEqual("v1.2.1", baseline.tag)
        self.assertEqual("v1.2.0", rolling.resolve_rolling_baseline(git, CANDIDATE, "1.2.2", baseline_tag="v1.2.0", formal=False, published=None).tag)
        for tags, given, formal, host in ((git.tags, "v1.2.0", True, published),
                                          (git.tags, None, True, None),
                                          ([git.tags[1]._replace(object_type="commit")], "v1.2.1", False, None),
                                          ([git.tags[1]._replace(ancestor=False)], "v1.2.1", False, None),
                                          (git.tags, "v1.2.2", False, None)):
            with self.subTest(given=given, tags=tags):
                git.tags = tags
                with self.assertRaises(execution.ExecutionError) as found:
                    rolling.resolve_rolling_baseline(git, CANDIDATE, "1.2.2", baseline_tag=given, formal=formal, published=host)
                self.assertEqual("PREDECESSOR_BASELINE_INVALID", found.exception.code)

    def test_local_rolling_tags_use_shared_annotated_object_and_peel_adapter(self):
        git = rolling.LocalRollingGitHost(ROOT)
        queries = []
        def query(*arguments):
            queries.append(arguments)
            if arguments[0] == "for-each-ref":
                return f"v1.2.1 tag {TAG_OBJECT}"
            if arguments[-1] == "refs/tags/v1.2.1^{tag}":
                return TAG_OBJECT
            if arguments[-1] == TAG_OBJECT + "^{commit}":
                return BASELINE
            self.fail(f"unexpected Git query: {arguments}")
        with (patch.object(git, "_git", side_effect=query),
              patch.object(rolling.subprocess, "run", return_value=subprocess.CompletedProcess([], 0))):
            tags = git.stable_tags(CANDIDATE)
        self.assertEqual([validation.RollingTag("v1.2.1", "tag", TAG_OBJECT, BASELINE, True, None)], tags)
        self.assertIn(("rev-parse", "--verify", "refs/tags/v1.2.1^{tag}"), queries)
        self.assertIn(("rev-parse", "--verify", TAG_OBJECT + "^{commit}"), queries)

    def test_wrong_declared_baseline_and_missing_changelog_id_block(self):
        for baseline in (True, False):
            with self.subTest(baseline=baseline):
                self.world = synthetic_world()
                declared_entry(self.world)
                if baseline:
                    self.world["declaration"]["baseline"]["tag"] = "v1.2.0"
                git = RollingFakeGit(self.world)
                if not baseline:
                    git.commits[CANDIDATE]["CHANGELOG.md"] = notes([]).encode()
                report, _, _ = self.run_world("different", git=git)
                self.assertIn("PREDECESSOR_BASELINE_INVALID" if baseline else "PREDECESSOR_RELEASE_NOTE_MISSING",
                              {item["code"] for item in report["gate"]["failures"]})

    def test_source_and_applied_digests_are_bound(self):
        report, _, git = self.run_world()
        sources = rolling.load_rolling_sources(git, CANDIDATE, baseline_tag="v1.2.1", formal=False, published=None)
        authority = sources.authority._replace(comparator_sha256="c" * 64)
        self.assertEqual([], validation.source_binding_failures(report, authority))
        for member in ("commit", "tree", "contracts", "ledgerSha256", "declarationSha256", "scriptSha256"):
            with self.subTest(member=member):
                changed = copy.deepcopy(report)
                if member in ("commit", "tree"):
                    changed["candidate"]["executor"][member] = "0" * 40
                elif member == "contracts":
                    changed["comparator"]["contracts"][0]["sha256"] = "0" * 64
                elif member == "scriptSha256":
                    changed["comparator"][member] = "0" * 64
                else:
                    changed[member] = "0" * 64
                self.assertIn("PREDECESSOR_SOURCE_MISMATCH", {failure.code for failure in validation.source_binding_failures(changed, authority)})

    def test_formal_pending_refuses_before_hosts_and_cli_returns_nonzero(self):
        git = RollingFakeGit(self.world)
        with self.assertRaises(execution.ExecutionError) as found:
            self.run_world(formal=True, git=git)
        self.assertEqual("PREDECESSOR_CONTRACT_PENDING", found.exception.code)
        self.assertEqual([], git.detached)
        self.assertEqual(1, execution.main(["rolling", "--candidate-commit", CANDIDATE, "--output", str(self.root / "formal.json"),
                                           "--temporary-root", str(self.root), "--formal"]))

    def test_settings_file_in_formal_run_refuses_when_interfaces_are_admitted(self):
        (self.settings / "event-buffer-format.v1.json").write_bytes(b"{}")
        git = RollingFakeGit(self.world)
        contract = json.loads(git.commits[CANDIDATE][rolling.CONTRACT])
        contract["executor"]["compilerHost"]["status"] = "in-effect"
        git.commits[CANDIDATE][rolling.CONTRACT] = encoded(contract)
        with patch.object(execution, "admit_execution_contract"), self.assertRaises(execution.ExecutionError) as found:
            self.run_world(formal=True, git=git)
        self.assertEqual("PREDECESSOR_ENVIRONMENT_INVALID", found.exception.code)

    def test_diagnostic_cli_dispatches_and_gate_controls_exit_status(self):
        for gate, expected in (("clear", 0), ("blocked", 1)):
            with self.subTest(gate=gate):
                with (patch.object(rolling, "run_rolling", return_value={"gate": {"result": gate}}) as run,
                      patch.object(execution, "local_settings_folder", return_value=self.settings)):
                    code = execution.main(["rolling", "--candidate-commit", CANDIDATE, "--baseline-tag", "v1.2.1",
                                           "--output", str(self.root / "cli.json"), "--temporary-root", str(self.root), "--diagnostic"])
                self.assertEqual(expected, code)
                self.assertFalse(run.call_args.kwargs["formal"])

    def test_exclusive_output_and_report_digest(self):
        report, _, _ = self.run_world()
        self.assertEqual(parity.canonical_json_sha256({key: value for key, value in report.items() if key != "deterministicSha256"}),
                         report["deterministicSha256"])
        output = self.root / "report-1.json"
        before = output.read_bytes()
        with self.assertRaises(parity.ParityError):
            parity.write_json_exclusive_atomic(output, report)
        self.assertEqual(before, output.read_bytes())

    def test_builder_reproduces_three_payload_free_run_fixtures_byte_for_byte(self):
        for name, behavior, declared in (("rolling-equal", "equal", False), ("rolling-declared", "different", True),
                                        ("rolling-undeclared", "different", False)):
            with self.subTest(name=name):
                self.world = synthetic_world()
                if declared:
                    declared_entry(self.world)
                builder = rolling.build_rolling_report
                with patch.object(rolling, "build_rolling_report", wraps=builder) as assembly:
                    self.run_world(behavior)
                arguments = list(assembly.call_args.args)
                # Replay measured run evidence with fixed synthetic environment
                # metadata, so the builder itself reproduces the fixture bytes.
                arguments[3] = {**arguments[3], "temporaryRootLength": 32}
                report = builder(*arguments, **assembly.call_args.kwargs)
                self.assertEqual((FIXTURES / (name + ".json")).read_bytes(), parity.canonical_json_bytes(report) + b"\n")


if __name__ == "__main__":
    unittest.main()
