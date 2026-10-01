# BUG-20261001-support-matrix-loading-query-flake: the support-matrix Loading query test failed once on the 1.2.x push CI

Status: open
Severity: P3
Found: 2026-10-01, Claude Code commander (Claude Opus 5.5), from the R50 workflow data: push CI run `36799283122`
on `1.2.x` at `d5770e52e` (the merge of #503), job `dotnet / test (bootstrap)` (`110169758777`). The test failed on
attempt 1 and passed on the in-job retry, so the decision 193 gate marked it flaky and `dotnet / build-test` failed
with "flaky test has no bug record". #503 changed no product or test code (G0 scripts and records only).
Where: `NvtFwCombiner.Bootstrap.Tests.CanonicalSupportMatrixHostTests.QueryReturnsLoadingWhileBackgroundWarmIsInFlight`,
`tests/NvtFwCombiner.Bootstrap.Tests/CanonicalSupportMatrixHostTests.cs` lines 37-71.
Observed: the attempt-1 TRX (artifact `dotnet-test-bootstrap-evidence-attempt-1`, downloaded with the owner's
approval) shows `Assert.Same() Failure` at line 57: expected the query task, actual the one-second `Task.Delay`;
the query task's status was **`WaitingToRun`**, so its `Task.Run` work item had not even started when the delay
elapsed (duration 1.83 s; 2157 of 2158 Bootstrap tests passed; attempt 2 passed). The test blocks a background
catalog load on `AllowLoad`, starts `catalog.Query` with `Task.Run` and requires it to finish within one second
(lines 51-57). The product query did not block; the thread pool did not schedule the work item in time on the
loaded runner.
Expected: the test proves that a query during an in-flight warm returns `Loading` without waiting for the load,
independent of runner load and thread-pool scheduling.
Evidence: the attempt-1 TRX above (`evidence/1.2.1/flake-36799283122/`, test-area relative). Still to do before a
fix: a local amplification with a saturated thread pool that reproduces `WaitingToRun` first.
Owner: unassigned; `1.2.1` flake bug work (decision 214, beside the other open flake records).
Resolution: pending.
