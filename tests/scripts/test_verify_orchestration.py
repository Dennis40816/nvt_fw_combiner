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
    @staticmethod
    def timeout_after_file_exists(path: Path, timeout_seconds: float = 0.2):
        def wait_for_file(_requested_timeout: float | None = None) -> float:
            deadline = time.monotonic() + 5
            while time.monotonic() < deadline:
                if path.exists():
                    return timeout_seconds
                time.sleep(0.01)
            raise AssertionError(f"timed out waiting for child readiness marker: {path}")

        return wait_for_file

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
        ):
            expected_calls = {
                "structure": ["structure"],
                "python": ["repository-scripts", "python"],
                "dotnet": ["dotnet"],
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

            def shutdown(
                self, *, wait: bool, cancel_futures: bool = False
            ) -> None:
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
                "exec(\"while not marker.exists():\\n"
                "    assert time.monotonic() < deadline\\n"
                "    time.sleep(0.01)\")"
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

    def test_internal_lane_timeout_terminates_descendants_without_a_new_group(
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

    def test_unix_tree_discovery_failure_is_reported_after_root_termination(
        self,
    ) -> None:
        fake_process = MagicMock()
        fake_process.poll.return_value = None
        with (
            patch.object(MODULE.sys, "platform", "linux"),
            patch.object(MODULE.subprocess, "run", side_effect=OSError("missing ps")),
            patch.object(
                MODULE.os, "killpg", side_effect=ProcessLookupError, create=True
            ),
            patch.object(MODULE.signal, "SIGKILL", 9, create=True),
        ):
            with self.assertRaisesRegex(
                RuntimeError, "unable to inspect Unix verification process tree"
            ):
                MODULE.terminate_process_tree(fake_process)

        fake_process.kill.assert_called_once_with()
        fake_process.wait.assert_called_once_with(
            timeout=MODULE.PROCESS_TERMINATION_TIMEOUT_SECONDS
        )

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
                "exec(\"while not marker.exists():\\n"
                "    assert time.monotonic() < deadline\\n"
                "    time.sleep(0.01)\")"
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

    def test_idle_worker_cleanup_timeout_without_lane_log_remains_best_effort(
        self,
    ) -> None:
        with (
            patch.object(MODULE.sys, "platform", "win32"),
            patch.object(MODULE.shutil, "which", return_value="powershell"),
            patch.object(
                MODULE.subprocess,
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
