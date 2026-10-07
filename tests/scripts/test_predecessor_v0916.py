"""Milestone orchestration on fake hosts and synthetic, local-only bytes."""

from __future__ import annotations

import copy
import json
import os
from pathlib import Path
import subprocess
import tempfile
import unittest
from unittest.mock import patch

from scripts import predecessor_comparison as execution
from scripts import predecessor_v0916 as milestone
from scripts import predecessor_validation as validation
from scripts import v0916_parity_certification as parity
from tests.scripts.predecessor_test_support import contract_for_fake_processes, compiler_identity
from tests.scripts.test_predecessor_rolling import (
    BASELINE, CANDIDATE, TAG_OBJECT, FIXTURES, PAYLOAD, CHANGED, ROOT,
    RollingFakeGit, SyntheticProcesses, SyntheticReader, encoded, identity, synthetic_world,
)


INPUT_COMMIT = "8" * 40
TP = PAYLOAD[:80]


def v0916_world():
    seed = synthetic_world()
    route = seed["policy"]["routes"][0]
    routes = []
    for index, (name, ic, workflow) in enumerate((
        ("exact", "NT51950", "standard-merge"),
        ("plan-correction", "NT51951", "standard-merge"),
        ("amendment-correction", "NT51928", "standard-merge"),
        ("full", "NT51950", "ctrlram-replace"),
        ("tp", "NT51950", "ctrlram-replace"),
        ("not-applicable", "NT51951", "ctrlram-replace"),
        ("unbound-a", "NT51925", "standard-merge"),
        ("unbound-b", "NT51926", "ab-merge"),
    )):
        routes.append({**route, "routeId": "route-" + name, "icId": ic, "workflowId": workflow,
                       "icCountVariant": "1-ic" if workflow == "ctrlram-replace" else "selector-free",
                       "mapVariant": "synthetic-" + name, "capabilityFingerprint": "abcdef12"[index] * 64})
    by_id = {row["routeId"]: row for row in routes}
    artifacts = [{"artifactId": name, "role": "input", "path": name + ".bin", **identity(payload)}
                 for name, payload in (("dp-input", PAYLOAD), ("tp-input", PAYLOAD), ("tp-base", TP), ("replacement", TP))]
    case = {"caseId": "synthetic-case", "artifacts": artifacts}
    manifest = {"cases": [{"caseId": "synthetic-case", "manifestPath": "case.json"}],
                "routeEvidence": [{"routeId": row["routeId"], "capabilityFingerprint": row["capabilityFingerprint"],
                                   "caseId": "synthetic-case", "kind": "direct-golden"} for row in routes[:6]]}
    policy = {"routes": routes}
    canonical = {"repositoryCommit": INPUT_COMMIT, "manifestPath": "testdata/golden/canonical/manifest.json",
                 "manifestRawSha256": identity(encoded(manifest))["sha256"], "manifestSize": len(encoded(manifest)),
                 "canonicalRootTree": "3" * 40,
                 "manifestBlob": SyntheticReader({"manifest": encoded(manifest)}).entry("manifest")[2],
                 "currentlyMissingRouteIds": [row["routeId"] for row in routes[6:]],
                 "ctrlRamExecutionBindings": [{"caseId": "synthetic-case", "tpBaseArtifactId": "tp-base",
                     "fullBaseRecipe": {"workflowId": "standard-merge", "dpArtifactId": "dp-input", "tpArtifactId": "tp-input"},
                     "replacements": [{"artifactId": "replacement", "slotId": "replace-ctrlram-master"}]}],
                 "ctrlRamBaseRoutes": []}
    for name in ("full", "tp", "not-applicable"):
        row = by_id["route-" + name]
        binding = {"routeId": row["routeId"], "capabilityFingerprint": row["capabilityFingerprint"],
                   "kind": "tp-input" if name == "tp" else "standard-merge"}
        if name != "tp":
            binding.update(standardMergeRouteId="route-exact", standardMergeCapabilityFingerprint=routes[0]["capabilityFingerprint"],
                           standardMergeMapVariant=routes[0]["mapVariant"])
        canonical["ctrlRamBaseRoutes"].append(binding)

    def correction(name):
        row = by_id["route-" + name]
        return {"routeId": row["routeId"], "capabilityFingerprint": row["capabilityFingerprint"],
                "kind": "synthetic-approved-correction", "ownerDecision": "synthetic-owner-decision",
                "baselineOutput": identity(PAYLOAD), "candidateOutput": identity(CHANGED),
                "differentByteCount": 1, "differentRanges": [{"start": 0, "endExclusive": 1}]}

    plan = {"baseline": {"tag": "v0.9.16", "tagObject": TAG_OBJECT, "peeledCommit": BASELINE},
            "canonicalInputAuthority": canonical, "policyBinding": {"path": "docs/contracts/canonical-capability-policy-v1.json",
                                                                     "sha256": identity(encoded(policy))["sha256"]},
            "selection": {"authoring": "available", "publication": "supported",
                          "includedWorkflows": ["standard-merge", "ctrlram-replace", "ab-merge"]},
            "transitiveRoutes": [{"routeId": "route-tp", "capabilityFingerprint": by_id["route-tp"]["capabilityFingerprint"],
                                  "fullRouteId": "route-full", "fullCapabilityFingerprint": by_id["route-full"]["capabilityFingerprint"],
                                  "tpLength": len(TP)}], "approvedSemanticCorrections": [correction("plan-correction")],
            "candidateAuthority": {"do-not-consume": "synthetic excluded member"}}
    amendment = {"plan": {**validation.v0916_plan_binding(plan), "policySha256": plan["policyBinding"]["sha256"],
                           "baselineTagObject": TAG_OBJECT, "baselinePeeledCommit": BASELINE},
                 "approvedSemanticCorrections": [correction("amendment-correction")],
                 "baselineNotApplicable": [{"routeId": "route-not-applicable", "capabilityFingerprint": routes[5]["capabilityFingerprint"],
                     "binding": {"evidenceCaseId": "synthetic-case", "inputCaseId": "synthetic-case", "precursorMapVariant": routes[0]["mapVariant"]},
                     "expectedBaseline": {"precursorOutput": identity(PAYLOAD), "rejectingStage": "preview",
                                          "issueCodes": ["synthetic.product-rejection"]},
                     "expectedCandidate": {"precursorOutput": identity(CHANGED), "output": identity(PAYLOAD)}}],
                 "baselineExecutor": json.loads((ROOT / milestone.AMENDMENT).read_bytes())["baselineExecutor"]}
    seed.update(policy=policy, case=case, manifest=manifest, plan=plan, amendment=amendment)
    return seed


def rebind_amendment(world):
    world["amendment"]["plan"].update(validation.v0916_plan_binding(world["plan"]))


def route_of(report, name):
    return next(row for row in report["routes"] if row["planRouteId"] == "route-" + name)


class V0916FakeGit(RollingFakeGit):
    def __init__(self, world):
        self.reads = []
        super().__init__(world)
        self.tags = [validation.RollingTag("v0.9.16", "tag", TAG_OBJECT, BASELINE, True, None)]
        self.commits[CANDIDATE].update({path: b"{}" for path in milestone.APPLIED_CONTRACTS
                                        if path not in (milestone.CONTRACT, milestone.PLAN)})
        baseline = json.loads((ROOT / "docs/contracts/v0916-baseline-executor-v1.json").read_bytes())
        baseline["source"].update(tagObject=TAG_OBJECT, peeledCommit=BASELINE, sourceTree="5" * 40)
        v2 = json.loads((ROOT / milestone.BASELINE_EXECUTOR).read_bytes())
        v2["source"] = baseline["source"]
        v2["v1Relation"]["contract"].update(identity(encoded(baseline)))
        self.commits[CANDIDATE]["docs/contracts/v0916-baseline-executor-v1.json"] = encoded(baseline)
        self.commits[CANDIDATE][milestone.BASELINE_EXECUTOR] = encoded(v2)
        world["amendment"]["baselineExecutor"]["contract"].update(identity(encoded(v2)))
        self.commits[CANDIDATE][milestone.AMENDMENT] = encoded(world["amendment"])
        for path in milestone.IMPLEMENTATION:
            self.commits[CANDIDATE][path] = (ROOT / path).read_bytes()
        self.commits[INPUT_COMMIT] = {world["plan"]["policyBinding"]["path"]: encoded(world["policy"]),
                                    "testdata/golden/canonical/manifest.json": encoded(world["manifest"]),
                                    "testdata/golden/canonical/case.json": encoded(world["case"])}
        for row in world["case"]["artifacts"]:
            self.commits[INPUT_COMMIT]["testdata/golden/canonical/" + row["path"]] = TP if row["artifactId"] in ("tp-base", "replacement") else PAYLOAD
        # Candidate Golden is deliberately unusable in the historical mode.
        self.commits[CANDIDATE]["testdata/golden/canonical/manifest.json"] = b"not-json"

    def snapshot_reader(self, commit):
        git = self

        class Reader(SyntheticReader):
            def read_file(self, requested, path):
                git.reads.append((requested, path))
                if "predecessor-comparison-declarations/" in path:
                    raise AssertionError("declaration read in historical mode")
                return super().read_file(requested, path)

        return Reader(self.commits[commit])


class BaselineBuilder:
    def __init__(self):
        self.calls = []

    def build(self, git, runner, commit, record):
        self.calls.append((commit, copy.deepcopy(record)))
        # The injected fake supplies no v2 recipe or pinned executor value.
        identity_row = {"commit": commit, "tree": git.commit_tree(commit), "tagObject": TAG_OBJECT,
                        "cliSha256": identity(b"baseline")["sha256"], "runtimeClosureSha256": "a" * 64,
                        "resolvedSdkVersion": "10.0.303", "lockFileSetSha256": "b" * 64, "compilerHost": compiler_identity(7),
                        "authorityTrees": {path: "3" * 40 for path in ("external-tools", "profiles", "src", "tools/crc-worker")}}
        root = runner.temporary_root / "fake-baseline"
        root.mkdir()
        (root / "cli.exe").write_bytes(b"baseline")
        closure = parity.runtime_closure_inventory(root, cli_relative="cli.exe")
        identity_row["runtimeClosureSha256"] = closure.identity_sha256
        return execution.Executor(identity_row, closure, "v0916", {})


class V0916Processes(SyntheticProcesses):
    def __init__(self, git, mutations=None):
        super().__init__(git)
        self.mutations = mutations or {}
        self.routes_run = []

    def respond(self, argv, cwd):
        if argv[0] == "dotnet":
            return super().respond(argv, cwd)
        side = Path(argv[0]).read_bytes().decode()
        workflow, action = argv[1:3]
        profile = argv[argv.index("--profile") + 1]
        name = {"NT51950": "exact", "NT51951": "plan-correction", "NT51928": "amendment-correction"}[profile]
        if workflow == "ctrlram-replace":
            name = "not-applicable" if profile == "NT51951" else (
                "tp" if Path(argv[argv.index("--base") + 1]).stat().st_size == len(TP) else "full")
        self.routes_run.append((side, name, workflow, action))
        change = self.mutations.get((side, name))
        if side == "baseline" and change in ("crash", "timeout", "missing-report"):
            if change == "crash":
                raise OSError("synthetic crash")
            if change == "timeout":
                raise subprocess.TimeoutExpired(argv, 1800)
            return subprocess.CompletedProcess(argv, 1, "", "synthetic stderr")
        rejection = name == "not-applicable" and side == "baseline" and change != "accept"
        self.behavior = side + "-rejects" if rejection or change == "reject" else "equal"
        self.payload = TP if name == "tp" else CHANGED if name in ("plan-correction", "amendment-correction") and side == "candidate" else PAYLOAD
        if isinstance(change, bytes):
            self.payload = change
        elif change not in (None, "reject", "wrong-rejection-code", "accept"):
            self.behavior = change
        # Parent supplies report generation, process-failure variants and the
        # shared B1 fake host. Its output selection is specialized by payload.
        if not rejection and change != "reject":
            saved = self.behavior
            self.behavior = "different" if side == "candidate" and saved == "equal" else saved
        value = super().respond(argv, cwd)
        report_path = Path(argv[argv.index("--report") + 1])
        if report_path.exists():
            report = json.loads(report_path.read_bytes())
            report.update(ProfileId=profile, IcId=profile)
            for item in report["Inputs"]:
                if item["ArtifactId"] in ("ld", "ldc"):
                    binding = item["ArtifactId"] + "-input"
                    if change == "candidate-alias" and binding == "ldc-input":
                        binding = "ld-input"
                    item.update(AddressSpaceId=binding, ArtifactId=binding)
            if change == "wrong-rejection-code":
                report["Issues"][0]["Code"] = "synthetic.other-rejection"
            if side == "baseline" and change in ("process-failed", "start-failed"):
                report.update(Output=None, Operations=[], Mutations=[], CompilationFingerprint=None,
                              Issues=[{"Code": "external-tool.process." + ("failed" if change == "process-failed" else "start-failed"),
                                       "Severity": "warning"}])
                value = subprocess.CompletedProcess(argv, 1, "", "")
            # Parent's baseline default is 160 bytes; TP output has 80 bytes.
            if name == "tp":
                for operation in report["Operations"]:
                    for member in ("SourceRange", "TargetRange"):
                        operation[member].update(Length=80, EndExclusive=80)
                for mutation in report["Mutations"]:
                    mutation["TargetRange"].update(Length=80, EndExclusive=80)
                    mutation["ChangedByteCount"] = 80
            if report["Output"] is not None:
                output_path = Path(argv[argv.index("--output") + 1])
                payload = self.payload if isinstance(change, bytes) or name == "tp" or side == "candidate" else PAYLOAD
                if action == "build":
                    output_path.write_bytes(payload)
                report["Output"].update(Size=len(payload), Sha256=identity(payload)["sha256"])
            report_path.write_bytes(encoded(report))
        return value


class V0916Tests(unittest.TestCase):
    def setUp(self):
        self.world = v0916_world()
        self.scratch = tempfile.TemporaryDirectory(prefix="p", dir=os.environ["TEMP"])
        self.root = Path(self.scratch.name)
        self.settings = self.root / "settings"
        self.settings.mkdir()
        self.counter = 0

    def tearDown(self):
        for path in self.root.rglob("*"):
            if path.is_file():
                path.chmod(0o600)
        self.scratch.cleanup()

    def run_world(self, mutations=None, *, git=None, formal=False, build=None,
                  host_factory=V0916Processes, temporary_name=None):
        git = git or V0916FakeGit(self.world)
        host = host_factory(git, mutations)
        builder = build or BaselineBuilder()
        self.counter += 1
        temporary = self.root / (temporary_name or f"run-{self.counter}")
        materialized = []

        def materialize(plan, *, git_reader, destination):
            self.assertNotIn("candidateAuthority", plan)
            self.assertEqual(self.world["plan"]["canonicalInputAuthority"], plan["canonicalInputAuthority"])
            materialized.append(plan)
            destination.mkdir()
            descriptor = plan["canonicalInputAuthority"]
            return parity.MaterializedCanonicalAuthority(destination, descriptor["manifestRawSha256"],
                                                          descriptor["manifestPath"], git_reader.files)

        def load_plan(path, *, repository_root, git_reader):
            raw = parity.load_json_reject_duplicates(path.read_bytes())
            self.assertNotIn("candidateAuthority", raw)
            policy = parity.load_json_reject_duplicates(git_reader.read_file(INPUT_COMMIT, raw["policyBinding"]["path"]))
            routes = []
            for row in validation.plan_selected_routes(raw, policy):
                transitive = next((item for item in raw["transitiveRoutes"] if item["routeId"] == row["routeId"]), {})
                routes.append(parity.Route(row["routeId"], row["capabilityFingerprint"], row["icId"], row["workflowId"],
                                          row["icCountVariant"], row["mapVariant"], "tp-prefix-transitive" if transitive else "exact-output",
                                          transitive.get("fullRouteId"), transitive.get("fullCapabilityFingerprint"), transitive.get("tpLength")))
            return parity.Plan(raw, tuple(routes), {}, path, path.stat().st_size, identity(path.read_bytes())["sha256"])

        original = milestone.load_v0916_sources

        def sources(*args, **kwargs):
            value = original(*args, **kwargs)
            return value._replace(contract=contract_for_fake_processes(value.contract, temporary),
                                  authority=value.authority._replace(comparator_sha256="c" * 64))

        with (patch.object(milestone, "load_v0916_sources", side_effect=sources),
              patch.object(milestone, "execute_v0916_side", wraps=milestone.execute_v0916_side) as execute):
            report = milestone.run_v0916(git=git, host=host, baseline_builder=builder, candidate_commit=CANDIDATE,
                                          output_path=self.root / f"report-{self.counter}.json",
                                          temporary_root=temporary, settings_folder=self.settings,
                                          formal=formal, milestone="1.2.0-release-approval" if formal else None, materializer=materialize, plan_loader=load_plan)
        self.executed = [(call.args[4].route_id, call.kwargs["side"]) for call in execute.call_args_list]
        self.assertEqual(1, len(materialized))
        self.assertEqual(parity.canonical_json_bytes(report) + b"\n", (self.root / f"report-{self.counter}.json").read_bytes())
        return report, host, git, builder

    def test_all_proofs_unbound_routes_and_each_side_execution_complete(self):
        report, host, git, baseline = self.run_world()
        self.assertEqual("consistent", report["result"], report["failures"])
        self.assertEqual({"consistent": 6, "inconsistent": 0, "invalid": 0, "notCovered": 2}, report["summary"])
        self.assertEqual("none", report["certification"])
        self.assertFalse(report["terminal"])
        self.assertTrue(all(len(row["planCapabilityFingerprint"]) == 64 for row in report["routes"]))
        self.assertEqual([(BASELINE, json.loads(git.commits[CANDIDATE][milestone.BASELINE_EXECUTOR]))], baseline.calls)
        self.assertEqual([CANDIDATE], [commit for commit, _ in git.detached])
        for side in ("baseline", "candidate"):
            for name in ("exact", "plan-correction", "amendment-correction", "full", "tp", "not-applicable"):
                expected = 0 if (side, name) == ("baseline", "tp") else 1
                self.assertEqual(expected, self.executed.count(("route-" + name, side)))
            for name in ("plan-correction", "amendment-correction", "full", "tp", "not-applicable"):
                for stage in ("preview", "build"):
                    skipped = (side, name) == ("baseline", "tp") or (side, name, stage) == ("baseline", "not-applicable", "build")
                    expected = 0 if skipped else 2 if name == "plan-correction" else 1
                    self.assertEqual(expected, sum(item[:2] == (side, name) and item[-1] == stage for item in host.routes_run))
            self.assertEqual(2, host.routes_run.count((side, "exact", "standard-merge", "preview")))
            self.assertEqual(2, host.routes_run.count((side, "exact", "standard-merge", "build")))
        self.assertFalse(any("declarations/" in path for _, path in git.reads))
        self.assertFalse(any(commit == CANDIDATE and "testdata/golden/" in path for commit, path in git.reads))

    def test_baseline_tp_crash_does_not_affect_candidate_transitive_proof(self):
        report, host, _, _ = self.run_world({("baseline", "tp"): "crash"})
        route = route_of(report, "tp")
        self.assertEqual("consistent", report["result"], report["failures"])
        self.assertEqual("consistent", route["result"])
        self.assertIsNone(route["baseline"])
        self.assertIsNone(route["comparison"])
        self.assertEqual([], route["informational"])
        self.assertTrue(all(route["transitive"][check] for check in validation.TRANSITIVE_CHECKS))
        self.assertNotIn(("route-tp", "baseline"), self.executed)
        self.assertFalse(any(item[:2] == ("baseline", "tp") for item in host.routes_run))

    def test_exact_output_typed_rejections_are_unapproved_differences(self):
        for mutations in ({("candidate", "exact"): "reject"},
                          {("baseline", "exact"): "reject", ("candidate", "exact"): "reject"}):
            with self.subTest(mutations=mutations):
                report, _, _, _ = self.run_world(mutations)
                route = route_of(report, "exact")
                self.assertEqual("inconsistent", report["result"])
                self.assertEqual("inconsistent", route["result"])
                self.assertEqual("PREDECESSOR_UNAPPROVED_DIFFERENCE", route["failureCode"])
                self.assertIsNone(route["comparison"])
                for side, _ in mutations:
                    self.assertEqual("rejected", route[side]["status"])
                self.assertTrue(any(item["subject"] == "route-exact" and
                                    item["code"] == "PREDECESSOR_UNAPPROVED_DIFFERENCE"
                                    for item in report["failures"]))

    def test_missing_admitted_artifact_returns_typed_input_failure(self):
        resolve = milestone.resolve_canonical_route_input

        def unmatched(*args, **kwargs):
            verified = resolve(*args, **kwargs)
            verified.request["orderedInputs"][0]["path"] = str(self.root / "unmatched.bin")
            return verified

        with (patch.object(milestone, "resolve_canonical_route_input", side_effect=unmatched),
              self.assertRaises(execution.ExecutionError) as found):
            self.run_world()
        self.assertEqual("PREDECESSOR_INPUT_INVALID", found.exception.code)
        self.assertIsInstance(found.exception.__cause__, StopIteration)
        self.assertFalse((self.root / "report-1.json").exists())

    def test_pending_formal_refuses_before_any_host(self):
        contract = json.loads((ROOT / milestone.CONTRACT).read_bytes())
        contract["executor"]["compilerHost"] = {"status": "pending-executor-record", "boardDecisions": ["1.1.12 board decision 79"]}
        with (patch.object(execution, "admit_execution_contract", side_effect=lambda **kw:
                           execution.admit_loaded_execution_contract(contract, mode="v0916-1x", formal=True,
                                                                      amendment=self.world["amendment"])),
              self.assertRaises(execution.ExecutionError) as found):
            milestone.run_v0916(git=None, host=None, baseline_builder=None, candidate_commit=CANDIDATE,
                                output_path=Path("unused.json"), temporary_root=Path(os.environ["TEMP"]),
                                settings_folder=Path("unused-settings"), formal=True)
        self.assertEqual("PREDECESSOR_CONTRACT_PENDING", found.exception.code)

    def test_formal_run_without_milestone_refuses_before_source_or_process(self):
        with (patch.object(execution, "admit_execution_contract"),
              patch.object(milestone, "load_v0916_sources") as sources,
              self.assertRaises(execution.ExecutionError) as found):
            milestone.run_v0916(git=None, host=None, baseline_builder=None, candidate_commit=CANDIDATE,
                                output_path=self.root / "formal.json", temporary_root=self.root,
                                settings_folder=self.settings, formal=True)
        self.assertEqual("PREDECESSOR_INPUT_INVALID", found.exception.code)
        sources.assert_not_called()
        self.assertFalse((self.root / "formal.json").exists())

    def alias_world_git(self, *, alias=True, matching_fingerprint=True):
        route = self.world["policy"]["routes"][2]
        historical = json.loads((ROOT / milestone.PLAN).read_bytes())["inputIdentityAliases"][0]
        self.world["plan"]["inputIdentityAliases"] = [{
            **historical, "routeId": route["routeId"],
            "capabilityFingerprint": route["capabilityFingerprint"] if matching_fingerprint else "0" * 64,
        }] if alias else []
        ldc = {"artifactId": "ldc-input", "role": "input", "path": "ldc-input.bin", **identity(PAYLOAD)}
        case = {"caseId": "alias-case", "artifacts": [*self.world["case"]["artifacts"][:2], ldc]}
        self.world["manifest"]["cases"].append({"caseId": "alias-case", "manifestPath": "alias-case.json"})
        self.world["manifest"]["routeEvidence"][2]["caseId"] = "alias-case"
        raw = encoded(self.world["manifest"])
        self.world["plan"]["canonicalInputAuthority"].update(
            manifestRawSha256=identity(raw)["sha256"], manifestSize=len(raw),
            manifestBlob=SyntheticReader({"manifest": raw}).entry("manifest")[2])
        rebind_amendment(self.world)
        git = V0916FakeGit(self.world)
        git.commits[INPUT_COMMIT].update({"testdata/golden/canonical/alias-case.json": encoded(case),
                                         "testdata/golden/canonical/ldc-input.bin": PAYLOAD})
        return git

    def test_plan_baseline_alias_accepts_ld_and_candidate_ldc_reports(self):
        git = self.alias_world_git()
        with patch.object(execution, "execute_cli_stage", wraps=execution.execute_cli_stage) as stages:
            report, host, _, _ = self.run_world(git=git)
        route = route_of(report, "amendment-correction")
        self.assertEqual("consistent", report["result"], report["failures"])
        self.assertEqual("consistent", route["result"])
        for side, option in (("baseline", "--ld"), ("candidate", "--ldc")):
            calls = [call for call in host.calls if "--profile" in call[0] and
                     call[0][call[0].index("--profile") + 1] == "NT51928" and
                     Path(call[0][0]).read_bytes() == side.encode()]
            self.assertEqual(2, len(calls))
            self.assertTrue(all(option in call[0] for call in calls))
            stage_calls = [call for call in stages.call_args_list if call.args[2]["routeId"] == "route-amendment-correction"
                           and call.kwargs["execution_role"] == side + "-exact"]
            self.assertEqual(2, len(stage_calls))
            self.assertTrue(all(call.args[5][-1] == {"artifactId": "ldc-input", "slotId": "ldc-input"}
                                for call in stage_calls))

    def test_historical_alias_on_candidate_or_unbound_route_is_report_invalid(self):
        for change in ("candidate", "no-alias", "wrong-fingerprint"):
            with self.subTest(change=change):
                self.world = v0916_world()
                git = self.alias_world_git(alias=change != "no-alias", matching_fingerprint=change != "wrong-fingerprint")
                mutations = {("candidate", "amendment-correction"): "candidate-alias"} if change == "candidate" else None
                report, _, _, _ = self.run_world(mutations, git=git)
                route = route_of(report, "amendment-correction")
                self.assertEqual("invalid", route["result"])
                self.assertEqual("PREDECESSOR_REPORT_INVALID", route["failureCode"])

    def test_amendment_binding_mismatch_refuses_before_materialization_or_execution(self):
        for member in ("withoutCandidateAuthorityJcsSha256", "canonicalInputAuthorityJcsSha256", "policySha256",
                       "baselineTagObject", "baselinePeeledCommit"):
            with self.subTest(member=member):
                self.world = v0916_world()
                self.world["amendment"]["plan"][member] = "0" * len(self.world["amendment"]["plan"][member])
                git = V0916FakeGit(self.world)
                with (patch.object(milestone, "materialize_v0916_inputs") as materialize,
                      self.assertRaises(execution.ExecutionError) as found):
                    self.run_world(git=git)
                self.assertEqual("PREDECESSOR_AMENDMENT_MISMATCH", found.exception.code)
                materialize.assert_not_called()
                self.assertEqual([], git.detached)
                self.assertFalse(any(commit == INPUT_COMMIT for commit, _ in git.reads))

    def test_corrections_require_both_hashes_count_size_and_complete_ranges(self):
        for source, name in (("plan", "plan-correction"), ("amendment", "amendment-correction")):
            for change in ("baseline-hash", "candidate-hash", "size", "count", "range", "payload"):
                with self.subTest(source=source, change=change):
                    self.world = v0916_world()
                    correction = self.world[source]["approvedSemanticCorrections"][0]
                    if change.endswith("hash"):
                        correction["baselineOutput" if change == "baseline-hash" else "candidateOutput"]["sha256"] = "0" * 64
                    elif change == "size":
                        correction["candidateOutput"]["size"] += 1
                    elif change == "count":
                        correction["differentByteCount"] += 1
                    elif change == "range":
                        correction["differentRanges"][0].update(start=1, endExclusive=2)
                    rebind_amendment(self.world)
                    mutations = {("candidate", name): b"z" + PAYLOAD[1:]} if change == "payload" else None
                    report, _, _, _ = self.run_world(mutations)
                    route = route_of(report, name)
                    self.assertEqual("inconsistent", route["result"])
                    self.assertEqual("PREDECESSOR_AMENDMENT_MISMATCH", route["failureCode"])

    def test_transitive_primitive_checks_and_plan_length_fail_closed(self):
        cases = (("candidate-prefix", {("candidate", "full"): b"z" + PAYLOAD[1:]}, (False, True, True)),
                 ("baseline-prefix", {("baseline", "full"): b"z" + PAYLOAD[1:]}, (True, False, True)),
                 ("tail", {("baseline", "full"): PAYLOAD[:-1] + b"z", ("candidate", "full"): PAYLOAD[:-1] + b"z"}, (True, True, False)),
                 ("length", {}, (False, False, False)))
        for name, mutations, checks in cases:
            with self.subTest(name=name):
                self.world = v0916_world()
                if name == "length":
                    self.world["plan"]["transitiveRoutes"][0]["tpLength"] -= 1
                    rebind_amendment(self.world)
                report, _, _, _ = self.run_world(mutations)
                route = route_of(report, "tp")
                self.assertEqual("inconsistent", route["result"])
                self.assertEqual("PREDECESSOR_UNAPPROVED_DIFFERENCE", route["failureCode"])
                self.assertEqual(checks, tuple(route["transitive"][check] for check in validation.TRANSITIVE_CHECKS))

    def test_passing_transitive_proof_still_requires_consistent_full_route(self):
        report, _, _, _ = self.run_world({("baseline", "full"): PAYLOAD[:-1] + b"z"})
        route = route_of(report, "tp")
        self.assertTrue(all(route["transitive"][check] for check in validation.TRANSITIVE_CHECKS))
        self.assertEqual("inconsistent", route["result"])
        self.assertEqual("inconsistent", route_of(report, "full")["result"])

    def test_unavailable_transitive_proof_is_null_and_cannot_be_supplied(self):
        for side, name, failure in (("candidate", "tp", "reject"), ("candidate", "full", "reject"),
                                    ("baseline", "full", "crash"), ("candidate", "tp", "timeout")):
            with self.subTest(side=side, name=name, failure=failure):
                report, _, _, _ = self.run_world({(side, name): failure})
                route = route_of(report, "tp")
                expected = "inconsistent" if failure == "reject" else "invalid"
                self.assertEqual(expected, route["result"])
                self.assertIsNone(route["transitive"])
                dispositions, _ = validation.v0916_route_dispositions(self.world["plan"], self.world["amendment"], self.world["policy"])
                route["transitive"] = {"fullRouteId": "route-full", "tpLength": len(TP), **dict.fromkeys(validation.TRANSITIVE_CHECKS, True)}
                failures = validation.v0916_report_failures(report, dispositions, {})
                self.assertTrue(any(item.detail == "reports a transitive proof that could not run" for item in failures))

    def test_not_covered_route_cannot_be_executed_or_counted_as_compared(self):
        report, _, _, _ = self.run_world()
        dispositions, _ = validation.v0916_route_dispositions(self.world["plan"], self.world["amendment"], self.world["policy"])
        for change in ("result", "side", "evidence"):
            with self.subTest(change=change):
                forged = copy.deepcopy(report)
                route = route_of(forged, "unbound-a")
                evidence = {}
                if change == "result":
                    route["result"] = "consistent"
                elif change == "side":
                    route["candidate"] = route_of(report, "exact")["candidate"]
                else:
                    evidence[route["planRouteId"]] = validation.V0916RouteEvidence({"output": None})
                failures = validation.v0916_report_failures(forged, dispositions, evidence)
                self.assertTrue(any("not-covered route" in item.detail for item in failures))

    def test_crash_timeout_report_and_process_failures_are_invalid_on_either_side(self):
        for side in ("baseline", "candidate"):
            for failure in ("crash", "timeout", "missing-report", "process-failed", "start-failed"):
                with self.subTest(side=side, failure=failure):
                    report, host, _, _ = self.run_world({(side, "exact"): failure})
                    route = route_of(report, "exact")
                    self.assertEqual("invalid", route["result"])
                    self.assertEqual("PREDECESSOR_PROCESS_FAILED", route["failureCode"])
                    self.assertEqual("invalid", report["result"])
                    self.assertEqual("invalid", route[side]["status"])
                    self.assertNotIn((side, "exact", "standard-merge", "build"), host.routes_run)

    def test_not_applicable_requires_exact_rejection_binding_precursors_and_output(self):
        for change in ("code", "stage", "binding", "baseline-precursor", "candidate-precursor", "output", "no-rejection"):
            with self.subTest(change=change):
                self.world = v0916_world()
                row = self.world["amendment"]["baselineNotApplicable"][0]
                mutations = None
                if change == "code":
                    mutations = {("baseline", "not-applicable"): "wrong-rejection-code"}
                elif change == "stage":
                    row["expectedBaseline"]["rejectingStage"] = "build"
                elif change == "binding":
                    row["binding"]["inputCaseId"] = "other-case"
                elif change.endswith("precursor"):
                    row["expectedBaseline" if change == "baseline-precursor" else "expectedCandidate"]["precursorOutput"]["sha256"] = "0" * 64
                elif change == "output":
                    row["expectedCandidate"]["output"]["sha256"] = "0" * 64
                else:
                    mutations = {("baseline", "not-applicable"): "accept"}
                report, _, _, _ = self.run_world(mutations)
                route = route_of(report, "not-applicable")
                self.assertEqual("inconsistent", route["result"])
                self.assertEqual("PREDECESSOR_AMENDMENT_MISMATCH", route["failureCode"])

    def test_declaration_present_and_excluded_candidate_authority_are_not_consumed(self):
        git = V0916FakeGit(self.world)
        declaration = "docs/contracts/predecessor-comparison-declarations/1.2.2.json"
        git.commits[CANDIDATE][declaration] = b"malformed declaration must not be read"
        plan = json.loads(git.commits[CANDIDATE][milestone.PLAN])
        plan["candidateAuthority"] = {"invalid-package-authority": {"malformed": True}}
        git.commits[CANDIDATE][milestone.PLAN] = encoded(plan)
        report, _, _, _ = self.run_world(git=git)
        self.assertEqual("consistent", report["result"])
        self.assertNotIn((CANDIDATE, declaration), git.reads)

    def test_report_from_other_source_or_with_other_applied_digests_is_rejected(self):
        original = milestone.build_v0916_report
        with patch.object(milestone, "build_v0916_report", wraps=original) as build:
            report, _, _, _ = self.run_world()
        sources, _, _, _, _, dispositions, evidence, _ = build.call_args.args
        for member in ("commit", "tree", "scriptSha256", "contracts", "amendmentSha256", "planBinding", "baseline"):
            with self.subTest(member=member):
                forged = copy.deepcopy(report)
                if member in ("commit", "tree"):
                    forged["candidate"]["executor"][member] = "0" * 40
                elif member == "contracts":
                    forged["comparator"]["contracts"][0]["sha256"] = "0" * 64
                elif member == "scriptSha256":
                    forged["comparator"][member] = "0" * 64
                elif member == "planBinding":
                    forged[member]["canonicalInputAuthorityJcsSha256"] = "0" * 64
                elif member == "baseline":
                    forged[member]["executor"]["tagObject"] = "0" * 40
                else:
                    forged[member] = "0" * 64
                failures = validation.v0916_report_failures(forged, dispositions, evidence, authority=sources.authority, plan=sources.plan)
                code = "PREDECESSOR_BASELINE_INVALID" if member == "baseline" else "PREDECESSOR_SOURCE_MISMATCH"
                self.assertIn(code, {item.code for item in failures})

    def test_builder_reproduces_both_payload_free_fixtures_byte_for_byte(self):
        for name, mutations in (("v0916-consistent", None), ("v0916-inconsistent", {("candidate", "exact"): CHANGED})):
            with self.subTest(name=name):
                builder = milestone.build_v0916_report
                with patch.object(milestone, "build_v0916_report", wraps=builder) as assembly:
                    self.run_world(mutations)
                arguments = list(assembly.call_args.args)
                arguments[3] = {**arguments[3], "temporaryRootLength": 32}
                report = builder(*arguments, **assembly.call_args.kwargs)
                self.assertEqual((FIXTURES / (name + ".json")).read_bytes(), parity.canonical_json_bytes(report) + b"\n")

    def test_cli_dispatch_policy_and_exit_status_use_mode_result(self):
        for result, expected in (("consistent", 0), ("inconsistent", 1), ("invalid", 1)):
            with self.subTest(result=result):
                with (patch.object(milestone, "run_v0916", return_value={"result": result}) as run,
                      patch.object(execution, "local_settings_folder", return_value=self.settings)):
                    actual = execution.main(["v0916-1x", "--candidate-commit", CANDIDATE, "--output", str(self.root / "cli.json"),
                                             "--temporary-root", str(self.root), "--diagnostic"])
                self.assertEqual(expected, actual)
                self.assertFalse(run.call_args.kwargs["formal"])
        with (patch.object(milestone, "run_v0916", return_value={"result": "consistent"}) as run,
              patch.object(execution, "local_settings_folder", return_value=self.settings)):
            actual = execution.main(["v0916-1x", "--candidate-commit", CANDIDATE, "--output", str(self.root / "formal.json"),
                                     "--temporary-root", str(self.root), "--formal", "--milestone", "1.2.0-release-approval"])
        self.assertEqual(0, actual)
        self.assertTrue(run.call_args.kwargs["formal"])
        self.assertIsInstance(run.call_args.kwargs["baseline_builder"], execution.V0916BaselineExecutorBuilder)

    def test_missing_injected_baseline_and_wrong_executor_identity_refuse(self):
        git = V0916FakeGit(self.world)
        host = V0916Processes(git)
        with self.assertRaises(execution.ExecutionError) as found:
            milestone.run_v0916(git=git, host=host, baseline_builder=None, candidate_commit=CANDIDATE,
                                 output_path=self.root / "missing.json", temporary_root=self.root / "missing",
                                 settings_folder=self.settings)
        self.assertEqual("PREDECESSOR_CONTRACT_PENDING", found.exception.code)
        self.assertEqual([], host.calls)
        builder = BaselineBuilder()
        original = builder.build

        def wrong(*args):
            executor = original(*args)
            return executor._replace(identity={**executor.identity, "commit": "0" * 40})

        with patch.object(builder, "build", side_effect=wrong), self.assertRaises(execution.ExecutionError) as found:
            self.run_world(build=builder)
        self.assertEqual("PREDECESSOR_BASELINE_INVALID", found.exception.code)

    def test_admitted_inputs_must_match_the_plan_snapshot_exactly(self):
        git = V0916FakeGit(self.world)
        git.commits[INPUT_COMMIT]["testdata/golden/canonical/dp-input.bin"] = CHANGED
        with self.assertRaises(execution.ExecutionError) as found:
            self.run_world(git=git)
        self.assertEqual("PREDECESSOR_INPUT_INVALID", found.exception.code)

    def test_report_digest_and_exclusive_atomic_output(self):
        report, _, _, _ = self.run_world()
        self.assertEqual(validation.deterministic_report_sha256(report),
                         report["deterministicSha256"])
        output = self.root / "report-1.json"
        before = output.read_bytes()
        with self.assertRaises(parity.ParityError):
            parity.write_json_exclusive_atomic(output, report)
        self.assertEqual(before, output.read_bytes())

    def test_repeated_milestone_process_runs_reproduce_digest_with_different_capture_evidence(self):
        class VaryingProcesses(V0916Processes):
            run_number = 0

            def __init__(self, *args):
                super().__init__(*args)
                type(self).run_number += 1
                self.run_number = type(self).run_number

            def respond(self, argv, cwd):
                result = super().respond(argv, cwd)
                if "--report" in argv:
                    path = Path(argv[argv.index("--report") + 1])
                    value = json.loads(path.read_bytes())
                    value.update(RunId=f"00000000-0000-0000-0000-{self.run_number:012d}",
                                 StartedAtUtc=f"2026-10-0{self.run_number}T00:00:00Z",
                                 CompletedAtUtc=f"2026-10-0{self.run_number}T00:00:01Z")
                    value["Inputs"][0]["OriginalFileName"] = str(cwd / "input.bin")
                    path.write_bytes(encoded(value))
                    return subprocess.CompletedProcess(argv, result.returncode, f"stdout {cwd}", f"stderr {path}")
                return result

        reports = [self.run_world(host_factory=VaryingProcesses, temporary_name=name)[0]
                   for name in ("short", "longer-temporary-directory")]
        first, second = reports
        self.assertEqual("consistent", first["result"])
        self.assertEqual("consistent", second["result"])
        self.assertNotEqual(parity.canonical_json_sha256(first), parity.canonical_json_sha256(second))
        self.assertNotEqual(first["environment"]["temporaryRootLength"], second["environment"]["temporaryRootLength"])
        for side in ("baseline", "candidate"):
            left, right = (r["routes"][0][side]["processes"][0] for r in reports)
            for field in ("stdoutSha256", "stderrSha256", "report"):
                self.assertNotEqual(left[field], right[field])
            self.assertNotEqual(left["report"]["size"], right["report"]["size"])
        self.assertEqual(first["deterministicSha256"], second["deterministicSha256"])

    def test_existing_output_refuses_before_v0916_cli_run_or_build(self):
        output = self.root / "existing.json"
        output.write_bytes(b"preserved")
        with (patch.object(milestone, "run_v0916", return_value={"result": "consistent"}) as run,
              patch.object(execution, "local_settings_folder", return_value=self.settings) as settings,
              patch.object(milestone.tempfile, "TemporaryDirectory", wraps=tempfile.TemporaryDirectory) as scratch):
            code = execution.main(["v0916-1x", "--candidate-commit", CANDIDATE, "--output", str(output),
                                   "--temporary-root", str(self.root), "--diagnostic"])
        self.assertEqual(1, code)
        run.assert_not_called()
        settings.assert_not_called()
        scratch.assert_not_called()
        with self.assertRaises(execution.ExecutionError) as found:
            milestone.run_v0916(git=None, host=None, baseline_builder=None, candidate_commit=CANDIDATE,
                                 output_path=output, temporary_root=self.root, settings_folder=self.settings)
        self.assertEqual("PARITY_WRITE_CONFLICT", found.exception.code)
        self.assertEqual(b"preserved", output.read_bytes())

    def test_v0916_family_has_no_invented_baseline_self_report_comparison(self):
        report, _, _, _ = self.run_world()
        self.assertNotIn("baselineIdentityReportSha256", report)
        with patch.object(milestone, "run_v0916") as run, self.assertRaises(SystemExit) as found:
            execution.main(["v0916-1x", "--candidate-commit", CANDIDATE,
                            "--baseline-report", "not-a-v0916-program-identity.json",
                            "--output", str(self.root / "unused.json"),
                            "--temporary-root", str(self.root), "--diagnostic"])
        self.assertEqual(2, found.exception.code)
        run.assert_not_called()


if __name__ == "__main__":
    unittest.main()
