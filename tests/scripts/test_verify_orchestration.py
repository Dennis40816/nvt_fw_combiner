"""Behavioral tests for canonical verifier lane orchestration."""

from __future__ import annotations

import contextlib
import importlib.util
import io
import os
import subprocess
import sys
import tempfile
import time
import unittest
from pathlib import Path
from unittest.mock import MagicMock, patch


ROOT = Path(__file__).resolve().parents[2]
SCRIPT = ROOT / "scripts" / "verify.py"
SPEC = importlib.util.spec_from_file_location("verify", SCRIPT)
assert SPEC is not None and SPEC.loader is not None
MODULE = importlib.util.module_from_spec(SPEC)
sys.modules[SPEC.name] = MODULE
SPEC.loader.exec_module(MODULE)


class VerifyOrchestrationTests(unittest.TestCase):
    def test_full_plan_assigns_each_verification_owner_once(self) -> None:
        lanes = MODULE.selected_lanes(MODULE.parse_args(["--all"]))

        self.assertEqual(
            ["structure", "python", "dotnet"], [lane.name for lane in lanes]
        )
        self.assertEqual(len(lanes), len({lane.name for lane in lanes}))
        self.assertTrue(all(lane.isolate_action for lane in lanes))

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

    def test_internal_lane_dispatches_the_canonical_structure_owner(self) -> None:
        with patch.object(MODULE, "verify_structure") as verify_structure:
            MODULE.run_internal_lane("structure")

        verify_structure.assert_called_once_with(None)

    def test_keyboard_interrupt_terminates_the_active_command_before_reraising(
        self,
    ) -> None:
        fake_process = MagicMock()
        fake_process.wait.side_effect = KeyboardInterrupt
        fake_process.poll.return_value = None
        with (
            patch.object(MODULE.subprocess, "Popen", return_value=fake_process),
            patch.object(MODULE, "terminate_process_tree") as terminate,
        ):
            with self.assertRaises(KeyboardInterrupt):
                MODULE.run([sys.executable, "-c", "pass"])

        terminate.assert_called_once_with(fake_process)
        self.assertNotIn(fake_process, MODULE.ACTIVE_PROCESSES)

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

            with self.assertRaises(subprocess.TimeoutExpired):
                MODULE.run(
                    [sys.executable, "-c", parent],
                    log_path=root / "process-tree.log",
                    timeout_seconds=0.5,
                )
            self.assertTrue(child_ready.exists())
            time.sleep(2.25)
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

    def test_skip_python_leaves_repository_script_tests_out_of_dotnet_only_lane(
        self,
    ) -> None:
        lanes = MODULE.selected_lanes(
            MODULE.parse_args(["--skip-python", "--skip-structure"])
        )

        self.assertEqual(["dotnet"], [lane.name for lane in lanes])

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
                patch.object(MODULE, "run", side_effect=fake_run),
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

    def test_parser_defaults_to_bounded_parallelism_and_rejects_excessive_jobs(
        self,
    ) -> None:
        parsed = MODULE.parse_args([])
        self.assertEqual(3, parsed.jobs)
        self.assertEqual(300, parsed.lane_timeout_seconds)
        with contextlib.redirect_stderr(io.StringIO()):
            with self.assertRaises(SystemExit):
                MODULE.parse_args(["--jobs", "4"])
            with self.assertRaises(SystemExit):
                MODULE.parse_args(["--lane-timeout-seconds", "59"])


if __name__ == "__main__":
    unittest.main()
