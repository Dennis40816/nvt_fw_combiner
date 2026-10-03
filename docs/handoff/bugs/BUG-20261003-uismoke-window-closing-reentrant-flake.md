# BUG-20261003-uismoke-window-closing-reentrant-flake: the re-entrant window close test failed once on a pull-request CI run

Status: suspected
Severity: P3
Found: 2026-10-03, Claude Code commander (Claude Sonnet 5.5), from the pull-request CI run named under Observed; no local
reproduction was attempted.
Where: `NvtFwCombiner.UiSmoke.Tests.WindowLifetimeTests.ClosingEventReentrantCloseDoesNotStackFinalClose`,
`tests/NvtFwCombiner.UiSmoke.Tests/WindowLifetimeTests.Ready.cs`, the final assertions at lines 390-391.
Observed: on the pull-request CI run `37113366585` for the R33-06 waits pull request (a tests-only change that does not
touch `WindowLifetimeTests` or the window code), job `dotnet / build-test` (`111177136601`) reported
`NvtFwCombiner.UiSmoke.Tests.WindowLifetimeTests.ClosingEventReentrantCloseDoesNotStackFinalClose` failed on attempt 1 and
passed on the in-job retry; the job failed with "flaky test has no bug record" under the decision 193 gate. The failure was
`Assert.Equal() Failure: Values differ, Expected: 2, Actual: 1` at `WindowLifetimeTests.Ready.cs` line 390
(`Assert.Equal(2, finalClosingEvents)`), after `window.Close()`, a wait for the `Closed` event and
`Dispatcher.UIThread.RunJobs()`. A passing retry does not establish a fix.
Expected: when the first final `Closing` event (seen while `ClosePhase` is `Closing`) calls `window.Close()` again from
inside its handler, the test sees exactly two final `Closing` events and one `Closed` event once the window has closed,
whatever the runner timing.
Evidence: the run, job, attempt outcome, message and failing line come from the job log of
https://github.com/Dennis40816/nvt_fw_combiner/actions/runs/37113366585/job/111177136601, read by the commander on
2026-10-03 (the log text is not stored in Git and GitHub keeps it for a limited time); the root cause is not established
and no hypothesis is claimed. Not investigated: whether the second final closing event is raised before the `Closed`
event completes the wait (so that `RunJobs()` is not enough to observe it) or is not raised at all in that interleaving.
Owner: unassigned
Resolution: pending investigation and a merged fix; keep this record open until a fix merges. No code, test or CI fix is
included in this record.
