"""Decision 191 behavior through the real shard and aggregate, with a fake runner."""

from contextlib import ExitStack, contextmanager, nullcontext, redirect_stdout
import io
import json
import os
from pathlib import Path
import shutil
import subprocess
import tempfile
import unittest
from unittest.mock import patch

from tests.scripts import test_verify_orchestration as fixtures

MODULE = fixtures.MODULE


class CiDotnetRetryTests(unittest.TestCase):
    @contextmanager
    def shard(self, scenario, *, bug_text="Probe.Tests.Case1"):
        fixture = fixtures.VerifyOrchestrationTests()
        project = MODULE.CiDotnetProject(
            "tests/NvtFwCombiner.Domain.Tests/NvtFwCombiner.Domain.Tests.csproj"
        )
        golden = MODULE.CiDotnetProject(
            "tests/NvtFwCombiner.GoldenRegression.Tests/NvtFwCombiner.GoldenRegression.Tests.csproj"
        )
        if scenario in {"golden", "golden-forged"}:
            project = golden
        projects = (project,) if project == golden else (project, golden)
        with tempfile.TemporaryDirectory() as temporary, ExitStack() as stack:
            root = Path(temporary)
            output = root / "output"
            output.mkdir()
            for item in projects:
                (output / f"{item.name}.dll").write_bytes(b"test assembly")
            if bug_text is not None:
                bug = root / "docs/handoff/bugs/BUG-probe.md"
                bug.parent.mkdir(parents=True)
                bug.write_text(bug_text, encoding="utf-8")
            commands = []
            attempts = []

            def fake_run(command, **kwargs):
                commands.append(command)
                companion = (
                    any(str(arg).endswith(f"{golden.name}.dll") for arg in command)
                    and project != golden
                )
                active_scenario = "green" if companion else scenario
                log = Path(kwargs["log_path"])
                log.parent.mkdir(parents=True, exist_ok=True)
                with log.open("a", encoding="utf-8") as stream:
                    stream.write("retained command output\n")
                if "build" in command and active_scenario == "build":
                    raise subprocess.CalledProcessError(1, command)
                if "--ListTests" in command:
                    fixture.write_vstest_discovery(
                        log,
                        (
                            "Probe.Tests.Case0",
                            "Probe.Tests.Case1(value: 1)",
                            "Probe.Tests.Case1(value: 2)",
                        )
                        if active_scenario in {"theory", "missing-theory"}
                        else (21 if active_scenario == "too-many" else 20)
                        if active_scenario in {"too-many", "twenty"} and not companion
                        else 3,
                    )
                    if active_scenario == "discovery":
                        raise subprocess.CalledProcessError(1, command)
                elif command[1] == "vstest":
                    results = Path(
                        next(
                            value.split(":", 1)[1]
                            for value in command
                            if value.startswith("--ResultsDirectory:")
                        )
                    )
                    if not companion:
                        attempts.append(results)
                    retry = not companion and len(attempts) == 2
                    if active_scenario == "retry-launch-error" and retry:
                        raise OSError("synthetic runner launch failure")
                    identities = (
                        ("Probe.Tests.Case1",)
                        if retry
                        else (
                            "Probe.Tests.Case0",
                            "Probe.Tests.Case1",
                            "Probe.Tests.Case2",
                        )
                    )
                    if active_scenario in {"theory", "missing-theory"}:
                        identities = (
                            (
                                "Probe.Tests.Case1(value: 1)",
                                "Probe.Tests.Case1(value: 2)",
                            )
                            if retry
                            else (
                                "Probe.Tests.Case0",
                                "Probe.Tests.Case1(value: 1)",
                                "Probe.Tests.Case1(value: 2)",
                            )
                        )
                        if retry and active_scenario == "missing-theory":
                            identities = identities[:1]
                    outcomes = ("Passed",) * len(identities)
                    if active_scenario != "green" and (
                        not retry or active_scenario == "twice"
                    ):
                        outcomes = (
                            ("Failed",) if retry else ("Passed", "Failed", "Passed")
                        )
                    if retry and active_scenario == "extra":
                        identities = ("Probe.Tests.Case1", "Probe.Tests.Case0")
                        outcomes = ("Passed", "Passed")
                    if active_scenario == "partial-recovery":
                        identities = (
                            ("Probe.Tests.Case1", "Probe.Tests.Case2")
                            if retry
                            else identities
                        )
                        outcomes = (
                            ("Passed", "Failed")
                            if retry
                            else ("Passed", "Failed", "Failed")
                        )
                    if active_scenario in {"too-many", "twenty"} and not companion:
                        count = 21 if active_scenario == "too-many" else 20
                        identities = tuple(f"Probe.Tests.Case{i}" for i in range(count))
                        outcomes = ("Passed" if retry else "Failed",) * count
                    if companion:
                        identities = tuple(f"Probe.Tests.Case{i}" for i in range(3))
                        outcomes = ("Passed",) * 3
                    fixture.write_ci_trx(
                        results / "test-results.trx",
                        total=len(identities),
                        skipped=0,
                        identities=identities,
                        outcomes=outcomes,
                    )
                    # Real TRX TestMethod metadata, not display-name filter inference.
                    tree = MODULE.ET.parse(results / "test-results.trx")
                    tree.find(".//{*}ResultSummary").set(
                        "outcome", "Failed" if "Failed" in outcomes else "Completed"
                    )
                    if "Failed" in outcomes:
                        summary = tree.find(".//{*}ResultSummary")
                        namespace = (
                            "{http://microsoft.com/schemas/VisualStudio/TeamTest/2010}"
                        )
                        infos = MODULE.ET.SubElement(summary, namespace + "RunInfos")
                        for identity, outcome in zip(identities, outcomes, strict=True):
                            if outcome == "Failed":
                                info = MODULE.ET.SubElement(
                                    infos, namespace + "RunInfo", outcome="Error"
                                )
                                MODULE.ET.SubElement(
                                    info, namespace + "Text"
                                ).text = (
                                    f"[xUnit.net 00:00:01.45]     {identity} [FAIL]"
                                )
                        if active_scenario in {"unknown-error", "unmatched-error"}:
                            info = MODULE.ET.SubElement(
                                infos, namespace + "RunInfo", outcome="Error"
                            )
                            MODULE.ET.SubElement(info, namespace + "Text").text = (
                                "adapter discovery failed"
                                if active_scenario == "unknown-error"
                                else "[xUnit.net 00:00:01.45]     Probe.Tests.Unknown [FAIL]"
                            )
                        counters = summary.find("{*}Counters")
                        for name in (
                            "error",
                            "aborted",
                            "timeout",
                            "disconnected",
                            "notRunnable",
                            "inconclusive",
                        ):
                            counters.set(name, "0")
                        if active_scenario == "platform-error":
                            counters.set("error", "1")
                    if active_scenario == "aborted" and not retry:
                        tree.find(".//{*}ResultSummary").set("outcome", "Aborted")
                    ns = "{http://microsoft.com/schemas/VisualStudio/TeamTest/2010}"
                    definitions = MODULE.ET.SubElement(
                        tree.getroot(), ns + "TestDefinitions"
                    )
                    for index, result in enumerate(
                        tree.findall(".//{*}UnitTestResult")
                    ):
                        identity = identities[index]
                        result.set("testId", str(index))
                        definition = MODULE.ET.SubElement(
                            definitions, ns + "UnitTest", id=str(index), name=identity
                        )
                        cls, name = identity.split("(", 1)[0].rsplit(".", 1)
                        MODULE.ET.SubElement(
                            definition, ns + "TestMethod", className=cls, name=name
                        )
                    if active_scenario == "incomplete" and not retry:
                        tree.getroot().find(ns + "Results").remove(
                            tree.findall(".//{*}UnitTestResult")[-1]
                        )
                    tree.write(
                        results / "test-results.trx",
                        encoding="utf-8",
                        xml_declaration=True,
                    )
                    if "--Collect:XPlat Code Coverage" in command:
                        fixture.write_ci_coverage_pair(results / "collector", {})
                    if active_scenario == "hang" or (
                        active_scenario == "retry-hang" and retry
                    ):
                        (results / "host.dmp").write_bytes(b"synthetic mini dump")
                        (
                            results / "Sequence_af6bc426faab481fa504c42e3d52afd2.xml"
                        ).write_text("<TestSequence />", encoding="utf-8")
                    if active_scenario == "retry-invalid-attachment" and retry:
                        (
                            results / "Sequence_af6bc426faab481fa504c42e3d52afd2.xml"
                        ).write_bytes(b"")
                    if active_scenario == "timeout" and not retry:
                        raise subprocess.TimeoutExpired(command, 300)
                    if not companion and (
                        (active_scenario == "retry-exit-two" and retry)
                        or (active_scenario == "first-exit-two" and not retry)
                    ):
                        raise subprocess.CalledProcessError(2, command)
                    if "Failed" in outcomes and active_scenario != "zero-with-failures":
                        raise subprocess.CalledProcessError(1, command)

            env = fixture.ci_finalizer_environment("a" * 40)
            env["GITHUB_STEP_SUMMARY"] = str(root / "summary.md")
            stack.enter_context(patch.dict(os.environ, env))
            for name, value in {
                "ROOT": root,
                "SOLUTION": root / "solution.slnx",
                "CI_DOTNET_EVIDENCE_ROOT": root / "work",
                "CI_DOTNET_UPLOAD_ROOT": root / "upload",
                "COVERAGE_ROOT": root / "coverage",
                "CI_DOTNET_SHARDS": {"core": projects},
            }.items():
                stack.enter_context(patch.object(MODULE, name, value))
            for name, value in {
                "resolve_dotnet": "dotnet",
                "repository_sdk_version": "10.0.301",
                "resolve_coverlet_adapter_path": root / "adapter",
                "find_project_release_output": (output, Path("bin/Release/net10.0")),
                "flatten_ci_dotnet_projects": projects,
            }.items():
                stack.enter_context(patch.object(MODULE, name, return_value=value))
            for name in (
                "require_logged_sdk_version",
                "run_solution_restore_preserving_lock_projections",
                "cleanup_dotnet_batch",
            ):
                stack.enter_context(patch.object(MODULE, name))
            stack.enter_context(patch.object(MODULE, "run", side_effect=fake_run))
            stack.enter_context(redirect_stdout(io.StringIO()))
            if scenario in {"budget", "budget-at-limit"}:
                stack.enter_context(
                    patch.object(
                        MODULE,
                        "CI_SHARD_RETRY_BUDGET_SECONDS",
                        419 if scenario == "budget" else 420,
                    )
                )
                stack.enter_context(patch.object(MODULE, "monotonic", return_value=0))
            error = None
            try:
                with (
                    patch.object(MODULE, "require_ci_retry_project")
                    if scenario == "golden-forged"
                    else nullcontext()
                ):
                    MODULE.verify_ci_dotnet_test_shard("core")
            except (
                RuntimeError,
                subprocess.CalledProcessError,
                subprocess.TimeoutExpired,
            ) as caught:
                error = caught
            manifest = json.loads(
                (root / "upload/shards/core/manifest.json").read_text(encoding="utf-8")
            )
            yield root, project, commands, attempts, manifest, error

    def test_first_pass_does_not_retry(self):
        with self.shard("green") as (_, _, _, attempts, manifest, error):
            self.assertIsNone(error)
            self.assertEqual(1, len(attempts))
            self.assertTrue(manifest["success"])
            self.assertEqual([], manifest["flakyTests"])

    def test_failed_tests_only_retry_once_and_preserve_both_attempts(self):
        with self.shard("recover") as (
            root,
            project,
            commands,
            attempts,
            manifest,
            error,
        ):
            self.assertIsNone(error)
            self.assertEqual(2, len(attempts))
            filtered = [
                arg
                for command in commands
                for arg in command
                if arg.startswith("--TestCaseFilter:")
            ]
            self.assertEqual(
                ["--TestCaseFilter:FullyQualifiedName=Probe.Tests.Case1"], filtered
            )
            self.assertEqual(
                [
                    {
                        "project": project.relative_path,
                        "fullyQualifiedName": "Probe.Tests.Case1",
                    }
                ],
                manifest["flakyTests"],
            )
            for attempt in (1, 2):
                paths = [p for p in manifest["files"] if f"attempt-{attempt}/" in p]
                self.assertTrue(any(p.endswith(".trx") for p in paths))
                self.assertTrue(any(p.endswith(".log") for p in paths))
                for path in paths:
                    self.assertEqual(
                        (root / "work" / path).read_bytes(),
                        (root / "upload" / path).read_bytes(),
                    )
            summary = (root / "summary.md").read_text(encoding="utf-8")
            self.assertIn("Probe.Tests.Case1", summary)
            self.assertIn("bug record", summary)

    def test_second_failure_fails_without_a_third_attempt(self):
        with self.shard("twice") as (_, _, _, attempts, manifest, error):
            self.assertIsNotNone(error)
            self.assertEqual(2, len(attempts))
            self.assertFalse(manifest["success"])
            self.assertEqual([], manifest["flakyTests"])

    def test_recovered_method_is_recorded_even_when_another_method_fails_twice(self):
        with self.shard("partial-recovery") as (
            root,
            project,
            _,
            attempts,
            manifest,
            error,
        ):
            self.assertIsNotNone(error)
            self.assertEqual(2, len(attempts))
            self.assertFalse(manifest["success"])
            self.assertEqual(
                [
                    {
                        "project": project.relative_path,
                        "fullyQualifiedName": "Probe.Tests.Case1",
                    }
                ],
                manifest["flakyTests"],
            )
            self.assertIn(
                "Probe.Tests.Case1", (root / "summary.md").read_text(encoding="utf-8")
            )

    def test_hang_has_blame_limit_and_sequence_evidence_but_no_dump_or_retry(self):
        with self.shard("hang") as (root, _, commands, attempts, manifest, error):
            self.assertIsNotNone(error)
            self.assertEqual(1, len(attempts))
            self.assertTrue(
                any(
                    "--Blame:CollectHangDump;TestTimeout=5m;HangDumpType=None"
                    in command
                    for command in commands
                )
            )
            dumps = [p for p in manifest["files"] if p.endswith(".dmp")]
            self.assertEqual([], dumps)
            self.assertFalse(list((root / "upload").rglob("*.dmp")))
            self.assertTrue(
                any(
                    p.endswith("Sequence_af6bc426faab481fa504c42e3d52afd2.xml")
                    for p in manifest["files"]
                )
            )

    def test_build_discovery_and_incomplete_inventory_do_not_retry(self):
        for scenario, expected in (
            ("build", 0),
            ("discovery", 0),
            ("incomplete", 1),
            ("aborted", 1),
            ("timeout", 1),
            ("zero-with-failures", 1),
            ("platform-error", 1),
            ("unknown-error", 1),
            ("unmatched-error", 1),
            ("first-exit-two", 1),
            ("retry-exit-two", 2),
        ):
            with (
                self.subTest(scenario=scenario),
                self.shard(scenario) as (_, _, _, attempts, manifest, error),
            ):
                self.assertIsNotNone(error)
                self.assertEqual(expected, len(attempts))
                self.assertFalse(manifest["success"])

    def test_retry_cannot_admit_extra_tests(self):
        with self.shard("extra") as (_, _, _, attempts, manifest, error):
            self.assertIsNotNone(error)
            self.assertEqual(2, len(attempts))
            self.assertFalse(manifest["success"])

    def test_theory_filter_repeats_only_failed_method_and_requires_all_its_rows(self):
        for scenario in ("theory", "missing-theory"):
            with (
                self.subTest(scenario=scenario),
                self.shard(scenario) as (_, _, commands, attempts, manifest, error),
            ):
                self.assertEqual(2, len(attempts))
                self.assertEqual(scenario == "theory", error is None)
                self.assertEqual(scenario == "theory", manifest["success"])
                self.assertEqual(
                    ["--TestCaseFilter:FullyQualifiedName=Probe.Tests.Case1"],
                    [
                        a
                        for c in commands
                        for a in c
                        if a.startswith("--TestCaseFilter:")
                    ],
                )

    def test_hang_in_retry_fails_and_keeps_both_attempts_and_sequence(self):
        with self.shard("retry-hang") as (_, _, _, attempts, manifest, error):
            self.assertIsNotNone(error)
            self.assertEqual(2, len(attempts))
            self.assertFalse(manifest["success"])
            self.assertEqual(
                2,
                len(
                    [
                        p
                        for p in manifest["files"]
                        if "Domain.Tests/" in p and p.endswith(".trx")
                    ]
                ),
            )
            self.assertTrue(
                any("attempt-2/" in p and "Sequence_" in p for p in manifest["files"])
            )

    def test_hang_attachment_allowlist_rejects_empty_oversized_and_excess_files(self):
        for scenario in ("empty", "oversized", "excess"):
            with (
                self.subTest(scenario=scenario),
                tempfile.TemporaryDirectory() as temporary,
            ):
                root = Path(temporary)
                for index in range(9 if scenario == "excess" else 1):
                    (root / f"Sequence_{index:032x}.xml").write_bytes(
                        b""
                        if scenario == "empty"
                        else b"12345"
                        if scenario == "oversized"
                        else b"1"
                    )
                with (
                    patch.object(MODULE, "CI_HANG_SEQUENCE_MAX_BYTES", 4),
                    self.assertRaisesRegex(
                        RuntimeError,
                        "CI hang attachment (is empty or oversized|count exceeds eight)",
                    ),
                ):
                    MODULE.collect_ci_hang_attachments(root)
        with tempfile.TemporaryDirectory() as temporary:
            root = Path(temporary)
            (root / "private.bin").write_bytes(b"synthetic non-evidence")
            for name in (
                "host.dmp",
                "host_Sequence.xml",
                "Sequence_abc.xml",
                "Sequence_" + "a" * 33 + ".xml",
            ):
                (root / name).write_bytes(b"not allowed")
            self.assertEqual((), MODULE.collect_ci_hang_attachments(root))

    def test_invalid_second_attempt_does_not_discard_valid_first_attempt(self):
        for scenario in ("retry-launch-error", "retry-invalid-attachment"):
            with (
                self.subTest(scenario=scenario),
                self.shard(scenario) as (root, _, _, attempts, manifest, error),
            ):
                self.assertIsNotNone(error)
                self.assertEqual(2, len(attempts))
                self.assertFalse(manifest["success"])
                first_trx = [
                    p
                    for p in manifest["files"]
                    if "Domain.Tests/attempt-1/" in p and p.endswith(".trx")
                ]
                self.assertEqual(1, len(first_trx))
                self.assertTrue((root / "upload" / first_trx[0]).is_file())
                if scenario == "retry-invalid-attachment":
                    second = [p for p in manifest["files"] if "attempt-2/" in p]
                    self.assertTrue(any(p.endswith(".trx") for p in second))
                    self.assertTrue(any(p.endswith(".log") for p in second))
                    self.assertFalse(any(p.endswith(".dmp") for p in second))

    def test_aggregate_rejects_retry_filter_log_hash_and_attachment_drift(self):
        for mutation in ("filter", "missing-log", "hash", "hang", "third-attempt"):
            with (
                self.subTest(mutation=mutation),
                self.shard("recover") as (root, _, _, _, manifest, _),
            ):
                fixture = fixtures.VerifyOrchestrationTests()
                downloads = root / "downloads"
                fixture.stage_complete_ci_dotnet_evidence(
                    downloads, "a" * 40, owners=("build",)
                )
                artifact = fixture.ci_artifact_root(downloads, "core")
                shutil.copytree(root / "upload", artifact)
                retry = manifest["projects"][0]["retry"]
                if mutation == "filter":
                    retry["filter"] = "FullyQualifiedName~Probe.Tests"
                elif mutation == "missing-log":
                    (artifact / retry["log"]).unlink()
                elif mutation == "hash":
                    (artifact / retry["trx"]).write_text("changed", encoding="utf-8")
                else:
                    extra = Path(retry["trx"]).parent / (
                        "host.dmp"
                        if mutation == "hang"
                        else "attempt-3/test-results.trx"
                    )
                    (artifact / extra).parent.mkdir(parents=True, exist_ok=True)
                    (artifact / extra).write_bytes(b"unexpected evidence")
                    manifest["files"][extra.as_posix()] = MODULE.sha256_file(
                        artifact / extra
                    )
                fixture.write_ci_manifest(
                    artifact / "shards/core/manifest.json", manifest
                )
                with (
                    self.assertRaisesRegex(
                        RuntimeError,
                        {
                            "filter": "retry filter or evidence paths changed",
                            "missing-log": "missing",
                            "hash": "invalid|syntax error",
                            "hang": "file inventory changed",
                            "third-attempt": "file inventory changed",
                        }[mutation],
                    ),
                    patch.object(MODULE, "verify_coverage") as coverage,
                ):
                    MODULE.finalize_ci_dotnet_evidence(downloads)
                coverage.assert_not_called()

    def test_retry_filter_rejects_missing_or_unsafe_metadata(self):
        for mutation in ("missing", "operator", "duplicate", "different-method"):
            with (
                self.subTest(mutation=mutation),
                self.shard("recover") as (root, _, _, _, manifest, _),
            ):
                first = root / "upload" / manifest["projects"][0]["trx"]
                second = root / "upload" / manifest["projects"][0]["retry"]["trx"]
                target = second if mutation == "different-method" else first
                tree = MODULE.ET.parse(target)
                definitions = tree.find(".//{*}TestDefinitions")
                definition = (
                    definitions[0] if mutation == "different-method" else definitions[1]
                )
                if mutation == "missing":
                    definitions.remove(definition)
                elif mutation == "duplicate":
                    definitions.append(definition)
                else:
                    definition.find("{*}TestMethod").set(
                        "name",
                        "Other"
                        if mutation == "different-method"
                        else "Case0|FullyQualifiedName~Other",
                    )
                tree.write(target, encoding="utf-8", xml_declaration=True)
                with self.assertRaisesRegex(
                    RuntimeError,
                    {
                        "missing": "no unique TRX TestMethod definition",
                        "duplicate": "unique TRX TestMethod definitions",
                        "operator": "unsupported fully-qualified test name",
                        "different-method": "TestMethod and execution identity disagree",
                    }[mutation],
                ):
                    MODULE.require_ci_retry_results(first, second)

    def test_retry_limits_keep_original_failure(self):
        for scenario, reason in (
            ("golden", "GoldenRegression tests are never retried"),
            ("budget", "insufficient shard time for a flaky retry"),
            ("too-many", "too many failures for a flaky retry"),
        ):
            with (
                self.subTest(scenario=scenario),
                self.shard(scenario) as (_, _, _, attempts, manifest, error),
            ):
                self.assertEqual(1, len(attempts))
                self.assertFalse(manifest["success"])
                self.assertIn(reason, str(error))

    def test_retry_bounds_admit_twenty_fqns_and_exact_time_margin(self):
        for scenario in ("twenty", "budget-at-limit"):
            with (
                self.subTest(scenario=scenario),
                self.shard(scenario) as (_, _, _, attempts, manifest, error),
            ):
                self.assertIsNone(error)
                self.assertEqual(2, len(attempts))
                self.assertTrue(manifest["success"])
                self.assertEqual(
                    20 if scenario == "twenty" else 1, len(manifest["flakyTests"])
                )

    def test_finalizer_rejects_forged_successful_golden_retry(self):
        with self.shard("golden-forged") as (root, _, _, _, manifest, error):
            self.assertIsNone(error)
            self.assertTrue(manifest["success"])
            fixture = fixtures.VerifyOrchestrationTests()
            downloads = root / "downloads"
            fixture.stage_complete_ci_dotnet_evidence(
                downloads, "a" * 40, owners=("build",)
            )
            shutil.copytree(
                root / "upload", fixture.ci_artifact_root(downloads, "core")
            )
            with (
                self.assertRaisesRegex(
                    RuntimeError, "GoldenRegression tests are never retried"
                ),
                patch.object(MODULE, "verify_coverage") as coverage,
            ):
                MODULE.finalize_ci_dotnet_evidence(downloads)
            coverage.assert_not_called()

    def test_flaky_bug_gate_matches_exact_fqn_only(self):
        for bug_text, accepted in (
            (None, False),
            ("Probe.Tests.Case10", False),
            ("OtherProbe.Tests.Case1", False),
            ("Tests and Case1", False),
            ("`Probe.Tests.Case1`", True),
        ):
            with (
                self.subTest(bug_text=bug_text),
                self.shard("recover", bug_text=bug_text) as (root, _, _, _, _, _),
            ):
                fixture = fixtures.VerifyOrchestrationTests()
                downloads = root / "downloads"
                fixture.stage_complete_ci_dotnet_evidence(
                    downloads, "a" * 40, owners=("build",)
                )
                shutil.copytree(
                    root / "upload", fixture.ci_artifact_root(downloads, "core")
                )
                console = io.StringIO()
                with (
                    redirect_stdout(console),
                    patch.object(MODULE, "verify_coverage") as coverage,
                ):
                    if accepted:
                        MODULE.finalize_ci_dotnet_evidence(downloads)
                        coverage.assert_called_once()
                    else:
                        with self.assertRaisesRegex(
                            RuntimeError,
                            "flaky test has no bug record.*Probe.Tests.Case1",
                        ):
                            MODULE.finalize_ci_dotnet_evidence(downloads)
                        coverage.assert_not_called()
                self.assertIn(
                    "::warning title=Flaky test::tests/NvtFwCombiner.Domain.Tests/NvtFwCombiner.Domain.Tests.csproj Probe.Tests.Case1",
                    console.getvalue(),
                )

    def test_producer_failure_wins_over_missing_or_broken_artifacts(self):
        for mutation in ("missing", "json", "download"):
            with (
                self.subTest(mutation=mutation),
                self.shard("recover") as (root, _, _, _, _, _),
            ):
                fixture = fixtures.VerifyOrchestrationTests()
                downloads = root / "downloads"
                fixture.stage_complete_ci_dotnet_evidence(
                    downloads, "a" * 40, owners=("build",)
                )
                artifact = fixture.ci_artifact_root(downloads, "core")
                if mutation != "missing":
                    shutil.copytree(root / "upload", artifact)
                    (artifact / "shards/core/manifest.json").write_text(
                        "broken", encoding="utf-8"
                    )
                env = {"NFC_CI_DOTNET_TEST_RESULT": "failure"}
                if mutation == "download":
                    env["NFC_CI_DOTNET_DOWNLOAD_OUTCOME"] = "failure"
                with (
                    patch.dict(os.environ, env),
                    self.assertRaisesRegex(
                        RuntimeError, "test producer failed: failure"
                    ),
                ):
                    MODULE.finalize_ci_dotnet_evidence(downloads)

    def test_unrelated_passing_definition_cannot_block_retry(self):
        with self.shard("recover") as (root, _, _, _, manifest, _):
            first = root / "upload" / manifest["projects"][0]["trx"]
            second = root / "upload" / manifest["projects"][0]["retry"]["trx"]
            tree = MODULE.ET.parse(first)
            definitions = tree.find(".//{*}TestDefinitions")
            definitions[0].find("{*}TestMethod").set("name", "Unsafe|Name")
            definitions.append(definitions[0])
            tree.write(first, encoding="utf-8", xml_declaration=True)
            self.assertEqual(
                ("Probe.Tests.Case1",), MODULE.require_ci_retry_results(first, second)
            )

    def test_local_and_release_command_default_has_no_blame_or_retry(self):
        command = MODULE.local_dotnet_vstest_command(
            "dotnet", Path("Probe.dll"), None, Path("results")
        )
        self.assertFalse(
            any("Blame" in arg or "TestCaseFilter" in arg for arg in command)
        )

    def test_aggregate_recomputes_and_reports_flaky_list(self):
        with self.shard("recover") as (root, _, _, _, manifest, error):
            self.assertIsNone(error)
            fixture = fixtures.VerifyOrchestrationTests()
            downloads = root / "downloads"
            fixture.stage_complete_ci_dotnet_evidence(
                downloads, "a" * 40, owners=("build",)
            )
            artifact = fixture.ci_artifact_root(downloads, "core")
            shutil.copytree(root / "upload", artifact)
            console = io.StringIO()
            with (
                redirect_stdout(console),
                patch.object(MODULE, "verify_coverage") as coverage,
            ):
                MODULE.finalize_ci_dotnet_evidence(downloads)
            coverage.assert_called_once()
            self.assertIn("Probe.Tests.Case1", console.getvalue())
            self.assertIn("bug record", console.getvalue())
            self.assertIn("GoldenRegression 3/3", console.getvalue())
            # Editing only the manifest must not erase evidence of a recovered failure.
            manifest["flakyTests"] = []
            fixture.write_ci_manifest(artifact / "shards/core/manifest.json", manifest)
            with (
                self.assertRaisesRegex(RuntimeError, "flaky"),
                patch.object(MODULE, "verify_coverage") as coverage,
            ):
                MODULE.finalize_ci_dotnet_evidence(downloads)
            coverage.assert_not_called()

    def test_failed_aggregate_still_shows_producer_flaky_declarations(self):
        with self.shard("recover") as (root, _, _, _, _, _):
            fixture = fixtures.VerifyOrchestrationTests()
            downloads = root / "downloads"
            fixture.stage_complete_ci_dotnet_evidence(
                downloads, "a" * 40, owners=("build",)
            )
            shutil.copytree(
                root / "upload", fixture.ci_artifact_root(downloads, "core")
            )
            console = io.StringIO()
            with (
                patch.dict(os.environ, {"NFC_CI_DOTNET_TEST_RESULT": "failure"}),
                redirect_stdout(console),
                self.assertRaisesRegex(RuntimeError, "test producer failed"),
                patch.object(MODULE, "verify_coverage") as coverage,
            ):
                MODULE.finalize_ci_dotnet_evidence(downloads)
            coverage.assert_not_called()
            self.assertIn("Probe.Tests.Case1", console.getvalue())
            self.assertIn("unverified", console.getvalue())


if __name__ == "__main__":
    unittest.main()
