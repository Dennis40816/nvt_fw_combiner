"""Behavioral tests for canonical verifier lane orchestration."""

from __future__ import annotations

import argparse
import contextlib
import importlib.util
import io
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


class VerifyOrchestrationTests(unittest.TestCase):
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

    def test_ctrl_c_waits_for_worker_process_handoff_before_cleanup(self) -> None:
        fake_process = MagicMock()
        activation_finished = threading.Event()
        cleanup_observations: list[bool] = []

        def create_during_interrupt(*_args: object, **_kwargs: object) -> MagicMock:
            self.assertIsNot(threading.current_thread(), threading.main_thread())
            signal.raise_signal(signal.SIGINT)
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
        ):
            result = MODULE.main()

        self.assertEqual(1, result)
        run_lanes.assert_not_called()

    def test_main_returns_sigterm_exit_code_after_owned_cleanup_path(self) -> None:
        cancellation = threading.Event()

        def request_sigterm(_args: argparse.Namespace) -> int:
            signal.raise_signal(signal.SIGTERM)
            return 0

        with (
            patch.object(MODULE, "PROCESS_CANCELLATION_REQUESTED", cancellation),
            patch.object(MODULE, "parse_args", return_value=MagicMock()),
            patch.object(MODULE, "execute_verification", side_effect=request_sigterm),
        ):
            result = MODULE.main()

        self.assertEqual(128 + signal.SIGTERM, result)
        self.assertTrue(cancellation.is_set())

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
                patch.object(MODULE, "verify_coverage"),
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
                patch.object(MODULE, "verify_coverage") as verify_coverage,
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
        format_index = next(
            index
            for index, command in enumerate(commands)
            if len(command) > 1 and command[1] == "format"
        )
        test_command = next(command for command in commands if "test" in command)

        self.assertLess(restore_index, ownership_index)
        self.assertLess(ownership_index, format_index)
        self.assertIn("--collect:XPlat Code Coverage", test_command)
        self.assertEqual(
            str(coverage_directory),
            test_command[test_command.index("--results-directory") + 1],
        )
        verify_coverage.assert_called_once_with("dotnet", coverage_directory)

    def test_python_lane_emits_one_json_report_before_policy_validation(self) -> None:
        commands: list[list[str]] = []
        with tempfile.TemporaryDirectory() as temporary:
            coverage_directory = Path(temporary) / "python"
            coverage_report = coverage_directory / "coverage.json"
            with (
                patch.object(MODULE, "require_python_modules"),
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
        verify_coverage.assert_called_once_with("python", coverage_report)

    def test_parser_defaults_to_bounded_parallelism_and_rejects_excessive_jobs(
        self,
    ) -> None:
        parsed = MODULE.parse_args([])
        self.assertEqual(3, parsed.jobs)
        self.assertEqual(600, parsed.lane_timeout_seconds)
        with contextlib.redirect_stderr(io.StringIO()):
            with self.assertRaises(SystemExit):
                MODULE.parse_args(["--jobs", "4"])
            with self.assertRaises(SystemExit):
                MODULE.parse_args(["--lane-timeout-seconds", "59"])

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
