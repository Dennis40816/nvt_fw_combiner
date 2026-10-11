"""Decision 191 behavior through the real shard and aggregate, with a fake runner."""

from contextlib import ExitStack, contextmanager, redirect_stdout
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


def flaky_declarations(manifest):
    return [
        {"project": row["relativePath"], "fullyQualifiedName": name}
        for row in manifest["projects"]
        for name in row["flakyTests"]
    ]


class CiFailureDetailsTests(unittest.TestCase):
    def write_trx(self, path, failures, *, passed=True):
        identities = tuple(failures) + (("Probe.Tests.Passed",) if passed else ())
        fixtures.VerifyOrchestrationTests().write_ci_trx(
            path,
            total=len(identities),
            skipped=0,
            identities=identities,
            outcomes=("Failed",) * len(failures) + (("Passed",) if passed else ()),
        )
        tree = MODULE.ET.parse(path)
        for result in tree.findall(".//{*}UnitTestResult"):
            output = MODULE.ET.SubElement(result, "Output")
            MODULE.ET.SubElement(output, "StdOut").text = "DO NOT REPORT STDOUT"
            info = MODULE.ET.SubElement(output, "ErrorInfo")
            message, stack = failures.get(
                result.get("testName"), ("DO NOT REPORT PASSED", "passed stack")
            )
            MODULE.ET.SubElement(info, "Message").text = message
            MODULE.ET.SubElement(info, "StackTrace").text = stack
        # Preserve CR in XML text, instead of XML's literal-CR normalization.
        path.write_bytes(MODULE.ET.tostring(tree.getroot()).replace(b"\r", b"&#13;"))

    def test_existing_outcome_parser_ignores_error_text_and_keeps_case_outcomes(self):
        with tempfile.TemporaryDirectory() as temporary:
            path = Path(temporary) / "test-results.trx"
            self.write_trx(path, {"Probe.Tests.Case(value: 1)": ("failure", "frame")})
            after = MODULE.parse_trx_test_outcomes(
                path, preserve_case_identity=True
            )
            self.assertEqual({"Probe.Tests.Case(value: 1)": 1}, after["Failed"])
            self.assertEqual({"Probe.Tests.Passed": 1}, after["Passed"])
            self.assertNotIn("failure", repr(after))
            self.assertNotIn("frame", repr(after))

    def test_report_excludes_messages_stacks_and_theory_arguments(self):
        with tempfile.TemporaryDirectory() as temporary:
            root = Path(temporary)
            trx, summary = root / "test-results.trx", root / "summary.md"
            self.write_trx(
                trx, {"Probe.Tests.Failed(value: 1)": ("private message", "private stack")}
            )
            console = io.StringIO()
            with (
                redirect_stdout(console),
                patch.dict(os.environ, {"GITHUB_STEP_SUMMARY": str(summary)}),
            ):
                MODULE.report_ci_test_failure_details(
                    "core", [("Project", "attempt-1", trx)]
                )
            for report in (console.getvalue(), summary.read_text()):
                self.assertIn("total 2 passed 1 failed 1 skipped 0", report)
                self.assertIn("Probe.Tests.Failed", report)
                for private in (
                    "value: 1",
                    "private message",
                    "private stack",
                    "DO NOT REPORT",
                ):
                    self.assertNotIn(private, report)

    def test_method_report_cap_preserves_each_attempt_counter(self):
        with tempfile.TemporaryDirectory() as temporary:
            root = Path(temporary)
            first, retry = root / "first.trx", root / "retry.trx"
            self.write_trx(
                first,
                {f"Probe.Tests.Case{i:02d}": ("private", "private") for i in range(21)},
            )
            self.write_trx(retry, {"Probe.Tests.Case00": ("private retry", "private")})
            console = io.StringIO()
            with (
                redirect_stdout(console),
                patch.dict(os.environ, {"GITHUB_STEP_SUMMARY": str(root / "summary.md")}),
            ):
                MODULE.report_ci_test_failure_details(
                    "core",
                    [("Project", "attempt-1", first), ("Project", "attempt-2", retry)],
                )
            for report in (console.getvalue(), (root / "summary.md").read_text()):
                self.assertIn("Probe.Tests.Case19", report)
                self.assertNotIn("Probe.Tests.Case20", report)
                self.assertIn("total 22 passed 1 failed 21 skipped 0", report)
                self.assertIn("total 2 passed 1 failed 1 skipped 0", report)
                self.assertNotIn("private", report)

    def test_missing_invalid_or_passing_trx_adds_no_diagnostics(self):
        with tempfile.TemporaryDirectory() as temporary:
            root = Path(temporary)
            invalid, passed = root / "invalid.trx", root / "passed.trx"
            unknown_encoding = root / "unknown-encoding.trx"
            invalid.write_text("<broken", encoding="utf-8")
            unknown_encoding.write_text(
                '<?xml version="1.0" encoding="not-a-real-encoding"?><TestRun/>',
                encoding="utf-8",
            )
            self.write_trx(passed, {})
            console = io.StringIO()
            summary = root / "summary.md"
            with (
                redirect_stdout(console),
                patch.dict(os.environ, {"GITHUB_STEP_SUMMARY": str(summary)}),
            ):
                MODULE.report_ci_test_failure_details(
                    "aggregate",
                    [
                        ("Project", "attempt-1", path)
                        for path in (
                            root / "missing.trx", invalid, unknown_encoding, passed
                        )
                    ],
                )
            self.assertEqual("", console.getvalue())
            self.assertFalse(summary.exists())

    def test_resolved_placeholder_identity_and_missing_errorinfo_remain_reportable(self):
        with tempfile.TemporaryDirectory() as temporary:
            path = Path(temporary) / "test-results.trx"
            self.write_trx(path, {"Probe.Tests.Case(value: 1)": ("private", "private")})
            tree = MODULE.ET.parse(path)
            result = tree.find(".//{*}UnitTestResult")
            result.set("testName", "<unknown test ID " + "a" * 64 + ">")
            result.remove(result.find("{*}Output"))
            tree.write(path, encoding="utf-8")
            console = io.StringIO()
            with (
                redirect_stdout(console),
                patch.dict(os.environ, {"GITHUB_STEP_SUMMARY": ""}),
            ):
                MODULE.report_ci_test_failure_details(
                    "core", [("Project", "attempt-1", path)]
                )
            self.assertIn("Probe.Tests.Case\n", console.getvalue())
            self.assertNotIn("value: 1", console.getvalue())
            self.assertNotIn("private", console.getvalue())

    def test_report_contains_only_counters_and_parameter_free_methods(self):
        with tempfile.TemporaryDirectory() as temporary:
            path = Path(temporary) / "test-results.trx"
            self.write_trx(path, {"Probe.Tests.Case": ("x" * 1500, "frame\n" * 15)})
            console = io.StringIO()
            with (
                redirect_stdout(console),
                patch.dict(os.environ, {"GITHUB_STEP_SUMMARY": ""}),
            ):
                MODULE.report_ci_test_failure_details(
                    "core", [("Project", "attempt-1", path)]
                )
            self.assertEqual(
                "total 2 passed 1 failed 1 skipped 0\nProbe.Tests.Case\n",
                console.getvalue(),
            )

    def test_inconsistent_duplicate_results_produce_no_public_report(
        self,
    ):
        with tempfile.TemporaryDirectory() as temporary:
            root = Path(temporary)
            path = root / "test-results.trx"
            self.write_trx(path, {"Probe.Tests.Case": ("private", "private")})
            tree = MODULE.ET.parse(path)
            results = tree.find("{*}Results")
            results.append(MODULE.ET.fromstring(MODULE.ET.tostring(results[0])))
            tree.write(path, encoding="utf-8")
            console = io.StringIO()
            with (
                redirect_stdout(console),
                patch.dict(os.environ, {"GITHUB_STEP_SUMMARY": ""}),
            ):
                MODULE.report_ci_test_failure_details(
                    "core", [("First", "attempt-1", path), ("Second", "attempt-1", path)]
                )
            self.assertEqual("", console.getvalue())

    def test_flaky_warning_contains_only_parameter_free_methods(self):
        console = io.StringIO()
        with redirect_stdout(console), patch.dict(os.environ, {"GITHUB_STEP_SUMMARY": ""}):
            MODULE.report_ci_flaky_tests(
                "core",
                [
                    {
                        "project": "private%\r\n::error::data",
                        "fullyQualifiedName": "Probe.Tests.Case(value: private)",
                    }
                ],
            )
        self.assertTrue(
            console.getvalue().startswith("::warning title=Flaky test::Probe.Tests.Case\n")
        )
        self.assertNotIn("private", console.getvalue())
        self.assertNotIn("::error::", console.getvalue())


class CiDotnetRetryTests(unittest.TestCase):
    @contextmanager
    def shard(self, scenario, *, bug_text="Probe.Tests.Case1", terminal_phase=None, terminal=None):
        fixture = fixtures.VerifyOrchestrationTests()
        project = MODULE.CiDotnetProject(
            "tests/NvtFwCombiner.Domain.Tests/NvtFwCombiner.Domain.Tests.csproj"
        )
        golden = MODULE.CiDotnetProject(
            "tests/NvtFwCombiner.GoldenRegression.Tests/NvtFwCombiner.GoldenRegression.Tests.csproj"
        )
        if scenario == "golden":
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
                        if active_scenario
                        in {"theory", "missing-theory", "mixed-theory"}
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
                    if active_scenario in {"theory", "missing-theory", "mixed-theory"}:
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
                        outcomes = ("Failed",) if retry else ("Passed", "Failed", "Passed")
                    if retry and active_scenario == "extra":
                        identities = ("Probe.Tests.Case1", "Probe.Tests.Case0")
                        outcomes = ("Passed", "Passed")
                    if active_scenario == "mixed-theory":
                        outcomes = (
                            ("Passed", "Failed")
                            if retry
                            else ("Passed", "Failed", "Failed")
                        )
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
                                ).text = f"[xUnit.net 00:00:01.45]     {identity} [FAIL]"
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
                    for old_definitions in tree.findall("{*}TestDefinitions"):
                        tree.getroot().remove(old_definitions)
                    definitions = MODULE.ET.SubElement(
                        tree.getroot(), ns + "TestDefinitions"
                    )
                    for index, result in enumerate(tree.findall(".//{*}UnitTestResult")):
                        identity = identities[index]
                        output = MODULE.ET.SubElement(result, ns + "Output")
                        info = MODULE.ET.SubElement(output, ns + "ErrorInfo")
                        MODULE.ET.SubElement(info, ns + "Message").text = (
                            f"attempt-{2 if retry else 1} failure for {identity}"
                            if result.get("outcome") == "Failed"
                            else "DO NOT REPORT PASSED"
                        )
                        MODULE.ET.SubElement(
                            info, ns + "StackTrace"
                        ).text = "at Probe.Tests.Run()\n at Runner.Main()"
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
                "CI_DOTNET_SHARDS": {**MODULE.CI_DOTNET_SHARDS, "core": projects},
            }.items():
                stack.enter_context(patch.object(MODULE, name, value))
            fixture.write_ci_summary_baseline(root)
            for name, value in {
                "resolve_dotnet": "dotnet",
                "repository_sdk_version": "10.0.301",
                "resolve_coverlet_adapter_path": root / "adapter",
                "find_project_release_output": (output, Path("bin/Release/net10.0")),
                "flatten_ci_dotnet_projects": tuple(
                    p for group in MODULE.CI_DOTNET_SHARDS.values() for p in group
                ),
            }.items():
                stack.enter_context(patch.object(MODULE, name, return_value=value))
            for name in (
                "require_logged_sdk_version",
                "run_locked_solution_restore",
                "cleanup_dotnet_batch",
            ):
                stack.enter_context(patch.object(
                    MODULE, name,
                    side_effect=terminal if name == {
                        "execution": "require_logged_sdk_version",
                        "cleanup": "cleanup_dotnet_batch",
                    }.get(terminal_phase) else None,
                ))
            stack.enter_context(patch.object(MODULE, "run", side_effect=fake_run))
            console = io.StringIO()
            stack.enter_context(redirect_stdout(console))
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
                MODULE.verify_ci_dotnet_test_shard("core")
            except (
                KeyboardInterrupt,
                SystemExit,
                RuntimeError,
                subprocess.CalledProcessError,
                subprocess.TimeoutExpired,
            ) as caught:
                error = caught
            manifest_path = root / "upload/shards/core/manifest.json"
            if not manifest_path.exists():
                manifest_path = root / "work/shards/core/manifest.json"
            manifest = json.loads(manifest_path.read_text(encoding="utf-8"))
            (root / "job.log").write_text(console.getvalue(), encoding="utf-8")
            yield root, project, commands, attempts, manifest, error

    def test_first_pass_does_not_retry(self):
        with self.shard("green") as (root, _, _, attempts, manifest, error):
            self.assertIsNone(error)
            self.assertEqual(1, len(attempts))
            self.assertTrue(manifest["success"])
            self.assertEqual([], flaky_declarations(manifest))
            self.assertNotIn("Failed test", (root / "job.log").read_text())

    def test_failed_tests_only_retry_once_and_preserve_both_attempts(self):
        with self.shard("recover") as (root, project, commands, attempts, manifest, error):
            self.assertIsNone(error)
            self.assertEqual(2, len(attempts))
            self.assertEqual(
                ["--TestCaseFilter:FullyQualifiedName=Probe.Tests.Case1"],
                [
                    arg
                    for command in commands
                    for arg in command
                    if arg.startswith("--TestCaseFilter:")
                ],
            )
            self.assertEqual(
                [
                    {
                        "project": project.relative_path,
                        "fullyQualifiedName": "Probe.Tests.Case1",
                    }
                ],
                flaky_declarations(manifest),
            )
            row = manifest["projects"][0]
            self.assertEqual({"retried": True, "recoveredTests": 1}, row["retry"])
            self.assertEqual(
                (3, 3, 0, 0),
                tuple(row[k] for k in ("total", "passed", "failed", "skipped")),
            )
            for attempt in attempts:
                self.assertTrue((attempt / "test-results.trx").is_file())
                self.assertTrue((attempt / "attempt.log").is_file())
            self.assertFalse(list((root / "upload").rglob("*.trx")))
            self.assertFalse(list((root / "upload").rglob("*.log")))
            for report in (
                (root / "summary.md").read_text(),
                (root / "job.log").read_text(),
            ):
                self.assertIn("Probe.Tests.Case1", report)
                self.assertIn("bug record", report)
                self.assertNotIn("attempt-1 failure", report)
                self.assertNotIn("at Probe.Tests.Run()", report)

    def test_second_failure_fails_without_a_third_attempt(self):
        with self.shard("twice") as (root, _, _, attempts, manifest, error):
            self.assertIsNotNone(error)
            self.assertEqual(2, len(attempts))
            self.assertFalse(manifest["success"])
            self.assertEqual([], flaky_declarations(manifest))
            self.assertEqual(["Probe.Tests.Case1"], manifest["projects"][0]["failedTests"])
            for report in (
                (root / "summary.md").read_text(),
                (root / "job.log").read_text(),
            ):
                self.assertIn("Probe.Tests.Case1", report)
                self.assertNotIn("failure for", report)
                self.assertNotIn("at Runner.Main()", report)

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
                flaky_declarations(manifest),
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
                    "--Blame:CollectHangDump;TestTimeout=5m;HangDumpType=None" in command
                    for command in commands
                )
            )
            self.assertTrue(list(attempts[0].rglob("Sequence_*.xml")))
            self.assertFalse(list((root / "upload").rglob("*.dmp")))
            self.assertFalse(list((root / "upload").rglob("Sequence_*.xml")))
            self.assertFalse(any("Sequence_" in path for path in manifest["files"]))

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
        with self.shard("retry-hang") as (root, _, _, attempts, manifest, error):
            self.assertIsNotNone(error)
            self.assertEqual(2, len(attempts))
            self.assertFalse(manifest["success"])
            self.assertTrue(all((path / "test-results.trx").is_file() for path in attempts))
            self.assertTrue(list(attempts[1].rglob("Sequence_*.xml")))
            self.assertFalse(list((root / "upload").rglob("*.trx")))
            self.assertFalse(list((root / "upload").rglob("Sequence_*.xml")))

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
                self.assertTrue((attempts[0] / "test-results.trx").is_file())
                self.assertTrue((attempts[0] / "attempt.log").is_file())
                self.assertFalse(list((root / "upload").rglob("*.trx")))
                self.assertFalse(list((root / "upload").rglob("*.log")))

    def test_aggregate_rejects_retry_filter_log_hash_and_attachment_drift(self):
        for mutation in ("filter", "missing-summary", "hash", "hang", "third-attempt"):
            with (
                self.subTest(mutation=mutation),
                self.shard("recover") as (root, _, _, _, manifest, _),
            ):
                fixture = fixtures.VerifyOrchestrationTests()
                downloads = root / "downloads"
                fixture.stage_complete_ci_dotnet_evidence(
                    downloads, "a" * 40, owners=("build", "bootstrap", "ui")
                )
                artifact = fixture.ci_artifact_root(downloads, "core")
                shutil.copytree(root / "upload", artifact)
                if mutation == "filter":
                    manifest["projects"][0]["retry"]["filter"] = "private"
                elif mutation == "missing-summary":
                    (artifact / "shards/core/shard-summary.json").unlink()
                elif mutation == "hash":
                    manifest["files"]["shards/core/shard-summary.json"] = "0" * 64
                else:
                    extra = artifact / (
                        "host.dmp" if mutation == "hang" else "attempt-3/test-results.trx"
                    )
                    extra.parent.mkdir(parents=True, exist_ok=True)
                    extra.write_text("private")
                fixture.write_ci_manifest(artifact / "shards/core/manifest.json", manifest)
                with (
                    self.assertRaisesRegex(
                        RuntimeError,
                        "^"
                        + {
                            "filter": r"Public evidence summary is invalid\.",
                            "missing-summary": r"Public evidence file inventory is invalid\.",
                            "hash": r"Public evidence hash does not match\.",
                            "hang": r"Public evidence contains unexpected files \(count: 1\)\.",
                            "third-attempt": r"Public evidence contains unexpected files \(count: 1\)\.",
                        }[mutation]
                        + "$",
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
                first = (
                    root
                    / "work/shards/core/results"
                    / Path(manifest["projects"][0]["relativePath"]).stem
                    / "attempt-1/test-results.trx"
                )
                second = first.parent.parent / "attempt-2/test-results.trx"
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

    def test_retry_limits_keep_private_diagnosis_and_fixed_public_failure(self):
        for scenario, reason in (
            ("golden", "GoldenRegression tests are never retried"),
            ("budget", "insufficient shard time for a flaky retry"),
            ("too-many", "too many failures for a flaky retry"),
        ):
            with (
                self.subTest(scenario=scenario),
                self.shard(scenario) as (root, project, _, attempts, manifest, error),
            ):
                self.assertEqual(1, len(attempts))
                self.assertFalse(manifest["success"])
                self.assertIn(reason, (root / "work/shards/core/shard.log").read_text())
                self.assertEqual(f"{project.name}: project failed.", str(error))

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
                    20 if scenario == "twenty" else 1, len(flaky_declarations(manifest))
                )

    def test_finalizer_rejects_forged_successful_golden_retry(self):
        with self.shard("green") as (root, _, _, _, manifest, error):
            self.assertIsNone(error)
            self.assertTrue(manifest["success"])
            fixture = fixtures.VerifyOrchestrationTests()
            downloads = root / "downloads"
            fixture.stage_complete_ci_dotnet_evidence(
                downloads, "a" * 40, owners=("build", "bootstrap", "ui")
            )
            shutil.copytree(root / "upload", fixture.ci_artifact_root(downloads, "core"))
            manifest_path = fixture.ci_artifact_root(downloads, "core") / "shards/core/manifest.json"
            golden = next(row for row in manifest["projects"] if Path(row["relativePath"]).stem == "NvtFwCombiner.GoldenRegression.Tests")
            golden["retry"] = {"retried": True, "recoveredTests": 1}
            golden["flakyTests"] = ["Probe.Tests.Case1"]
            fixture.write_ci_manifest(manifest_path, manifest)
            with (
                self.assertRaisesRegex(
                    RuntimeError, r"^Public evidence summary is invalid\.$"
                ),
                patch.object(MODULE, "verify_coverage") as coverage,
            ):
                MODULE.finalize_ci_dotnet_evidence(downloads)
            coverage.assert_not_called()

    def test_unrelated_unknown_identity_does_not_veto_failed_filter(self):
        ns = "http://microsoft.com/schemas/VisualStudio/TeamTest/2010"
        placeholder = "<unknown test ID " + "a" * 64 + ">"
        trx = f"""<?xml version="1.0" encoding="utf-8"?>
<TestRun xmlns="{ns}">
  <Results>
    <UnitTestResult testId="1" testName="Probe.Tests.Case1" outcome="Failed" />
    <UnitTestResult testId="2" testName="{placeholder.replace("<", "&lt;").replace(">", "&gt;")}" outcome="Passed" />
  </Results>
  <TestDefinitions>
    <UnitTest id="1" name="Probe.Tests.Case1"><TestMethod className="Probe.Tests" name="Case1" /></UnitTest>
    <UnitTest id="2" name="Probe.Tests.Other"><TestMethod className="Probe.Tests" name="Other" /></UnitTest>
  </TestDefinitions>
  <ResultSummary outcome="Failed"><Counters total="2" executed="2" passed="1" failed="1" /></ResultSummary>
</TestRun>
"""
        with tempfile.TemporaryDirectory() as directory:
            path = Path(directory) / "test-results.trx"
            path.write_text(trx, encoding="utf-8")
            selection, names, _ = MODULE.ci_retry_selection(path)
        self.assertEqual("FullyQualifiedName=Probe.Tests.Case1", selection)
        self.assertEqual(("Probe.Tests.Case1",), names)

    def test_flaky_bug_gate_matches_exact_fqn_only(self):
        for bug_text, accepted in (
            (None, False),
            ("Probe.Tests.Case10", False),
            ("OtherProbe.Tests.Case1", False),
            ("Tests and Case1", False),
            ("`Probe.Tests.Case1`", True),
            # Only open records count (decision 193): a closed record must be reopened.
            ("Status: fixed on a branch\n`Probe.Tests.Case1`", False),
            ("Status: wontfix\n`Probe.Tests.Case1`", False),
            ("Status: closed, superseded\n`Probe.Tests.Case1`", False),
            ("Status: duplicate of BUG-x\n`Probe.Tests.Case1`", False),
            ("Status: open\n`Probe.Tests.Case1`", True),
            (
                "Status: Application part fixed; host follow-up reopened\n`Probe.Tests.Case1`",
                True,
            ),
        ):
            with (
                self.subTest(bug_text=bug_text),
                self.shard("recover", bug_text=bug_text) as (root, _, _, _, _, _),
            ):
                fixture = fixtures.VerifyOrchestrationTests()
                downloads = root / "downloads"
                fixture.stage_complete_ci_dotnet_evidence(
                    downloads, "a" * 40, owners=("build", "bootstrap", "ui")
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
                            r"^Flaky test has no open bug record\.$",
                        ):
                            MODULE.finalize_ci_dotnet_evidence(downloads)
                        coverage.assert_not_called()
                self.assertIn(
                    "::warning title=Flaky test::Probe.Tests.Case1",
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
                    downloads, "a" * 40, owners=("build", "bootstrap", "ui")
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
                    self.assertRaisesRegex(RuntimeError, "test producer failed: failure"),
                ):
                    MODULE.finalize_ci_dotnet_evidence(downloads)

    def test_unrelated_passing_definition_cannot_block_retry(self):
        with self.shard("recover") as (root, _, _, _, manifest, _):
            first = (
                root
                / "work/shards/core/results"
                / Path(manifest["projects"][0]["relativePath"]).stem
                / "attempt-1/test-results.trx"
            )
            second = first.parent.parent / "attempt-2/test-results.trx"
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
        with self.shard("recover") as (root, project, _, _, manifest, error):
            self.assertIsNone(error)
            fixture = fixtures.VerifyOrchestrationTests()
            downloads = root / "downloads"
            fixture.stage_complete_ci_dotnet_evidence(
                downloads, "a" * 40, owners=("build", "bootstrap", "ui")
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
            self.assertIn(
                "::warning title=Flaky test::Probe.Tests.Case1", console.getvalue()
            )
            self.assertIn("GoldenRegression 3/3", console.getvalue())
            self.assertNotIn("failure for", console.getvalue())
            manifest["projects"][0]["flakyTests"] = []
            fixture.write_ci_manifest(artifact / "shards/core/manifest.json", manifest)
            with (
                self.assertRaisesRegex(
                    RuntimeError, r"^Public evidence summary is invalid\.$"
                ),
                patch.object(MODULE, "verify_coverage") as coverage,
            ):
                MODULE.finalize_ci_dotnet_evidence(downloads)
            coverage.assert_not_called()

    def test_failed_aggregate_still_shows_producer_flaky_declarations(self):
        with self.shard("recover") as (root, _, _, _, _, _):
            fixture = fixtures.VerifyOrchestrationTests()
            downloads = root / "downloads"
            fixture.stage_complete_ci_dotnet_evidence(
                downloads, "a" * 40, owners=("build", "bootstrap", "ui")
            )
            shutil.copytree(root / "upload", fixture.ci_artifact_root(downloads, "core"))
            console = io.StringIO()
            with (
                patch.dict(os.environ, {"NFC_CI_DOTNET_TEST_RESULT": "failure"}),
                redirect_stdout(console),
                self.assertRaisesRegex(RuntimeError, "test producer failed: failure"),
                patch.object(MODULE, "verify_coverage") as coverage,
            ):
                MODULE.finalize_ci_dotnet_evidence(downloads)
            coverage.assert_not_called()
            self.assertIn("Probe.Tests.Case1", console.getvalue())
            self.assertIn("aggregate unverified flaky tests.", console.getvalue())
            self.assertNotIn("failure for", console.getvalue())

    def test_failed_aggregate_reports_public_counters_and_keeps_producer_failure(self):
        for github_actions in ("", "true"):
            with (
                self.subTest(github_actions=github_actions),
                self.shard("twice") as (root, _, _, _, _, error),
            ):
                self.assertIsNotNone(error)
                fixture = fixtures.VerifyOrchestrationTests()
                downloads = root / "downloads"
                fixture.stage_complete_ci_dotnet_evidence(
                    downloads, "a" * 40, owners=("build", "bootstrap", "ui")
                )
                shutil.copytree(
                    root / "upload", fixture.ci_artifact_root(downloads, "core")
                )
                console = io.StringIO()
                with (
                    patch.dict(
                        os.environ,
                        {
                            "NFC_CI_DOTNET_TEST_RESULT": "failure",
                            "GITHUB_ACTIONS": github_actions,
                        },
                    ),
                    redirect_stdout(console),
                    self.assertRaisesRegex(
                        RuntimeError, "test producer failed: failure"
                    ),
                    patch.object(MODULE, "verify_coverage") as coverage,
                ):
                    MODULE.finalize_ci_dotnet_evidence(downloads)
                coverage.assert_not_called()
                self.assertIn(
                    "total 6 passed 5 failed 1 skipped 0", console.getvalue()
                )
                self.assertIn("Probe.Tests.Case1", console.getvalue())
                self.assertNotIn("(", console.getvalue().split("total 6", 1)[1])
                self.assertNotIn("failure for", console.getvalue())

    def test_failed_aggregate_with_missing_or_invalid_public_summary_keeps_producer_failure(
        self,
    ):
        for case in ("missing", "invalid", "invalid-encoding"):
            with (
                self.subTest(case=case),
                self.shard("recover") as (root, _, _, attempts, _, _),
            ):
                fixture = fixtures.VerifyOrchestrationTests()
                downloads = root / "downloads"
                fixture.stage_complete_ci_dotnet_evidence(
                    downloads, "a" * 40, owners=("build", "bootstrap", "ui")
                )
                shutil.copytree(
                    root / "upload", fixture.ci_artifact_root(downloads, "core")
                )
                first = (
                    fixture.ci_artifact_root(downloads, "core")
                    / "shards/core/shard-summary.json"
                )
                if case == "missing":
                    first.unlink()
                else:
                    first.write_bytes(b"{broken" if case == "invalid" else b"\xff")
                console = io.StringIO()
                with (
                    patch.dict(os.environ, {"NFC_CI_DOTNET_TEST_RESULT": "failure"}),
                    redirect_stdout(console),
                    self.assertRaisesRegex(RuntimeError, "test producer failed: failure"),
                    patch.object(MODULE, "verify_coverage") as coverage,
                ):
                    MODULE.finalize_ci_dotnet_evidence(downloads)
                coverage.assert_not_called()
                self.assertNotIn("Probe.Tests.Case1", console.getvalue())
                self.assertNotIn("failure for", console.getvalue())

    def test_mixed_theory_recovery_does_not_declare_a_still_failed_method_flaky(self):
        with self.shard("mixed-theory") as (_, project, _, attempts, manifest, error):
            self.assertIsNotNone(error)
            self.assertEqual(2, len(attempts))
            self.assertEqual(
                (),
                MODULE.require_ci_retry_results(
                    attempts[0] / "test-results.trx",
                    attempts[1] / "test-results.trx",
                    allow_failed=True,
                ),
            )
            row = next(
                row
                for row in manifest["projects"]
                if row["relativePath"] == project.relative_path
            )
            self.assertEqual(["Probe.Tests.Case1"], row["failedTests"])
            self.assertEqual([], row["flakyTests"])
            self.assertEqual({"retried": True, "recoveredTests": 1}, row["retry"])


if __name__ == "__main__":
    unittest.main()
