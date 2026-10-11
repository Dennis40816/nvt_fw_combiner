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
                MODULE.run(["dotnet", "vstest", "test.dll"], log_path=Path("unused.log"))
            scrub.assert_not_called()
        with patch.dict(os.environ, {"NFC_CI_SCRUB_TEST_OUTPUT": "1"}), patch.object(MODULE, "run_scrubbed_test") as scrub:
            with patch.object(MODULE, "_run_to_logs"):
                MODULE.run(["dotnet", "vstest", "test.dll", "--ListTests"], log_path=Path("unused.log"))
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
                    MODULE.run(["dotnet", "vstest", MARKER], log_path=log)
            self.assertEqual(["dotnet"], caught.exception.cmd)
            self.assertEqual(42, caught.exception.timeout)
            self.assertIsNone(caught.exception.output)
            self.assertNotIn(MARKER, str(caught.exception) + log.read_text())
            self.assertTrue(directories)
            self.assertTrue(all(not directory.exists() for directory in directories))


if __name__ == "__main__":
    unittest.main()
