# BUG-20261003-uismoke-retry-tooltip-open-timeout-flake: the Retry tooltip test failed once on a pull-request CI run

Status: suspected
Severity: P3
Found: 2026-10-03, Claude Code commander (Claude Sonnet 5.5), from the pull-request CI run named under Observed; no local
reproduction was attempted.
Where: `NvtFwCombiner.UiSmoke.Tests.LocalStateSaveNoticeTests.RetryTooltipStaysClearOfStatusTextAndRail`,
`tests/NvtFwCombiner.UiSmoke.Tests/LocalStateSaveNoticeTests.cs`, the wait for the tooltip to open (line 567, the helper
`WaitUntilAsync` at line 1184).
Observed: on the pull-request CI run `37116257149` for the R33-06 waits pull request (a tests-only change that does not
touch `LocalStateSaveNoticeTests`), job `dotnet / build-test` (`111185000111`) reported
`NvtFwCombiner.UiSmoke.Tests.LocalStateSaveNoticeTests.RetryTooltipStaysClearOfStatusTextAndRail(width: 1920, height: 1080, chinese: False)`
failed on attempt 1 with `System.Threading.Tasks.TaskCanceledException : A task was canceled.` raised by `WaitUntilAsync`
(called from line 567, `await WaitUntilAsync(() => ToolTip.GetIsOpen(retry), TimeSpan.FromSeconds(5))`) and passed on the
in-job retry; the job failed with "flaky test has no bug record" under the decision 193 gate. A passing retry does not
establish a fix.
Expected: after the pointer moves onto the Retry button, the tooltip opens within the five second wait on any runner, and
the test passes on its first attempt.
Evidence: the run, job, attempt outcome, message and failing line come from the job log of
https://github.com/Dennis40816/nvt_fw_combiner/actions/runs/37116257149/job/111185000111, read by the commander on
2026-10-03 (the log text is not stored in Git and GitHub keeps it for a limited time); the root cause is not established
and no hypothesis is claimed. Not investigated: whether the tooltip open timer (the show delay) was starved on the loaded
runner, whether the simulated pointer move did not reach the button, or whether the five second bound is too short for a
slow runner.
Owner: unassigned
Resolution: pending investigation and a merged fix; keep this record open until a fix merges. No code, test or CI fix is
included in this record.
