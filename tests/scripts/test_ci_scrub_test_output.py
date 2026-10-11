"""The opt-in `NFC_CI_SCRUB_TEST_OUTPUT` mode keeps test output out of console, logs and summaries."""
from contextlib import contextmanager, redirect_stderr, redirect_stdout
import io
import os
from pathlib import Path
import subprocess
import sys
import tempfile
import unittest
from unittest.mock import patch

from tests.scripts.test_ci_test_baseline import MARKER, write_trx


@contextmanager
def recorded_scrub_directories(module):
    factory, directories = module.tempfile.TemporaryDirectory, []

    def record(*args, **kwargs):
        temporary = factory(*args, **kwargs)
        directories.append(Path(temporary.name))
        return temporary

    with patch.object(module.tempfile, "TemporaryDirectory", side_effect=record):
        yield directories


class ScrubTestOutputTests(unittest.TestCase):
    def test_real_child_marker_is_captured_deleted_and_not_reported(self):
        from tests.scripts.test_verify_orchestration import MODULE
        with (tempfile.TemporaryDirectory() as temporary,
              tempfile.TemporaryDirectory(prefix="nfc-scrub-test-other-") as unrelated):
            root = Path(temporary)
            trx = write_trx(root / "results", ("Failed",))
            before = trx.read_bytes()
            log = root / "attempt.log"
            mirror = root / "mirror.log"
            output, errors = io.StringIO(), io.StringIO()
            original_start = MODULE.start_owned_process

            def start_fake(command, **kwargs):
                code = f"import sys; print('{MARKER}'); print('{MARKER}',file=sys.stderr); sys.exit(1)"
                return original_start([sys.executable, "-c", code], **kwargs)

            command = [sys.executable, "vstest", MARKER, f"--ResultsDirectory:{root / 'results'}"]
            with (recorded_scrub_directories(MODULE) as directories,
                  patch.dict(os.environ, {"NFC_CI_SCRUB_TEST_OUTPUT": "1"}),
                  patch.object(MODULE, "start_owned_process", side_effect=start_fake),
                  redirect_stdout(output), redirect_stderr(errors)):
                with self.assertRaises(subprocess.CalledProcessError) as caught:
                    MODULE.run(command, cwd=root, log_path=log, mirror_log_path=mirror)
                MODULE.report_ci_test_failure_details("probe", [("project", "attempt-1", trx)])
                MODULE.report_ci_failed_tests("core", "artifact", [
                    ("project", MODULE.CiFailedProjectEvidence((trx,), (MARKER,)))])
            public = output.getvalue() + errors.getvalue() + str(caught.exception)
            self.assertNotIn(MARKER, public)
            self.assertIn("exit 1", public)
            self.assertIn("total 1 passed 0 failed 1 skipped 0", public)
            self.assertIn("Tests.Sample.Method0\n", public)
            self.assertNotIn(MARKER, log.read_text() + mirror.read_text())
            self.assertEqual(before, trx.read_bytes())
            self.assertTrue(directories)
            self.assertTrue(all(not directory.exists() for directory in directories))
            self.assertTrue(Path(unrelated).is_dir())

    def test_scrub_is_opt_in_and_does_not_change_discovery(self):
        from tests.scripts.test_verify_orchestration import MODULE
        with patch.dict(os.environ, {"NFC_CI_SCRUB_TEST_OUTPUT": "0"}), patch.object(MODULE, "run_scrubbed_test") as scrub:
            with patch.object(MODULE, "_run_to_logs"):
                MODULE.run(["dotnet", "vstest", "test.dll"], log_path=Path(tempfile.gettempdir()) / "nfc-unused-scrub.log")
            scrub.assert_not_called()
        with patch.dict(os.environ, {"NFC_CI_SCRUB_TEST_OUTPUT": "1"}), patch.object(MODULE, "run_scrubbed_test") as scrub:
            with patch.object(MODULE, "_run_to_logs"):
                MODULE.run(["dotnet", "vstest", "test.dll", "--ListTests"], log_path=Path(tempfile.gettempdir()) / "nfc-unused-scrub.log")
            scrub.assert_not_called()
            with patch.object(MODULE, "_run_to_logs"):
                MODULE.run(["dotnet", "vstest", "test.dll", "--ListFullyQualifiedTests",
                            "--ListTestsTargetPath:names.txt"], log_path=Path(tempfile.gettempdir()) / "nfc-unused-scrub.log")
            scrub.assert_not_called()

    def test_scrub_preserves_fatal_timeout_type_without_private_arguments(self):
        from tests.scripts.test_verify_orchestration import MODULE
        with tempfile.TemporaryDirectory() as temporary:
            log = Path(temporary) / "attempt.log"
            with (recorded_scrub_directories(MODULE) as directories,
                  patch.dict(os.environ, {"NFC_CI_SCRUB_TEST_OUTPUT": "1"}),
                  patch.object(MODULE, "_run_to_logs", side_effect=
                               subprocess.TimeoutExpired(["dotnet", MARKER], 42, output=MARKER))):
                with self.assertRaises(subprocess.TimeoutExpired) as caught:
                    MODULE.run(["dotnet", "vstest", MARKER, "--ResultsDirectory:results"], log_path=log)
            self.assertEqual(["dotnet"], caught.exception.cmd)
            self.assertEqual(42, caught.exception.timeout)
            self.assertIsNone(caught.exception.output)
            self.assertNotIn(MARKER, str(caught.exception) + log.read_text())
            self.assertTrue(directories)
            self.assertTrue(all(not directory.exists() for directory in directories))


    def test_success_prints_nothing_and_keeps_only_the_placeholder(self):
        from tests.scripts.test_verify_orchestration import MODULE
        with tempfile.TemporaryDirectory() as temporary:
            root = Path(temporary)
            log = root / "attempt.log"
            output, errors = io.StringIO(), io.StringIO()
            original_start = MODULE.start_owned_process

            def start_fake(command, **kwargs):
                code = f"import sys; print('{MARKER}'); print('{MARKER}', file=sys.stderr)"
                return original_start([sys.executable, "-c", code], **kwargs)

            command = [sys.executable, "vstest", MARKER, f"--ResultsDirectory:{root / 'results'}"]
            with (patch.dict(os.environ, {"NFC_CI_SCRUB_TEST_OUTPUT": "1"}),
                  patch.object(MODULE, "start_owned_process", side_effect=start_fake),
                  redirect_stdout(output), redirect_stderr(errors)):
                MODULE.run(command, cwd=root, log_path=log)
            self.assertEqual("", output.getvalue() + errors.getvalue())
            self.assertEqual("Test output scrubbed.\n", log.read_text())

    def test_raised_errors_carry_no_context_with_the_original_command(self):
        from tests.scripts.test_verify_orchestration import MODULE
        with tempfile.TemporaryDirectory() as temporary:
            root = Path(temporary)
            command = [sys.executable, "vstest", MARKER, f"--ResultsDirectory:{root / 'results'}"]
            for failure in (subprocess.CalledProcessError(1, [MARKER]),
                            subprocess.TimeoutExpired([MARKER], 7, output=MARKER)):
                with self.subTest(kind=type(failure).__name__):
                    with (patch.dict(os.environ, {"NFC_CI_SCRUB_TEST_OUTPUT": "1"}),
                          patch.object(MODULE, "_run_to_logs", side_effect=failure),
                          redirect_stdout(io.StringIO())):
                        with self.assertRaises(type(failure)) as caught:
                            MODULE.run(command, cwd=root, log_path=root / "attempt.log")
                    self.assertIsNone(caught.exception.__context__)
                    self.assertIsNone(caught.exception.__cause__)
                    self.assertNotIn(MARKER, str(caught.exception))

    def test_placeholder_write_failure_does_not_replace_the_original_error(self):
        from tests.scripts.test_verify_orchestration import MODULE
        with tempfile.TemporaryDirectory() as temporary:
            root = Path(temporary)
            blocker = root / "blocker"
            blocker.write_text("file", encoding="utf-8")
            log = blocker / "attempt.log"  # the parent is a file, so the placeholder cannot be written
            output = io.StringIO()
            with (patch.dict(os.environ, {"NFC_CI_SCRUB_TEST_OUTPUT": "1"}),
                  patch.object(MODULE, "_run_to_logs", side_effect=subprocess.TimeoutExpired([MARKER], 9)),
                  redirect_stdout(output)):
                with self.assertRaises(subprocess.TimeoutExpired) as caught:
                    MODULE.run([sys.executable, "vstest", MARKER, "--ResultsDirectory:r"], cwd=root, log_path=log)
            self.assertEqual(9, caught.exception.timeout)
            self.assertIn("Test output placeholder could not be written.", output.getvalue())
            self.assertNotIn(MARKER, output.getvalue())


if __name__ == "__main__":
    unittest.main()
