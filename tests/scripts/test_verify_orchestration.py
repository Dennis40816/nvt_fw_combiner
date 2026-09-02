"""Behavioral tests for canonical verifier lane orchestration."""

from __future__ import annotations

import argparse
import contextlib
import hashlib
import importlib.util
import io
import json
import os
import signal
import subprocess
import sys
import tempfile
import threading
import time
import unittest
from pathlib import Path
from unittest.mock import MagicMock, call, patch


ROOT = Path(__file__).resolve().parents[2]
SCRIPT = ROOT / "scripts" / "verify.py"
sys.path.insert(0, str(SCRIPT.parent))
SPEC = importlib.util.spec_from_file_location("verify", SCRIPT)
assert SPEC is not None and SPEC.loader is not None
MODULE = importlib.util.module_from_spec(SPEC)
sys.modules[SPEC.name] = MODULE
SPEC.loader.exec_module(MODULE)
import validate_repository as REPOSITORY_VALIDATOR  # noqa: E402


class VerifyOrchestrationTests(unittest.TestCase):
    TEST_SESSION_ENVIRONMENT_VARIABLES = (
        "NFC_TEST_AREA_ROOT",
        "NFC_TEST_SESSION_ROOT",
        "NFC_TEST_REPOSITORY_ROOT",
        "GITHUB_ACTIONS",
        "RUNNER_TEMP",
        "TEMP",
        "TMP",
        "TMPDIR",
        "DOTNET_BUNDLE_EXTRACT_BASE_DIR",
        "RUFF_CACHE_DIR",
        "PYTHONPYCACHEPREFIX",
    )

    def test_ci_dotnet_verifier_contract_rejects_legacy_project_execution(self) -> None:
        verifier = SCRIPT.read_text(encoding="utf-8")
        errors: list[str] = []
        REPOSITORY_VALIDATOR.validate_ci_dotnet_verifier_contract(verifier, errors)
        self.assertEqual([], errors)

        for marker in (
            "def ci_dotnet_test_command(",
            '"--collect:XPlat Code Coverage",',
            '"--results-directory",',
        ):
            with self.subTest(marker=marker):
                errors = []
                REPOSITORY_VALIDATOR.validate_ci_dotnet_verifier_contract(
                    f"{verifier}\n{marker}\n",
                    errors,
                )
                self.assertTrue(
                    any("obsolete project-level" in error for error in errors),
                    errors,
                )

    @contextlib.contextmanager
    def verifier_environment(self, **values: str):
        previous = {
            name: os.environ.get(name)
            for name in self.TEST_SESSION_ENVIRONMENT_VARIABLES
        }
        try:
            for name in self.TEST_SESSION_ENVIRONMENT_VARIABLES:
                os.environ.pop(name, None)
            os.environ.update(values)
            yield
        finally:
            for name in self.TEST_SESSION_ENVIRONMENT_VARIABLES:
                os.environ.pop(name, None)
            for name, value in previous.items():
                if value is not None:
                    os.environ[name] = value

    @contextlib.contextmanager
    def nested_public_verifier_invocation(self):
        inherited = os.environ.pop("NFC_TEST_SESSION_ROOT", None)
        try:
            yield
        finally:
            if inherited is not None:
                os.environ["NFC_TEST_SESSION_ROOT"] = inherited

    @staticmethod
    def timeout_after_file_exists(path: Path, timeout_seconds: float = 0.2):
        def wait_for_file(_requested_timeout: float | None = None) -> float:
            deadline = time.monotonic() + 5
            while time.monotonic() < deadline:
                if path.exists():
                    return timeout_seconds
                time.sleep(0.01)
            raise AssertionError(
                f"timed out waiting for child readiness marker: {path}"
            )

        return wait_for_file

    @staticmethod
    def write_ci_trx(
        path: Path,
        *,
        total: int,
        skipped: int,
        identities: tuple[str, ...] | None = None,
        outcomes: tuple[str, ...] | None = None,
    ) -> None:
        path.parent.mkdir(parents=True, exist_ok=True)
        identities = identities or tuple(
            f"Probe.Tests.Case{index}" for index in range(total)
        )
        outcomes = outcomes or (
            *("Passed" for _ in range(total - skipped)),
            *("NotExecuted" for _ in range(skipped)),
        )
        if len(identities) != total or len(outcomes) != total:
            raise AssertionError("TRX fixture identities/outcomes must match total")
        if outcomes.count("NotExecuted") != skipped:
            raise AssertionError("TRX fixture skipped count must match outcomes")
        passed = outcomes.count("Passed")
        failed = outcomes.count("Failed")
        root = MODULE.ET.Element(
            "TestRun",
            xmlns="http://microsoft.com/schemas/VisualStudio/TeamTest/2010",
        )
        results = MODULE.ET.SubElement(root, "Results")
        for identity, outcome in zip(identities, outcomes, strict=True):
            MODULE.ET.SubElement(
                results,
                "UnitTestResult",
                testName=identity,
                outcome=outcome,
            )
        summary = MODULE.ET.SubElement(root, "ResultSummary")
        MODULE.ET.SubElement(
            summary,
            "Counters",
            total=str(total),
            executed=str(passed + failed),
            passed=str(passed),
            failed=str(failed),
            notExecuted=str(skipped),
        )
        MODULE.ET.ElementTree(root).write(path, encoding="utf-8", xml_declaration=True)

    @staticmethod
    def write_ci_manifest(path: Path, document: dict[str, object]) -> None:
        path.parent.mkdir(parents=True, exist_ok=True)
        path.write_text(
            json.dumps(document, indent=2, sort_keys=True) + "\n",
            encoding="utf-8",
        )

    @staticmethod
    def write_vstest_discovery(path: Path, total: int | tuple[str, ...]) -> None:
        path.parent.mkdir(parents=True, exist_ok=True)
        identities = (
            tuple(f"Probe.Tests.Case{index}" for index in range(total))
            if isinstance(total, int)
            else total
        )
        path.write_text(
            "VSTest test discovery\n"
            + "\n".join(f"    {identity}" for identity in identities)
            + "\n",
            encoding="utf-8",
        )

    @staticmethod
    def write_ci_coverage_pair(
        report_root: Path,
        json_document: dict[str, object],
        *,
        source_roots: tuple[str, ...] = (),
        class_filenames: tuple[str | None, ...] = (),
    ) -> tuple[Path, Path]:
        report_root.mkdir(parents=True, exist_ok=True)
        json_report = report_root / "coverage.json"
        json_report.write_text(json.dumps(json_document), encoding="utf-8")
        coverage = MODULE.ET.Element("coverage")
        sources = MODULE.ET.SubElement(coverage, "sources")
        for source_root in source_roots:
            MODULE.ET.SubElement(sources, "source").text = source_root
        packages = MODULE.ET.SubElement(coverage, "packages")
        classes = MODULE.ET.SubElement(
            MODULE.ET.SubElement(packages, "package"),
            "classes",
        )
        for index, filename in enumerate(class_filenames):
            attributes = {"name": f"Probe{index}"}
            if filename is not None:
                attributes["filename"] = filename
            MODULE.ET.SubElement(classes, "class", attributes)
        cobertura_report = report_root / "coverage.cobertura.xml"
        MODULE.ET.ElementTree(coverage).write(
            cobertura_report,
            encoding="utf-8",
            xml_declaration=True,
        )
        return json_report, cobertura_report

    def stage_complete_ci_dotnet_evidence(
        self,
        download_root: Path,
        source_sha: str,
    ) -> None:
        sdk_version = "10.0.301"
        build_root = download_root / "dotnet-build-evidence"
        build_log = build_root / "build/build.log"
        build_log.parent.mkdir(parents=True)
        build_log.write_text("build passed\n", encoding="utf-8")
        self.write_ci_manifest(
            build_root / "build/manifest.json",
            {
                "schemaVersion": 2,
                "kind": "dotnet-build",
                "sourceSha": source_sha,
                "sdkVersion": sdk_version,
                "success": True,
                "files": {
                    "build/build.log": hashlib.sha256(
                        build_log.read_bytes()
                    ).hexdigest()
                },
            },
        )
        for shard, projects in MODULE.CI_DOTNET_SHARDS.items():
            artifact_root = download_root / f"dotnet-test-{shard}-evidence"
            shard_root = artifact_root / "shards" / shard
            shard_log = shard_root / "shard.log"
            shard_log.parent.mkdir(parents=True)
            shard_log.write_text(f"{shard} passed\n", encoding="utf-8")
            files = {
                shard_log.relative_to(artifact_root).as_posix(): hashlib.sha256(
                    shard_log.read_bytes()
                ).hexdigest()
            }
            rows: list[dict[str, object]] = []
            for project in projects:
                total = 3
                if project.name == "NvtFwCombiner.Infrastructure.Tests":
                    identities = (
                        "Probe.Tests.Case0",
                        *MODULE.UNIX_SPECIAL_FILE_INFRASTRUCTURE_SKIPS,
                    )
                    skipped = 2
                else:
                    identities = tuple(
                        f"Probe.Tests.Case{index}" for index in range(total)
                    )
                    skipped = 0
                result_root = shard_root / "results" / project.name
                discovery = result_root / "discovered-tests.txt"
                trx = result_root / "test-results.trx"
                coverage_root = result_root / "coverage"
                coverage_json = coverage_root / "coverage.json"
                cobertura = coverage_root / "coverage.cobertura.xml"
                self.write_vstest_discovery(discovery, identities)
                self.write_ci_trx(
                    trx,
                    total=total,
                    skipped=skipped,
                    identities=identities,
                )
                coverage_root.mkdir()
                coverage_json.write_text("{}\n", encoding="utf-8")
                cobertura.write_text("<coverage />\n", encoding="utf-8")
                evidence_paths = (discovery, trx, coverage_json, cobertura)
                for evidence in evidence_paths:
                    relative = evidence.relative_to(artifact_root).as_posix()
                    files[relative] = hashlib.sha256(evidence.read_bytes()).hexdigest()
                rows.append(
                    {
                        "relativePath": project.relative_path,
                        "total": total,
                        "passed": total - skipped,
                        "failed": 0,
                        "skipped": skipped,
                        "testAssemblySha256": "a" * 64,
                        "discovery": discovery.relative_to(artifact_root).as_posix(),
                        "trx": trx.relative_to(artifact_root).as_posix(),
                        "coverageJson": coverage_json.relative_to(
                            artifact_root
                        ).as_posix(),
                        "coverageCobertura": cobertura.relative_to(
                            artifact_root
                        ).as_posix(),
                    }
                )
            self.write_ci_manifest(
                shard_root / "manifest.json",
                {
                    "schemaVersion": 2,
                    "kind": "dotnet-test-shard",
                    "sourceSha": source_sha,
                    "sdkVersion": sdk_version,
                    "success": True,
                    "shard": shard,
                    "producerPlatform": "windows",
                    "projects": rows,
                    "files": files,
                },
            )

    def test_full_plan_assigns_each_verification_owner_once(self) -> None:
        lanes = MODULE.selected_lanes(MODULE.parse_args(["--all"]))

        self.assertEqual(
            ["structure", "python", "dotnet"], [lane.name for lane in lanes]
        )
        self.assertEqual(len(lanes), len({lane.name for lane in lanes}))
        self.assertTrue(all(lane.isolate_action for lane in lanes))

    def test_public_full_plan_sequences_lock_reader_before_restore_writer(
        self,
    ) -> None:
        calls: list[tuple[list[str], int, int]] = []

        def record_phase(lanes, *, jobs, lane_timeout_seconds):
            calls.append(([lane.name for lane in lanes], jobs, lane_timeout_seconds))

        with (
            patch.dict(
                os.environ,
                {MODULE.INTERNAL_LANE_ENVIRONMENT_VARIABLE: ""},
                clear=False,
            ),
            patch.object(MODULE, "run_selected_lanes", side_effect=record_phase),
            contextlib.redirect_stdout(io.StringIO()),
        ):
            result = MODULE.execute_verification(MODULE.parse_args(["--all"]))

        self.assertEqual(0, result)
        self.assertEqual(
            [
                (
                    ["structure"],
                    MODULE.DEFAULT_VERIFY_JOBS,
                    MODULE.DEFAULT_LANE_TIMEOUT_SECONDS,
                ),
                (
                    ["python"],
                    MODULE.DEFAULT_VERIFY_JOBS,
                    MODULE.DEFAULT_LANE_TIMEOUT_SECONDS,
                ),
                (
                    ["dotnet"],
                    MODULE.DEFAULT_VERIFY_JOBS,
                    MODULE.DEFAULT_LANE_TIMEOUT_SECONDS,
                ),
            ],
            calls,
        )

    def test_public_full_plan_stops_before_restore_when_structure_fails(self) -> None:
        calls: list[list[str]] = []

        def fail_structure(lanes, *, jobs, lane_timeout_seconds):
            del jobs, lane_timeout_seconds
            calls.append([lane.name for lane in lanes])
            raise RuntimeError("structure failed")

        with (
            patch.dict(
                os.environ,
                {MODULE.INTERNAL_LANE_ENVIRONMENT_VARIABLE: ""},
                clear=False,
            ),
            patch.object(MODULE, "run_selected_lanes", side_effect=fail_structure),
            contextlib.redirect_stdout(io.StringIO()),
            contextlib.redirect_stderr(io.StringIO()),
        ):
            result = MODULE.execute_verification(MODULE.parse_args(["--all"]))

        self.assertEqual(1, result)
        self.assertEqual([["structure"]], calls)

    def test_public_single_phase_plans_keep_one_execution_phase(self) -> None:
        calls: list[list[str]] = []

        def record_phase(lanes, *, jobs, lane_timeout_seconds):
            del jobs, lane_timeout_seconds
            calls.append([lane.name for lane in lanes])

        with (
            patch.dict(
                os.environ,
                {MODULE.INTERNAL_LANE_ENVIRONMENT_VARIABLE: ""},
                clear=False,
            ),
            patch.object(MODULE, "run_selected_lanes", side_effect=record_phase),
            contextlib.redirect_stdout(io.StringIO()),
        ):
            self.assertEqual(
                0,
                MODULE.execute_verification(MODULE.parse_args(["--structure-only"])),
            )
            self.assertEqual(
                0,
                MODULE.execute_verification(
                    MODULE.parse_args(["--skip-structure", "--skip-python"])
                ),
            )

        self.assertEqual([["structure"], ["dotnet"]], calls)

    def test_package_import_works_without_a_scripts_pythonpath_entry(self) -> None:
        environment = os.environ.copy()
        environment.pop("PYTHONPATH", None)

        result = subprocess.run(
            [sys.executable, "-c", "import scripts.verify"],
            cwd=ROOT,
            env=environment,
            capture_output=True,
            text=True,
            check=False,
        )

        self.assertEqual(0, result.returncode, result.stderr)

    def test_coverage_reset_rejects_symlinked_repository_ancestor(self) -> None:
        with tempfile.TemporaryDirectory() as temporary:
            temporary_root = Path(temporary)
            root = temporary_root / "repo"
            external = temporary_root / "external"
            root.mkdir()
            report_directory = external / "coverage/python"
            report_directory.mkdir(parents=True)
            sentinel = report_directory / "keep.txt"
            sentinel.write_text("keep", encoding="utf-8")
            try:
                (root / "artifacts").symlink_to(external, target_is_directory=True)
            except OSError as error:
                self.skipTest(f"directory symlinks are unavailable: {error}")

            with (
                patch.object(MODULE, "ROOT", root),
                patch.object(MODULE, "COVERAGE_ROOT", root / "artifacts/coverage"),
                self.assertRaisesRegex(RuntimeError, "symbolic link"),
            ):
                MODULE.reset_coverage_directory("python")

            self.assertTrue(sentinel.is_file())

    @unittest.skipUnless(
        sys.platform == "win32" and hasattr(Path, "is_junction"),
        "Windows junction contract",
    )
    def test_coverage_reset_rejects_repository_internal_junction(self) -> None:
        with tempfile.TemporaryDirectory() as temporary:
            root = Path(temporary) / "repo"
            target = root / "docs"
            report_directory = target / "coverage/python"
            report_directory.mkdir(parents=True)
            sentinel = report_directory / "keep.txt"
            sentinel.write_text("keep", encoding="utf-8")
            junction = root / "artifacts"
            created = subprocess.run(
                [
                    "cmd.exe",
                    "/d",
                    "/c",
                    "mklink",
                    "/J",
                    str(junction),
                    str(target),
                ],
                capture_output=True,
                text=True,
                check=False,
            )
            self.assertEqual(0, created.returncode, created.stderr or created.stdout)
            self.assertTrue(junction.is_junction())

            with (
                patch.object(MODULE, "ROOT", root),
                patch.object(MODULE, "COVERAGE_ROOT", junction / "coverage"),
                self.assertRaisesRegex(RuntimeError, "junction"),
            ):
                MODULE.reset_coverage_directory("python")

            self.assertTrue(sentinel.is_file())

    def test_jobs_one_runs_every_lane_once_in_declared_order(self) -> None:
        calls: list[str] = []

        def action(name: str):
            def run(log_path: Path) -> None:
                calls.append(name)
                log_path.write_text(f"{name}\n", encoding="utf-8")

            return run

        lanes = (
            MODULE.VerificationLane("structure", action("structure")),
            MODULE.VerificationLane("python", action("python")),
            MODULE.VerificationLane("dotnet", action("dotnet")),
        )
        with tempfile.TemporaryDirectory() as temporary:
            results = MODULE.run_lanes(lanes, jobs=1, log_directory=Path(temporary))

        self.assertEqual(["structure", "python", "dotnet"], calls)
        self.assertEqual(
            [("structure", True), ("python", True), ("dotnet", True)],
            [(result.name, result.succeeded) for result in results],
        )

    def test_parallel_lanes_collect_all_results_and_keep_logs_isolated(self) -> None:
        calls: list[str] = []

        def failing(log_path: Path) -> None:
            calls.append("failing")
            log_path.write_text("failing\n", encoding="utf-8")
            raise RuntimeError("simulated lane failure")

        def passing(log_path: Path) -> None:
            calls.append("passing")
            log_path.write_text("passing\n", encoding="utf-8")

        lanes = (
            MODULE.VerificationLane("failing", failing),
            MODULE.VerificationLane("passing", passing),
        )
        with tempfile.TemporaryDirectory() as temporary:
            root = Path(temporary)
            results = MODULE.run_lanes(lanes, jobs=2, log_directory=root)

            self.assertEqual({"failing", "passing"}, set(calls))
            self.assertEqual(
                ["failing", "passing"], [result.name for result in results]
            )
            self.assertFalse(results[0].succeeded)
            self.assertTrue(results[1].succeeded)
            self.assertEqual(
                "failing\n", (root / "failing.log").read_text(encoding="utf-8")
            )
            self.assertEqual(
                "passing\n", (root / "passing.log").read_text(encoding="utf-8")
            )

    def test_ctrl_c_waits_for_worker_process_handoff_before_cleanup(self) -> None:
        fake_process = MagicMock()
        activation_finished = threading.Event()
        process_creation_started = threading.Event()
        cleanup_observations: list[bool] = []

        def create_during_interrupt(*_args: object, **_kwargs: object) -> MagicMock:
            self.assertIsNot(threading.current_thread(), threading.main_thread())
            process_creation_started.set()
            self.assertTrue(MODULE.PROCESS_CANCELLATION_REQUESTED.wait(5))
            return fake_process

        def activate(_process: object) -> None:
            activation_finished.set()

        def cleanup() -> None:
            cleanup_observations.append(activation_finished.is_set())

        def start_process(_log_path: Path | None) -> None:
            MODULE.start_owned_process(
                [sys.executable, "-c", "pass"],
                cwd=ROOT,
                environment=None,
            )

        def await_cancellation(_log_path: Path | None) -> None:
            self.assertTrue(MODULE.PROCESS_CANCELLATION_REQUESTED.wait(5))
            with self.assertRaisesRegex(RuntimeError, "creation was cancelled"):
                MODULE.start_owned_process(
                    [sys.executable, "-c", "pass"],
                    cwd=ROOT,
                    environment=None,
                )

        def interrupt_after_submissions(_futures: object) -> None:
            self.assertTrue(process_creation_started.wait(5))
            raise KeyboardInterrupt

        lanes = (
            MODULE.VerificationLane("worker", start_process),
            MODULE.VerificationLane("peer", await_cancellation),
        )
        with (
            tempfile.TemporaryDirectory() as temporary,
            patch.object(
                MODULE.subprocess,
                "Popen",
                side_effect=create_during_interrupt,
            ) as popen,
            patch.object(
                MODULE,
                "activate_owned_process",
                side_effect=activate,
            ),
            patch.object(
                MODULE,
                "terminate_active_processes",
                side_effect=cleanup,
            ),
            patch.object(
                MODULE, "as_completed", side_effect=interrupt_after_submissions
            ),
            self.assertRaises(KeyboardInterrupt),
        ):
            MODULE.run_lanes(lanes, jobs=2, log_directory=Path(temporary))

        self.assertEqual([True], cleanup_observations)
        self.assertEqual(1, popen.call_count)
        self.assertFalse(MODULE.PROCESS_CANCELLATION_REQUESTED.is_set())

    def test_lane_deadline_terminates_a_hung_command(self) -> None:
        def sleeping(log_path: Path) -> None:
            MODULE.run(
                [sys.executable, "-c", "import time; time.sleep(2)"],
                log_path=log_path,
            )

        lanes = (MODULE.VerificationLane("sleeping", sleeping),)
        with tempfile.TemporaryDirectory() as temporary:
            results = MODULE.run_lanes(
                lanes,
                jobs=1,
                log_directory=Path(temporary),
                lane_timeout_seconds=0.2,
            )

        self.assertFalse(results[0].succeeded)
        self.assertIn("TimeoutExpired", results[0].error)

    def test_lane_deadline_rejects_an_action_that_returns_after_its_budget(
        self,
    ) -> None:
        def late_return(log_path: Path | None) -> None:
            del log_path
            time.sleep(0.05)

        lanes = (MODULE.VerificationLane("late", late_return),)
        with tempfile.TemporaryDirectory() as temporary:
            results = MODULE.run_lanes(
                lanes,
                jobs=1,
                log_directory=Path(temporary),
                lane_timeout_seconds=0.01,
            )

        self.assertFalse(results[0].succeeded)
        self.assertIn("TimeoutExpired", results[0].error)

    def test_isolated_lane_runs_inside_one_parent_owned_subprocess(self) -> None:
        def unexpected_action(log_path: Path | None) -> None:
            del log_path
            self.fail("isolated actions must run in the parent-owned subprocess")

        lane = MODULE.VerificationLane(
            "structure", unexpected_action, isolate_action=True
        )
        with tempfile.TemporaryDirectory() as temporary:
            log_path = Path(temporary) / "structure.log"
            with patch.object(MODULE, "run") as run_command:
                results = MODULE.run_lanes(
                    (lane,), jobs=1, log_directory=Path(temporary)
                )

        self.assertTrue(results[0].succeeded)
        run_command.assert_called_once()
        command = run_command.call_args.args[0]
        environment = run_command.call_args.kwargs["environment"]
        self.assertEqual("--internal-lane", command[-2])
        self.assertEqual("structure", command[-1])
        self.assertEqual("1", environment[MODULE.INTERNAL_LANE_ENVIRONMENT_VARIABLE])
        self.assertEqual(log_path, run_command.call_args.kwargs["log_path"])

    def test_isolated_lane_uses_its_focused_internal_owner(self) -> None:
        lane = MODULE.VerificationLane(
            "dotnet",
            lambda _log_path: None,
            isolate_action=True,
            internal_name="dotnet-windows",
        )
        with tempfile.TemporaryDirectory() as temporary:
            with patch.object(MODULE, "run") as run_command:
                MODULE.run_lanes(
                    (lane,),
                    jobs=1,
                    log_directory=Path(temporary),
                )

        command = run_command.call_args.args[0]
        self.assertEqual(["--internal-lane", "dotnet-windows"], command[-2:])

    def test_internal_lanes_dispatch_each_canonical_owner_exactly_once(self) -> None:
        calls: list[str] = []
        with (
            patch.object(
                MODULE,
                "verify_structure",
                side_effect=lambda _log_path: calls.append("structure"),
            ),
            patch.object(
                MODULE,
                "verify_repository_scripts",
                side_effect=lambda _log_path: calls.append("repository-scripts"),
            ),
            patch.object(
                MODULE,
                "verify_python",
                side_effect=lambda _log_path: calls.append("python"),
            ),
            patch.object(
                MODULE,
                "verify_dotnet",
                side_effect=lambda _log_path: calls.append("dotnet"),
            ),
            patch.object(
                MODULE,
                "verify_windows_process_orchestration",
                side_effect=lambda _log_path: calls.append("windows-orchestration"),
                create=True,
            ),
        ):
            expected_calls = {
                "structure": ["structure"],
                "python": ["repository-scripts", "python"],
                "dotnet": ["dotnet"],
                "dotnet-windows": ["windows-orchestration", "dotnet"],
            }
            for lane_name, expected in expected_calls.items():
                calls.clear()
                MODULE.run_internal_lane(lane_name)
                self.assertEqual(expected, calls, lane_name)

    def test_keyboard_interrupt_terminates_the_active_command_before_reraising(
        self,
    ) -> None:
        fake_process = MagicMock()
        fake_process.wait.side_effect = KeyboardInterrupt
        fake_process.poll.return_value = None
        with (
            patch.object(MODULE.subprocess, "Popen", return_value=fake_process),
            patch.object(
                MODULE.WindowsKillOnCloseJob,
                "attach",
                return_value=MagicMock(),
            ),
            patch.object(MODULE, "resume_active_process"),
            patch.object(MODULE, "terminate_process_tree") as terminate,
        ):
            with self.assertRaises(KeyboardInterrupt):
                MODULE.run([sys.executable, "-c", "pass"])

        terminate.assert_called_once_with(fake_process)
        self.assertNotIn(fake_process, MODULE.ACTIVE_PROCESSES)

    def test_interrupt_before_registration_terminates_the_unowned_process(
        self,
    ) -> None:
        fake_process = MagicMock()
        with (
            patch.object(MODULE.subprocess, "Popen", return_value=fake_process),
            patch.object(
                MODULE,
                "activate_owned_process",
                side_effect=KeyboardInterrupt,
            ),
            patch.object(MODULE, "terminate_process_tree") as terminate,
            patch.object(MODULE, "unregister_active_process") as unregister,
            self.assertRaises(KeyboardInterrupt),
        ):
            MODULE.start_owned_process(
                [sys.executable, "-c", "pass"],
                cwd=ROOT,
                environment=None,
            )

        terminate.assert_called_once_with(fake_process)
        unregister.assert_called_once_with(fake_process)

    def test_ctrl_c_during_creation_is_replayed_after_process_ownership(
        self,
    ) -> None:
        fake_process = MagicMock()

        def create_then_interrupt(*_args: object, **_kwargs: object) -> MagicMock:
            signal.raise_signal(signal.SIGINT)
            return fake_process

        with (
            patch.object(
                MODULE.subprocess,
                "Popen",
                side_effect=create_then_interrupt,
            ),
            patch.object(MODULE, "activate_owned_process") as activate,
            patch.object(MODULE, "terminate_process_tree") as terminate,
            patch.object(MODULE, "unregister_active_process") as unregister,
            self.assertRaises(KeyboardInterrupt),
        ):
            MODULE.start_owned_process(
                [sys.executable, "-c", "pass"],
                cwd=ROOT,
                environment=None,
            )

        activate.assert_called_once_with(fake_process)
        terminate.assert_called_once_with(fake_process)
        unregister.assert_called_once_with(fake_process)

    def test_sigterm_during_creation_is_replayed_after_process_ownership(
        self,
    ) -> None:
        fake_process = MagicMock()
        cancellation = threading.Event()

        def create_then_terminate(*_args: object, **_kwargs: object) -> MagicMock:
            signal.raise_signal(signal.SIGTERM)
            return fake_process

        with (
            MODULE.handle_external_termination(),
            patch.object(MODULE, "PROCESS_CANCELLATION_REQUESTED", cancellation),
            patch.object(
                MODULE.subprocess,
                "Popen",
                side_effect=create_then_terminate,
            ),
            patch.object(MODULE, "activate_owned_process") as activate,
            patch.object(MODULE, "terminate_process_tree") as terminate,
            patch.object(MODULE, "unregister_active_process") as unregister,
            self.assertRaises(MODULE.VerificationTerminationRequested),
        ):
            MODULE.start_owned_process(
                [sys.executable, "-c", "pass"],
                cwd=ROOT,
                environment=None,
            )

        activate.assert_called_once_with(fake_process)
        terminate.assert_called_once_with(fake_process)
        unregister.assert_called_once_with(fake_process)
        self.assertTrue(cancellation.is_set())

    def test_sigterm_latches_cancellation_before_dotnet_cleanup_can_spawn(
        self,
    ) -> None:
        cancellation = threading.Event()
        commands: list[list[str]] = []

        def interrupt_first_command(command: list[str], **_kwargs: object) -> None:
            commands.append(command)
            if len(commands) == 1:
                signal.raise_signal(signal.SIGTERM)

        with (
            MODULE.handle_external_termination(),
            patch.object(MODULE, "PROCESS_CANCELLATION_REQUESTED", cancellation),
            patch.object(MODULE, "resolve_dotnet", return_value="dotnet"),
            patch.object(
                MODULE,
                "reset_coverage_directory",
                return_value=ROOT / "artifacts" / "test-dotnet-coverage",
            ),
            patch.object(MODULE, "run", side_effect=interrupt_first_command),
            patch.object(MODULE, "stop_idle_build_workers") as stop_idle_workers,
            self.assertRaises(MODULE.VerificationTerminationRequested),
        ):
            MODULE.verify_dotnet()

        self.assertTrue(cancellation.is_set())
        self.assertEqual([["dotnet", "--version"]], commands)
        stop_idle_workers.assert_not_called()

    def test_windows_process_is_suspended_until_job_boundary_is_attached(
        self,
    ) -> None:
        fake_process = MagicMock()
        fake_job = MagicMock()
        events: list[str] = []

        def attach(_process: object):
            events.append("attach")
            return fake_job

        def resume(_process: object) -> None:
            events.append("resume")

        with (
            patch.object(MODULE.sys, "platform", "win32"),
            patch.dict(
                os.environ,
                {MODULE.INTERNAL_LANE_ENVIRONMENT_VARIABLE: ""},
            ),
            patch.object(
                MODULE.WindowsKillOnCloseJob,
                "attach",
                side_effect=attach,
            ),
            patch.object(MODULE, "resume_active_process", side_effect=resume),
        ):
            creation_flags = MODULE.process_group_options()["creationflags"]
            MODULE.activate_owned_process(fake_process)
            MODULE.unregister_active_process(fake_process)

        self.assertNotEqual(0, creation_flags & MODULE.WINDOWS_CREATE_SUSPENDED)
        self.assertEqual(["attach", "resume"], events)
        fake_job.close.assert_called_once_with()

    def test_interrupting_lane_orchestration_terminates_all_active_commands(
        self,
    ) -> None:
        def interrupt(log_path: Path | None) -> None:
            del log_path
            raise KeyboardInterrupt

        with (
            tempfile.TemporaryDirectory() as temporary,
            patch.object(MODULE, "terminate_active_processes") as terminate,
        ):
            with self.assertRaises(KeyboardInterrupt):
                MODULE.run_lanes(
                    (MODULE.VerificationLane("interrupt", interrupt),),
                    jobs=1,
                    log_directory=Path(temporary),
                )

        terminate.assert_called_once_with()

    def test_parallel_interrupt_terminates_commands_before_waiting_for_workers(
        self,
    ) -> None:
        events: list[str] = []
        futures = (MagicMock(), MagicMock())
        futures[0].result.side_effect = KeyboardInterrupt

        class RecordingExecutor:
            def __init__(self) -> None:
                self.submitted = 0

            def __enter__(self):
                return self

            def __exit__(self, *_args: object) -> None:
                self.shutdown(wait=True)

            def submit(self, *_args: object):
                future = futures[self.submitted]
                self.submitted += 1
                return future

            def shutdown(self, *, wait: bool, cancel_futures: bool = False) -> None:
                events.append(f"shutdown:{wait}:{cancel_futures}")

        executor = RecordingExecutor()
        lanes = (
            MODULE.VerificationLane("first", lambda _log_path: None),
            MODULE.VerificationLane("second", lambda _log_path: None),
        )
        with (
            tempfile.TemporaryDirectory() as temporary,
            patch.object(MODULE, "ThreadPoolExecutor", return_value=executor),
            patch.object(MODULE, "as_completed", return_value=[futures[0]]),
            patch.object(
                MODULE,
                "terminate_active_processes",
                side_effect=lambda: events.append("terminate"),
            ),
            self.assertRaises(KeyboardInterrupt),
        ):
            MODULE.run_lanes(
                lanes,
                jobs=2,
                log_directory=Path(temporary),
            )

        self.assertLess(events.index("terminate"), events.index("shutdown:True:True"))
        for future in futures:
            future.cancel.assert_called_once_with()

    def test_active_process_cleanup_attempts_every_tree_before_reporting_failure(
        self,
    ) -> None:
        first_process = MagicMock()
        second_process = MagicMock()
        attempted: list[object] = []

        def terminate(process: object) -> None:
            attempted.append(process)
            if process is first_process:
                raise RuntimeError("first tree cleanup failed")

        with (
            patch.object(MODULE, "ACTIVE_PROCESSES", [first_process, second_process]),
            patch.object(MODULE, "terminate_process_tree", side_effect=terminate),
            self.assertRaisesRegex(RuntimeError, "1 active process tree"),
        ):
            MODULE.terminate_active_processes()

        self.assertEqual([first_process, second_process], attempted)

    def test_windows_tree_termination_rejects_nonzero_taskkill_result(self) -> None:
        fake_process = MagicMock()
        fake_process.poll.return_value = None
        with (
            patch.object(MODULE.sys, "platform", "win32"),
            patch.object(
                MODULE.subprocess,
                "run",
                return_value=subprocess.CompletedProcess([], 1),
            ),
            self.assertRaisesRegex(
                RuntimeError, "Windows verification process-tree termination"
            ),
        ):
            MODULE.terminate_process_tree(fake_process)

        fake_process.kill.assert_called_once_with()
        fake_process.wait.assert_called_once_with(
            timeout=MODULE.PROCESS_TERMINATION_TIMEOUT_SECONDS
        )

    def test_windows_tree_termination_rejects_taskkill_launch_failure(self) -> None:
        fake_process = MagicMock()
        fake_process.poll.return_value = None
        with (
            patch.object(MODULE.sys, "platform", "win32"),
            patch.object(
                MODULE.subprocess,
                "run",
                side_effect=OSError("taskkill unavailable"),
            ),
            self.assertRaisesRegex(
                RuntimeError, "Windows verification process-tree termination"
            ),
        ):
            MODULE.terminate_process_tree(fake_process)

        fake_process.kill.assert_called_once_with()
        fake_process.wait.assert_called_once_with(
            timeout=MODULE.PROCESS_TERMINATION_TIMEOUT_SECONDS
        )

    def test_unix_graceful_termination_failure_still_forces_the_owned_group(
        self,
    ) -> None:
        fake_process = MagicMock()
        fake_process.poll.return_value = None
        boundary = MODULE.ProcessTerminationBoundary(
            unix_process_group_id=4321,
            unix_graceful_termination_seconds=2,
        )
        with (
            patch.object(MODULE.sys, "platform", "linux"),
            patch.object(
                MODULE.os,
                "killpg",
                side_effect=[OSError("SIGTERM denied"), None],
                create=True,
            ) as kill_group,
            patch.object(MODULE.signal, "SIGKILL", 9, create=True),
        ):
            MODULE.terminate_process_tree(fake_process, boundary)

        self.assertEqual(
            [
                call(4321, signal.SIGTERM),
                call(4321, 9),
            ],
            kill_group.call_args_list,
        )
        fake_process.wait.assert_called_once_with(
            timeout=MODULE.PROCESS_TERMINATION_TIMEOUT_SECONDS
        )

    def test_unix_group_kill_retries_after_root_fallback(self) -> None:
        fake_process = MagicMock()
        fake_process.poll.return_value = None
        boundary = MODULE.ProcessTerminationBoundary(unix_process_group_id=4321)
        with (
            patch.object(MODULE.sys, "platform", "linux"),
            patch.object(
                MODULE.os,
                "killpg",
                side_effect=[OSError("first SIGKILL denied"), None],
                create=True,
            ) as kill_group,
            patch.object(MODULE.signal, "SIGKILL", 9, create=True),
        ):
            MODULE.terminate_process_tree(fake_process, boundary)

        self.assertEqual([call(4321, 9), call(4321, 9)], kill_group.call_args_list)
        fake_process.kill.assert_called_once_with()
        fake_process.wait.assert_called_once_with(
            timeout=MODULE.PROCESS_TERMINATION_TIMEOUT_SECONDS
        )

    def test_unix_group_kill_reports_first_and_retry_failures(self) -> None:
        fake_process = MagicMock()
        fake_process.poll.return_value = None
        boundary = MODULE.ProcessTerminationBoundary(unix_process_group_id=4321)
        with (
            patch.object(MODULE.sys, "platform", "linux"),
            patch.object(
                MODULE.os,
                "killpg",
                side_effect=[
                    OSError("first SIGKILL denied"),
                    OSError("retry SIGKILL denied"),
                ],
                create=True,
            ) as kill_group,
            patch.object(MODULE.signal, "SIGKILL", 9, create=True),
            self.assertRaisesRegex(
                RuntimeError,
                "first SIGKILL denied; retry failed with OSError: retry SIGKILL denied",
            ),
        ):
            MODULE.terminate_process_tree(fake_process, boundary)

        self.assertEqual([call(4321, 9), call(4321, 9)], kill_group.call_args_list)
        self.assertEqual(2, fake_process.kill.call_count)
        fake_process.wait.assert_called_once_with(
            timeout=MODULE.PROCESS_TERMINATION_TIMEOUT_SECONDS
        )

    def test_unix_retry_kill_failure_is_reported_after_root_fallback(self) -> None:
        fake_process = MagicMock()
        fake_process.poll.return_value = None
        fake_process.wait.side_effect = [
            subprocess.TimeoutExpired("verification", 5),
            0,
        ]
        boundary = MODULE.ProcessTerminationBoundary(unix_process_group_id=4321)
        with (
            patch.object(MODULE.sys, "platform", "linux"),
            patch.object(
                MODULE.os,
                "killpg",
                side_effect=[ProcessLookupError, OSError("SIGKILL denied")],
                create=True,
            ),
            patch.object(MODULE.signal, "SIGKILL", 9, create=True),
            self.assertRaisesRegex(
                RuntimeError, "Unix verification process-group termination"
            ),
        ):
            MODULE.terminate_process_tree(fake_process, boundary)

        self.assertEqual(2, fake_process.kill.call_count)
        self.assertEqual(
            [
                call(timeout=MODULE.PROCESS_TERMINATION_TIMEOUT_SECONDS),
                call(timeout=MODULE.PROCESS_TERMINATION_TIMEOUT_SECONDS),
            ],
            fake_process.wait.call_args_list,
        )

    @unittest.skipUnless(
        sys.platform == "win32", "Windows Job Object behavior is Windows-specific"
    )
    def test_windows_owned_job_kills_descendants_after_root_exit(self) -> None:
        with tempfile.TemporaryDirectory() as temporary:
            root = Path(temporary)
            child_ready = root / "job-child-ready"
            orphan_output = root / "job-orphan-output"
            child = (
                "from pathlib import Path; import time; "
                f"Path({str(child_ready)!r}).write_text('ready'); "
                "time.sleep(0.8); "
                f"Path({str(orphan_output)!r}).write_text('orphan')"
            )
            parent = (
                "from pathlib import Path; import subprocess, sys, time; "
                f"subprocess.Popen([sys.executable, '-c', {child!r}]); "
                f"marker = Path({str(child_ready)!r}); "
                "deadline = time.monotonic() + 5; "
                'exec("while not marker.exists():\\n'
                "    assert time.monotonic() < deadline\\n"
                '    time.sleep(0.01)")'
            )

            MODULE.run(
                [sys.executable, "-c", parent],
                timeout_seconds=5,
            )

            self.assertTrue(child_ready.exists())
            time.sleep(0.9)
            self.assertFalse(orphan_output.exists())

    def test_timeout_terminates_the_entire_command_process_tree(self) -> None:
        with tempfile.TemporaryDirectory() as temporary:
            root = Path(temporary)
            child_ready = root / "child-ready"
            orphan_output = root / "orphan-output"
            child = (
                "from pathlib import Path; import time; "
                f"Path({str(child_ready)!r}).write_text('ready'); "
                "time.sleep(2); "
                f"Path({str(orphan_output)!r}).write_text('orphan')"
            )
            parent = (
                "import subprocess, sys, time; "
                f"subprocess.Popen([sys.executable, '-c', {child!r}]); "
                "time.sleep(5)"
            )

            with (
                patch.object(
                    MODULE,
                    "remaining_timeout",
                    side_effect=self.timeout_after_file_exists(child_ready),
                ),
                self.assertRaises(subprocess.TimeoutExpired),
            ):
                MODULE.run(
                    [sys.executable, "-c", parent],
                    log_path=root / "process-tree.log",
                    timeout_seconds=0.5,
                )
            self.assertTrue(child_ready.exists())
            time.sleep(2.25)
            self.assertFalse(orphan_output.exists())

    @unittest.skipIf(
        sys.platform == "win32", "Unix process-group behavior requires Unix"
    )
    def test_unix_timeout_kills_child_forked_after_cleanup_snapshot(self) -> None:
        with tempfile.TemporaryDirectory() as temporary:
            root = Path(temporary)
            trigger = root / "spawn-trigger"
            child_ready = root / "late-child-ready"
            orphan_output = root / "late-orphan-output"
            child = "\n".join(
                (
                    "from pathlib import Path",
                    "import time",
                    f"Path({str(child_ready)!r}).write_text('ready')",
                    "time.sleep(0.8)",
                    f"Path({str(orphan_output)!r}).write_text('orphan')",
                )
            )
            parent = "\n".join(
                (
                    "from pathlib import Path",
                    "import subprocess, sys, time",
                    f"trigger = Path({str(trigger)!r})",
                    "deadline = time.monotonic() + 5",
                    "while not trigger.exists():",
                    "    assert time.monotonic() < deadline",
                    "    time.sleep(0.01)",
                    f"subprocess.Popen([sys.executable, '-c', {child!r}])",
                    "time.sleep(5)",
                )
            )
            supervisor = "\n".join(
                (
                    "import os, subprocess, time",
                    "from pathlib import Path",
                    "import scripts.verify as module",
                    f"trigger = Path({str(trigger)!r})",
                    f"child_ready = Path({str(child_ready)!r})",
                    "if hasattr(module, 'unix_descendant_process_ids'):",
                    "    original_discovery = module.unix_descendant_process_ids",
                    "    def coordinated_discovery(process_id):",
                    "        descendants = original_discovery(process_id)",
                    "        trigger.write_text('spawn')",
                    "        deadline = time.monotonic() + 5",
                    "        while not child_ready.exists():",
                    "            assert time.monotonic() < deadline",
                    "            time.sleep(0.01)",
                    "        return descendants",
                    "    module.unix_descendant_process_ids = coordinated_discovery",
                    "else:",
                    "    original_killpg = module.os.killpg",
                    "    def coordinated_killpg(process_group_id, signal_number):",
                    "        if not trigger.exists():",
                    "            trigger.write_text('spawn')",
                    "            deadline = time.monotonic() + 5",
                    "            while not child_ready.exists():",
                    "                assert time.monotonic() < deadline",
                    "                time.sleep(0.01)",
                    "        original_killpg(process_group_id, signal_number)",
                    "    module.os.killpg = coordinated_killpg",
                    "os.environ[module.INTERNAL_LANE_ENVIRONMENT_VARIABLE] = '1'",
                    "try:",
                    f"    module.run({[sys.executable, '-c', parent]!r}, timeout_seconds=0.2)",
                    "except subprocess.TimeoutExpired:",
                    "    pass",
                )
            )

            process = subprocess.Popen(
                [sys.executable, "-c", supervisor],
                cwd=ROOT,
                start_new_session=True,
            )
            process.wait(timeout=10)
            self.assertTrue(child_ready.exists())
            time.sleep(0.9)
            self.assertFalse(orphan_output.exists())

    @unittest.skipIf(sys.platform == "win32", "Unix termination behavior requires Unix")
    def test_sigterm_cleans_detached_owned_commands(self) -> None:
        with tempfile.TemporaryDirectory() as temporary:
            root = Path(temporary)
            child_ready = root / "sigterm-child-ready"
            orphan_output = root / "sigterm-orphan-output"
            child = "\n".join(
                (
                    "from pathlib import Path",
                    "import time",
                    f"Path({str(child_ready)!r}).write_text('ready')",
                    "time.sleep(0.8)",
                    f"Path({str(orphan_output)!r}).write_text('orphan')",
                )
            )
            supervisor = "\n".join(
                (
                    "import contextlib",
                    "import scripts.verify as module",
                    "handler = getattr(",
                    "    module, 'handle_external_termination', contextlib.nullcontext",
                    ")",
                    "with handler():",
                    f"    module.run({[sys.executable, '-c', child]!r})",
                )
            )
            process = subprocess.Popen(
                [sys.executable, "-c", supervisor],
                cwd=ROOT,
                start_new_session=True,
                stdout=subprocess.DEVNULL,
                stderr=subprocess.DEVNULL,
            )
            deadline = time.monotonic() + 5
            while not child_ready.exists():
                self.assertIsNone(process.poll())
                self.assertLess(time.monotonic(), deadline)
                time.sleep(0.01)

            os.killpg(process.pid, signal.SIGTERM)
            process.wait(timeout=10)
            time.sleep(0.9)
            self.assertFalse(orphan_output.exists())

    def test_internal_lane_timeout_terminates_descendants_in_a_new_group(
        self,
    ) -> None:
        with tempfile.TemporaryDirectory() as temporary:
            root = Path(temporary)
            child_ready = root / "internal-child-ready"
            orphan_output = root / "internal-orphan-output"
            child = (
                "from pathlib import Path; import time; "
                f"Path({str(child_ready)!r}).write_text('ready'); "
                "time.sleep(0.8); "
                f"Path({str(orphan_output)!r}).write_text('orphan')"
            )
            parent = (
                "import subprocess, sys, time; "
                f"subprocess.Popen([sys.executable, '-c', {child!r}]); "
                "time.sleep(2)"
            )

            with (
                patch.dict(
                    os.environ, {MODULE.INTERNAL_LANE_ENVIRONMENT_VARIABLE: "1"}
                ),
                patch.object(
                    MODULE,
                    "remaining_timeout",
                    side_effect=self.timeout_after_file_exists(child_ready),
                ),
                self.assertRaises(subprocess.TimeoutExpired),
            ):
                MODULE.run(
                    [sys.executable, "-c", parent],
                    log_path=root / "internal-process-tree.log",
                    timeout_seconds=0.2,
                )
            self.assertTrue(child_ready.exists())
            time.sleep(0.9)
            self.assertFalse(orphan_output.exists())

    def test_internal_lane_commands_use_a_dedicated_unix_process_group(self) -> None:
        with (
            patch.object(MODULE.sys, "platform", "linux"),
            patch.dict(
                os.environ,
                {MODULE.INTERNAL_LANE_ENVIRONMENT_VARIABLE: "1"},
            ),
        ):
            options = MODULE.process_group_options()

        self.assertEqual({"start_new_session": True}, options)

    def test_unix_group_termination_survives_an_exited_root_process(self) -> None:
        fake_process = MagicMock()
        fake_process.poll.return_value = 0
        boundary = MODULE.ProcessTerminationBoundary(unix_process_group_id=4321)
        with (
            patch.object(MODULE.sys, "platform", "linux"),
            patch.object(
                MODULE,
                "PROCESS_TERMINATION_BOUNDARIES",
                {fake_process: boundary},
            ),
            patch.object(MODULE.os, "killpg", create=True) as kill_group,
            patch.object(MODULE.signal, "SIGKILL", 9, create=True),
        ):
            MODULE.terminate_process_tree(fake_process)

        kill_group.assert_called_once_with(4321, 9)
        fake_process.kill.assert_not_called()
        fake_process.wait.assert_called_once_with(
            timeout=MODULE.PROCESS_TERMINATION_TIMEOUT_SECONDS
        )

    def test_unix_registration_uses_the_childs_actual_process_group(self) -> None:
        fake_process = MagicMock()
        fake_process.pid = 1234
        with (
            patch.object(MODULE.sys, "platform", "linux"),
            patch.dict(
                os.environ,
                {MODULE.INTERNAL_LANE_ENVIRONMENT_VARIABLE: "1"},
            ),
            patch.object(MODULE.os, "getpgid", return_value=4321, create=True),
        ):
            MODULE.register_active_process(fake_process)
            try:
                boundary = MODULE.PROCESS_TERMINATION_BOUNDARIES[fake_process]
            finally:
                MODULE.unregister_active_process(fake_process)

        self.assertEqual(4321, boundary.unix_process_group_id)

    def test_unix_registration_falls_back_after_the_child_root_exits(self) -> None:
        fake_process = MagicMock()
        fake_process.pid = 1234
        with (
            patch.object(MODULE.sys, "platform", "linux"),
            patch.dict(
                os.environ,
                {MODULE.INTERNAL_LANE_ENVIRONMENT_VARIABLE: "1"},
            ),
            patch.object(
                MODULE.os,
                "getpgid",
                side_effect=ProcessLookupError,
                create=True,
            ),
        ):
            MODULE.register_active_process(fake_process)
            try:
                boundary = MODULE.PROCESS_TERMINATION_BOUNDARIES[fake_process]
            finally:
                MODULE.unregister_active_process(fake_process)

        self.assertEqual(1234, boundary.unix_process_group_id)

    @unittest.skipIf(
        sys.platform == "win32", "Unix process-group behavior requires Unix"
    )
    def test_unix_owned_group_kills_descendants_after_root_exit(self) -> None:
        with tempfile.TemporaryDirectory() as temporary:
            root = Path(temporary)
            child_ready = root / "group-child-ready"
            orphan_output = root / "group-orphan-output"
            child = (
                "from pathlib import Path; import time; "
                f"Path({str(child_ready)!r}).write_text('ready'); "
                "time.sleep(0.8); "
                f"Path({str(orphan_output)!r}).write_text('orphan')"
            )
            parent = (
                "from pathlib import Path; import subprocess, sys, time; "
                f"subprocess.Popen([sys.executable, '-c', {child!r}]); "
                f"marker = Path({str(child_ready)!r}); "
                "deadline = time.monotonic() + 5; "
                'exec("while not marker.exists():\\n'
                "    assert time.monotonic() < deadline\\n"
                '    time.sleep(0.01)")'
            )
            process = subprocess.Popen(
                [sys.executable, "-c", parent],
                start_new_session=True,
            )
            MODULE.register_active_process(process)
            try:
                process.wait(timeout=5)
                self.assertTrue(child_ready.exists())
                MODULE.terminate_process_tree(process)
            finally:
                MODULE.unregister_active_process(process)

            time.sleep(0.9)
            self.assertFalse(orphan_output.exists())

    @unittest.skipIf(
        sys.platform == "win32", "Unix process-group behavior requires Unix"
    )
    def test_unix_group_kill_retry_cleans_descendants_after_root_fallback(
        self,
    ) -> None:
        with tempfile.TemporaryDirectory() as temporary:
            root = Path(temporary)
            child_ready = root / "retry-child-ready"
            orphan_output = root / "retry-orphan-output"
            child = (
                "from pathlib import Path; import time; "
                f"Path({str(child_ready)!r}).write_text('ready'); "
                "time.sleep(0.8); "
                f"Path({str(orphan_output)!r}).write_text('orphan')"
            )
            parent = (
                "from pathlib import Path; import subprocess, sys, time; "
                f"subprocess.Popen([sys.executable, '-c', {child!r}]); "
                f"marker = Path({str(child_ready)!r}); "
                "deadline = time.monotonic() + 5; "
                'exec("while not marker.exists():\\n'
                "    assert time.monotonic() < deadline\\n"
                '    time.sleep(0.01)"); '
                "time.sleep(5)"
            )
            process = subprocess.Popen(
                [sys.executable, "-c", parent],
                start_new_session=True,
            )
            MODULE.register_active_process(process)
            original_kill_group = os.killpg
            kill_attempts = 0

            def fail_first_group_kill(
                process_group_id: int, signal_number: int
            ) -> None:
                nonlocal kill_attempts
                kill_attempts += 1
                if kill_attempts == 1:
                    raise OSError("simulated transient group failure")
                original_kill_group(process_group_id, signal_number)

            try:
                deadline = time.monotonic() + 5
                while not child_ready.exists():
                    self.assertIsNone(process.poll())
                    self.assertLess(time.monotonic(), deadline)
                    time.sleep(0.01)
                with patch.object(MODULE.os, "killpg", fail_first_group_kill):
                    MODULE.terminate_process_tree(process)
            finally:
                MODULE.unregister_active_process(process)

            self.assertEqual(2, kill_attempts)
            time.sleep(0.9)
            self.assertFalse(orphan_output.exists())

    def test_cleanup_ceiling_is_shared_and_cannot_extend_the_current_lane_deadline(
        self,
    ) -> None:
        lane_token = MODULE.LANE_DEADLINE.set(200)
        cleanup_token = MODULE.CLEANUP_DEADLINE.set(130)
        try:
            with patch.object(MODULE, "monotonic", return_value=100):
                first_timeout = MODULE.remaining_timeout(MODULE.CLEANUP_TIMEOUT_SECONDS)
            with patch.object(MODULE, "monotonic", return_value=120):
                second_timeout = MODULE.remaining_timeout(
                    MODULE.CLEANUP_TIMEOUT_SECONDS
                )
        finally:
            MODULE.CLEANUP_DEADLINE.reset(cleanup_token)
            MODULE.LANE_DEADLINE.reset(lane_token)

        self.assertEqual(30, first_timeout)
        self.assertEqual(10, second_timeout)

    def test_report_uses_declared_lane_order_and_one_aggregate_summary(self) -> None:
        with tempfile.TemporaryDirectory() as temporary:
            root = Path(temporary)
            for name in ("structure", "python", "dotnet"):
                (root / f"{name}.log").write_text(f"{name}\n", encoding="utf-8")
            results = (
                MODULE.LaneResult("structure", True, 3.0, root / "structure.log"),
                MODULE.LaneResult(
                    "python",
                    False,
                    2.0,
                    root / "python.log",
                    "RuntimeError: simulated",
                ),
                MODULE.LaneResult("dotnet", True, 1.0, root / "dotnet.log"),
            )
            output = io.StringIO()
            with contextlib.redirect_stdout(output):
                MODULE.report_lane_results(results)

        report = output.getvalue()
        self.assertLess(
            report.index("=== structure lane"), report.index("=== python lane")
        )
        self.assertLess(
            report.index("=== python lane"), report.index("=== dotnet lane")
        )
        self.assertIn("[lane failed] RuntimeError: simulated", report)
        self.assertEqual(1, report.count("Verification lane summary:"))

    def test_report_replaces_invalid_log_bytes_without_losing_lane_summary(
        self,
    ) -> None:
        with tempfile.TemporaryDirectory() as temporary:
            root = Path(temporary)
            log_path = root / "python.log"
            log_path.write_bytes(b"before\xffafter\n")
            result = MODULE.LaneResult("python", True, 1.0, log_path)
            output = io.StringIO()
            with contextlib.redirect_stdout(output):
                MODULE.report_lane_results([result])

        self.assertIn("before\ufffdafter", output.getvalue())
        self.assertIn("Verification lane summary:", output.getvalue())

    def test_single_lane_capture_does_not_reread_the_complete_log(self) -> None:
        with tempfile.TemporaryDirectory() as temporary:
            log_path = Path(temporary) / "single.log"
            with patch.object(
                Path,
                "read_bytes",
                side_effect=AssertionError("complete log reread"),
            ):
                MODULE._run_to_logs(
                    [sys.executable, "-c", "print('bounded output')"],
                    log_paths=(log_path,),
                    cwd=ROOT,
                    environment=None,
                    echo=False,
                )

            self.assertIn("bounded output", log_path.read_text(encoding="utf-8"))

    def test_uncaptured_command_emits_its_duration(self) -> None:
        fake_process = MagicMock()
        fake_process.wait.return_value = 0
        output = io.StringIO()
        with (
            patch.object(MODULE, "monotonic", side_effect=[10.0, 12.34]),
            patch.object(MODULE, "start_owned_process", return_value=fake_process),
            patch.object(MODULE, "unregister_active_process"),
            contextlib.redirect_stdout(output),
        ):
            MODULE.run(["tool", "check"])

        self.assertIn("Command timing: 2.3s", output.getvalue())

    def test_captured_failed_command_emits_its_duration(self) -> None:
        fake_process = MagicMock()
        fake_process.wait.return_value = 7
        with tempfile.TemporaryDirectory() as temporary:
            log_path = Path(temporary) / "failed.log"
            with (
                patch.object(MODULE, "monotonic", side_effect=[10.0, 12.34]),
                patch.object(MODULE, "start_owned_process", return_value=fake_process),
                patch.object(MODULE, "unregister_active_process"),
                self.assertRaises(subprocess.CalledProcessError),
            ):
                MODULE._run_to_logs(
                    ["tool", "check"],
                    log_paths=(log_path,),
                    cwd=ROOT,
                    environment=None,
                    echo=False,
                )

            logged = log_path.read_text(encoding="utf-8")

        self.assertIn("Command timing: 2.3s", logged)

    def test_lane_report_streams_logs_without_reading_the_complete_file(
        self,
    ) -> None:
        with tempfile.TemporaryDirectory() as temporary:
            log_path = Path(temporary) / "python.log"
            log_path.write_bytes(b"before\xffafter\n")
            output = io.StringIO()
            with (
                patch.object(
                    Path,
                    "read_text",
                    side_effect=AssertionError("complete log read"),
                ),
                contextlib.redirect_stdout(output),
            ):
                MODULE.report_lane_results(
                    [MODULE.LaneResult("python", True, 1.0, log_path)]
                )

        self.assertIn("before\ufffdafter", output.getvalue())
        self.assertIn("Verification lane summary:", output.getvalue())

    def test_stream_log_tail_mirrors_only_appended_bytes_across_utf8_chunks(
        self,
    ) -> None:
        with tempfile.TemporaryDirectory() as temporary:
            root = Path(temporary)
            primary = root / "primary.log"
            mirror = root / "mirror.log"
            historical = b"historical|"
            appended = "before-\u20ac-after".encode()
            primary.write_bytes(historical + appended)
            mirror.write_bytes(b"mirror-prefix|")
            output = io.StringIO()
            with (
                patch.object(MODULE, "LOG_STREAM_CHUNK_BYTES", 2),
                contextlib.redirect_stdout(output),
            ):
                MODULE.stream_log_tail(
                    primary,
                    start_offset=len(historical),
                    mirror_paths=(mirror,),
                    echo=True,
                )

            mirrored = mirror.read_bytes()

        self.assertEqual(b"mirror-prefix|" + appended, mirrored)
        self.assertEqual(appended.decode(), output.getvalue())

    def test_stream_log_tail_preserves_replacement_for_split_invalid_utf8(
        self,
    ) -> None:
        with tempfile.TemporaryDirectory() as temporary:
            primary = Path(temporary) / "invalid.log"
            invalid = b"before-\xe2\x82-after"
            primary.write_bytes(invalid)
            output = io.StringIO()
            with (
                patch.object(MODULE, "LOG_STREAM_CHUNK_BYTES", 2),
                contextlib.redirect_stdout(output),
            ):
                MODULE.stream_log_tail(
                    primary,
                    start_offset=0,
                    echo=True,
                )

        self.assertEqual(invalid.decode("utf-8", errors="replace"), output.getvalue())

    def test_stream_log_tail_escapes_text_the_console_cannot_encode(self) -> None:
        with tempfile.TemporaryDirectory() as temporary:
            primary = Path(temporary) / "invalid.log"
            primary.write_bytes(b"before-\xff-after")
            raw_console = io.BytesIO()
            console = io.TextIOWrapper(
                raw_console,
                encoding="cp950",
                errors="strict",
                newline="",
            )
            try:
                with patch.object(MODULE.sys, "stdout", console):
                    MODULE.stream_log_tail(
                        primary,
                        start_offset=0,
                        echo=True,
                    )
                console.flush()
                rendered = raw_console.getvalue().decode("cp950")
            finally:
                console.detach()

        self.assertEqual(r"before-\ufffd-after", rendered)

    def test_skip_python_leaves_repository_script_tests_out_of_dotnet_only_lane(
        self,
    ) -> None:
        lanes = MODULE.selected_lanes(
            MODULE.parse_args(["--skip-python", "--skip-structure"])
        )

        self.assertEqual(["dotnet"], [lane.name for lane in lanes])

    def test_windows_dotnet_only_plan_selects_platform_orchestration_owner(
        self,
    ) -> None:
        with patch.object(MODULE.sys, "platform", "win32"):
            lanes = MODULE.selected_lanes(
                MODULE.parse_args(["--skip-python", "--skip-structure"])
            )

        self.assertEqual(["dotnet"], [lane.name for lane in lanes])
        self.assertEqual("dotnet-windows", lanes[0].internal_name)

    def test_windows_platform_owner_runs_only_the_job_object_integration(
        self,
    ) -> None:
        with tempfile.TemporaryDirectory() as temporary:
            log_path = Path(temporary) / "dotnet.log"
            with patch.object(MODULE, "run") as run_command:
                MODULE.verify_windows_process_orchestration(log_path)

        run_command.assert_called_once_with(
            [
                sys.executable,
                "-m",
                "unittest",
                MODULE.WINDOWS_PROCESS_ORCHESTRATION_TEST,
            ],
            log_path=log_path,
        )

    def test_public_invocation_rejects_an_empty_verification_plan(self) -> None:
        with (
            patch.object(
                sys,
                "argv",
                [
                    str(SCRIPT),
                    "--skip-structure",
                    "--skip-python",
                    "--skip-dotnet",
                ],
            ),
            patch.dict(
                os.environ,
                {MODULE.INTERNAL_LANE_ENVIRONMENT_VARIABLE: ""},
            ),
            patch.object(MODULE, "run_lanes") as run_lanes,
            contextlib.redirect_stdout(io.StringIO()),
            contextlib.redirect_stderr(io.StringIO()),
            self.nested_public_verifier_invocation(),
        ):
            result = MODULE.main()

        self.assertEqual(1, result)
        run_lanes.assert_not_called()

    def test_main_returns_sigterm_exit_code_after_owned_cleanup_path(self) -> None:
        cancellation = threading.Event()
        owned_session: Path | None = None

        def request_sigterm(_args: argparse.Namespace) -> int:
            nonlocal owned_session
            owned_session = Path(os.environ["NFC_TEST_SESSION_ROOT"])
            signal.raise_signal(signal.SIGTERM)
            return 0

        with tempfile.TemporaryDirectory() as temporary:
            root = Path(temporary)
            stale = root / "sessions" / "stale"
            stale.mkdir(parents=True)
            (stale / "keep.txt").write_text("keep", encoding="utf-8")
            coverage = root / "coverage"
            upload = root / "upload"
            coverage.mkdir()
            upload.mkdir()
            (coverage / "sentinel").write_text("coverage", encoding="utf-8")
            (upload / "sentinel").write_text("upload", encoding="utf-8")
            with self.verifier_environment(
                NFC_TEST_AREA_ROOT=str(root),
                TEMP="prior-temp",
                TMP="prior-tmp",
                TMPDIR="prior-tmpdir",
            ):
                original_tempdir = tempfile.tempdir
                original_dotnet_work = MODULE.DOTNET_COVERAGE_WORK_ROOT
                original_ci_work = MODULE.CI_DOTNET_EVIDENCE_ROOT
                with (
                    patch.object(
                        MODULE, "PROCESS_CANCELLATION_REQUESTED", cancellation
                    ),
                    patch.object(
                        MODULE,
                        "parse_args",
                        return_value=MagicMock(internal_lane=None),
                    ),
                    patch.object(
                        MODULE,
                        "execute_verification",
                        side_effect=request_sigterm,
                    ),
                    patch.object(MODULE, "COVERAGE_ROOT", coverage),
                    patch.object(MODULE, "CI_DOTNET_UPLOAD_ROOT", upload),
                ):
                    result = MODULE.main()
                self.assertEqual("prior-temp", os.environ["TEMP"])
                self.assertEqual("prior-tmp", os.environ["TMP"])
                self.assertEqual("prior-tmpdir", os.environ["TMPDIR"])
                self.assertEqual(original_tempdir, tempfile.tempdir)
                self.assertEqual(original_dotnet_work, MODULE.DOTNET_COVERAGE_WORK_ROOT)
                self.assertEqual(original_ci_work, MODULE.CI_DOTNET_EVIDENCE_ROOT)

            assert owned_session is not None
            self.assertFalse(owned_session.exists())
            self.assertEqual("keep", (stale / "keep.txt").read_text(encoding="utf-8"))
            self.assertEqual(
                "coverage", (coverage / "sentinel").read_text(encoding="utf-8")
            )
            self.assertEqual(
                "upload", (upload / "sentinel").read_text(encoding="utf-8")
            )

        self.assertEqual(128 + signal.SIGTERM, result)
        self.assertFalse(cancellation.is_set())

    def test_sigterm_main_does_not_poison_the_next_owned_command(self) -> None:
        def request_sigterm(_args: argparse.Namespace) -> int:
            signal.raise_signal(signal.SIGTERM)
            return 0

        MODULE.PROCESS_CANCELLATION_REQUESTED.clear()
        try:
            with tempfile.TemporaryDirectory() as temporary:
                root = Path(temporary)
                command_log = root / "after-sigterm.log"
                with (
                    self.verifier_environment(NFC_TEST_AREA_ROOT=str(root)),
                    patch.object(
                        MODULE,
                        "parse_args",
                        return_value=MagicMock(internal_lane=None),
                    ),
                    patch.object(
                        MODULE,
                        "execute_verification",
                        side_effect=request_sigterm,
                    ),
                    contextlib.redirect_stdout(io.StringIO()),
                ):
                    self.assertEqual(128 + signal.SIGTERM, MODULE.main())
                    self.assertFalse(MODULE.PROCESS_CANCELLATION_REQUESTED.is_set())
                    MODULE.run(
                        [sys.executable, "-c", "print('after-sigterm')"],
                        cwd=root,
                        log_path=command_log,
                    )

                self.assertIn("after-sigterm", command_log.read_text(encoding="utf-8"))
        finally:
            MODULE.PROCESS_CANCELLATION_REQUESTED.clear()

    def test_idle_worker_cleanup_warnings_are_written_to_the_active_lane_log(
        self,
    ) -> None:
        with tempfile.TemporaryDirectory() as temporary:
            log_path = Path(temporary) / "dotnet.log"
            with (
                patch.object(MODULE.sys, "platform", "win32"),
                patch.object(MODULE.shutil, "which", return_value="powershell"),
                patch.object(
                    MODULE,
                    "_run_to_logs",
                    side_effect=subprocess.TimeoutExpired("cleanup", 30),
                ),
            ):
                MODULE.stop_idle_build_workers(log_path, timeout_seconds=30)

            logged = log_path.read_text(encoding="utf-8")

        self.assertIn(
            "warning: idle Avalonia build worker cleanup exceeded its timeout.", logged
        )

    def test_idle_worker_cleanup_timeout_without_lane_log_remains_best_effort(
        self,
    ) -> None:
        with (
            patch.object(MODULE.sys, "platform", "win32"),
            patch.object(MODULE.shutil, "which", return_value="powershell"),
            patch.object(
                MODULE,
                "run",
                side_effect=subprocess.TimeoutExpired("cleanup", 30),
            ),
            patch.object(MODULE, "write_cleanup_warning") as write_warning,
        ):
            MODULE.stop_idle_build_workers(None, timeout_seconds=30)

        write_warning.assert_called_once_with(
            "warning: idle Avalonia build worker cleanup exceeded its timeout.",
            None,
        )

    def test_idle_worker_cleanup_without_lane_log_uses_timed_runner(self) -> None:
        with (
            patch.object(MODULE.sys, "platform", "win32"),
            patch.object(MODULE.shutil, "which", return_value="powershell"),
            patch.object(MODULE, "run") as timed_run,
        ):
            MODULE.stop_idle_build_workers(None, timeout_seconds=30)

        timed_run.assert_called_once()
        self.assertEqual(
            30,
            timed_run.call_args.kwargs["timeout_seconds"],
        )

    def test_idle_worker_cleanup_nonzero_without_lane_log_remains_a_warning(
        self,
    ) -> None:
        with (
            patch.object(MODULE.sys, "platform", "win32"),
            patch.object(MODULE.shutil, "which", return_value="powershell"),
            patch.object(
                MODULE,
                "run",
                side_effect=subprocess.CalledProcessError(7, ["powershell"]),
            ),
            patch.object(MODULE, "write_cleanup_warning") as write_warning,
        ):
            MODULE.stop_idle_build_workers(None, timeout_seconds=30)

        write_warning.assert_called_once_with(
            "warning: idle Avalonia build worker cleanup returned exit code 7.",
            None,
        )

    def test_dotnet_build_mirrors_the_ci_upload_log_once(self) -> None:
        calls: list[tuple[list[str], dict[str, object]]] = []

        def fake_run(command: list[str], **kwargs: object) -> None:
            calls.append((command, kwargs))
            mirror = kwargs.get("mirror_log_path")
            if isinstance(mirror, Path):
                mirror.write_text("build output\n", encoding="utf-8")

        with tempfile.TemporaryDirectory() as temporary:
            root = Path(temporary)
            ci_log = root / "ci-build.log"
            with (
                patch.dict(os.environ, {"NFC_DOTNET_BUILD_LOG": str(ci_log)}),
                patch.object(MODULE, "resolve_dotnet", return_value="dotnet"),
                patch.object(
                    MODULE,
                    "reset_coverage_directory",
                    return_value=root / "coverage",
                ),
                patch.object(MODULE, "run", side_effect=fake_run),
                patch.object(MODULE, "collect_local_dotnet_coverage"),
                patch.object(MODULE, "stop_idle_build_workers"),
            ):
                MODULE.verify_dotnet(root / "dotnet.log")

            build_calls = [
                kwargs
                for command, kwargs in calls
                if len(command) > 1 and command[1] == "build"
            ]
            ci_log_text = ci_log.read_text(encoding="utf-8")

        self.assertEqual(1, len(build_calls))
        self.assertEqual(ci_log, build_calls[0]["mirror_log_path"])
        self.assertEqual("build output\n", ci_log_text)

    def test_dotnet_lane_owns_restore_source_evaluation_and_coverage_in_order(
        self,
    ) -> None:
        commands: list[list[str]] = []
        with tempfile.TemporaryDirectory() as temporary:
            coverage_directory = Path(temporary) / "dotnet"
            with (
                patch.object(MODULE, "resolve_dotnet", return_value="dotnet"),
                patch.object(
                    MODULE,
                    "reset_coverage_directory",
                    return_value=coverage_directory,
                ),
                patch.object(
                    MODULE,
                    "run",
                    side_effect=lambda command, **_kwargs: commands.append(command),
                ),
                patch.object(
                    MODULE,
                    "collect_local_dotnet_coverage",
                    side_effect=lambda *_args, **_kwargs: commands.append(
                        ["collect-local-dotnet-coverage"]
                    ),
                ) as collect_coverage,
                patch.object(MODULE, "stop_idle_build_workers"),
            ):
                MODULE.verify_dotnet()

        restore_index = commands.index(["dotnet", "restore", str(MODULE.SOLUTION)])
        ownership_index = commands.index(
            [
                sys.executable,
                "scripts/validate_repository.py",
                "--evaluated-source-ownership-only",
            ]
        )
        build_index = commands.index(
            [
                "dotnet",
                "build",
                str(MODULE.SOLUTION),
                "-c",
                "Release",
                "--no-restore",
            ]
        )
        format_index = next(
            index
            for index, command in enumerate(commands)
            if len(command) > 1 and command[1] == "format"
        )
        format_command = commands[format_index]
        collect_index = commands.index(["collect-local-dotnet-coverage"])

        self.assertLess(restore_index, ownership_index)
        self.assertLess(ownership_index, format_index)
        self.assertLess(format_index, build_index)
        self.assertLess(build_index, collect_index)
        self.assertEqual(
            [
                "dotnet",
                "format",
                str(MODULE.SOLUTION),
                "whitespace",
                "--verify-no-changes",
                "--no-restore",
            ],
            format_command,
        )
        self.assertFalse(any("test" in command for command in commands))
        collect_coverage.assert_called_once()
        self.assertEqual(
            coverage_directory,
            collect_coverage.call_args.args[1],
        )

    def test_solution_restore_restores_only_removed_windows_rid_projection(
        self,
    ) -> None:
        with tempfile.TemporaryDirectory() as temporary:
            root = Path(temporary)
            original = {
                "version": 1,
                "dependencies": {
                    "net10.0": {"Package": {"type": "Direct", "resolved": "1.0.0"}},
                    "net10.0/win-x64": {
                        "Runtime.Package": {"type": "Direct", "resolved": "2.0.0"}
                    },
                },
            }
            original_bytes = json.dumps(original, indent=2).encode("utf-8")
            lock_path, solution = self.create_solution_lock_fixture(
                root, original_bytes
            )
            second_lock = root / "src" / "Product01" / "packages.lock.json"

            def restore(_command, **_kwargs):
                projected = dict(original)
                projected["dependencies"] = {
                    "net10.0": original["dependencies"]["net10.0"]
                }
                lock_path.write_text(json.dumps(projected), encoding="utf-8")
                second_lock.write_text(json.dumps(projected), encoding="utf-8")

            with patch.object(MODULE, "run", side_effect=restore):
                MODULE.run_solution_restore_preserving_lock_projections(
                    ["dotnet", "restore", "solution"],
                    environment={},
                    log_path=None,
                    repository_root=root,
                    solution=solution,
                )

            self.assertEqual(original_bytes, lock_path.read_bytes())
            self.assertEqual(original_bytes, second_lock.read_bytes())

    def test_solution_restore_retains_and_rejects_dependency_drift(self) -> None:
        with tempfile.TemporaryDirectory() as temporary:
            root = Path(temporary)
            original = {
                "version": 1,
                "dependencies": {
                    "net10.0": {"Package": {"type": "Direct", "resolved": "1.0.0"}},
                    "net10.0/win-x64": {},
                },
            }
            lock_path, solution = self.create_solution_lock_fixture(
                root, json.dumps(original).encode("utf-8")
            )

            def restore(_command, **_kwargs):
                drifted = {
                    "version": 1,
                    "dependencies": {
                        "net10.0": {"Package": {"type": "Direct", "resolved": "1.0.1"}}
                    },
                }
                lock_path.write_text(json.dumps(drifted), encoding="utf-8")

            with (
                patch.object(MODULE, "run", side_effect=restore),
                self.assertRaisesRegex(
                    RuntimeError, "package-lock inspection failed"
                ),
            ):
                MODULE.run_solution_restore_preserving_lock_projections(
                    ["dotnet", "restore", "solution"],
                    environment={},
                    log_path=None,
                    repository_root=root,
                    solution=solution,
                )

            self.assertIn('"1.0.1"', lock_path.read_text(encoding="utf-8"))

    def create_solution_lock_fixture(
        self, root: Path, lock_bytes: bytes
    ) -> tuple[Path, Path]:
        projects: list[Path] = []
        for index in range(25):
            name = "Product" if index == 0 else f"Product{index:02d}"
            project = root / "src" / name / f"{name}.csproj"
            project.parent.mkdir(parents=True)
            project.write_text("<Project />", encoding="utf-8")
            (project.parent / "packages.lock.json").write_bytes(lock_bytes)
            projects.append(project)
        lock = projects[0].parent / "packages.lock.json"
        solution = root / "NvtFwCombiner.slnx"
        solution.write_text(
            "<Solution>"
            + "".join(
                f'<Project Path="{project.relative_to(root).as_posix()}" />'
                for project in projects
            )
            + "</Solution>",
            encoding="utf-8",
        )
        return lock, solution

    def test_solution_restore_restores_projection_before_rethrowing_failure(
        self,
    ) -> None:
        with tempfile.TemporaryDirectory() as temporary:
            root = Path(temporary)
            original = {
                "version": 1,
                "dependencies": {"net10.0": {}, "net10.0/win-x64": {}},
            }
            original_bytes = json.dumps(original).encode("utf-8")
            lock, solution = self.create_solution_lock_fixture(root, original_bytes)

            def restore(_command, **_kwargs):
                lock.write_text(
                    json.dumps({"version": 1, "dependencies": {"net10.0": {}}}),
                    encoding="utf-8",
                )
                raise RuntimeError("restore primary")

            with (
                patch.object(MODULE, "run", side_effect=restore),
                self.assertRaisesRegex(RuntimeError, "restore primary"),
            ):
                MODULE.run_solution_restore_preserving_lock_projections(
                    ["dotnet", "restore", "solution"],
                    environment={},
                    log_path=None,
                    repository_root=root,
                    solution=solution,
                )

            self.assertEqual(original_bytes, lock.read_bytes())

    def test_solution_restore_rejects_duplicate_json_and_retains_it(self) -> None:
        with tempfile.TemporaryDirectory() as temporary:
            root = Path(temporary)
            original = b'{"version":1,"dependencies":{"net10.0":{},"net10.0/win-x64":{}}}'
            lock, solution = self.create_solution_lock_fixture(root, original)
            duplicate = (
                b'{"version":1,"dependencies":{"net10.0":{"Package":'
                b'{"type":"Direct","type":"Direct"}}}}'
            )

            with (
                patch.object(
                    MODULE,
                    "run",
                    side_effect=lambda *_args, **_kwargs: lock.write_bytes(duplicate),
                ),
                self.assertRaisesRegex(RuntimeError, "duplicate package-lock key"),
            ):
                MODULE.run_solution_restore_preserving_lock_projections(
                    ["dotnet", "restore", "solution"],
                    environment={},
                    log_path=None,
                    repository_root=root,
                    solution=solution,
                )

            self.assertEqual(duplicate, lock.read_bytes())

    def test_solution_restore_rejects_malformed_snapshot_before_running(self) -> None:
        with tempfile.TemporaryDirectory() as temporary:
            root = Path(temporary)
            _, solution = self.create_solution_lock_fixture(
                root, b'{"version":1,"dependencies":{"net10.0":{},}}'
            )
            execute = MagicMock()
            with (
                patch.object(MODULE, "run", execute),
                self.assertRaisesRegex(RuntimeError, "invalid committed"),
            ):
                MODULE.run_solution_restore_preserving_lock_projections(
                    ["dotnet", "restore", "solution"],
                    environment={},
                    log_path=None,
                    repository_root=root,
                    solution=solution,
                )
            execute.assert_not_called()

    def test_solution_restore_ignores_unowned_lock_and_rejects_missing_owned_lock(
        self,
    ) -> None:
        with tempfile.TemporaryDirectory() as temporary:
            root = Path(temporary)
            original = b'{"version":1,"dependencies":{"net10.0":{}}}'
            lock, solution = self.create_solution_lock_fixture(root, original)
            unowned = root / "src" / "Unowned" / "packages.lock.json"
            unowned.parent.mkdir(parents=True)
            unowned.write_bytes(b"unowned")
            with patch.object(MODULE, "run"):
                MODULE.run_solution_restore_preserving_lock_projections(
                    ["dotnet", "restore", "solution"],
                    environment={},
                    log_path=None,
                    repository_root=root,
                    solution=solution,
                )
            self.assertEqual(b"unowned", unowned.read_bytes())

            with (
                patch.object(MODULE, "run", side_effect=lambda *_a, **_k: lock.unlink()),
                self.assertRaisesRegex(RuntimeError, "inventory"),
            ):
                MODULE.run_solution_restore_preserving_lock_projections(
                    ["dotnet", "restore", "solution"],
                    environment={},
                    log_path=None,
                    repository_root=root,
                    solution=solution,
                )
            self.assertFalse(lock.exists())

    def test_solution_restore_rejects_rewrite_without_removed_windows_rid(self) -> None:
        with tempfile.TemporaryDirectory() as temporary:
            root = Path(temporary)
            original = b'{"version":1,"dependencies":{"net10.0":{}}}'
            lock, solution = self.create_solution_lock_fixture(root, original)
            rewritten = b'{\n  "version": 1,\n  "dependencies": {"net10.0": {}}\n}'
            with (
                patch.object(
                    MODULE,
                    "run",
                    side_effect=lambda *_args, **_kwargs: lock.write_bytes(rewritten),
                ),
                self.assertRaisesRegex(RuntimeError, "removed no case-exact"),
            ):
                MODULE.run_solution_restore_preserving_lock_projections(
                    ["dotnet", "restore", "solution"],
                    environment={},
                    log_path=None,
                    repository_root=root,
                    solution=solution,
                )
            self.assertEqual(rewritten, lock.read_bytes())

    def test_solution_restore_rejects_invalid_or_duplicate_project_inventory(
        self,
    ) -> None:
        for invalid_project in (
            "../Escape/Escape.csproj",
            "src/./Product/Product.csproj",
            "src/Product/Product.csproj",
        ):
            with (
                self.subTest(project=invalid_project),
                tempfile.TemporaryDirectory() as temporary,
            ):
                root = Path(temporary)
                _, solution = self.create_solution_lock_fixture(
                    root, b'{"version":1,"dependencies":{"net10.0":{}}}'
                )
                document = MODULE.ET.parse(solution)
                MODULE.ET.SubElement(
                    document.getroot(), "Project", Path=invalid_project
                )
                document.write(solution, encoding="utf-8")
                execute = MagicMock()
                with (
                    patch.object(MODULE, "run", execute),
                    self.assertRaises(RuntimeError),
                ):
                    MODULE.run_solution_restore_preserving_lock_projections(
                        ["dotnet", "restore", "solution"],
                        environment={},
                        log_path=None,
                        repository_root=root,
                        solution=solution,
                    )
                execute.assert_not_called()

    def test_solution_restore_aggregates_set_substitution_with_primary_failure(
        self,
    ) -> None:
        with tempfile.TemporaryDirectory() as temporary:
            root = Path(temporary)
            original = b'{"version":1,"dependencies":{"net10.0":{}}}'
            _, solution = self.create_solution_lock_fixture(root, original)

            def substitute_then_fail(_command, **_kwargs):
                replacement = root / "src" / "Replacement" / "Replacement.csproj"
                replacement.parent.mkdir(parents=True)
                replacement.write_text("<Project />", encoding="utf-8")
                (replacement.parent / "packages.lock.json").write_bytes(original)
                document = MODULE.ET.parse(solution)
                projects = document.getroot().findall("Project")
                projects[-1].set("Path", "src/Replacement/Replacement.csproj")
                document.write(solution, encoding="utf-8")
                raise RuntimeError("restore primary")

            with (
                patch.object(MODULE, "run", side_effect=substitute_then_fail),
                self.assertRaisesRegex(RuntimeError, "unowned package lock") as raised,
            ):
                MODULE.run_solution_restore_preserving_lock_projections(
                    ["dotnet", "restore", "solution"],
                    environment={},
                    log_path=None,
                    repository_root=root,
                    solution=solution,
                )

            self.assertIn("restore primary", " ".join(raised.exception.__notes__))

    def test_solution_restore_rejects_reparse_owned_lock_before_running(self) -> None:
        with tempfile.TemporaryDirectory() as temporary:
            root = Path(temporary)
            lock, solution = self.create_solution_lock_fixture(
                root, b'{"version":1,"dependencies":{"net10.0":{}}}'
            )
            execute = MagicMock()
            actual_is_reparse = MODULE.is_reparse_point

            with (
                patch.object(
                    MODULE,
                    "is_reparse_point",
                    side_effect=lambda path: path == lock or actual_is_reparse(path),
                ),
                patch.object(MODULE, "run", execute),
                self.assertRaisesRegex(RuntimeError, "reparse-point"),
            ):
                MODULE.run_solution_restore_preserving_lock_projections(
                    ["dotnet", "restore", "solution"],
                    environment={},
                    log_path=None,
                    repository_root=root,
                    solution=solution,
                )
            execute.assert_not_called()

    def test_solution_restore_reports_read_and_atomic_write_failures(self) -> None:
        with tempfile.TemporaryDirectory() as temporary:
            root = Path(temporary)
            original = b'{"version":1,"dependencies":{"net10.0":{},"net10.0/win-x64":{}}}'
            lock, solution = self.create_solution_lock_fixture(root, original)
            actual_read = Path.read_bytes
            reads = 0

            def fail_second_lock_read(path: Path) -> bytes:
                nonlocal reads
                if path == lock:
                    reads += 1
                    if reads == 2:
                        raise OSError("read probe")
                return actual_read(path)

            with (
                patch.object(Path, "read_bytes", fail_second_lock_read),
                patch.object(MODULE, "run"),
                self.assertRaisesRegex(RuntimeError, "read probe"),
            ):
                MODULE.run_solution_restore_preserving_lock_projections(
                    ["dotnet", "restore", "solution"],
                    environment={},
                    log_path=None,
                    repository_root=root,
                    solution=solution,
                )

            projected = b'{"version":1,"dependencies":{"net10.0":{}}}'

            def restore_then_fail(_command, **_kwargs):
                lock.write_bytes(projected)
                raise RuntimeError("restore primary")

            with (
                patch.object(MODULE, "run", side_effect=restore_then_fail),
                patch.object(MODULE.os, "replace", side_effect=OSError("write probe")),
                self.assertRaisesRegex(RuntimeError, "write probe") as raised,
            ):
                MODULE.run_solution_restore_preserving_lock_projections(
                    ["dotnet", "restore", "solution"],
                    environment={},
                    log_path=None,
                    repository_root=root,
                    solution=solution,
                )

            self.assertIn("restore primary", " ".join(raised.exception.__notes__))
            self.assertEqual(projected, lock.read_bytes())
            self.assertTrue(
                tuple(lock.parent.glob(f".{lock.name}.nfc-restore-*.tmp"))
            )

    def test_solution_restore_reports_partial_multi_lock_restoration(self) -> None:
        with tempfile.TemporaryDirectory() as temporary:
            root = Path(temporary)
            original = b'{"version":1,"dependencies":{"net10.0":{},"net10.0/win-x64":{}}}'
            first, solution = self.create_solution_lock_fixture(root, original)
            second = root / "src" / "Product01" / "packages.lock.json"
            projected = b'{"version":1,"dependencies":{"net10.0":{}}}'
            actual_replace = os.replace
            replacements = 0

            def fail_second_replace(source, destination):
                nonlocal replacements
                replacements += 1
                if replacements == 2:
                    raise OSError("second replace probe")
                actual_replace(source, destination)

            def mutate_two(_command, **_kwargs):
                first.write_bytes(projected)
                second.write_bytes(projected)

            with (
                patch.object(MODULE, "run", side_effect=mutate_two),
                patch.object(MODULE.os, "replace", side_effect=fail_second_replace),
                self.assertRaisesRegex(
                    RuntimeError, "successfully restored before failure.*Product"
                ),
            ):
                MODULE.run_solution_restore_preserving_lock_projections(
                    ["dotnet", "restore", "solution"],
                    environment={},
                    log_path=None,
                    repository_root=root,
                    solution=solution,
                )

            self.assertEqual(original, first.read_bytes())
            self.assertEqual(projected, second.read_bytes())

    def test_solution_restore_rejects_failed_exact_byte_postcheck(self) -> None:
        with tempfile.TemporaryDirectory() as temporary:
            root = Path(temporary)
            original = b'{"version":1,"dependencies":{"net10.0":{},"net10.0/win-x64":{}}}'
            lock, solution = self.create_solution_lock_fixture(root, original)
            projected = b'{"version":1,"dependencies":{"net10.0":{}}}'
            actual_replace = os.replace

            def corrupt_after_replace(source, destination):
                actual_replace(source, destination)
                Path(destination).write_bytes(b"corrupt")

            with (
                patch.object(
                    MODULE,
                    "run",
                    side_effect=lambda *_args, **_kwargs: lock.write_bytes(projected),
                ),
                patch.object(MODULE.os, "replace", side_effect=corrupt_after_replace),
                self.assertRaisesRegex(RuntimeError, "differs"),
            ):
                MODULE.run_solution_restore_preserving_lock_projections(
                    ["dotnet", "restore", "solution"],
                    environment={},
                    log_path=None,
                    repository_root=root,
                    solution=solution,
                )

            self.assertEqual(b"corrupt", lock.read_bytes())

    def test_dotnet_build_plan_routes_restore_through_one_explicit_owner(self) -> None:
        restore = MagicMock()
        commands: list[list[str]] = []
        with (
            patch.object(MODULE, "run", side_effect=lambda command, **_k: commands.append(command)),
            patch.object(
                MODULE,
                "run_solution_restore_preserving_lock_projections",
                restore,
            ),
        ):
            MODULE.run_dotnet_build_plan("dotnet", environment={}, log_path=None)

        restore.assert_called_once_with(
            ["dotnet", "restore", str(MODULE.SOLUTION)],
            environment={},
            log_path=None,
            repository_root=MODULE.ROOT,
            solution=MODULE.SOLUTION,
        )
        self.assertEqual(["dotnet", "--version"], commands[0])
        self.assertFalse(any(command[1] == "restore" for command in commands))

    def test_local_dotnet_preserves_primary_and_cleanup_failures(self) -> None:
        with tempfile.TemporaryDirectory() as temporary:
            root = Path(temporary)
            with (
                patch.object(MODULE, "resolve_dotnet", return_value="dotnet"),
                patch.object(
                    MODULE,
                    "reset_coverage_directory",
                    return_value=root / "coverage",
                ),
                patch.object(
                    MODULE,
                    "run_dotnet_commands",
                    side_effect=RuntimeError("primary probe"),
                ),
                patch.object(
                    MODULE,
                    "cleanup_dotnet_batch",
                    side_effect=RuntimeError("cleanup probe"),
                ),
                self.assertRaisesRegex(
                    RuntimeError,
                    "primary probe.*cleanup also failed.*cleanup probe",
                ),
            ):
                MODULE.verify_dotnet()

    def test_nested_dotnet_cancellation_stays_latched_through_outer_cleanup(
        self,
    ) -> None:
        projects = (
            MODULE.CiDotnetProject("tests/First/First.Tests.csproj"),
            MODULE.CiDotnetProject("tests/Second/Second.Tests.csproj"),
        )
        cancellation = threading.Event()
        cancellation_handoffs: list[str] = []

        def cancelled_project(*_args: object, **_kwargs: object) -> None:
            raise MODULE.VerificationTerminationRequested("cancel probe")

        def cancel_active() -> None:
            cancellation_handoffs.append("cancel")
            cancellation.set()

        with tempfile.TemporaryDirectory() as temporary:
            root = Path(temporary)
            coverage = root / "coverage"
            coverage.mkdir()
            work = root / "work"
            with (
                patch.object(MODULE, "PROCESS_CANCELLATION_REQUESTED", cancellation),
                patch.object(MODULE, "resolve_dotnet", return_value="dotnet"),
                patch.object(
                    MODULE,
                    "reset_coverage_directory",
                    return_value=coverage,
                ),
                patch.object(MODULE, "DOTNET_COVERAGE_WORK_ROOT", work),
                patch.object(MODULE, "ROOT", root),
                patch.object(MODULE, "run_dotnet_build_plan"),
                patch.object(MODULE, "run_dotnet_commands"),
                patch.object(
                    MODULE, "flatten_ci_dotnet_projects", return_value=projects
                ),
                patch.object(
                    MODULE,
                    "resolve_coverlet_adapter_path",
                    return_value=root / "adapter",
                ),
                patch.object(
                    MODULE,
                    "prepare_local_dotnet_coverage_stage",
                    side_effect=lambda project, *_args, **_kwargs: (
                        MODULE.LocalDotnetCoverageStage(
                            project,
                            root / project.name / "source",
                            work / project.name / "shadow",
                            work / project.name / f"{project.name}.dll",
                            work / project.name / "discovered-tests.txt",
                            coverage / project.name,
                            {},
                            (),
                        )
                    ),
                ),
                patch.object(
                    MODULE,
                    "run_local_dotnet_coverage_project",
                    side_effect=cancelled_project,
                ),
                patch.object(
                    MODULE,
                    "cancel_active_processes_after_handoffs",
                    side_effect=cancel_active,
                ),
                patch.object(MODULE, "run") as run_command,
                patch.object(MODULE, "stop_idle_build_workers") as stop_idle_workers,
                self.assertRaises(MODULE.VerificationTerminationRequested),
            ):
                MODULE.verify_dotnet()

            self.assertTrue(cancellation.is_set())
            self.assertEqual(["cancel"], cancellation_handoffs)
            run_command.assert_not_called()
            stop_idle_workers.assert_not_called()

    def test_local_vstest_command_uses_one_shadow_assembly_and_pinned_collector(
        self,
    ) -> None:
        assembly = Path("shadow/Probe.Tests/bin/Release/net10.0/Probe.Tests.dll")
        adapter = Path(".packages/coverlet.collector/6.0.4/build/netstandard2.0")
        results = Path("artifacts/coverage/dotnet/Probe.Tests")

        command = MODULE.local_dotnet_vstest_command(
            "dotnet",
            assembly,
            adapter,
            results,
        )

        self.assertEqual(["dotnet", "vstest", str(assembly)], command[:3])
        self.assertIn(f"--TestAdapterPath:{adapter}", command)
        self.assertIn("--Collect:XPlat Code Coverage", command)
        self.assertIn(f"--ResultsDirectory:{results}", command)
        self.assertIn("--Logger:trx;LogFileName=test-results.trx", command)
        self.assertNotIn("--filter", tuple(part.casefold() for part in command))
        self.assertFalse(any("exclude" in part.casefold() for part in command))
        self.assertEqual(
            "DataCollectionRunSettings.DataCollectors.DataCollector.Configuration.Format=json,cobertura",
            command[-1],
        )
        self.assertEqual(
            ["dotnet", "vstest", str(assembly), "--ListTests"],
            MODULE.dotnet_vstest_discovery_command("dotnet", assembly),
        )

    def test_infrastructure_vstest_discovery_and_execution_share_xunit_settings(
        self,
    ) -> None:
        assembly = Path(
            "shadow/NvtFwCombiner.Infrastructure.Tests/bin/Release/net10.0/"
            "NvtFwCombiner.Infrastructure.Tests.dll"
        )
        adapter = Path(".packages/coverlet.collector/6.0.4/build/netstandard2.0")
        results = Path("artifacts/coverage/dotnet/NvtFwCombiner.Infrastructure.Tests")
        expected_settings = [
            "DataCollectionRunSettings.DataCollectors.DataCollector."
            "Configuration.Format=json,cobertura",
            "xUnit.ParallelizeTestCollections=false",
            "xUnit.MaxParallelThreads=1",
            "xUnit.Seed=1738590270",
        ]

        local_command = MODULE.local_dotnet_vstest_command(
            "dotnet",
            assembly,
            adapter,
            results,
        )
        discovery_command = MODULE.dotnet_vstest_discovery_command(
            "dotnet",
            assembly,
        )
        self.assertEqual(expected_settings, local_command[-4:])
        self.assertEqual(["--", *expected_settings[-3:]], discovery_command[-4:])
        self.assertEqual(1, local_command.count("xUnit.Seed=1738590270"))
        self.assertEqual(1, discovery_command.count("xUnit.Seed=1738590270"))

        ordinary_assembly = Path(
            "shadow/NvtFwCombiner.Domain.Tests/bin/Release/net10.0/"
            "NvtFwCombiner.Domain.Tests.dll"
        )
        ordinary_local = MODULE.local_dotnet_vstest_command(
            "dotnet",
            ordinary_assembly,
            adapter,
            results,
        )
        ordinary_discovery = MODULE.dotnet_vstest_discovery_command(
            "dotnet",
            ordinary_assembly,
        )
        self.assertFalse(any(part.startswith("xUnit.Seed=") for part in ordinary_local))
        self.assertFalse(
            any(part.startswith("xUnit.Seed=") for part in ordinary_discovery)
        )

    def test_coverlet_adapter_comes_only_from_baseline_and_repository_packages(
        self,
    ) -> None:
        with tempfile.TemporaryDirectory() as temporary:
            root = Path(temporary)
            adapter = root / ".packages/coverlet.collector/6.0.4/build/netstandard2.0"
            adapter.mkdir(parents=True)
            (adapter / "coverlet.collector.dll").write_bytes(b"collector")
            (adapter / "coverlet.collector.deps.json").write_text(
                "{}", encoding="utf-8"
            )

            resolved = MODULE.resolve_coverlet_adapter_path(
                root,
                {
                    "collection": {
                        "dotnet": {
                            "collector": "coverlet.collector",
                            "version": "6.0.4",
                            "format": "json,cobertura",
                        }
                    }
                },
            )

            self.assertEqual(adapter, resolved)

            with self.assertRaisesRegex(RuntimeError, "collector version"):
                MODULE.resolve_coverlet_adapter_path(
                    root,
                    {
                        "collection": {
                            "dotnet": {
                                "collector": "coverlet.collector",
                                "version": "../global-cache",
                                "format": "json,cobertura",
                            }
                        }
                    },
                )

            (adapter / "coverlet.collector.dll").unlink()
            with self.assertRaisesRegex(RuntimeError, "coverlet.collector.dll"):
                MODULE.resolve_coverlet_adapter_path(
                    root,
                    {
                        "collection": {
                            "dotnet": {
                                "collector": "coverlet.collector",
                                "version": "6.0.4",
                                "format": "json,cobertura",
                            }
                        }
                    },
                )

    def test_coverlet_adapter_rejects_a_link_before_vstest(self) -> None:
        with tempfile.TemporaryDirectory() as temporary:
            root = Path(temporary)
            external = root / "external"
            external.mkdir()
            (external / "coverlet.collector.dll").write_bytes(b"collector")
            (external / "coverlet.collector.deps.json").write_text(
                "{}", encoding="utf-8"
            )
            adapter = root / ".packages/coverlet.collector/6.0.4/build/netstandard2.0"
            adapter.parent.mkdir(parents=True)
            try:
                adapter.symlink_to(external, target_is_directory=True)
            except OSError as error:
                self.skipTest(f"directory symlinks are unavailable: {error}")

            with self.assertRaisesRegex(RuntimeError, "reparse|symbolic|junction"):
                MODULE.resolve_coverlet_adapter_path(
                    root,
                    {
                        "collection": {
                            "dotnet": {
                                "collector": "coverlet.collector",
                                "version": "6.0.4",
                                "format": "json,cobertura",
                            }
                        }
                    },
                )

    def test_shadow_snapshot_is_exact_and_detects_source_or_destination_mutation(
        self,
    ) -> None:
        with tempfile.TemporaryDirectory() as temporary:
            root = Path(temporary)
            source = root / "source/bin/Release/net10.0"
            destination = root / "work/Probe.Tests/bin/Release/net10.0"
            source.mkdir(parents=True)
            (source / "Probe.Tests.dll").write_bytes(b"test")
            (source / "Probe.Tests.pdb").write_bytes(b"symbols")
            (source / "fixture.bin").write_bytes(b"fixture")

            hashes = MODULE.snapshot_regular_tree(
                source,
                destination,
                source_boundary=root / "source",
                destination_boundary=root / "work",
            )

            self.assertEqual(
                hashes,
                MODULE.regular_tree_hashes(
                    destination,
                    boundary=root / "work",
                    description="shadow output",
                ),
            )
            (destination / "Probe.Tests.dll").write_bytes(b"mutated")
            with self.assertRaisesRegex(RuntimeError, "changed|hash|inventory"):
                MODULE.require_regular_tree_hashes(
                    destination,
                    hashes,
                    boundary=root / "work",
                    description="shadow output",
                )

            original_copy = MODULE.shutil.copyfile
            mutation_done = False

            def mutate_source_after_copy(
                from_path: Path,
                to_path: Path,
                *,
                follow_symlinks: bool = True,
            ) -> str:
                nonlocal mutation_done
                result = original_copy(
                    from_path,
                    to_path,
                    follow_symlinks=follow_symlinks,
                )
                if not mutation_done:
                    Path(from_path).write_bytes(b"changed during copy")
                    mutation_done = True
                return result

            second_destination = root / "work/Second/bin/Release/net10.0"
            with (
                patch.object(
                    MODULE.shutil,
                    "copyfile",
                    side_effect=mutate_source_after_copy,
                ),
                self.assertRaisesRegex(RuntimeError, "changed during snapshot"),
            ):
                MODULE.snapshot_regular_tree(
                    source,
                    second_destination,
                    source_boundary=root / "source",
                    destination_boundary=root / "work",
                )

    def test_shadow_snapshot_rejects_links_and_path_escape(self) -> None:
        with tempfile.TemporaryDirectory() as temporary:
            root = Path(temporary)
            source = root / "source/bin/Release/net10.0"
            source.mkdir(parents=True)
            (source / "Probe.Tests.dll").write_bytes(b"test")
            outside = root / "outside"
            outside.mkdir()
            with self.assertRaisesRegex(RuntimeError, "outside|escapes"):
                MODULE.snapshot_regular_tree(
                    source,
                    outside / "shadow",
                    source_boundary=root / "source",
                    destination_boundary=root / "work",
                )

            link = source / "linked.dll"
            try:
                link.symlink_to(root / "outside.dll")
            except OSError as error:
                self.skipTest(f"file symlinks are unavailable: {error}")
            with self.assertRaisesRegex(RuntimeError, "reparse|symbolic|junction"):
                MODULE.regular_tree_hashes(
                    source,
                    boundary=root / "source",
                    description="test output",
                )

    def test_production_dll_and_pdb_must_match_canonical_release(self) -> None:
        with tempfile.TemporaryDirectory() as temporary:
            root = Path(temporary)
            test_output = root / "test-output"
            canonical_output = root / "canonical"
            test_output.mkdir()
            canonical_output.mkdir()
            for suffix, content in (("dll", b"binary"), ("pdb", b"symbols")):
                (test_output / f"NvtFwCombiner.Probe.{suffix}").write_bytes(content)
                (canonical_output / f"NvtFwCombiner.Probe.{suffix}").write_bytes(
                    content
                )

            MODULE.require_production_release_matches(
                test_output,
                {"NvtFwCombiner.Probe": canonical_output},
            )

            (test_output / "NvtFwCombiner.Probe.pdb").write_bytes(b"stale")
            with self.assertRaisesRegex(RuntimeError, "PDB|pdb|hash"):
                MODULE.require_production_release_matches(
                    test_output,
                    {"NvtFwCombiner.Probe": canonical_output},
                )

    def test_post_collector_freshness_rejects_canonical_output_mutation(self) -> None:
        with tempfile.TemporaryDirectory() as temporary:
            root = Path(temporary)
            source = root / "source"
            source.mkdir()
            source_file = source / "Probe.Tests.dll"
            source_file.write_bytes(b"test")
            canonical = root / "canonical/NvtFwCombiner.Probe.dll"
            canonical.parent.mkdir()
            canonical.write_bytes(b"product")
            shadow = root / "shadow"
            source_hashes = MODULE.snapshot_regular_tree(
                source,
                shadow,
                source_boundary=root,
                destination_boundary=root,
            )
            stage = MODULE.LocalDotnetCoverageStage(
                MODULE.CiDotnetProject("tests/Probe/Probe.Tests.csproj"),
                source,
                shadow,
                shadow / "Probe.Tests.dll",
                root / "discovered-tests.txt",
                root / "results",
                source_hashes,
                ((canonical, MODULE.sha256_file(canonical)),),
            )

            MODULE.require_local_dotnet_sources_unchanged((stage,), root)

            canonical.write_bytes(b"mutated")
            with self.assertRaisesRegex(RuntimeError, "production output hash changed"):
                MODULE.require_local_dotnet_sources_unchanged((stage,), root)

    def test_post_collector_freshness_uses_distinct_repository_and_work_roots(
        self,
    ) -> None:
        with tempfile.TemporaryDirectory() as temporary:
            owner = Path(temporary)
            repository_root = owner / "repository"
            work_owner_root = owner / "test-area"
            source = repository_root / "source"
            source.mkdir(parents=True)
            source_file = source / "Probe.Tests.dll"
            source_file.write_bytes(b"test")
            canonical = repository_root / "canonical/NvtFwCombiner.Probe.dll"
            canonical.parent.mkdir()
            canonical.write_bytes(b"product")
            shadow = work_owner_root / "work/shadow"
            source_hashes = MODULE.snapshot_regular_tree(
                source,
                shadow,
                source_boundary=repository_root,
                destination_boundary=work_owner_root,
            )
            stage = MODULE.LocalDotnetCoverageStage(
                MODULE.CiDotnetProject("tests/Probe/Probe.Tests.csproj"),
                source,
                shadow,
                shadow / "Probe.Tests.dll",
                work_owner_root / "work/discovered-tests.txt",
                work_owner_root / "results",
                source_hashes,
                ((canonical, MODULE.sha256_file(canonical)),),
            )

            MODULE.require_local_dotnet_sources_unchanged(
                (stage,),
                repository_root,
                work_owner_root,
            )

            source_file.write_bytes(b"mutated")
            with self.assertRaisesRegex(RuntimeError, "canonical test output"):
                MODULE.require_local_dotnet_sources_unchanged(
                    (stage,),
                    repository_root,
                    work_owner_root,
                )
            source_file.write_bytes(b"test")
            (shadow / "Probe.Tests.dll").write_bytes(b"mutated")
            with self.assertRaisesRegex(
                RuntimeError,
                "shadow test output inventory or hash changed",
            ):
                MODULE.require_local_dotnet_sources_unchanged(
                    (stage,),
                    repository_root,
                    work_owner_root,
                )

    def test_post_collector_freshness_rejects_shadow_output_mutation(self) -> None:
        project = MODULE.CiDotnetProject("tests/Probe/Probe.Tests.csproj")
        with tempfile.TemporaryDirectory() as temporary:
            owner = Path(temporary)
            repository_root = owner / "repository"
            work_owner_root = owner / "test-area"
            work = work_owner_root / "work"
            coverage = repository_root / "coverage"
            coverage.mkdir(parents=True)
            source = repository_root / "source/bin/Release/net10.0"
            source.mkdir(parents=True)
            (source / "Probe.Tests.dll").write_bytes(b"test")
            (source / "NvtFwCombiner.Probe.dll").write_bytes(b"product")
            canonical = repository_root / "canonical/NvtFwCombiner.Probe.dll"
            canonical.parent.mkdir()
            canonical.write_bytes(b"product")

            def prepare(*_args: object, **_kwargs: object):
                shadow = work / "12345678/bin/Release/net10.0"
                hashes = MODULE.snapshot_regular_tree(
                    source,
                    shadow,
                    source_boundary=repository_root,
                    destination_boundary=work_owner_root,
                )
                results = coverage / project.name
                results.mkdir()
                return MODULE.LocalDotnetCoverageStage(
                    project,
                    source,
                    shadow,
                    shadow / "Probe.Tests.dll",
                    work / "12345678/discovered-tests.txt",
                    results,
                    hashes,
                    ((canonical, MODULE.sha256_file(canonical)),),
                )

            def mutate_shadow(stage: MODULE.LocalDotnetCoverageStage, *_args: object):
                (stage.shadow_output / "NvtFwCombiner.Probe.dll").write_bytes(
                    b"mutated"
                )

            with (
                patch.object(
                    MODULE, "flatten_ci_dotnet_projects", return_value=(project,)
                ),
                patch.object(
                    MODULE,
                    "resolve_coverlet_adapter_path",
                    return_value=repository_root / "adapter",
                ),
                patch.object(
                    MODULE,
                    "prepare_local_dotnet_coverage_stage",
                    side_effect=prepare,
                ),
                patch.object(
                    MODULE,
                    "run_local_dotnet_coverage_project",
                    side_effect=mutate_shadow,
                ),
                patch.object(MODULE, "verify_coverage") as verify_coverage,
                self.assertRaisesRegex(
                    RuntimeError,
                    "shadow test output inventory or hash changed",
                ),
            ):
                MODULE.collect_local_dotnet_coverage(
                    "dotnet",
                    coverage,
                    work,
                    {},
                    None,
                    repository_root=repository_root,
                    work_owner_root=work_owner_root,
                )

            verify_coverage.assert_not_called()
            self.assertFalse(work.exists())

    def test_canonical_hash_snapshot_cannot_move_between_equality_and_baseline(
        self,
    ) -> None:
        with tempfile.TemporaryDirectory() as temporary:
            root = Path(temporary)
            test_output = root / "test-output"
            canonical_output = root / "canonical"
            source = root / "source"
            test_output.mkdir()
            canonical_output.mkdir()
            source.mkdir()
            test_dll = test_output / "NvtFwCombiner.Probe.dll"
            canonical_dll = canonical_output / "NvtFwCombiner.Probe.dll"
            test_dll.write_bytes(b"accepted")
            canonical_dll.write_bytes(b"accepted")
            source_file = source / "Probe.Tests.dll"
            source_file.write_bytes(b"test")
            shadow = root / "shadow"
            source_hashes = MODULE.snapshot_regular_tree(
                source,
                shadow,
                source_boundary=root,
                destination_boundary=root,
            )
            original_hash = MODULE.sha256_file
            canonical_calls = 0

            def mutate_after_first_canonical_hash(path: Path) -> str:
                nonlocal canonical_calls
                value = original_hash(path)
                if Path(path) == canonical_dll:
                    canonical_calls += 1
                    if canonical_calls == 1:
                        canonical_dll.write_bytes(b"changed")
                return value

            with patch.object(
                MODULE,
                "sha256_file",
                side_effect=mutate_after_first_canonical_hash,
            ):
                canonical_hashes = MODULE.require_production_release_matches(
                    test_output,
                    {"NvtFwCombiner.Probe": canonical_output},
                )

            stage = MODULE.LocalDotnetCoverageStage(
                MODULE.CiDotnetProject("tests/Probe/Probe.Tests.csproj"),
                source,
                shadow,
                shadow / "Probe.Tests.dll",
                root / "discovered-tests.txt",
                root / "results",
                source_hashes,
                canonical_hashes,
            )
            with self.assertRaisesRegex(RuntimeError, "production output hash changed"):
                MODULE.require_local_dotnet_sources_unchanged((stage,), root)

    def test_optional_production_pdb_presence_must_be_exact_and_regular(self) -> None:
        with tempfile.TemporaryDirectory() as temporary:
            root = Path(temporary)
            test_output = root / "test-output"
            canonical_output = root / "canonical"
            test_output.mkdir()
            canonical_output.mkdir()
            for output in (test_output, canonical_output):
                (output / "NvtFwCombiner.Probe.dll").write_bytes(b"binary")
            test_pdb = test_output / "NvtFwCombiner.Probe.pdb"
            canonical_pdb = canonical_output / "NvtFwCombiner.Probe.pdb"

            canonical_pdb.write_bytes(b"symbols")
            with self.assertRaisesRegex(RuntimeError, "PDB pairing mismatch"):
                MODULE.require_production_release_matches(
                    test_output,
                    {"NvtFwCombiner.Probe": canonical_output},
                )
            canonical_pdb.unlink()
            test_pdb.write_bytes(b"symbols")
            with self.assertRaisesRegex(RuntimeError, "PDB pairing mismatch"):
                MODULE.require_production_release_matches(
                    test_output,
                    {"NvtFwCombiner.Probe": canonical_output},
                )
            test_pdb.unlink()
            canonical_pdb.mkdir()
            with self.assertRaisesRegex(RuntimeError, "non-regular.*PDB"):
                MODULE.require_production_release_matches(
                    test_output,
                    {"NvtFwCombiner.Probe": canonical_output},
                )

    def test_optional_production_pdb_rejects_a_symbolic_link(self) -> None:
        with tempfile.TemporaryDirectory() as temporary:
            root = Path(temporary)
            test_output = root / "test-output"
            canonical_output = root / "canonical"
            test_output.mkdir()
            canonical_output.mkdir()
            for output in (test_output, canonical_output):
                (output / "NvtFwCombiner.Probe.dll").write_bytes(b"binary")
            target = root / "symbols.pdb"
            target.write_bytes(b"symbols")
            canonical_pdb = canonical_output / "NvtFwCombiner.Probe.pdb"
            try:
                canonical_pdb.symlink_to(target)
            except OSError as error:
                self.skipTest(f"file symlinks are unavailable: {error}")

            with self.assertRaisesRegex(RuntimeError, "link|reparse"):
                MODULE.require_production_release_matches(
                    test_output,
                    {"NvtFwCombiner.Probe": canonical_output},
                )

    def test_local_shadow_path_is_short_stable_and_preserves_release_suffix(
        self,
    ) -> None:
        project = MODULE.CiDotnetProject(
            "tests/Deep/NvtFwCombiner.DeepFixture.Tests/"
            "NvtFwCombiner.DeepFixture.Tests.csproj",
        )
        expected_token = hashlib.sha256(
            project.relative_path.encode("utf-8")
        ).hexdigest()[:8]
        with tempfile.TemporaryDirectory() as temporary:
            root = Path(temporary)
            source = root / "source/bin/Release/net10.0"
            source.mkdir(parents=True)
            (source / f"{project.name}.dll").write_bytes(b"test")
            work = root / "work"
            coverage = root / "coverage"
            coverage.mkdir()
            release_suffix = Path("bin/Release/net10.0")
            with (
                patch.object(
                    MODULE,
                    "find_project_release_output",
                    return_value=(source, release_suffix),
                ),
                patch.object(
                    MODULE,
                    "canonical_production_release_outputs",
                    return_value={},
                ),
                patch.object(
                    MODULE,
                    "require_production_release_matches",
                    return_value=(),
                ),
            ):
                stage = MODULE.prepare_local_dotnet_coverage_stage(
                    project,
                    work,
                    coverage,
                    repository_root=root,
                )

            self.assertEqual(
                (expected_token, "bin", "Release", "net10.0"),
                stage.shadow_output.relative_to(work).parts,
            )
            self.assertRegex(expected_token, r"^[0-9a-f]{8}$")
            self.assertEqual(
                stage.shadow_output / f"{project.name}.dll",
                stage.test_assembly,
            )
            self.assertNotIn("NvtFwCombiner.DeepFixture.Tests", expected_token)

    def test_local_stage_copies_external_tools_only_for_flagged_project(self) -> None:
        flagged = MODULE.CiDotnetProject(
            "tests/Flagged/Flagged.Tests.csproj",
            requires_external_tools_fixture=True,
        )
        unflagged = MODULE.CiDotnetProject("tests/Plain/Plain.Tests.csproj")
        with tempfile.TemporaryDirectory() as temporary:
            root = Path(temporary)
            external_tools = root / "external-tools"
            (external_tools / "bindings").mkdir(parents=True)
            (external_tools / "bindings/manifest.json").write_text(
                "{}",
                encoding="utf-8",
            )
            work = root / "work"
            coverage = root / "coverage"
            coverage.mkdir()
            release_suffix = Path("bin/Release/net10.0")

            def prepare(
                project: MODULE.CiDotnetProject,
            ) -> MODULE.LocalDotnetCoverageStage:
                source = root / project.name / release_suffix
                source.mkdir(parents=True)
                (source / f"{project.name}.dll").write_bytes(b"test")
                with (
                    patch.object(
                        MODULE,
                        "find_project_release_output",
                        return_value=(source, release_suffix),
                    ),
                    patch.object(
                        MODULE,
                        "canonical_production_release_outputs",
                        return_value={},
                    ),
                    patch.object(
                        MODULE,
                        "require_production_release_matches",
                        return_value=(),
                    ),
                ):
                    return MODULE.prepare_local_dotnet_coverage_stage(
                        project,
                        work,
                        coverage,
                        repository_root=root,
                    )

            flagged_stage = prepare(flagged)
            unflagged_stage = prepare(unflagged)

            self.assertEqual(external_tools, flagged_stage.external_tools_source_root)
            self.assertEqual(
                flagged_stage.shadow_output.parents[2] / "external-tools",
                flagged_stage.external_tools_shadow_root,
            )
            self.assertEqual(
                {
                    "bindings/manifest.json": MODULE.sha256_file(
                        external_tools / "bindings/manifest.json"
                    )
                },
                flagged_stage.external_tools_hashes,
            )
            self.assertEqual(
                b"{}",
                (
                    flagged_stage.external_tools_shadow_root / "bindings/manifest.json"
                ).read_bytes(),
            )
            self.assertIsNone(unflagged_stage.external_tools_source_root)
            self.assertIsNone(unflagged_stage.external_tools_shadow_root)
            self.assertIsNone(unflagged_stage.external_tools_hashes)
            self.assertFalse(
                (unflagged_stage.shadow_output.parents[2] / "external-tools").exists()
            )

    def test_post_collector_freshness_rejects_external_tools_fixture_mutation(
        self,
    ) -> None:
        project = MODULE.CiDotnetProject(
            "tests/Probe/Probe.Tests.csproj",
            requires_external_tools_fixture=True,
        )
        with tempfile.TemporaryDirectory() as temporary:
            owner = Path(temporary)
            repository_root = owner / "repository"
            work_owner_root = owner / "test-area"
            source = repository_root / "source"
            source.mkdir(parents=True)
            (source / "Probe.Tests.dll").write_bytes(b"test")
            shadow = work_owner_root / "work/project/bin/Release/net10.0"
            source_hashes = MODULE.snapshot_regular_tree(
                source,
                shadow,
                source_boundary=repository_root,
                destination_boundary=work_owner_root,
            )
            fixture_source = repository_root / "external-tools"
            fixture_source.mkdir()
            fixture_file = fixture_source / "manifest.json"
            fixture_file.write_bytes(b"accepted")
            fixture_shadow = work_owner_root / "work/project/external-tools"
            fixture_hashes = MODULE.snapshot_regular_tree(
                fixture_source,
                fixture_shadow,
                source_boundary=repository_root,
                destination_boundary=work_owner_root,
            )
            stage = MODULE.LocalDotnetCoverageStage(
                project,
                source,
                shadow,
                shadow / "Probe.Tests.dll",
                work_owner_root / "work/project/discovered-tests.txt",
                work_owner_root / "results",
                source_hashes,
                (),
                fixture_source,
                fixture_shadow,
                fixture_hashes,
            )

            MODULE.require_local_dotnet_sources_unchanged(
                (stage,),
                repository_root,
                work_owner_root,
            )

            incomplete_stage = MODULE.LocalDotnetCoverageStage(
                stage.project,
                stage.source_output,
                stage.shadow_output,
                stage.test_assembly,
                stage.discovery_report,
                stage.results_directory,
                stage.source_hashes,
                stage.canonical_hashes,
                stage.external_tools_source_root,
                stage.external_tools_shadow_root,
                None,
            )
            with self.assertRaisesRegex(
                RuntimeError,
                "external-tools fixture evidence is incomplete",
            ):
                MODULE.require_local_dotnet_sources_unchanged(
                    (incomplete_stage,),
                    repository_root,
                    work_owner_root,
                )

            fixture_file.write_bytes(b"source-mutated")
            with self.assertRaisesRegex(RuntimeError, "external-tools source fixture"):
                MODULE.require_local_dotnet_sources_unchanged(
                    (stage,),
                    repository_root,
                    work_owner_root,
                )
            fixture_file.write_bytes(b"accepted")
            (fixture_shadow / "manifest.json").write_bytes(b"shadow-mutated")
            with self.assertRaisesRegex(RuntimeError, "external-tools shadow fixture"):
                MODULE.require_local_dotnet_sources_unchanged(
                    (stage,),
                    repository_root,
                    work_owner_root,
                )

    def test_local_project_evidence_requires_exact_counter_trx_and_pair(self) -> None:
        project = MODULE.CiDotnetProject("tests/Probe/Probe.Tests.csproj")
        with tempfile.TemporaryDirectory() as temporary:
            results = Path(temporary)
            discovery = results / "discovered-tests.txt"
            self.write_vstest_discovery(discovery, 3)
            self.write_ci_trx(results / "test-results.trx", total=3, skipped=0)
            report = results / "coverage"
            report.mkdir()
            (report / "coverage.json").write_text("{}", encoding="utf-8")
            (report / "coverage.cobertura.xml").write_text(
                "<coverage />", encoding="utf-8"
            )

            MODULE.require_local_dotnet_project_evidence(project, results, discovery)

            self.write_ci_trx(results / "extra.trx", total=3, skipped=0)
            with self.assertRaisesRegex(RuntimeError, "exactly one TRX"):
                MODULE.require_local_dotnet_project_evidence(
                    project, results, discovery
                )
            (results / "extra.trx").unlink()

            (report / "coverage.cobertura.xml").unlink()
            with self.assertRaisesRegex(RuntimeError, "paired coverage"):
                MODULE.require_local_dotnet_project_evidence(
                    project, results, discovery
                )

            (report / "coverage.cobertura.xml").write_text(
                "<coverage />", encoding="utf-8"
            )
            self.write_ci_trx(results / "test-results.trx", total=4, skipped=0)
            with self.assertRaisesRegex(RuntimeError, "discovered/executed"):
                MODULE.require_local_dotnet_project_evidence(
                    project,
                    results,
                    discovery,
                )

    def test_duplicate_coverage_attachments_must_be_identical_before_collapse(
        self,
    ) -> None:
        with tempfile.TemporaryDirectory() as temporary:
            results = Path(temporary)
            self.write_ci_trx(results / "test-results.trx", total=1, skipped=0)
            for parent in (results / "attachment", results / "trx/In/host"):
                parent.mkdir(parents=True)
                (parent / "coverage.json").write_text("{}", encoding="utf-8")
                (parent / "coverage.cobertura.xml").write_text(
                    "<coverage />", encoding="utf-8"
                )

            _, json_report, cobertura_report = (
                MODULE.canonicalize_dotnet_project_reports("Probe", results)
            )

            self.assertEqual(results / "attachment/coverage.json", json_report)
            self.assertEqual(
                results / "attachment/coverage.cobertura.xml",
                cobertura_report,
            )
            self.assertEqual(1, len(tuple(results.rglob("coverage.json"))))
            self.assertEqual(1, len(tuple(results.rglob("coverage.cobertura.xml"))))

            for divergent_name, divergent_content in (
                ("coverage.json", '{"different": true}'),
                ("coverage.cobertura.xml", '<coverage branch-rate="1" />'),
            ):
                with self.subTest(divergent_name=divergent_name):
                    duplicate = results / "other"
                    duplicate.mkdir()
                    (duplicate / "coverage.json").write_text("{}", encoding="utf-8")
                    (duplicate / "coverage.cobertura.xml").write_text(
                        "<coverage />", encoding="utf-8"
                    )
                    (duplicate / divergent_name).write_text(
                        divergent_content, encoding="utf-8"
                    )
                    with self.assertRaisesRegex(RuntimeError, "divergent"):
                        MODULE.canonicalize_dotnet_project_reports("Probe", results)
                    MODULE.shutil.rmtree(duplicate)

    def test_local_coverage_isolates_opted_in_ui_and_uses_three_workers_for_rest(
        self,
    ) -> None:
        projects = tuple(
            MODULE.CiDotnetProject(
                f"tests/P{index}/P{index}.Tests.csproj",
                requires_exclusive_local_coverage=index == 0,
            )
            for index in range(8)
        )
        attempted: list[str] = []
        active = 0
        maximum_active = 0
        exclusive_active = False
        overlapped_exclusive = False
        lock = threading.Lock()

        def successful_run(
            stage: MODULE.LocalDotnetCoverageStage,
            *_args: object,
        ) -> None:
            nonlocal active, maximum_active, exclusive_active, overlapped_exclusive
            with lock:
                overlapped_exclusive |= exclusive_active or (
                    stage.project.requires_exclusive_local_coverage and active > 0
                )
                attempted.append(stage.project.name)
                active += 1
                maximum_active = max(maximum_active, active)
                exclusive_active |= stage.project.requires_exclusive_local_coverage
            time.sleep(0.03)
            with lock:
                active -= 1
                exclusive_active &= not stage.project.requires_exclusive_local_coverage

        with tempfile.TemporaryDirectory() as temporary:
            root = Path(temporary)
            work = root / "work"
            coverage = root / "coverage"
            coverage.mkdir()
            with (
                patch.object(
                    MODULE,
                    "flatten_ci_dotnet_projects",
                    return_value=projects,
                ),
                patch.object(
                    MODULE,
                    "resolve_coverlet_adapter_path",
                    return_value=root / "adapter",
                ),
                patch.object(
                    MODULE,
                    "prepare_local_dotnet_coverage_stage",
                    side_effect=lambda project, *_args, **_kwargs: (
                        MODULE.LocalDotnetCoverageStage(
                            project,
                            root / project.name / "source",
                            root / project.name / "shadow",
                            root / project.name / f"{project.name}.dll",
                            root / project.name / "discovered-tests.txt",
                            root / project.name / "results",
                            {},
                            (),
                        )
                    ),
                ),
                patch.object(
                    MODULE,
                    "run_local_dotnet_coverage_project",
                    side_effect=successful_run,
                ),
                patch.object(MODULE, "require_local_dotnet_sources_unchanged"),
                patch.object(MODULE, "verify_coverage") as verify_coverage,
            ):
                MODULE.collect_local_dotnet_coverage(
                    "dotnet",
                    coverage,
                    work,
                    {},
                    None,
                    repository_root=root,
                )

            self.assertEqual(
                {project.name for project in projects},
                set(attempted),
            )
            self.assertEqual(len(projects), len(attempted))
            self.assertEqual(3, maximum_active)
            self.assertFalse(overlapped_exclusive)
            verify_coverage.assert_called_once_with("dotnet", coverage)
            self.assertFalse(work.exists())

    def test_invalid_collector_blocks_every_project_runner(self) -> None:
        project = MODULE.CiDotnetProject("tests/Probe/Probe.Tests.csproj")
        with tempfile.TemporaryDirectory() as temporary:
            root = Path(temporary)
            work = root / "work"
            work.mkdir()
            (work / "stale.txt").write_text("stale", encoding="utf-8")
            with (
                patch.object(
                    MODULE,
                    "flatten_ci_dotnet_projects",
                    return_value=(project,),
                ),
                patch.object(
                    MODULE,
                    "resolve_coverlet_adapter_path",
                    side_effect=RuntimeError("collector missing"),
                ),
                patch.object(MODULE, "prepare_local_dotnet_coverage_stage") as prepare,
                patch.object(MODULE, "run_local_dotnet_coverage_project") as run,
                patch.object(MODULE, "verify_coverage") as verify_coverage,
                self.assertRaisesRegex(RuntimeError, "collector missing"),
            ):
                MODULE.collect_local_dotnet_coverage(
                    "dotnet",
                    root / "coverage",
                    work,
                    {},
                    None,
                    repository_root=root,
                )

            prepare.assert_not_called()
            run.assert_not_called()
            verify_coverage.assert_not_called()
            self.assertFalse(work.exists())

    def test_local_coverage_orchestration_aggregates_failures_before_policy(
        self,
    ) -> None:
        projects = (
            MODULE.CiDotnetProject("tests/First/First.Tests.csproj"),
            MODULE.CiDotnetProject("tests/Second/Second.Tests.csproj"),
            MODULE.CiDotnetProject("tests/Third/Third.Tests.csproj"),
        )
        attempted: list[str] = []

        def fail_two(stage: MODULE.LocalDotnetCoverageStage, *_args: object) -> None:
            attempted.append(stage.project.name)
            if stage.project.name != "Third.Tests":
                raise RuntimeError(f"{stage.project.name} failed")

        with tempfile.TemporaryDirectory() as temporary:
            root = Path(temporary)
            work = root / "work"
            work.mkdir()
            with (
                patch.object(
                    MODULE,
                    "flatten_ci_dotnet_projects",
                    return_value=projects,
                ),
                patch.object(
                    MODULE,
                    "prepare_local_dotnet_coverage_stage",
                    side_effect=lambda project, *_args, **_kwargs: (
                        MODULE.LocalDotnetCoverageStage(
                            project,
                            root / project.name / "source",
                            root / project.name / "shadow",
                            root / project.name / f"{project.name}.dll",
                            root / project.name / "discovered-tests.txt",
                            root / project.name / "results",
                            {},
                            (),
                        )
                    ),
                ),
                patch.object(
                    MODULE,
                    "resolve_coverlet_adapter_path",
                    return_value=root / "adapter",
                ),
                patch.object(
                    MODULE,
                    "run_local_dotnet_coverage_project",
                    side_effect=fail_two,
                ),
                patch.object(MODULE, "require_local_dotnet_sources_unchanged"),
                patch.object(MODULE, "verify_coverage") as verify_coverage,
                self.assertRaisesRegex(RuntimeError, "First.Tests.*Second.Tests"),
            ):
                MODULE.collect_local_dotnet_coverage(
                    "dotnet",
                    root / "coverage",
                    work,
                    {},
                    None,
                    repository_root=root,
                )

            self.assertEqual(
                {project.name for project in projects},
                set(attempted),
            )
            verify_coverage.assert_not_called()
            self.assertFalse(work.exists())

    def test_python_lane_emits_one_json_report_before_policy_validation(self) -> None:
        commands: list[list[str]] = []
        python_collection = {
            "coveragePyVersion": "7.14.3",
            "pytestCovVersion": "6.3.0",
        }
        with tempfile.TemporaryDirectory() as temporary:
            coverage_directory = Path(temporary) / "python"
            coverage_report = coverage_directory / "coverage.json"
            with (
                patch.object(MODULE, "require_python_modules"),
                patch.object(
                    MODULE,
                    "load_baseline",
                    return_value={"collection": {"python": python_collection}},
                ),
                patch.object(
                    MODULE,
                    "require_python_distribution_versions",
                ) as require_versions,
                patch.object(
                    MODULE,
                    "reset_coverage_directory",
                    return_value=coverage_directory,
                ),
                patch.object(
                    MODULE,
                    "run",
                    side_effect=lambda command, **_kwargs: commands.append(command),
                ),
                patch.object(MODULE, "verify_coverage") as verify_coverage,
            ):
                MODULE.verify_python()

        pytest_command = next(command for command in commands if "pytest" in command)
        self.assertIn(f"--cov-report=json:{coverage_report}", pytest_command)
        require_versions.assert_called_once_with(
            {"coverage": "7.14.3", "pytest-cov": "6.3.0"}
        )
        verify_coverage.assert_called_once_with("python", coverage_report)

    def test_python_lane_rejects_coverage_environment_overrides_before_commands(
        self,
    ) -> None:
        overrides = {
            "PYTEST_ADDOPTS": "--no-cov",
            "COVERAGE_RCFILE": "coverage-alt.ini",
            "COVERAGE_PROCESS_START": "coverage-alt.ini",
        }
        for variable, value in overrides.items():
            with (
                self.subTest(variable=variable),
                patch.dict(os.environ, {variable: value}, clear=True),
                patch.object(MODULE, "require_python_modules") as require_modules,
                patch.object(MODULE, "run") as run_command,
                self.assertRaisesRegex(RuntimeError, variable),
            ):
                MODULE.verify_python()

            require_modules.assert_not_called()
            run_command.assert_not_called()

    def test_python_coverage_collector_versions_must_match_baseline(self) -> None:
        expected = {"coverage": "7.14.3", "pytest-cov": "6.3.0"}
        with patch.object(
            MODULE.importlib_metadata,
            "version",
            side_effect=lambda distribution: expected[distribution],
        ):
            MODULE.require_python_distribution_versions(expected)

        with (
            patch.object(
                MODULE.importlib_metadata,
                "version",
                side_effect=lambda distribution: (
                    "7.14.2" if distribution == "coverage" else expected[distribution]
                ),
            ),
            self.assertRaisesRegex(
                RuntimeError,
                "coverage expected 7.14.3, found 7.14.2",
            ),
        ):
            MODULE.require_python_distribution_versions(expected)

    def test_ci_dotnet_shards_form_one_closed_exact_project_partition(self) -> None:
        expected = {
            "bootstrap": (
                "tests/NvtFwCombiner.Bootstrap.Tests/"
                "NvtFwCombiner.Bootstrap.Tests.csproj",
            ),
            "ui": (
                "tests/NvtFwCombiner.UiSmoke.Tests/NvtFwCombiner.UiSmoke.Tests.csproj",
            ),
            "core": (
                "tests/NvtFwCombiner.Domain.Tests/NvtFwCombiner.Domain.Tests.csproj",
                "tests/NvtFwCombiner.Application.Tests/"
                "NvtFwCombiner.Application.Tests.csproj",
                "tests/NvtFwCombiner.Infrastructure.Tests/"
                "NvtFwCombiner.Infrastructure.Tests.csproj",
                "tests/NvtFwCombiner.ProfileContract.Tests/"
                "NvtFwCombiner.ProfileContract.Tests.csproj",
                "tests/NvtFwCombiner.GoldenRegression.Tests/"
                "NvtFwCombiner.GoldenRegression.Tests.csproj",
                "tests/NvtFwCombiner.Architecture.Tests/"
                "NvtFwCombiner.Architecture.Tests.csproj",
            ),
        }

        actual = {
            shard: tuple(project.relative_path for project in projects)
            for shard, projects in MODULE.CI_DOTNET_SHARDS.items()
        }

        self.assertEqual(expected, actual)
        self.assertEqual(
            ["tests/NvtFwCombiner.UiSmoke.Tests/NvtFwCombiner.UiSmoke.Tests.csproj"],
            [
                project.relative_path
                for projects in MODULE.CI_DOTNET_SHARDS.values()
                for project in projects
                if project.requires_exclusive_local_coverage
            ],
        )
        self.assertEqual(
            [
                "tests/NvtFwCombiner.Bootstrap.Tests/"
                "NvtFwCombiner.Bootstrap.Tests.csproj"
            ],
            [
                project.relative_path
                for projects in MODULE.CI_DOTNET_SHARDS.values()
                for project in projects
                if project.requires_external_tools_fixture
            ],
        )
        flattened = [path for projects in actual.values() for path in projects]
        solution_test_projects = {
            project.attrib["Path"].replace("\\", "/")
            for project in MODULE.ET.parse(MODULE.SOLUTION).findall(".//Project")
            if (
                MODULE.PurePosixPath(project.attrib["Path"]).parts[0] == "tests"
                and MODULE.PurePosixPath(project.attrib["Path"]).stem.endswith(".Tests")
            )
        }
        self.assertEqual(8, len(flattened))
        self.assertEqual(8, len(set(flattened)))
        self.assertEqual(solution_test_projects, set(flattened))
        self.assertFalse(hasattr(MODULE.CiDotnetProject, "expected_total"))
        self.assertFalse(hasattr(MODULE.CiDotnetProject, "expected_skipped"))

    def test_vstest_discovery_drives_expected_inventory_without_manual_totals(
        self,
    ) -> None:
        project = MODULE.CiDotnetProject("tests/Probe/Probe.Tests.csproj")
        with tempfile.TemporaryDirectory() as temporary:
            discovery = Path(temporary) / "discovered-tests.txt"
            trx = Path(temporary) / "test-results.trx"
            self.write_vstest_discovery(discovery, 3)
            self.write_ci_trx(trx, total=3, skipped=0)
            counters = {"total": 3, "passed": 3, "failed": 0, "skipped": 0}

            MODULE.require_discovered_test_results(
                project, discovery, trx, counters, "windows"
            )

            counters["total"] = 2
            with self.assertRaisesRegex(RuntimeError, "discovered/executed"):
                MODULE.require_discovered_test_results(
                    project, discovery, trx, counters, "windows"
                )

            discovery_identities = (
                'Probe.Tests.Theory(value: "console-truncated…")',
                'Probe.Tests.Theory(value: "second-console-truncated…")',
            )
            trx_identities = (
                'Probe.Tests.Theory(value: "complete-long-value-a")',
                'Probe.Tests.Theory(value: "complete-long-value-b")',
            )
            self.write_vstest_discovery(discovery, discovery_identities)
            self.write_ci_trx(
                trx,
                total=2,
                skipped=0,
                identities=trx_identities,
            )
            MODULE.require_discovered_test_results(
                project,
                discovery,
                trx,
                {"total": 2, "passed": 2, "failed": 0, "skipped": 0},
                "windows",
            )

    def test_trx_placeholder_result_uses_its_exact_test_definition(self) -> None:
        test_id = "ddf21aca-0ac4-b253-2683-07db54c563b2"
        placeholder = (
            "<unknown test ID "
            "7319304e40d766aa6b0fbff5fa1c07f149c0b3ee9f65fece7100f329d87d7e20>"
        )
        identity = (
            "NvtFwCombiner.Infrastructure.Tests.Bundles."
            "ProfileBundleSchemaValidatorTests."
            "ValidateEntriesRejectsMissingOrNullCompositionProfileShape"
            '(mutation: "clone-source")'
        )
        with tempfile.TemporaryDirectory() as temporary:
            trx = Path(temporary) / "test-results.trx"
            root = MODULE.ET.Element(
                "TestRun",
                xmlns="http://microsoft.com/schemas/VisualStudio/TeamTest/2010",
            )
            results = MODULE.ET.SubElement(root, "Results")
            MODULE.ET.SubElement(
                results,
                "UnitTestResult",
                testId=test_id,
                testName=placeholder,
                outcome="Passed",
            )
            definitions = MODULE.ET.SubElement(root, "TestDefinitions")
            definition = MODULE.ET.SubElement(
                definitions,
                "UnitTest",
                id=test_id,
                name=identity,
            )
            MODULE.ET.ElementTree(root).write(
                trx,
                encoding="utf-8",
                xml_declaration=True,
            )

            outcomes = MODULE.parse_trx_test_outcomes(trx)
            definition.set("id", "different-test-id")
            MODULE.ET.ElementTree(root).write(
                trx,
                encoding="utf-8",
                xml_declaration=True,
            )
            with self.assertRaisesRegex(RuntimeError, "no unique test definition"):
                MODULE.parse_trx_test_outcomes(trx)

        self.assertEqual(
            MODULE.Counter(
                {
                    "NvtFwCombiner.Infrastructure.Tests.Bundles."
                    "ProfileBundleSchemaValidatorTests."
                    "ValidateEntriesRejectsMissingOrNullCompositionProfileShape": 1
                }
            ),
            outcomes["Passed"],
        )

    def test_compiled_inventory_admits_only_exact_producer_platform_skips(self) -> None:
        project = MODULE.CiDotnetProject(
            "tests/NvtFwCombiner.Infrastructure.Tests/NvtFwCombiner.Infrastructure.Tests.csproj"
        )
        identities = (
            "Probe.Tests.Pass",
            *MODULE.UNIX_SPECIAL_FILE_INFRASTRUCTURE_SKIPS,
        )
        outcomes = ("Passed", "NotExecuted", "NotExecuted")
        counters = {"total": 3, "passed": 1, "failed": 0, "skipped": 2}
        with tempfile.TemporaryDirectory() as temporary:
            discovery = Path(temporary) / "discovered-tests.txt"
            trx = Path(temporary) / "test-results.trx"
            self.write_vstest_discovery(discovery, identities)
            self.write_ci_trx(
                trx,
                total=3,
                skipped=2,
                identities=identities,
                outcomes=outcomes,
            )

            MODULE.require_discovered_test_results(
                project, discovery, trx, counters, "windows"
            )
            with self.assertRaisesRegex(RuntimeError, "unapproved skipped"):
                MODULE.require_discovered_test_results(
                    project, discovery, trx, counters, "non-windows"
                )

            bootstrap = MODULE.CiDotnetProject(
                "tests/NvtFwCombiner.Bootstrap.Tests/NvtFwCombiner.Bootstrap.Tests.csproj"
            )
            bootstrap_identities = MODULE.WINDOWS_PROCESSOR_BOOTSTRAP_SKIPS
            self.write_vstest_discovery(discovery, bootstrap_identities)
            self.write_ci_trx(
                trx,
                total=len(bootstrap_identities),
                skipped=len(bootstrap_identities),
                identities=bootstrap_identities,
            )
            bootstrap_counters = {
                "total": len(bootstrap_identities),
                "passed": 0,
                "failed": 0,
                "skipped": len(bootstrap_identities),
            }
            MODULE.require_discovered_test_results(
                bootstrap,
                discovery,
                trx,
                bootstrap_counters,
                "non-windows",
            )
            with self.assertRaisesRegex(RuntimeError, "unapproved skipped"):
                MODULE.require_discovered_test_results(
                    bootstrap,
                    discovery,
                    trx,
                    bootstrap_counters,
                    "windows",
                )

    def test_compiled_inventory_rejects_unapproved_skip_and_identity_substitution(
        self,
    ) -> None:
        project = MODULE.CiDotnetProject("tests/Probe/Probe.Tests.csproj")
        with tempfile.TemporaryDirectory() as temporary:
            discovery = Path(temporary) / "discovered-tests.txt"
            trx = Path(temporary) / "test-results.trx"
            self.write_vstest_discovery(discovery, ("Probe.A", "Probe.B"))
            self.write_ci_trx(
                trx,
                total=2,
                skipped=1,
                identities=("Probe.A", "Probe.B"),
                outcomes=("Passed", "NotExecuted"),
            )
            counters = {"total": 2, "passed": 1, "failed": 0, "skipped": 1}
            with self.assertRaisesRegex(RuntimeError, "unapproved skipped"):
                MODULE.require_discovered_test_results(
                    project, discovery, trx, counters, "windows"
                )

            self.write_ci_trx(
                trx,
                total=2,
                skipped=0,
                identities=("Probe.A", "Probe.C"),
            )
            counters = {"total": 2, "passed": 2, "failed": 0, "skipped": 0}
            with self.assertRaisesRegex(RuntimeError, "identities changed"):
                MODULE.require_discovered_test_results(
                    project, discovery, trx, counters, "windows"
                )

    def test_ci_dotnet_build_command_prepares_the_immutable_snapshot(self) -> None:
        project = MODULE.CI_DOTNET_SHARDS["bootstrap"][0]
        assembly = Path(
            "tests/NvtFwCombiner.Bootstrap.Tests/bin/Release/net10.0/"
            "NvtFwCombiner.Bootstrap.Tests.dll"
        )
        adapter = Path(".packages/coverlet.collector/6.0.4/build/netstandard2.0")
        results = Path("artifacts/ci-dotnet-work/results")

        command = MODULE.ci_dotnet_build_command("dotnet", project)
        test_command = MODULE.local_dotnet_vstest_command(
            "dotnet",
            assembly,
            adapter,
            results,
        )

        self.assertEqual("build", command[1])
        self.assertEqual(str(MODULE.ROOT / project.relative_path), command[2])
        self.assertIn("--no-restore", command)
        self.assertNotIn("--filter", command)
        self.assertEqual(["dotnet", "vstest", str(assembly)], test_command[:3])
        self.assertIn(f"--TestAdapterPath:{adapter}", test_command)
        self.assertIn("--Collect:XPlat Code Coverage", test_command)
        self.assertIn(f"--ResultsDirectory:{results}", test_command)

    def test_ci_coverage_normalization_removes_the_windows_producer_root(self) -> None:
        with tempfile.TemporaryDirectory() as temporary:
            repository_root = Path(temporary) / "repository"
            report_root = repository_root / "artifacts/coverage"
            report_root.mkdir(parents=True)
            source = repository_root / "src/Probe/Probe.cs"
            json_report = report_root / "coverage.json"
            json_report.write_text(
                json.dumps({"Probe.dll": {str(source): {"Probe": {}}}}),
                encoding="utf-8",
            )
            cobertura_report = report_root / "coverage.cobertura.xml"
            cobertura_report.write_text(
                "<coverage><sources><source>"
                + str(repository_root)
                + "</source></sources><packages><package><classes>"
                '<class name="Probe" filename="src\\Probe\\Probe.cs" />'
                "</classes></package></packages></coverage>",
                encoding="utf-8",
            )

            MODULE.normalize_ci_dotnet_coverage_reports(
                json_report,
                cobertura_report,
                repository_root,
            )

            normalized_json = json.loads(json_report.read_text(encoding="utf-8"))
            self.assertEqual(
                ["src/Probe/Probe.cs"],
                list(normalized_json["Probe.dll"]),
            )
            normalized_xml = MODULE.ET.parse(cobertura_report).getroot()
            self.assertEqual(".", normalized_xml.findtext("./sources/source"))
            self.assertEqual(
                "src/Probe/Probe.cs",
                normalized_xml.find(".//class").get("filename"),
            )
            normalized_bytes = (json_report.read_bytes(), cobertura_report.read_bytes())

            MODULE.normalize_ci_dotnet_coverage_reports(
                json_report,
                cobertura_report,
                repository_root,
            )

            self.assertEqual(
                normalized_bytes,
                (json_report.read_bytes(), cobertura_report.read_bytes()),
            )

    def test_ci_coverage_normalization_rejects_sources_outside_the_repository(
        self,
    ) -> None:
        with tempfile.TemporaryDirectory() as temporary:
            root = Path(temporary)
            repository_root = root / "repository"
            report_root = repository_root / "artifacts/coverage"
            report_root.mkdir(parents=True)
            json_report = report_root / "coverage.json"
            json_report.write_text(
                json.dumps(
                    {"Probe.dll": {str(root / "outside/Probe.cs"): {"Probe": {}}}}
                ),
                encoding="utf-8",
            )
            cobertura_report = report_root / "coverage.cobertura.xml"
            cobertura_report.write_text("<coverage />", encoding="utf-8")

            with self.assertRaisesRegex(
                RuntimeError, "outside the producer repository"
            ):
                MODULE.normalize_ci_dotnet_coverage_reports(
                    json_report,
                    cobertura_report,
                    repository_root,
                )

    def test_ci_coverage_normalization_rejects_json_source_aliases(self) -> None:
        with tempfile.TemporaryDirectory() as temporary:
            repository_root = Path(temporary) / "repository"
            source = repository_root / "src/Probe/Probe.cs"
            json_report, cobertura_report = self.write_ci_coverage_pair(
                repository_root / "artifacts/coverage",
                {
                    "Probe.dll": {
                        str(source): {"Absolute": {}},
                        "src/Probe/Probe.cs": {"Relative": {}},
                    }
                },
            )

            with self.assertRaisesRegex(RuntimeError, "identity collides"):
                MODULE.normalize_ci_dotnet_coverage_reports(
                    json_report,
                    cobertura_report,
                    repository_root,
                )

    def test_ci_coverage_normalization_rejects_duplicate_json_keys(self) -> None:
        with tempfile.TemporaryDirectory() as temporary:
            repository_root = Path(temporary) / "repository"
            json_report, cobertura_report = self.write_ci_coverage_pair(
                repository_root / "artifacts/coverage",
                {},
            )
            json_report.write_text(
                '{"Probe.dll": {}, "Probe.dll": {}}',
                encoding="utf-8",
            )

            with self.assertRaisesRegex(RuntimeError, "duplicate JSON key"):
                MODULE.normalize_ci_dotnet_coverage_reports(
                    json_report,
                    cobertura_report,
                    repository_root,
                )

    def test_ci_coverage_normalization_rejects_cobertura_root_outside_repository(
        self,
    ) -> None:
        with tempfile.TemporaryDirectory() as temporary:
            root = Path(temporary)
            repository_root = root / "repository"
            json_report, cobertura_report = self.write_ci_coverage_pair(
                repository_root / "artifacts/coverage",
                {},
                source_roots=(str(root / "outside"),),
                class_filenames=("Probe.cs",),
            )

            with self.assertRaisesRegex(RuntimeError, "not a unique"):
                MODULE.normalize_ci_dotnet_coverage_reports(
                    json_report,
                    cobertura_report,
                    repository_root,
                )

    def test_ci_coverage_normalization_accepts_deterministic_virtual_root(
        self,
    ) -> None:
        with tempfile.TemporaryDirectory() as temporary:
            repository_root = Path(temporary) / "repository"
            json_report, cobertura_report = self.write_ci_coverage_pair(
                repository_root / "artifacts/coverage",
                {
                    "Probe.dll": {
                        "/_/src/Probe/Probe.cs": {"Probe": {}},
                    }
                },
                source_roots=("\\",),
                class_filenames=("/_/src/Probe/Probe.cs",),
            )

            MODULE.normalize_ci_dotnet_coverage_reports(
                json_report,
                cobertura_report,
                repository_root,
            )

            normalized_json = json.loads(json_report.read_text(encoding="utf-8"))
            self.assertEqual(
                ["src/Probe/Probe.cs"],
                list(normalized_json["Probe.dll"]),
            )
            self.assertEqual(
                "src/Probe/Probe.cs",
                MODULE.ET.parse(cobertura_report)
                .getroot()
                .find(".//class")
                .get("filename"),
            )

    def test_ci_coverage_normalization_rejects_ambiguous_cobertura_roots(
        self,
    ) -> None:
        with tempfile.TemporaryDirectory() as temporary:
            repository_root = Path(temporary) / "repository"
            json_report, cobertura_report = self.write_ci_coverage_pair(
                repository_root / "artifacts/coverage",
                {},
                source_roots=(
                    str(repository_root / "src/First"),
                    str(repository_root / "src/Second"),
                ),
                class_filenames=("Probe.cs",),
            )

            with self.assertRaisesRegex(RuntimeError, "not a unique"):
                MODULE.normalize_ci_dotnet_coverage_reports(
                    json_report,
                    cobertura_report,
                    repository_root,
                )

    def test_ci_coverage_normalization_rejects_cobertura_source_aliases(
        self,
    ) -> None:
        with tempfile.TemporaryDirectory() as temporary:
            repository_root = Path(temporary) / "repository"
            source = repository_root / "src/Probe/Probe.cs"
            json_report, cobertura_report = self.write_ci_coverage_pair(
                repository_root / "artifacts/coverage",
                {},
                source_roots=(str(repository_root),),
                class_filenames=(str(source), "src/Probe/Probe.cs"),
            )

            with self.assertRaisesRegex(RuntimeError, "identity collides"):
                MODULE.normalize_ci_dotnet_coverage_reports(
                    json_report,
                    cobertura_report,
                    repository_root,
                )

    def test_ci_coverage_normalization_rejects_missing_cobertura_filename(
        self,
    ) -> None:
        with tempfile.TemporaryDirectory() as temporary:
            repository_root = Path(temporary) / "repository"
            json_report, cobertura_report = self.write_ci_coverage_pair(
                repository_root / "artifacts/coverage",
                {},
                class_filenames=(None,),
            )

            with self.assertRaisesRegex(RuntimeError, "missing its source filename"):
                MODULE.normalize_ci_dotnet_coverage_reports(
                    json_report,
                    cobertura_report,
                    repository_root,
                )

    def test_ci_coverage_normalization_rejects_multiple_source_containers(
        self,
    ) -> None:
        with tempfile.TemporaryDirectory() as temporary:
            repository_root = Path(temporary) / "repository"
            json_report, cobertura_report = self.write_ci_coverage_pair(
                repository_root / "artifacts/coverage",
                {},
                source_roots=(str(repository_root),),
                class_filenames=("src/Probe/Probe.cs",),
            )
            coverage = MODULE.ET.parse(cobertura_report)
            extra_sources = MODULE.ET.SubElement(coverage.getroot(), "sources")
            MODULE.ET.SubElement(extra_sources, "source").text = str(
                repository_root / "second-runner-root"
            )
            coverage.write(cobertura_report, encoding="utf-8", xml_declaration=True)

            with self.assertRaisesRegex(
                RuntimeError, "multiple source-root containers"
            ):
                MODULE.normalize_ci_dotnet_coverage_reports(
                    json_report,
                    cobertura_report,
                    repository_root,
                )

    def test_ci_project_evidence_hashes_normalized_coverage_bytes(self) -> None:
        project = MODULE.CiDotnetProject("tests/Probe/Probe.csproj")
        with tempfile.TemporaryDirectory() as temporary:
            repository_root = Path(temporary) / "repository"
            results = repository_root / "artifacts/ci-dotnet-work/results"
            discovery = results / "discovered-tests.txt"
            self.write_vstest_discovery(discovery, 1)
            self.write_ci_trx(results / "test-results.trx", total=1, skipped=0)
            source = repository_root / "src/Probe/Probe.cs"
            self.write_ci_coverage_pair(
                results / "collector",
                {"Probe.dll": {str(source): {"Probe": {}}}},
                source_roots=(str(repository_root),),
                class_filenames=("src/Probe/Probe.cs",),
            )

            with patch.object(MODULE, "ROOT", repository_root):
                row, paths = MODULE.collect_ci_project_evidence(
                    project,
                    results,
                    repository_root,
                    discovery,
                    "a" * 64,
                    "windows",
                )
            hashes = MODULE.ci_file_hashes(paths, repository_root)
            json_path = repository_root / str(row["coverageJson"])
            cobertura_path = repository_root / str(row["coverageCobertura"])

            self.assertNotIn(
                str(repository_root), json_path.read_text(encoding="utf-8")
            )
            self.assertNotIn(
                str(repository_root),
                cobertura_path.read_text(encoding="utf-8"),
            )
            self.assertEqual(MODULE.sha256_file(json_path), hashes[row["coverageJson"]])
            self.assertEqual(
                MODULE.sha256_file(cobertura_path),
                hashes[row["coverageCobertura"]],
            )

    def test_ci_project_evidence_collapses_only_identical_trx_attachment_copies(
        self,
    ) -> None:
        project = MODULE.CiDotnetProject("tests/Probe/Probe.csproj")
        with tempfile.TemporaryDirectory() as temporary:
            evidence_root = Path(temporary)
            results = evidence_root / "results"
            discovery = results / "discovered-tests.txt"
            self.write_vstest_discovery(discovery, 1)
            self.write_ci_trx(results / "test-results.trx", total=1, skipped=0)
            for relative in ("collector", "trx/In/machine"):
                parent = results / relative
                parent.mkdir(parents=True)
                (parent / "coverage.json").write_text("{}\n", encoding="utf-8")
                (parent / "coverage.cobertura.xml").write_text(
                    "<coverage />\n", encoding="utf-8"
                )

            row, paths = MODULE.collect_ci_project_evidence(
                project,
                results,
                evidence_root,
                discovery,
                "a" * 64,
                "windows",
            )

            self.assertEqual(4, len(paths))
            self.assertEqual(1, len(tuple(results.rglob("coverage.json"))))
            self.assertEqual(1, len(tuple(results.rglob("coverage.cobertura.xml"))))
            self.assertIn("collector/coverage.json", row["coverageJson"])

            duplicate = results / "trx/In/machine/coverage.json"
            duplicate.write_text('{"different": true}\n', encoding="utf-8")
            (duplicate.parent / "coverage.cobertura.xml").write_text(
                "<coverage />\n", encoding="utf-8"
            )
            with self.assertRaisesRegex(RuntimeError, "divergent coverage"):
                MODULE.collect_ci_project_evidence(
                    project,
                    results,
                    evidence_root,
                    discovery,
                    "a" * 64,
                    "windows",
                )

    def test_ci_project_evidence_rejects_reparse_points_before_reading(self) -> None:
        project = MODULE.CiDotnetProject("tests/Probe/Probe.csproj")
        with tempfile.TemporaryDirectory() as temporary:
            evidence_root = Path(temporary)
            results = evidence_root / "results"
            discovery = results / "discovered-tests.txt"
            self.write_vstest_discovery(discovery, 1)
            coverage = results / "collector/coverage.json"
            coverage.parent.mkdir(parents=True)
            coverage.write_text("{}\n", encoding="utf-8")

            with (
                patch.object(
                    MODULE,
                    "is_reparse_point",
                    side_effect=lambda path: path.name == "coverage.json",
                ),
                self.assertRaisesRegex(RuntimeError, "reparse-point"),
            ):
                MODULE.collect_ci_project_evidence(
                    project,
                    results,
                    evidence_root,
                    discovery,
                    "a" * 64,
                    "windows",
                )

    def test_ci_artifact_publication_copies_only_declared_regular_files(self) -> None:
        with tempfile.TemporaryDirectory() as temporary:
            root = Path(temporary)
            source = root / "work"
            upload = root / "upload"
            declared = source / "build/build.log"
            declared.parent.mkdir(parents=True)
            declared.write_text("build passed\n", encoding="utf-8")
            manifest = source / "build/manifest.json"
            manifest.write_text("{}\n", encoding="utf-8")
            (source / "private.bin").write_bytes(b"not upload evidence")
            hashes = {
                "build/build.log": hashlib.sha256(declared.read_bytes()).hexdigest()
            }

            with patch.object(MODULE, "ROOT", root):
                MODULE.publish_ci_dotnet_artifact(
                    source,
                    upload,
                    "build/manifest.json",
                    hashes,
                )

            self.assertEqual(
                {"build/build.log", "build/manifest.json"},
                {
                    path.relative_to(upload).as_posix()
                    for path in upload.rglob("*")
                    if path.is_file()
                },
            )
            self.assertFalse((upload / "private.bin").exists())

    def test_ci_dotnet_modes_are_explicit_and_mutually_exclusive(self) -> None:
        build = MODULE.parse_args(["--ci-dotnet-build"])
        shard = MODULE.parse_args(["--ci-dotnet-test-shard", "core"])
        finalize = MODULE.parse_args(
            ["--ci-dotnet-finalize", "artifacts/ci-dotnet-downloads"]
        )

        self.assertTrue(build.ci_dotnet_build)
        self.assertEqual("core", shard.ci_dotnet_test_shard)
        self.assertEqual(
            Path("artifacts/ci-dotnet-downloads"),
            finalize.ci_dotnet_finalize,
        )
        with self.assertRaisesRegex(SystemExit, "CI .NET modes cannot be combined"):
            MODULE.execute_verification(
                MODULE.parse_args(["--ci-dotnet-build", "--ci-dotnet-test-shard", "ui"])
            )

    def test_ci_dotnet_finalizer_fails_closed_when_evidence_is_missing(self) -> None:
        with tempfile.TemporaryDirectory() as temporary:
            with self.assertRaisesRegex(RuntimeError, r"missing.*\.NET CI evidence"):
                MODULE.finalize_ci_dotnet_evidence(Path(temporary))

    def test_ci_dotnet_finalizer_rejects_manifest_reparse_before_reading(self) -> None:
        source_sha = "7" * 40
        with tempfile.TemporaryDirectory() as temporary:
            root = Path(temporary)
            download_root = root / "ci-dotnet-downloads"
            self.stage_complete_ci_dotnet_evidence(download_root, source_sha)
            load_manifest = MagicMock()

            with (
                patch.dict(
                    os.environ,
                    {
                        "GITHUB_SHA": source_sha,
                        "NFC_CI_DOTNET_BUILD_RESULT": "success",
                        "NFC_CI_DOTNET_TEST_RESULT": "success",
                    },
                    clear=False,
                ),
                patch.object(
                    MODULE,
                    "is_reparse_point",
                    side_effect=lambda path: (
                        path.name == "manifest.json"
                        and "dotnet-build-evidence" in path.parts
                    ),
                ),
                patch.object(MODULE, "load_ci_manifest", load_manifest),
                self.assertRaisesRegex(RuntimeError, "reparse-point"),
            ):
                MODULE.finalize_ci_dotnet_evidence(download_root)

            load_manifest.assert_not_called()

    def test_ci_dotnet_finalizer_validates_exact_evidence_before_coverage(self) -> None:
        source_sha = "1" * 40
        with tempfile.TemporaryDirectory() as temporary:
            root = Path(temporary)
            download_root = root / "ci-dotnet-downloads"
            self.stage_complete_ci_dotnet_evidence(download_root, source_sha)
            events: list[str] = []
            with (
                patch.dict(
                    os.environ,
                    {
                        "GITHUB_SHA": source_sha,
                        "NFC_CI_DOTNET_BUILD_RESULT": "success",
                        "NFC_CI_DOTNET_TEST_RESULT": "success",
                    },
                    clear=False,
                ),
                patch.object(MODULE, "ROOT", root),
                patch.object(MODULE, "COVERAGE_ROOT", root / "coverage"),
                patch.object(MODULE, "repository_sdk_version", return_value="10.0.301"),
                patch.object(
                    MODULE,
                    "verify_coverage",
                    side_effect=lambda *_args: events.append("coverage"),
                ) as verify_coverage,
            ):
                MODULE.finalize_ci_dotnet_evidence(download_root)

            coverage_root = root / "coverage/dotnet"
            self.assertEqual(8, len(tuple(coverage_root.rglob("coverage.json"))))
            self.assertEqual(
                8, len(tuple(coverage_root.rglob("coverage.cobertura.xml")))
            )
            verify_coverage.assert_called_once_with("dotnet", coverage_root)
            self.assertEqual(["coverage"], events)

    def test_ci_dotnet_finalizer_rejects_hash_extra_and_job_result_drift(self) -> None:
        source_sha = "2" * 40
        mutations = ("hash", "extra", "job")
        for mutation in mutations:
            with (
                self.subTest(mutation=mutation),
                tempfile.TemporaryDirectory() as temporary,
            ):
                root = Path(temporary)
                download_root = root / "ci-dotnet-downloads"
                self.stage_complete_ci_dotnet_evidence(download_root, source_sha)
                build_result = "success"
                if mutation == "hash":
                    (
                        download_root / "dotnet-build-evidence/build/build.log"
                    ).write_text("mutated\n", encoding="utf-8")
                elif mutation == "extra":
                    (download_root / "dotnet-build-evidence/unexpected.txt").write_text(
                        "unexpected\n", encoding="utf-8"
                    )
                else:
                    build_result = "failure"
                with (
                    patch.dict(
                        os.environ,
                        {
                            "GITHUB_SHA": source_sha,
                            "NFC_CI_DOTNET_BUILD_RESULT": build_result,
                            "NFC_CI_DOTNET_TEST_RESULT": "success",
                        },
                        clear=False,
                    ),
                    patch.object(MODULE, "ROOT", root),
                    patch.object(MODULE, "COVERAGE_ROOT", root / "coverage"),
                    patch.object(
                        MODULE, "repository_sdk_version", return_value="10.0.301"
                    ),
                    patch.object(MODULE, "verify_coverage") as verify_coverage,
                    patch.object(MODULE, "run") as run_command,
                    self.assertRaises(RuntimeError),
                ):
                    MODULE.finalize_ci_dotnet_evidence(download_root)
                verify_coverage.assert_not_called()
                run_command.assert_not_called()

    def test_ci_dotnet_finalizer_rejects_cross_artifact_and_report_reuse(self) -> None:
        source_sha = "5" * 40
        mutations = ("cross-artifact", "reuse", "cross-parent")
        for mutation in mutations:
            with (
                self.subTest(mutation=mutation),
                tempfile.TemporaryDirectory() as temporary,
            ):
                root = Path(temporary)
                download_root = root / "ci-dotnet-downloads"
                self.stage_complete_ci_dotnet_evidence(download_root, source_sha)
                core_manifest_path = (
                    download_root
                    / "dotnet-test-core-evidence/shards/core/manifest.json"
                )
                if mutation == "cross-artifact":
                    collision = (
                        download_root / "dotnet-test-ui-evidence/build/build.log"
                    )
                    collision.parent.mkdir(parents=True)
                    collision.write_text("collision\n", encoding="utf-8")
                else:
                    core_manifest = json.loads(
                        core_manifest_path.read_text(encoding="utf-8")
                    )
                    first, second = core_manifest["projects"][:2]
                    if mutation == "reuse":
                        second["coverageJson"] = first["coverageJson"]
                        second["coverageCobertura"] = first["coverageCobertura"]
                    else:
                        first["coverageCobertura"] = second["coverageCobertura"]
                    self.write_ci_manifest(core_manifest_path, core_manifest)
                with (
                    patch.dict(
                        os.environ,
                        {
                            "GITHUB_SHA": source_sha,
                            "NFC_CI_DOTNET_BUILD_RESULT": "success",
                            "NFC_CI_DOTNET_TEST_RESULT": "success",
                        },
                        clear=False,
                    ),
                    patch.object(MODULE, "ROOT", root),
                    patch.object(
                        MODULE, "repository_sdk_version", return_value="10.0.301"
                    ),
                    patch.object(MODULE, "verify_coverage") as verify_coverage,
                    patch.object(MODULE, "run") as run_command,
                    self.assertRaises(RuntimeError),
                ):
                    MODULE.finalize_ci_dotnet_evidence(download_root)
                verify_coverage.assert_not_called()
                run_command.assert_not_called()

    def test_ci_dotnet_finalizer_rejects_discovery_schema_platform_and_assembly_drift(
        self,
    ) -> None:
        source_sha = "8" * 40
        mutations = (
            "missing-discovery",
            "tampered-discovery",
            "wrong-discovery-path",
            "legacy-schema",
            "wrong-producer",
            "invalid-assembly-sha",
        )
        for mutation in mutations:
            with (
                self.subTest(mutation=mutation),
                tempfile.TemporaryDirectory() as temporary,
            ):
                root = Path(temporary)
                download_root = root / "ci-dotnet-downloads"
                self.stage_complete_ci_dotnet_evidence(download_root, source_sha)
                artifact_root = download_root / "dotnet-test-core-evidence"
                manifest_path = artifact_root / "shards/core/manifest.json"
                manifest = json.loads(manifest_path.read_text(encoding="utf-8"))
                first = manifest["projects"][0]
                discovery = artifact_root / first["discovery"]
                if mutation == "missing-discovery":
                    discovery.unlink()
                elif mutation == "tampered-discovery":
                    discovery.write_text("tampered\n", encoding="utf-8")
                elif mutation == "wrong-discovery-path":
                    first["discovery"] = manifest["projects"][1]["discovery"]
                    self.write_ci_manifest(manifest_path, manifest)
                elif mutation == "legacy-schema":
                    manifest["schemaVersion"] = 1
                    self.write_ci_manifest(manifest_path, manifest)
                elif mutation == "wrong-producer":
                    manifest["producerPlatform"] = "non-windows"
                    self.write_ci_manifest(manifest_path, manifest)
                else:
                    first["testAssemblySha256"] = "0" * 63
                    self.write_ci_manifest(manifest_path, manifest)
                with (
                    patch.dict(
                        os.environ,
                        {
                            "GITHUB_SHA": source_sha,
                            "NFC_CI_DOTNET_BUILD_RESULT": "success",
                            "NFC_CI_DOTNET_TEST_RESULT": "success",
                        },
                        clear=False,
                    ),
                    patch.object(MODULE, "ROOT", root),
                    patch.object(MODULE, "COVERAGE_ROOT", root / "coverage"),
                    patch.object(
                        MODULE, "repository_sdk_version", return_value="10.0.301"
                    ),
                    patch.object(MODULE, "verify_coverage") as verify_coverage,
                    self.assertRaises(RuntimeError),
                ):
                    MODULE.finalize_ci_dotnet_evidence(download_root)
                verify_coverage.assert_not_called()

    def test_ci_dotnet_finalizer_reaches_coverage_after_closed_evidence_validation(
        self,
    ) -> None:
        source_sha = "6" * 40
        with tempfile.TemporaryDirectory() as temporary:
            root = Path(temporary)
            download_root = root / "ci-dotnet-downloads"
            self.stage_complete_ci_dotnet_evidence(download_root, source_sha)
            events: list[str] = []

            def fail_coverage(*_args: object) -> None:
                events.append("coverage")
                raise RuntimeError("coverage probe")

            with (
                patch.dict(
                    os.environ,
                    {
                        "GITHUB_SHA": source_sha,
                        "NFC_CI_DOTNET_BUILD_RESULT": "success",
                        "NFC_CI_DOTNET_TEST_RESULT": "success",
                    },
                    clear=False,
                ),
                patch.object(MODULE, "ROOT", root),
                patch.object(MODULE, "COVERAGE_ROOT", root / "coverage"),
                patch.object(MODULE, "repository_sdk_version", return_value="10.0.301"),
                patch.object(MODULE, "verify_coverage", side_effect=fail_coverage),
                self.assertRaisesRegex(RuntimeError, "coverage probe"),
            ):
                MODULE.finalize_ci_dotnet_evidence(download_root)

            self.assertEqual(["coverage"], events)

    def test_ci_dotnet_shard_continues_after_ordinary_project_failure(self) -> None:
        first = MODULE.CiDotnetProject("tests/First/First.csproj")
        second = MODULE.CiDotnetProject("tests/Second/Second.csproj")
        commands: list[list[str]] = []
        restore = MagicMock()

        def fake_run(command: list[str], **_kwargs: object) -> None:
            commands.append(command)
            if (
                len(command) > 2
                and command[1] == "build"
                and "First.csproj" in command[2]
            ):
                raise subprocess.CalledProcessError(1, command)

        with tempfile.TemporaryDirectory() as temporary:
            root = Path(temporary)
            evidence_root = root / "artifacts/ci-dotnet-work"
            adapter = root / "adapter"
            resolve_adapter = MagicMock(return_value=adapter)
            output = root / "output"
            output.mkdir()
            (output / f"{second.name}.dll").write_bytes(b"test assembly")
            with (
                patch.dict(os.environ, {"GITHUB_SHA": "3" * 40}, clear=False),
                patch.object(MODULE, "ROOT", root),
                patch.object(MODULE, "SOLUTION", root / "NvtFwCombiner.slnx"),
                patch.object(MODULE, "CI_DOTNET_EVIDENCE_ROOT", evidence_root),
                patch.object(
                    MODULE,
                    "CI_DOTNET_UPLOAD_ROOT",
                    root / "artifacts/ci-dotnet-upload",
                ),
                patch.dict(MODULE.CI_DOTNET_SHARDS, {"probe": (first, second)}),
                patch.object(MODULE, "resolve_dotnet", return_value="dotnet"),
                patch.object(
                    MODULE,
                    "resolve_coverlet_adapter_path",
                    resolve_adapter,
                ),
                patch.object(MODULE, "repository_sdk_version", return_value="10.0.301"),
                patch.object(MODULE, "require_logged_sdk_version"),
                patch.object(MODULE, "run", side_effect=fake_run),
                patch.object(
                    MODULE,
                    "run_solution_restore_preserving_lock_projections",
                    restore,
                ),
                patch.object(
                    MODULE,
                    "find_project_release_output",
                    return_value=(output, Path("bin/Release/net10.0")),
                ),
                patch.object(
                    MODULE,
                    "cleanup_dotnet_batch",
                    side_effect=RuntimeError("cleanup probe"),
                ),
                patch.object(
                    MODULE,
                    "collect_ci_project_evidence",
                    return_value=(
                        {
                            "relativePath": second.relative_path,
                            "total": 1,
                            "passed": 1,
                            "failed": 0,
                            "skipped": 0,
                            "testAssemblySha256": "a" * 64,
                            "discovery": "discovery",
                            "trx": "trx",
                            "coverageJson": "json",
                            "coverageCobertura": "xml",
                        },
                        (),
                    ),
                ),
                self.assertRaisesRegex(
                    RuntimeError,
                    "First.*cleanup also failed.*cleanup probe",
                ),
            ):
                MODULE.verify_ci_dotnet_test_shard("probe")

        resolve_adapter.assert_called_once_with(root)
        restore.assert_called_once()
        self.assertEqual(
            ["dotnet", "restore", str(root / "NvtFwCombiner.slnx")],
            restore.call_args.args[0],
        )
        self.assertEqual(
            str(root),
            restore.call_args.kwargs["environment"][
                MODULE.TEST_REPOSITORY_ROOT_ENVIRONMENT_VARIABLE
            ],
        )
        self.assertEqual(
            evidence_root / "shards/probe/shard.log",
            restore.call_args.kwargs["log_path"],
        )
        self.assertEqual(root, restore.call_args.kwargs["repository_root"])
        self.assertEqual(
            root / "NvtFwCombiner.slnx", restore.call_args.kwargs["solution"]
        )
        build_commands = [command for command in commands if "build" in command]
        self.assertEqual(2, len(build_commands))
        self.assertIn("First.csproj", build_commands[0][2])
        self.assertIn("Second.csproj", build_commands[1][2])
        vstest_commands = [command for command in commands if command[1] == "vstest"]
        self.assertEqual(2, len(vstest_commands))
        expected_assembly = str(output / "Second.dll")
        self.assertTrue(all(command[2] == expected_assembly for command in vstest_commands))
        execution_commands = [
            command
            for command in vstest_commands
            if "--Collect:XPlat Code Coverage" in command
        ]
        self.assertEqual(1, len(execution_commands))
        self.assertIn(f"--TestAdapterPath:{adapter}", execution_commands[0])

    def test_ci_dotnet_shard_rejects_snapshot_hash_drift_before_evidence(self) -> None:
        project = MODULE.CiDotnetProject("tests/Probe/Probe.csproj")
        with tempfile.TemporaryDirectory() as temporary:
            root = Path(temporary)
            collect = MagicMock()
            resolve_adapter = MagicMock(return_value=root / "adapter")
            output = root / "output"
            output.mkdir()
            (output / f"{project.name}.dll").write_bytes(b"test assembly")
            with (
                patch.dict(os.environ, {"GITHUB_SHA": "9" * 40}, clear=False),
                patch.object(MODULE, "ROOT", root),
                patch.object(MODULE, "SOLUTION", root / "NvtFwCombiner.slnx"),
                patch.object(
                    MODULE,
                    "CI_DOTNET_EVIDENCE_ROOT",
                    root / "artifacts/ci-dotnet-work",
                ),
                patch.object(
                    MODULE,
                    "CI_DOTNET_UPLOAD_ROOT",
                    root / "artifacts/ci-dotnet-upload",
                ),
                patch.dict(MODULE.CI_DOTNET_SHARDS, {"probe": (project,)}),
                patch.object(MODULE, "resolve_dotnet", return_value="dotnet"),
                patch.object(
                    MODULE,
                    "resolve_coverlet_adapter_path",
                    resolve_adapter,
                ),
                patch.object(MODULE, "repository_sdk_version", return_value="10.0.301"),
                patch.object(MODULE, "require_logged_sdk_version"),
                patch.object(MODULE, "run"),
                patch.object(
                    MODULE, "run_solution_restore_preserving_lock_projections"
                ),
                patch.object(
                    MODULE,
                    "find_project_release_output",
                    return_value=(output, Path("bin/Release/net10.0")),
                ),
                patch.object(
                    MODULE,
                    "require_regular_tree_hashes",
                    side_effect=RuntimeError("snapshot hash drift"),
                ),
                patch.object(MODULE, "collect_ci_project_evidence", collect),
                patch.object(MODULE, "cleanup_dotnet_batch"),
                patch.object(MODULE, "ci_file_hashes", return_value={}),
                patch.object(MODULE, "publish_ci_dotnet_artifact"),
                self.assertRaisesRegex(RuntimeError, "snapshot hash drift"),
            ):
                MODULE.verify_ci_dotnet_test_shard("probe")

            resolve_adapter.assert_called_once_with(root)
            collect.assert_not_called()

    def test_ci_dotnet_shard_timeout_stops_before_the_next_project(self) -> None:
        first = MODULE.CiDotnetProject("tests/First/First.csproj")
        second = MODULE.CiDotnetProject("tests/Second/Second.csproj")
        commands: list[list[str]] = []

        def fake_run(command: list[str], **_kwargs: object) -> None:
            commands.append(command)
            if len(command) > 1 and command[1] == "build":
                raise subprocess.TimeoutExpired(command, 30)

        with tempfile.TemporaryDirectory() as temporary:
            root = Path(temporary)
            resolve_adapter = MagicMock(return_value=root / "adapter")
            with (
                patch.dict(os.environ, {"GITHUB_SHA": "4" * 40}, clear=False),
                patch.object(MODULE, "ROOT", root),
                patch.object(MODULE, "SOLUTION", root / "NvtFwCombiner.slnx"),
                patch.object(
                    MODULE,
                    "CI_DOTNET_EVIDENCE_ROOT",
                    root / "artifacts/ci-dotnet-work",
                ),
                patch.object(
                    MODULE,
                    "CI_DOTNET_UPLOAD_ROOT",
                    root / "artifacts/ci-dotnet-upload",
                ),
                patch.dict(MODULE.CI_DOTNET_SHARDS, {"probe": (first, second)}),
                patch.object(MODULE, "resolve_dotnet", return_value="dotnet"),
                patch.object(MODULE, "repository_sdk_version", return_value="10.0.301"),
                patch.object(MODULE, "run", side_effect=fake_run),
                patch.object(
                    MODULE, "run_solution_restore_preserving_lock_projections"
                ),
                patch.object(
                    MODULE,
                    "resolve_coverlet_adapter_path",
                    resolve_adapter,
                ),
                patch.object(MODULE, "cleanup_dotnet_batch"),
                self.assertRaises(subprocess.TimeoutExpired),
            ):
                MODULE.verify_ci_dotnet_test_shard("probe")

        resolve_adapter.assert_called_once_with(root)
        self.assertEqual(1, sum(command[1] == "build" for command in commands))

    def test_parser_defaults_to_bounded_parallelism_and_rejects_excessive_jobs(
        self,
    ) -> None:
        parsed = MODULE.parse_args([])
        self.assertEqual(3, parsed.jobs)
        self.assertEqual(900, parsed.lane_timeout_seconds)
        self.assertEqual(
            600,
            MODULE.parse_args(["--lane-timeout-seconds", "600"]).lane_timeout_seconds,
        )
        with contextlib.redirect_stderr(io.StringIO()):
            with self.assertRaises(SystemExit):
                MODULE.parse_args(["--jobs", "4"])
            with self.assertRaises(SystemExit):
                MODULE.parse_args(["--lane-timeout-seconds", "59"])
            with self.assertRaises(SystemExit):
                MODULE.parse_args(["--lane-timeout-seconds", "901"])

    def test_local_public_session_requires_an_explicit_absolute_test_area(
        self,
    ) -> None:
        with self.verifier_environment():
            with self.assertRaisesRegex(RuntimeError, "NFC_TEST_AREA_ROOT"):
                with MODULE.verification_test_session(internal_lane=False):
                    self.fail("missing test area must fail closed")

        with self.verifier_environment(NFC_TEST_AREA_ROOT="relative-test-area"):
            with self.assertRaisesRegex(RuntimeError, "absolute"):
                with MODULE.verification_test_session(internal_lane=False):
                    self.fail("relative test area must fail closed")

    def test_test_area_normalizes_dot_segments_before_session_creation(self) -> None:
        with tempfile.TemporaryDirectory() as temporary:
            root = Path(temporary) / "declared"
            nested = root / "nested"
            nested.mkdir(parents=True)
            declared = nested / ".."
            with self.verifier_environment(NFC_TEST_AREA_ROOT=str(declared)):
                with MODULE.verification_test_session(internal_lane=False) as session:
                    self.assertEqual(
                        root.resolve(), Path(os.environ["NFC_TEST_AREA_ROOT"])
                    )
                    self.assertEqual(root.resolve() / "sessions", session.parent)
                    self.assertNotIn("..", session.parts)

    def test_main_reports_test_area_admission_failure_without_a_traceback(self) -> None:
        environment = os.environ.copy()
        environment["NFC_TEST_AREA_ROOT"] = "relative-test-area"
        environment.pop("NFC_TEST_SESSION_ROOT", None)
        result = subprocess.run(
            [sys.executable, str(SCRIPT), "--structure-only"],
            cwd=MODULE.ROOT,
            env=environment,
            text=True,
            capture_output=True,
            check=False,
        )

        self.assertEqual(1, result.returncode)
        self.assertIn("VERIFICATION FAILED", result.stderr)
        self.assertNotIn("Traceback", result.stderr)

    def test_session_setup_failure_removes_the_exact_unmarked_child(self) -> None:
        with tempfile.TemporaryDirectory() as temporary:
            root = Path(temporary)
            with (
                self.verifier_environment(NFC_TEST_AREA_ROOT=str(root)),
                patch.object(
                    MODULE,
                    "test_session_marker_bytes",
                    side_effect=RuntimeError("marker setup probe"),
                ),
                self.assertRaisesRegex(RuntimeError, "marker setup probe"),
            ):
                with MODULE.verification_test_session(internal_lane=False):
                    self.fail("session setup failure must not enter the workload")

            self.assertEqual((), tuple((root / "sessions").iterdir()))

    def test_session_identity_probe_failure_removes_the_exact_unmarked_child(
        self,
    ) -> None:
        with tempfile.TemporaryDirectory() as temporary:
            root = Path(temporary)
            actual_identity = MODULE.filesystem_identity
            failed = False

            def fail_first_session_probe(path: Path) -> tuple[int, int]:
                nonlocal failed
                if path.name.startswith("s-") and not failed:
                    failed = True
                    raise RuntimeError("identity setup probe")
                return actual_identity(path)

            with (
                self.verifier_environment(NFC_TEST_AREA_ROOT=str(root)),
                patch.object(
                    MODULE,
                    "filesystem_identity",
                    side_effect=fail_first_session_probe,
                ),
                self.assertRaisesRegex(RuntimeError, "identity setup probe"),
            ):
                with MODULE.verification_test_session(internal_lane=False):
                    self.fail("session setup failure must not enter the workload")

            self.assertEqual((), tuple((root / "sessions").iterdir()))

    @unittest.skipUnless(sys.platform == "win32", "Windows custody contract")
    def test_provisional_session_reacquire_race_retains_marker_and_replacement(
        self,
    ) -> None:
        with tempfile.TemporaryDirectory() as temporary:
            base = Path(temporary)
            root = base / "test-area"
            root.mkdir()
            external = base / "external"
            external.mkdir()
            (external / "sentinel").write_text("external", encoding="utf-8")
            actual_identity = MODULE.filesystem_identity
            identity_failed = False
            replacement_blocked = False
            replacement_succeeded = False
            displaced: Path | None = None
            session_path: Path | None = None

            def fail_session_probe(path: Path) -> tuple[int, int]:
                nonlocal identity_failed
                if (
                    path.parent.name == "sessions"
                    and path.name.startswith("s-")
                    and not identity_failed
                ):
                    identity_failed = True
                    raise RuntimeError("identity setup probe")
                return actual_identity(path)

            def replace_before_session_reacquire(
                path: Path,
                expected_identity: tuple[int, int],
                *,
                share_write: bool = True,
                read_data: bool = False,
            ) -> tuple[int, int, bool]:
                nonlocal displaced
                nonlocal replacement_blocked
                nonlocal replacement_succeeded
                nonlocal session_path
                if (
                    path.parent.name == "sessions"
                    and path.name.startswith("s-")
                    and session_path is None
                ):
                    displaced = path.with_name(f"{path.name}-original")
                    session_path = path
                    try:
                        path.rename(displaced)
                    except OSError:
                        replacement_blocked = True
                    else:
                        replacement_succeeded = True
                    raise RuntimeError("session reacquire probe")
                self.fail(
                    f"unexpected cleanup handle request during transition: {path}"
                )

            with (
                self.verifier_environment(NFC_TEST_AREA_ROOT=str(root)),
                patch.object(
                    MODULE,
                    "filesystem_identity",
                    side_effect=fail_session_probe,
                ),
                patch.object(
                    MODULE,
                    "_require_windows_cleanup_handle",
                    side_effect=replace_before_session_reacquire,
                ),
                self.assertRaisesRegex(RuntimeError, "diagnostic residue"),
            ):
                with MODULE.verification_test_session(internal_lane=False):
                    self.fail("session setup race must not enter the workload")

            self.assertTrue(replacement_blocked)
            self.assertFalse(replacement_succeeded)
            assert displaced is not None
            assert session_path is not None
            marker = session_path / MODULE.TEST_SESSION_MARKER_NAME
            self.assertTrue(marker.is_file())
            self.assertEqual(
                session_path.name, json.loads(marker.read_text())["sessionId"]
            )
            self.assertFalse(displaced.exists())
            self.assertEqual(
                "external",
                (external / "sentinel").read_text(encoding="utf-8"),
            )

    @unittest.skipUnless(sys.platform == "win32", "Windows custody contract")
    def test_session_setup_retains_parent_custody_through_marker_write(self) -> None:
        with tempfile.TemporaryDirectory() as temporary:
            base = Path(temporary)
            root = base / "test-area"
            root.mkdir()
            external = base / "external"
            external.mkdir()
            (external / "sentinel").write_text("external", encoding="utf-8")
            actual_marker_bytes = MODULE.test_session_marker_bytes
            displaced = root / "sessions-displaced"
            parent_swap_blocked = False

            def attempt_parent_swap(owner: Path, session: Path) -> bytes:
                nonlocal parent_swap_blocked
                sessions = owner / "sessions"
                try:
                    sessions.rename(displaced)
                except OSError:
                    parent_swap_blocked = True
                else:
                    displaced.rename(sessions)
                return actual_marker_bytes(owner, session)

            with (
                self.verifier_environment(NFC_TEST_AREA_ROOT=str(root)),
                patch.object(
                    MODULE,
                    "test_session_marker_bytes",
                    side_effect=attempt_parent_swap,
                ),
            ):
                with MODULE.verification_test_session(internal_lane=False):
                    pass

            self.assertTrue(parent_swap_blocked)
            self.assertEqual((), tuple((root / "sessions").iterdir()))
            self.assertEqual(
                "external", (external / "sentinel").read_text(encoding="utf-8")
            )
            self.assertFalse((external / MODULE.TEST_SESSION_MARKER_NAME).exists())

    @unittest.skipUnless(sys.platform == "win32", "Windows custody contract")
    def test_sessions_root_creation_is_handle_relative_during_parent_swap(
        self,
    ) -> None:
        with tempfile.TemporaryDirectory() as temporary:
            base = Path(temporary)
            root = base / "test-area"
            root.mkdir()
            external = base / "external"
            external.mkdir()
            (external / "sentinel").write_text("external", encoding="utf-8")
            displaced = base / "test-area-displaced"
            actual_create_relative = MODULE._create_windows_relative_path
            parent_swapped = False
            external_write_seen = False

            def swap_before_sessions_creation(
                root_handle: int,
                name: str,
                *,
                directory: bool,
            ) -> int:
                nonlocal parent_swapped
                nonlocal external_write_seen
                if name == "sessions" and not parent_swapped:
                    root.rename(displaced)
                    external.rename(root)
                    parent_swapped = True
                handle = actual_create_relative(
                    root_handle,
                    name,
                    directory=directory,
                )
                if parent_swapped:
                    external_write_seen = (root / "sessions").exists()
                return handle

            try:
                with (
                    self.verifier_environment(NFC_TEST_AREA_ROOT=str(root)),
                    patch.object(
                        MODULE,
                        "_create_windows_relative_path",
                        side_effect=swap_before_sessions_creation,
                    ),
                    self.assertRaisesRegex(RuntimeError, "identity"),
                ):
                    with MODULE.verification_test_session(internal_lane=False):
                        self.fail("parent replacement must fail before workload")
            finally:
                if parent_swapped:
                    root.rename(external)
                    displaced.rename(root)

            self.assertTrue(parent_swapped)
            self.assertFalse(external_write_seen)
            self.assertEqual(
                "external", (external / "sentinel").read_text(encoding="utf-8")
            )

    @unittest.skipUnless(sys.platform == "win32", "Windows custody contract")
    def test_workload_custody_blocks_session_and_sessions_root_replacement(
        self,
    ) -> None:
        for target_kind in ("session", "sessions-root"):
            with self.subTest(target_kind=target_kind):
                with tempfile.TemporaryDirectory() as temporary:
                    base = Path(temporary)
                    root = base / "test-area"
                    root.mkdir()
                    stale = root / "sessions" / "stale"
                    stale.mkdir(parents=True)
                    (stale / "sentinel").write_text("stale", encoding="utf-8")
                    external = base / "external"
                    external.mkdir()
                    (external / "sentinel").write_text("external", encoding="utf-8")
                    session: Path | None = None
                    displaced: Path | None = None
                    escaped_write: Path | None = None
                    replacement_blocked = False
                    cleanup_error: BaseException | None = None
                    try:
                        with self.verifier_environment(NFC_TEST_AREA_ROOT=str(root)):
                            try:
                                with MODULE.verification_test_session(
                                    internal_lane=False
                                ) as session:
                                    if target_kind == "session":
                                        displaced = session.with_name(
                                            f"{session.name}-displaced"
                                        )
                                        replacement = session
                                        external_temp = external / "temp"
                                    else:
                                        sessions_root = session.parent
                                        displaced = root / "sessions-displaced"
                                        replacement = sessions_root
                                        external_temp = external / session.name / "temp"
                                    external_temp.mkdir(parents=True)
                                    escaped_write = (
                                        external_temp / "first-workload-write.txt"
                                    )
                                    try:
                                        replacement.rename(displaced)
                                    except OSError:
                                        replacement_blocked = True
                                    else:
                                        subprocess.run(
                                            [
                                                "cmd",
                                                "/c",
                                                "mklink",
                                                "/J",
                                                str(replacement),
                                                str(external),
                                            ],
                                            check=True,
                                            capture_output=True,
                                            text=True,
                                        )
                                        Path(os.environ["TEMP"]).joinpath(
                                            "first-workload-write.txt"
                                        ).write_text("escaped", encoding="utf-8")
                            except BaseException as error:
                                cleanup_error = error
                    finally:
                        if not replacement_blocked and displaced is not None:
                            replacement = (
                                session
                                if target_kind == "session"
                                else root / "sessions"
                            )
                            if replacement.is_junction():
                                os.rmdir(replacement)
                            displaced.rename(replacement)
                            assert session is not None
                            MODULE.cleanup_test_session(
                                MODULE.validate_test_session(root, session)
                            )

                    self.assertTrue(replacement_blocked)
                    self.assertIsNone(cleanup_error)
                    assert escaped_write is not None
                    self.assertFalse(escaped_write.exists())
                    self.assertEqual(
                        "external",
                        (external / "sentinel").read_text(encoding="utf-8"),
                    )
                    self.assertEqual(
                        "stale", (stale / "sentinel").read_text(encoding="utf-8")
                    )

    def test_termination_boundary_wraps_session_setup_workload_and_cleanup(
        self,
    ) -> None:
        events: list[str] = []
        termination_active = False

        @contextlib.contextmanager
        def termination_boundary():
            nonlocal termination_active
            termination_active = True
            events.append("termination-enter")
            try:
                yield
            finally:
                events.append("termination-exit")
                termination_active = False

        @contextlib.contextmanager
        def session_boundary(*, internal_lane: bool):
            self.assertFalse(internal_lane)
            self.assertTrue(termination_active)
            events.append("session-enter")
            try:
                yield Path("session")
            finally:
                self.assertTrue(termination_active)
                events.append("session-exit")

        with (
            patch.object(
                MODULE,
                "parse_args",
                return_value=MagicMock(internal_lane=None),
            ),
            patch.object(MODULE, "handle_external_termination", termination_boundary),
            patch.object(MODULE, "verification_test_session", session_boundary),
            patch.object(MODULE, "execute_verification", return_value=0),
        ):
            self.assertEqual(0, MODULE.main())

        self.assertEqual(
            [
                "termination-enter",
                "session-enter",
                "session-exit",
                "termination-exit",
            ],
            events,
        )

    def test_sigterm_during_scratch_setup_cleans_the_owned_session(self) -> None:
        with tempfile.TemporaryDirectory() as temporary:
            root = Path(temporary)
            real_mkdir = MODULE.Path.mkdir

            def interrupt_scratch(path: Path, *args: object, **kwargs: object):
                result = real_mkdir(path, *args, **kwargs)
                if path.name == "ruff-cache":
                    signal.raise_signal(signal.SIGTERM)
                return result

            with (
                self.verifier_environment(NFC_TEST_AREA_ROOT=str(root)),
                patch.object(
                    MODULE,
                    "parse_args",
                    return_value=MagicMock(internal_lane=None),
                ),
                patch.object(
                    MODULE.Path, "mkdir", autospec=True, side_effect=interrupt_scratch
                ),
                patch.object(MODULE, "execute_verification") as execute,
            ):
                result = MODULE.main()

            self.assertEqual(128 + signal.SIGTERM, result)
            execute.assert_not_called()
            self.assertEqual((), tuple((root / "sessions").iterdir()))

    def test_sigterm_during_cleanup_is_deferred_until_exact_session_removal(
        self,
    ) -> None:
        with tempfile.TemporaryDirectory() as temporary:
            root = Path(temporary)
            interrupted = False
            real_delete = MODULE._delete_windows_session_entry

            def interrupt_cleanup(path: Path) -> None:
                nonlocal interrupted
                if not interrupted:
                    interrupted = True
                    signal.raise_signal(signal.SIGTERM)
                real_delete(path)

            def create_cleanup_probe(_args: argparse.Namespace) -> int:
                session = Path(os.environ["NFC_TEST_SESSION_ROOT"])
                (session / "cleanup-probe.txt").write_text("probe", encoding="utf-8")
                return 0

            with (
                self.verifier_environment(NFC_TEST_AREA_ROOT=str(root)),
                patch.object(
                    MODULE,
                    "parse_args",
                    return_value=MagicMock(internal_lane=None),
                ),
                patch.object(
                    MODULE,
                    "execute_verification",
                    side_effect=create_cleanup_probe,
                ),
                patch.object(
                    MODULE,
                    "_delete_windows_session_entry",
                    side_effect=interrupt_cleanup,
                ),
            ):
                result = MODULE.main()

            self.assertTrue(interrupted)
            self.assertEqual(128 + signal.SIGTERM, result)
            self.assertEqual((), tuple((root / "sessions").iterdir()))

    def test_public_session_rejects_an_externally_supplied_session(self) -> None:
        with tempfile.TemporaryDirectory() as temporary:
            root = Path(temporary)
            with self.verifier_environment(
                NFC_TEST_AREA_ROOT=str(root),
                NFC_TEST_SESSION_ROOT=str(root / "sessions" / "external"),
            ):
                with self.assertRaisesRegex(RuntimeError, "NFC_TEST_SESSION_ROOT"):
                    with MODULE.verification_test_session(internal_lane=False):
                        self.fail("public callers must not select a session")

    def test_local_test_area_rejects_files_roots_and_repository_overlap(self) -> None:
        with tempfile.TemporaryDirectory() as temporary:
            root = Path(temporary)
            file_root = root / "not-a-directory"
            file_root.write_text("probe", encoding="utf-8")
            candidates = (
                (file_root, "directory"),
                (Path(root.anchor), "filesystem root"),
                (MODULE.ROOT.parent, "overlap"),
                (MODULE.ROOT / "tests", "overlap"),
            )
            for candidate, expected in candidates:
                with (
                    self.subTest(candidate=candidate),
                    self.verifier_environment(NFC_TEST_AREA_ROOT=str(candidate)),
                    self.assertRaisesRegex(RuntimeError, expected),
                ):
                    with MODULE.verification_test_session(internal_lane=False):
                        self.fail("unsafe test area must fail closed")

    def test_test_area_rejects_an_existing_reparse_component_before_writing(
        self,
    ) -> None:
        with tempfile.TemporaryDirectory() as temporary:
            root = Path(temporary)
            with (
                self.verifier_environment(NFC_TEST_AREA_ROOT=str(root)),
                patch.object(
                    MODULE,
                    "is_reparse_point",
                    side_effect=lambda path: path == root,
                ),
                self.assertRaisesRegex(RuntimeError, "reparse"),
            ):
                with MODULE.verification_test_session(internal_lane=False):
                    self.fail("reparse test area must fail closed")
            self.assertFalse((root / "sessions").exists())

    def test_reparse_detection_uses_the_windows_file_attribute(self) -> None:
        status = MagicMock(
            st_mode=MODULE.stat.S_IFDIR,
            st_file_attributes=MODULE.stat.FILE_ATTRIBUTE_REPARSE_POINT,
        )
        with (
            patch.object(MODULE.os, "lstat", return_value=status),
            patch.object(MODULE.Path, "is_symlink", return_value=False),
            patch.object(MODULE.Path, "is_junction", return_value=False),
        ):
            self.assertTrue(MODULE.is_reparse_point(Path("attribute-probe")))

    def test_reparse_probe_errors_fail_closed_before_test_area_writes(self) -> None:
        for error in (PermissionError("denied"), OSError("probe failed")):
            with self.subTest(error=type(error).__name__):
                with tempfile.TemporaryDirectory() as temporary:
                    root = Path(temporary)
                    with (
                        self.verifier_environment(NFC_TEST_AREA_ROOT=str(root)),
                        patch.object(MODULE.os, "lstat", side_effect=error),
                        self.assertRaises(type(error)),
                    ):
                        with MODULE.verification_test_session(internal_lane=False):
                            self.fail("reparse probe failure must stop before writes")

                    self.assertFalse((root / "sessions").exists())

    @unittest.skipUnless(sys.platform == "win32", "Windows junction contract")
    def test_cleanup_rejects_a_real_descendant_junction_without_touching_target(
        self,
    ) -> None:
        with tempfile.TemporaryDirectory() as temporary:
            root = Path(temporary)
            external = root / "external"
            external.mkdir()
            (external / "sentinel").write_text("external", encoding="utf-8")
            with (
                self.verifier_environment(NFC_TEST_AREA_ROOT=str(root)),
                self.assertRaisesRegex(RuntimeError, "reparse"),
            ):
                with MODULE.verification_test_session(internal_lane=False) as session:
                    junction = session / "junction"
                    subprocess.run(
                        ["cmd", "/c", "mklink", "/J", str(junction), str(external)],
                        check=True,
                        capture_output=True,
                        text=True,
                    )

            self.assertEqual(
                "external", (external / "sentinel").read_text(encoding="utf-8")
            )
            self.assertTrue(junction.is_junction())
            os.rmdir(junction)

    @unittest.skipUnless(sys.platform == "win32", "Windows cleanup contract")
    def test_windows_session_cleanup_never_uses_broad_rmtree(self) -> None:
        with tempfile.TemporaryDirectory() as temporary:
            root = Path(temporary)
            with (
                self.verifier_environment(NFC_TEST_AREA_ROOT=str(root)),
                patch.object(
                    MODULE.shutil,
                    "rmtree",
                    side_effect=AssertionError("broad rmtree must not run"),
                ),
            ):
                with MODULE.verification_test_session(internal_lane=False) as session:
                    (session / "scratch.txt").write_text("scratch", encoding="utf-8")

            self.assertEqual((), tuple((root / "sessions").iterdir()))

    @unittest.skipUnless(sys.platform == "win32", "Windows readonly contract")
    def test_windows_cleanup_removes_readonly_file_and_directory_exactly(self) -> None:
        with tempfile.TemporaryDirectory() as temporary:
            base = Path(temporary)
            root = base / "test-area"
            root.mkdir()
            external = base / "external"
            external.mkdir()
            (external / "sentinel").write_text("external", encoding="utf-8")
            stale = root / "sessions" / "stale"
            stale.mkdir(parents=True)
            (stale / "sentinel").write_text("stale", encoding="utf-8")
            session: Path | None = None
            readonly_directory: Path | None = None
            readonly_file: Path | None = None
            try:
                with self.verifier_environment(NFC_TEST_AREA_ROOT=str(root)):
                    with MODULE.verification_test_session(
                        internal_lane=False
                    ) as session:
                        readonly_directory = session / "readonly-directory"
                        readonly_directory.mkdir()
                        readonly_file = readonly_directory / "readonly.txt"
                        readonly_file.write_text("readonly", encoding="utf-8")
                        subprocess.run(["attrib", "+R", str(readonly_file)], check=True)
                        subprocess.run(
                            ["attrib", "+R", str(readonly_directory)], check=True
                        )
                        self.assertTrue(
                            os.lstat(readonly_file).st_file_attributes
                            & MODULE.stat.FILE_ATTRIBUTE_READONLY
                        )
                        self.assertTrue(
                            os.lstat(readonly_directory).st_file_attributes
                            & MODULE.stat.FILE_ATTRIBUTE_READONLY
                        )
            finally:
                if session is not None and session.exists():
                    assert readonly_directory is not None
                    assert readonly_file is not None
                    subprocess.run(["attrib", "-R", str(readonly_file)], check=True)
                    subprocess.run(
                        ["attrib", "-R", str(readonly_directory)], check=True
                    )
                    MODULE.cleanup_test_session(
                        MODULE.validate_test_session(root, session)
                    )

            assert session is not None
            self.assertFalse(session.exists())
            self.assertEqual("stale", (stale / "sentinel").read_text(encoding="utf-8"))
            self.assertEqual(
                "external", (external / "sentinel").read_text(encoding="utf-8")
            )

    @unittest.skipUnless(sys.platform == "win32", "Windows long-path cleanup contract")
    def test_windows_file_api_path_uses_extended_length_namespace(self) -> None:
        drive = Path(r"C:\workspace\payload.bin")
        unc = Path(r"\\server\share\payload.bin")
        extended = Path(r"\\?\C:\workspace\payload.bin")
        extended_unc = Path(r"\\?\unc\server\share\payload.bin")
        relative = Path("relative-payload.bin")

        self.assertEqual(
            r"\\?\C:\workspace\payload.bin",
            MODULE._windows_file_api_path(drive),
        )
        self.assertEqual(
            r"\\?\UNC\server\share\payload.bin",
            MODULE._windows_file_api_path(unc),
        )
        self.assertEqual(str(extended), MODULE._windows_file_api_path(extended))
        self.assertEqual(
            str(extended_unc), MODULE._windows_file_api_path(extended_unc)
        )
        self.assertEqual(
            "\\\\?\\" + str(relative.absolute()),
            MODULE._windows_file_api_path(relative),
        )
        with self.assertRaisesRegex(RuntimeError, "device namespace"):
            MODULE._windows_file_api_path(Path(r"\\.\C:\payload.bin"))
        for forbidden in (
            Path(r"\\?\GLOBALROOT\Device\HarddiskVolume1\payload.bin"),
            Path(r"\\?\Volume{01234567-89ab-cdef-0123-456789abcdef}\payload.bin"),
            Path(r"\\?\pipe\nfc-cleanup-probe"),
        ):
            with self.subTest(forbidden=forbidden), self.assertRaisesRegex(
                RuntimeError, "extended namespace"
            ):
                MODULE._windows_file_api_path(forbidden)

    @unittest.skipUnless(sys.platform == "win32", "Windows long-path cleanup contract")
    def test_windows_cleanup_removes_extended_length_descendant_exactly(self) -> None:
        with tempfile.TemporaryDirectory() as temporary:
            base = Path(temporary)
            root = base / "test-area"
            root.mkdir()
            external = base / "external"
            external.mkdir()
            (external / "sentinel").write_text("external", encoding="utf-8")
            stale = root / "sessions" / "stale"
            stale.mkdir(parents=True)
            (stale / "sentinel").write_text("stale", encoding="utf-8")

            with self.verifier_environment(NFC_TEST_AREA_ROOT=str(root)):
                with MODULE.verification_test_session(
                    internal_lane=False
                ) as session:
                    long_parent = session / "long-path"
                    segment = "segment-xxxxxxxxxxxxxxxx"
                    while len(str(long_parent)) < 210:
                        long_parent /= segment
                    payload = long_parent / (
                        "payload-yyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyy.bin"
                    )
                    self.assertLess(len(str(long_parent)), 260)
                    self.assertGreater(len(str(payload)), 260)
                    native_parent = Path("\\\\?\\" + str(long_parent.absolute()))
                    native_parent.mkdir(parents=True)
                    native_payload = native_parent / payload.name
                    native_payload.write_bytes(b"long-path")
                    self.assertEqual(b"long-path", native_payload.read_bytes())

            self.assertFalse(session.exists())
            self.assertEqual("stale", (stale / "sentinel").read_text(encoding="utf-8"))
            self.assertEqual(
                "external", (external / "sentinel").read_text(encoding="utf-8")
            )

    def test_github_session_uses_only_the_runner_temp_derived_test_area(self) -> None:
        with tempfile.TemporaryDirectory() as temporary:
            runner_temp = Path(temporary)
            derived_root = runner_temp / "NvtFwCombiner-TestArea"
            with self.verifier_environment(
                GITHUB_ACTIONS="true",
                RUNNER_TEMP=str(runner_temp),
            ):
                with MODULE.verification_test_session(internal_lane=False) as session:
                    self.assertEqual(
                        derived_root.resolve(), session.parents[1].resolve()
                    )
                    self.assertRegex(session.name, r"^s-[a-z2-7]{26}$")
                    self.assertEqual("t", Path(os.environ["TEMP"]).name)

            self.assertTrue(derived_root.is_dir())
            self.assertTrue((derived_root / "sessions").is_dir())
            self.assertEqual((), tuple((derived_root / "sessions").iterdir()))

            conflicting_root = runner_temp / "conflict"
            conflicting_root.mkdir()
            with self.verifier_environment(
                GITHUB_ACTIONS="true",
                RUNNER_TEMP=str(runner_temp),
                NFC_TEST_AREA_ROOT=str(conflicting_root),
            ):
                with self.assertRaisesRegex(RuntimeError, "conflicts"):
                    with MODULE.verification_test_session(internal_lane=False):
                        self.fail("conflicting GitHub test root must fail closed")

    @unittest.skipUnless(sys.platform == "win32", "Windows custody contract")
    def test_created_session_custody_allows_nested_nondelete_custody(self) -> None:
        with tempfile.TemporaryDirectory() as temporary:
            runner_temp = Path(temporary)
            with self.verifier_environment(
                GITHUB_ACTIONS="true",
                RUNNER_TEMP=str(runner_temp),
            ):
                with MODULE.verification_test_session(internal_lane=False) as session:
                    handles: list[int] = []
                    try:
                        for path in (session.parents[1], session.parent, session):
                            handles.append(
                                MODULE._open_windows_path(
                                    path,
                                    delete_access=False,
                                    share_delete=False,
                                )
                            )
                    finally:
                        for handle in reversed(handles):
                            MODULE._close_windows_handle(handle)

    @unittest.skipUnless(sys.platform == "win32", "Windows path budget contract")
    def test_session_path_budget_creates_previous_canonical_projection(self) -> None:
        root_value = os.environ.get("NFC_TEST_AREA_ROOT")
        self.assertIsNotNone(root_value)
        assert root_value is not None
        relative = Path(
            "outputs-precursor-map-mismatch",
            "route-7-nt51917-15-ctrlram-replace-4-1-ic-39-nt51927-ctrlram-fw141-single-full-flash",
            "workflow",
            "00-preview",
            "input-01-02-postbuild-mp-ctrlram.bin",
        )
        with self.verifier_environment(NFC_TEST_AREA_ROOT=root_value):
            with MODULE.verification_test_session(internal_lane=False) as session:
                target = Path(os.environ["TEMP"]) / "tmpfwezljm6" / relative
                self.assertRegex(session.name, r"^s-[a-z2-7]{26}$")
                self.assertLessEqual(len(str(target)), 259)
                target.parent.mkdir(parents=True)
                target.write_bytes(b"path-budget")
                self.assertEqual(b"path-budget", target.read_bytes())

    def test_public_session_owns_scratch_environment_and_restores_process_state(
        self,
    ) -> None:
        scratch_names = (
            "TEMP",
            "TMP",
            "TMPDIR",
            "DOTNET_BUNDLE_EXTRACT_BASE_DIR",
            "RUFF_CACHE_DIR",
            "PYTHONPYCACHEPREFIX",
        )
        original_tempdir = tempfile.tempdir
        original_dotnet_work = MODULE.DOTNET_COVERAGE_WORK_ROOT
        original_ci_work = MODULE.CI_DOTNET_EVIDENCE_ROOT
        with tempfile.TemporaryDirectory() as temporary:
            root = Path(temporary)
            stale = root / "sessions" / "stale-sibling"
            stale.mkdir(parents=True)
            (stale / "keep.txt").write_text("keep", encoding="utf-8")
            with self.verifier_environment(
                NFC_TEST_AREA_ROOT=str(root),
                TEMP="prior-temp",
                TMP="prior-tmp",
                TMPDIR="prior-tmpdir",
            ):
                with MODULE.verification_test_session(internal_lane=False) as session:
                    self.assertEqual(session, Path(os.environ["NFC_TEST_SESSION_ROOT"]))
                    for name in scratch_names:
                        scratch = Path(os.environ[name])
                        self.assertTrue(scratch.is_dir(), name)
                        self.assertTrue(scratch.is_relative_to(session), name)
                    self.assertEqual(os.environ["TEMP"], tempfile.tempdir)
                    self.assertTrue(
                        MODULE.DOTNET_COVERAGE_WORK_ROOT.is_relative_to(session)
                    )
                    self.assertTrue(
                        MODULE.CI_DOTNET_EVIDENCE_ROOT.is_relative_to(session)
                    )
                    marker = json.loads(
                        (session / MODULE.TEST_SESSION_MARKER_NAME).read_text(
                            encoding="utf-8"
                        )
                    )
                    self.assertEqual(
                        MODULE.TEST_SESSION_MARKER_SCHEMA_VERSION,
                        marker["schemaVersion"],
                    )
                    self.assertEqual(session.name, marker["sessionId"])
                    self.assertEqual(
                        os.path.normcase(str(root.resolve())), marker["normalizedRoot"]
                    )
                self.assertFalse(session.exists())
                self.assertEqual("prior-temp", os.environ["TEMP"])
                self.assertEqual("prior-tmp", os.environ["TMP"])
                self.assertEqual("prior-tmpdir", os.environ["TMPDIR"])
                self.assertEqual(original_tempdir, tempfile.tempdir)
                self.assertEqual(original_dotnet_work, MODULE.DOTNET_COVERAGE_WORK_ROOT)
                self.assertEqual(original_ci_work, MODULE.CI_DOTNET_EVIDENCE_ROOT)
                self.assertEqual(
                    "keep", (stale / "keep.txt").read_text(encoding="utf-8")
                )

    def test_session_cleanup_restores_every_owner_for_terminal_failures(self) -> None:
        failures = (
            RuntimeError("runtime probe"),
            subprocess.TimeoutExpired(["probe"], 1),
            KeyboardInterrupt(),
        )
        for failure in failures:
            with self.subTest(failure=type(failure).__name__):
                original_dotnet_work = MODULE.DOTNET_COVERAGE_WORK_ROOT
                original_ci_work = MODULE.CI_DOTNET_EVIDENCE_ROOT
                with tempfile.TemporaryDirectory() as temporary:
                    original_tempdir = tempfile.tempdir
                    root = Path(temporary)
                    stale = root / "sessions" / "stale"
                    stale.mkdir(parents=True)
                    (stale / "keep.txt").write_text("keep", encoding="utf-8")
                    with self.verifier_environment(
                        NFC_TEST_AREA_ROOT=str(root),
                        TEMP="prior-temp",
                        TMP="prior-tmp",
                        TMPDIR="prior-tmpdir",
                    ):
                        with self.assertRaises(type(failure)):
                            with MODULE.verification_test_session(
                                internal_lane=False
                            ) as session:
                                raise failure
                        self.assertFalse(session.exists())
                        self.assertEqual("prior-temp", os.environ["TEMP"])
                        self.assertEqual("prior-tmp", os.environ["TMP"])
                        self.assertEqual("prior-tmpdir", os.environ["TMPDIR"])
                        self.assertEqual(original_tempdir, tempfile.tempdir)
                        self.assertEqual(
                            original_dotnet_work, MODULE.DOTNET_COVERAGE_WORK_ROOT
                        )
                        self.assertEqual(
                            original_ci_work, MODULE.CI_DOTNET_EVIDENCE_ROOT
                        )
                        self.assertEqual(
                            "keep", (stale / "keep.txt").read_text(encoding="utf-8")
                        )

    def test_workload_failure_remains_primary_when_session_cleanup_also_fails(
        self,
    ) -> None:
        failures = (
            RuntimeError("workload runtime"),
            subprocess.TimeoutExpired("workload", 1),
            KeyboardInterrupt("workload interrupt"),
        )
        for failure in failures:
            with self.subTest(failure=type(failure).__name__):
                with tempfile.TemporaryDirectory() as temporary:
                    root = Path(temporary)
                    session: Path | None = None
                    with self.verifier_environment(
                        NFC_TEST_AREA_ROOT=str(root),
                        TEMP="prior-temp",
                        TMP="prior-tmp",
                        TMPDIR="prior-tmpdir",
                    ):
                        with (
                            patch.object(
                                MODULE,
                                "cleanup_test_session",
                                side_effect=RuntimeError(
                                    "cleanup failed; exact residue retained"
                                ),
                            ),
                            self.assertRaises(type(failure)) as captured,
                        ):
                            with MODULE.verification_test_session(
                                internal_lane=False
                            ) as session:
                                raise failure

                        self.assertIs(failure, captured.exception)
                        notes = getattr(captured.exception, "__notes__", ())
                        self.assertTrue(
                            any(
                                "cleanup failed; exact residue retained" in note
                                for note in notes
                            )
                        )
                        self.assertEqual("prior-temp", os.environ["TEMP"])
                        self.assertEqual("prior-tmp", os.environ["TMP"])
                        self.assertEqual("prior-tmpdir", os.environ["TMPDIR"])

                    assert session is not None
                    MODULE.cleanup_test_session(
                        MODULE.validate_test_session(root, session)
                    )

    @unittest.skipUnless(sys.platform == "win32", "Windows custody contract")
    def test_custody_close_attempts_all_handles_and_restores_process_state(
        self,
    ) -> None:
        cases = (
            ("workload", RuntimeError("workload primary")),
            ("cleanup", None),
        )
        actual_create = MODULE._create_windows_test_session_child
        actual_close = MODULE._close_windows_handle
        for case, workload_error in cases:
            with self.subTest(case=case), tempfile.TemporaryDirectory() as temporary:
                root = Path(temporary)
                cleanup_error = RuntimeError("cleanup primary")
                primary_error = workload_error or cleanup_error
                original_tempdir = tempfile.tempdir
                original_dotnet_work = MODULE.DOTNET_COVERAGE_WORK_ROOT
                original_ci_work = MODULE.CI_DOTNET_EVIDENCE_ROOT
                ownerships: list[MODULE.TestSessionOwnership] = []
                custody_handles: list[int] = []
                close_attempts: list[int] = []
                successfully_closed: set[int] = set()
                session: Path | None = None

                def capture_ownership(*args, **kwargs):
                    ownership = actual_create(*args, **kwargs)
                    custody = ownership.custody
                    assert custody is not None
                    handles = (
                        custody.marker_handle,
                        custody.session_handle,
                        custody.sessions.windows_handle,
                        custody.root.windows_handle,
                    )
                    assert all(handle is not None for handle in handles)
                    ownerships.append(ownership)
                    custody_handles.extend(int(handle) for handle in handles)
                    return ownership

                def fail_first_custody_close(handle: int) -> None:
                    if handle not in custody_handles:
                        actual_close(handle)
                        return
                    close_attempts.append(handle)
                    if handle == custody_handles[0]:
                        raise OSError("first custody close failed")
                    actual_close(handle)
                    successfully_closed.add(handle)

                try:
                    with self.verifier_environment(
                        NFC_TEST_AREA_ROOT=str(root),
                        TEMP="prior-temp",
                        TMP="prior-tmp",
                        TMPDIR="prior-tmpdir",
                    ):
                        with (
                            patch.object(
                                MODULE,
                                "_create_windows_test_session_child",
                                side_effect=capture_ownership,
                            ),
                            patch.object(
                                MODULE,
                                "cleanup_test_session",
                                side_effect=cleanup_error,
                            ),
                            patch.object(
                                MODULE,
                                "_close_windows_handle",
                                side_effect=fail_first_custody_close,
                            ),
                            self.assertRaises(type(primary_error)) as captured,
                        ):
                            with MODULE.verification_test_session(
                                internal_lane=False
                            ) as session:
                                if workload_error is not None:
                                    raise workload_error

                        self.assertIs(primary_error, captured.exception)
                        self.assertEqual(4, len(set(custody_handles)))
                        self.assertEqual(custody_handles, close_attempts)
                        for handle in custody_handles:
                            self.assertEqual(1, close_attempts.count(handle))
                        notes = getattr(captured.exception, "__notes__", ())
                        self.assertTrue(
                            any("first custody close failed" in note for note in notes)
                        )
                        if workload_error is not None:
                            self.assertTrue(
                                any("cleanup primary" in note for note in notes)
                            )
                        self.assertEqual("prior-temp", os.environ["TEMP"])
                        self.assertEqual("prior-tmp", os.environ["TMP"])
                        self.assertEqual("prior-tmpdir", os.environ["TMPDIR"])
                        self.assertEqual(original_tempdir, tempfile.tempdir)
                        self.assertEqual(
                            original_dotnet_work, MODULE.DOTNET_COVERAGE_WORK_ROOT
                        )
                        self.assertEqual(
                            original_ci_work, MODULE.CI_DOTNET_EVIDENCE_ROOT
                        )
                finally:
                    tempfile.tempdir = original_tempdir
                    MODULE.DOTNET_COVERAGE_WORK_ROOT = original_dotnet_work
                    MODULE.CI_DOTNET_EVIDENCE_ROOT = original_ci_work
                    for handle in custody_handles:
                        if handle not in successfully_closed:
                            actual_close(handle)
                    if session is not None and session.exists():
                        MODULE.cleanup_test_session(
                            MODULE.validate_test_session(root, session)
                        )

                self.assertEqual(1, len(ownerships))

    def test_cleanup_rejects_a_descendant_reparse_and_preserves_other_roots(
        self,
    ) -> None:
        with tempfile.TemporaryDirectory() as temporary:
            root = Path(temporary)
            stale = root / "sessions" / "stale"
            stale.mkdir(parents=True)
            (stale / "keep.txt").write_text("keep", encoding="utf-8")
            coverage = root / "coverage-final"
            upload = root / "upload-final"
            coverage.mkdir()
            upload.mkdir()
            (coverage / "sentinel").write_text("coverage", encoding="utf-8")
            (upload / "sentinel").write_text("upload", encoding="utf-8")
            actual_is_reparse = MODULE.is_reparse_point
            descendant: Path | None = None
            with (
                self.verifier_environment(NFC_TEST_AREA_ROOT=str(root)),
                patch.object(MODULE, "COVERAGE_ROOT", coverage),
                patch.object(MODULE, "CI_DOTNET_UPLOAD_ROOT", upload),
                patch.object(
                    MODULE,
                    "is_reparse_point",
                    side_effect=lambda path: (
                        path == descendant or actual_is_reparse(path)
                    ),
                ),
                self.assertRaisesRegex(RuntimeError, "reparse"),
            ):
                with MODULE.verification_test_session(internal_lane=False) as session:
                    descendant = session / "descendant"
                    descendant.mkdir()

            self.assertTrue(session.is_dir())
            assert descendant is not None
            self.assertTrue(descendant.is_dir())
            self.assertEqual("keep", (stale / "keep.txt").read_text(encoding="utf-8"))
            self.assertEqual(
                "coverage", (coverage / "sentinel").read_text(encoding="utf-8")
            )
            self.assertEqual(
                "upload", (upload / "sentinel").read_text(encoding="utf-8")
            )

    def test_workload_custody_blocks_session_identity_replacement(self) -> None:
        with tempfile.TemporaryDirectory() as temporary:
            root = Path(temporary)
            with self.verifier_environment(NFC_TEST_AREA_ROOT=str(root)):
                with MODULE.verification_test_session(internal_lane=False) as session:
                    original = session.with_name(f"{session.name}-original")
                    with self.assertRaises(OSError):
                        session.rename(original)

            self.assertFalse(original.exists())
            self.assertFalse(session.exists())

    def test_workload_custody_blocks_marker_identity_replacement_with_identical_bytes(
        self,
    ) -> None:
        with tempfile.TemporaryDirectory() as temporary:
            root = Path(temporary)
            with self.verifier_environment(NFC_TEST_AREA_ROOT=str(root)):
                with MODULE.verification_test_session(internal_lane=False) as session:
                    marker = session / MODULE.TEST_SESSION_MARKER_NAME
                    original = session / "original-marker"
                    with self.assertRaises(OSError):
                        marker.replace(original)

            self.assertFalse(original.exists())
            self.assertFalse(marker.exists())

    def test_cleanup_rejects_descendant_identity_drift_before_deletion(self) -> None:
        with tempfile.TemporaryDirectory() as temporary:
            root = Path(temporary)
            drift = False
            real_identity = MODULE.filesystem_identity

            def observed_identity(path: Path):
                identity = real_identity(path)
                if drift and path.name == "race.txt":
                    return (identity[0], identity[1] + 1)
                return identity

            with (
                self.verifier_environment(NFC_TEST_AREA_ROOT=str(root)),
                patch.object(
                    MODULE, "filesystem_identity", side_effect=observed_identity
                ),
                self.assertRaisesRegex(RuntimeError, "identity"),
            ):
                with MODULE.verification_test_session(internal_lane=False) as session:
                    raced = session / "race.txt"
                    raced.write_text("race", encoding="utf-8")
                    drift = True

            self.assertTrue(session.is_dir())
            self.assertTrue(raced.is_file())

    def test_internal_lane_inherits_the_exact_parent_session_and_never_cleans_it(
        self,
    ) -> None:
        with tempfile.TemporaryDirectory() as temporary:
            root = Path(temporary)
            with self.verifier_environment(NFC_TEST_AREA_ROOT=str(root)):
                with MODULE.verification_test_session(internal_lane=False) as parent:
                    marker = parent / MODULE.TEST_SESSION_MARKER_NAME
                    with MODULE.verification_test_session(
                        internal_lane=True
                    ) as inherited:
                        self.assertEqual(parent, inherited)
                    self.assertTrue(parent.is_dir())
                    self.assertTrue(marker.is_file())

    def test_isolated_and_dotnet_descendants_receive_the_exact_session_environment(
        self,
    ) -> None:
        scratch_names = (
            "TEMP",
            "TMP",
            "TMPDIR",
            "DOTNET_BUNDLE_EXTRACT_BASE_DIR",
            "RUFF_CACHE_DIR",
            "PYTHONPYCACHEPREFIX",
        )
        with tempfile.TemporaryDirectory() as temporary:
            root = Path(temporary)
            with self.verifier_environment(NFC_TEST_AREA_ROOT=str(root)):
                with (
                    MODULE.verification_test_session(internal_lane=False) as parent,
                    patch.object(MODULE, "run") as run_command,
                ):
                    MODULE.run_isolated_lane("structure", parent / "lane.log")
                    isolated_environment = run_command.call_args.kwargs["environment"]
                    dotnet_environment = MODULE.dotnet_batch_environment()
                    for environment in (isolated_environment, dotnet_environment):
                        self.assertEqual(
                            str(parent),
                            environment[MODULE.TEST_SESSION_ENVIRONMENT_VARIABLE],
                        )
                        for name in scratch_names:
                            value = Path(environment[name])
                            self.assertTrue(value.is_relative_to(parent), name)
                    with MODULE.verification_test_session(
                        internal_lane=True
                    ) as inherited:
                        self.assertEqual(parent, inherited)
                    self.assertTrue(parent.is_dir())

    def test_dotnet_batch_environment_overwrites_repository_root_without_parent_mutation(
        self,
    ) -> None:
        variable = MODULE.TEST_REPOSITORY_ROOT_ENVIRONMENT_VARIABLE
        with patch.dict(
            os.environ,
            {variable: str(Path("Z:/inherited-wrong-repository"))},
            clear=False,
        ):
            parent_environment = os.environ.copy()

            child_environment = MODULE.dotnet_batch_environment()

            self.assertEqual(str(MODULE.ROOT), child_environment[variable])
            self.assertEqual(parent_environment, dict(os.environ))

    def test_repository_script_descendant_can_run_nested_main_under_parent_session(
        self,
    ) -> None:
        with tempfile.TemporaryDirectory() as temporary:
            root = Path(temporary)
            with self.verifier_environment(NFC_TEST_AREA_ROOT=str(root)):
                with MODULE.verification_test_session(internal_lane=False):
                    result = subprocess.run(
                        [
                            sys.executable,
                            "-m",
                            "unittest",
                            (
                                "tests.scripts.test_verify_orchestration."
                                "VerifyOrchestrationTests."
                                "test_public_lane_rejects_the_parent_owned_marker"
                            ),
                        ],
                        cwd=MODULE.ROOT,
                        text=True,
                        capture_output=True,
                        check=False,
                    )
            self.assertEqual(0, result.returncode, result.stdout + result.stderr)

    def test_cleanup_refuses_a_marker_mismatch_but_restores_process_state(self) -> None:
        with tempfile.TemporaryDirectory() as temporary:
            original_tempdir = tempfile.tempdir
            root = Path(temporary)
            with self.verifier_environment(
                NFC_TEST_AREA_ROOT=str(root), TEMP="prior-temp"
            ):
                with self.assertRaisesRegex(RuntimeError, "marker"):
                    with MODULE.verification_test_session(
                        internal_lane=False
                    ) as session:
                        (session / MODULE.TEST_SESSION_MARKER_NAME).write_text(
                            "tampered\n", encoding="utf-8"
                        )
                self.assertTrue(session.is_dir())
                self.assertEqual("prior-temp", os.environ["TEMP"])
                self.assertEqual(original_tempdir, tempfile.tempdir)

    def test_python_verification_pins_pytest_base_and_cache_to_the_session(
        self,
    ) -> None:
        with tempfile.TemporaryDirectory() as temporary:
            root = Path(temporary)
            commands: list[list[str]] = []
            with self.verifier_environment(NFC_TEST_AREA_ROOT=str(root)):
                with (
                    MODULE.verification_test_session(internal_lane=False) as session,
                    patch.object(MODULE, "require_python_modules"),
                    patch.object(MODULE, "require_python_distribution_versions"),
                    patch.object(
                        MODULE,
                        "load_baseline",
                        return_value={
                            "collection": {
                                "python": {
                                    "coveragePyVersion": "1",
                                    "pytestCovVersion": "1",
                                }
                            }
                        },
                    ),
                    patch.object(
                        MODULE,
                        "reset_coverage_directory",
                        return_value=MODULE.COVERAGE_ROOT / "python",
                    ),
                    patch.object(
                        MODULE,
                        "run",
                        side_effect=lambda command, **_kwargs: commands.append(command),
                    ),
                    patch.object(MODULE, "verify_coverage"),
                ):
                    MODULE.verify_python()

            pytest_command = next(
                command for command in commands if "pytest" in command
            )
            self.assertIn(f"--basetemp={session / 'pytest' / 'base'}", pytest_command)
            self.assertIn(
                f"cache_dir={session / 'pytest' / 'cache'}",
                pytest_command,
            )

    def test_parser_rejects_public_option_abbreviations(self) -> None:
        for arguments in (
            ["--job=3"],
            ["--lane-timeout=300"],
        ):
            with self.subTest(arguments=arguments):
                with contextlib.redirect_stderr(io.StringIO()):
                    with self.assertRaises(SystemExit):
                        MODULE.parse_args(arguments)

    def test_internal_lane_rejects_public_gate_flags(self) -> None:
        with (
            patch.object(
                sys,
                "argv",
                [str(SCRIPT), "--all", "--internal-lane", "structure"],
            ),
            patch.dict(
                os.environ,
                {MODULE.INTERNAL_LANE_ENVIRONMENT_VARIABLE: "1"},
            ),
            self.assertRaisesRegex(SystemExit, "--internal-lane cannot be combined"),
        ):
            MODULE.main()

    def test_internal_lane_requires_the_parent_owned_marker(self) -> None:
        with (
            patch.object(
                sys,
                "argv",
                [str(SCRIPT), "--internal-lane", "structure"],
            ),
            patch.dict(
                os.environ,
                {MODULE.INTERNAL_LANE_ENVIRONMENT_VARIABLE: ""},
            ),
            self.assertRaisesRegex(
                SystemExit, "--internal-lane requires a parent-owned process"
            ),
        ):
            MODULE.main()

    def test_public_lane_rejects_the_parent_owned_marker(self) -> None:
        with (
            patch.object(sys, "argv", [str(SCRIPT), "--structure-only"]),
            patch.dict(
                os.environ,
                {MODULE.INTERNAL_LANE_ENVIRONMENT_VARIABLE: "1"},
            ),
            patch.object(MODULE, "run_selected_lanes") as run_selected,
            self.nested_public_verifier_invocation(),
            self.assertRaisesRegex(
                SystemExit, "process marker is reserved for --internal-lane"
            ),
        ):
            MODULE.main()

        run_selected.assert_not_called()

    def test_internal_lane_rejects_explicit_default_jobs(self) -> None:
        with (
            patch.object(
                sys,
                "argv",
                [
                    str(SCRIPT),
                    "--internal-lane",
                    "structure",
                    "--jobs",
                    str(MODULE.DEFAULT_VERIFY_JOBS),
                ],
            ),
            patch.dict(
                os.environ,
                {MODULE.INTERNAL_LANE_ENVIRONMENT_VARIABLE: "1"},
            ),
            self.assertRaisesRegex(SystemExit, "--internal-lane cannot be combined"),
        ):
            MODULE.main()

    def test_internal_lane_rejects_explicit_default_lane_timeout(self) -> None:
        with (
            patch.object(
                sys,
                "argv",
                [
                    str(SCRIPT),
                    "--internal-lane",
                    "structure",
                    (f"--lane-timeout-seconds={MODULE.DEFAULT_LANE_TIMEOUT_SECONDS}"),
                ],
            ),
            patch.dict(
                os.environ,
                {MODULE.INTERNAL_LANE_ENVIRONMENT_VARIABLE: "1"},
            ),
            self.assertRaisesRegex(SystemExit, "--internal-lane cannot be combined"),
        ):
            MODULE.main()


if __name__ == "__main__":
    unittest.main()
