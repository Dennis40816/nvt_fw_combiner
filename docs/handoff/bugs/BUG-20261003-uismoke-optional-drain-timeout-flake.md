# BUG-20261003-uismoke-optional-drain-timeout-flake: the optional-drain timeout test failed once on a pull-request CI run

Status: open
Severity: P3
Found: 2026-10-03, Claude Code commander (Claude Sonnet 5.5), from the pull-request CI run named under Observed; no local
reproduction was attempted.
Where: `NvtFwCombiner.UiSmoke.Tests.ShellPreloadSessionTests.OptionalDrainTimeoutInvalidatesLateProgress`,
`tests/NvtFwCombiner.UiSmoke.Tests/ShellPreloadSessionTests.Cancellation.cs` lines 88-137.
Observed: on the pull-request CI run `37099285238` for #545 (a documents-only change; the head of that run was replaced by a
later force-push, and the test file is unchanged by that pull request, so read it at the trunk `45ca90614`), job
`dotnet / build-test` (`111137201845`) reported
`NvtFwCombiner.UiSmoke.Tests.ShellPreloadSessionTests.OptionalDrainTimeoutInvalidatesLateProgress` failed on attempt 1 and
passed on the in-job retry; the job failed with "flaky test has no bug record" under the decision 193 gate. The failure was
`Assert.False() Failure, Expected: False, Actual: True` at `ShellPreloadSessionTests.Cancellation.cs` line 125, the first
`TryRetryOptionalAsync` assertion after the drain. A passing retry does not establish a fix.
Expected: after the 20 ms drain timeout the optional history stage cannot be retried while its worker still runs, so the
assertion holds independent of runner timing.
Evidence: the run, job, attempt outcome and failing line come from the job log of
https://github.com/Dennis40816/nvt_fw_combiner/actions/runs/37099285238/job/111137201845, downloaded and read by the commander
on 2026-10-03 (the log text is not stored in Git and GitHub keeps it for a limited time); the root cause is not
established and no hypothesis is claimed. Not investigated: the retry decision depends on the drain state and the 20 ms
`drainTimeout` in `TryRetryOptionalCoreAsync` (`src/NvtFwCombiner.Presentation.Avalonia/ShellPreloadSession.cs` lines
214-232); in the test the stage worker only completes after the asserting line, so the path that returned `true` is unknown.
Owner: unassigned
Resolution: pending investigation and a merged fix; keep this record open until a fix merges. No code, test or CI fix is
included in this record.
