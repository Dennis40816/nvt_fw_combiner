# BUG-20261006-uismoke-transparent-popup-spacing-flake: the transparent popup spacing test failed once on a pull-request CI run

Status: suspected
Severity: P3
Found: 2026-10-06, Claude Code commander (Claude Opus 5.5), from the pull-request CI run named under Observed; no local
reproduction was attempted.
Where: `NvtFwCombiner.UiSmoke.Tests.MemoryCoveragePopupTests.TransparentPopupSpacingDoesNotInterceptLegend`,
`tests/NvtFwCombiner.UiSmoke.Tests/MemoryCoveragePopupTests.Passive.cs` line 34, through the helper `BoundsInWindow` in
`tests/NvtFwCombiner.UiSmoke.Tests/MemoryCoveragePopupTests.cs` line 1031.
Observed: on the pull-request CI run `37422784672` for the Core package delivery pull request (a build-script and
documentation change that does not touch the UI or this test), the test failed on attempt 1 and passed on the in-job
retry. The failure was an `Assert.IsType<Point>` failure with "Value is null" in `BoundsInWindow`:
`control.TranslatePoint(default, window)` returned null. The job `dotnet / build-test` then failed with "flaky test has no
bug record" under the decision 193 gate. A passing retry does not establish a fix.
Expected: after the first render, the popup card and the first rail target are in the window's visual tree, `TranslatePoint`
returns a point, and the test passes on its first attempt on any runner.
Evidence: the run, attempt outcome, message and failing lines come from the job log of
https://github.com/Dennis40816/nvt_fw_combiner/actions/runs/37422784672, read by the commander on 2026-10-06 (the log
text is not stored in Git and GitHub keeps it for a limited time). The trunk moved to Avalonia 12.1.1 in the pull request
merged just before this run. The root cause is not established and no hypothesis is claimed. Not investigated: whether
the control was not yet attached or laid out when the helper ran, and whether the Avalonia 12.1.1 upgrade changed the
timing.
Owner: unassigned
Resolution: pending investigation and a merged fix; keep this record open until a fix merges. No code, test or CI fix is
included in this record.
