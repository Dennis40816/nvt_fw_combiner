# BUG-20261128-uismoke-saved-report-capture-cleanup-flake: test cleanup found the copied Desktop executable still in use once on CI

Status: fixed (test-side mitigation in the pull request that adds this resolution); reopen if it recurs
Severity: P3 (test flake; no product failure is known)
Found: 2026-11-28, Claude Code NFC session (Claude Opus 5.5), from pull request 696 run `46039997105`
(job `140645002071`, shard `dotnet / test (ui)`) at `e163e303`. That pull request adds internal IC file loader
code and moves the Domain region graph rules; it changes no UI, Desktop or capture code. The test failed on
attempt 1 and passed on the retry, so the decision 193 gate marked it flaky and `dotnet / build-test` failed with
"flaky test has no bug record".
Where: `NvtFwCombiner.UiSmoke.Tests.DesktopCaptureProcessTests.SavedReportCapturePublishesPngAndProcessExitsZero`,
`tests/NvtFwCombiner.UiSmoke.Tests/DesktopCaptureProcessTests.cs` (the stack trace names line 76, the end of the
test's `using var workspace` scope).
Observed: on attempt 1 the test body finished, then `TempWorkspace.Dispose()`
(`tests/NvtFwCombiner.TestSupport/TempWorkspace.cs`, line 62) failed to delete the workspace with
`System.IO.IOException: The process cannot access the file 'NvtFwCombiner.Desktop.exe' because it is being used by
another process.`
Expected: when the test ends, no process holds the copied `NvtFwCombiner.Desktop.exe`, so the workspace can be
deleted.
Suspected cause, not reproduced locally: another handle, from the exiting process or from a scanner, still held
the copied executable for a short time after the test read the exit code. The error is a sharing violation, not
access denied, so an open file handle is more likely than a mapped image. This is the same shape as
`BUG-20261029-sequential-bootstrap-lifetime-lock-dispose-flake`, where a handle stayed open a few hundred
milliseconds after termination was confirmed.
Evidence: the attempt-1 files of run `46039997105` (`dotnet-test-ui-evidence-attempt-1`) show the stack above.
Next step: none. If the test fails again in cleanup, reopen this record and add the new run.
Owner: NFC session (UI smoke cleanup fix, 2026-11-28).
Resolution: `DesktopCaptureProcessTests.RunAsync` now waits after the process exit and after the `Process`
object is disposed, until the copied `NvtFwCombiner.Desktop.exe` can be opened with `FileShare.None`. Only then
does the test end and the workspace delete the copied host. The wait stops after 10 seconds and writes the cause
to the test output. The workspace disposal then reports the failure as before. This is the same approach as the
Bootstrap fix in pull request 660. `git grep "Process.Start"` finds no call in the Desktop, Bootstrap or Presentation.Avalonia sources, so no
Desktop child process can hold the file. The fix
covers every test in the class, because each one copies the host and calls `RunAsync`. Local evidence: 20
consecutive runs of the test passed, and one run of the whole class passed.
Release impact: board decision 193 makes the release source CI reject any observed flaky test, even with this
record.
