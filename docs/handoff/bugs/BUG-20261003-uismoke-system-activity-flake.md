# BUG-20261003-uismoke-system-activity-flake: the System Activity content test failed once on the 1.2.x push CI

Status: open
Severity: P3
Found: 2026-10-03, Claude Code commander (Claude Sonnet 5.5), from the push CI run named under Observed.
Where: `NvtFwCombiner.UiSmoke.Tests.ShellScreenInventoryTests.SystemActivityContentFitsAndFilters`,
`tests/NvtFwCombiner.UiSmoke.Tests/ShellScreenInventoryTests.cs` lines 28-125 (the failing comparison is line 80).
Observed: on the `1.2.x` push CI run `37090768759`, head `0947121fb` (the merge of #542, a documents-only change),
`NvtFwCombiner.UiSmoke.Tests.ShellScreenInventoryTests.SystemActivityContentFitsAndFilters(width: 980, height: 640, dark: True, chinese: True)`
failed on attempt 1 with `Assert.Equal() Failure: Collections differ` (expected `SystemActivityEntry[]`, actual
`ReadOnlyCollection<SystemActivityEntry>`) at line 80, and passed on the in-job retry. Job `dotnet / build-test`
(`111112231330`) then failed with "flaky test has no bug record" under the decision 193 gate. A passing retry does not
establish a fix.
Expected: the activity list of the System Information screen is the same before and after switching to Run Reports and
back (`Assert.Equal(entries, services.SystemInformation.Activity)`), independent of runner timing.
Evidence: the failure text, line and attempt come from the job log of
https://github.com/Dennis40816/nvt_fw_combiner/actions/runs/37090768759/job/111112231330, downloaded and read by the commander
on 2026-10-03 (the log text is not stored in Git and GitHub keeps it for a limited time); the entry contents are cut off in the
log and the root cause is not established. Unverified hypothesis from reading the test: an activity entry is recorded between
the snapshot at line 77 and the comparison at line 80 (the two `Activate` calls at lines 78-79 switch screens), so the two
lists differ.
Owner: unassigned
Resolution: pending investigation and a merged fix; keep this record open until a fix merges. No code, test or CI fix
is included in this record.
