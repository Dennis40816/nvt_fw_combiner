# BUG-20261001-support-matrix-loading-query-flake: the support-matrix Loading query test failed once on the 1.2.x push CI

Status: fixed locally on `feature/1.2.1/owner-decisions` (test-only); closes when that branch merges into `1.2.x`
Severity: P3
Found: 2026-10-01, Claude Code commander (Claude Opus 5.5), from the R50 workflow data: push CI run `36799283122`
on `1.2.x` at `d5770e52e` (the merge of #503), job `dotnet / test (bootstrap)` (`110169758777`). The test failed on
attempt 1 and passed on the in-job retry, so the decision 193 gate marked it flaky and `dotnet / build-test` failed
with "flaky test has no bug record". #503 changed no product or test code (G0 scripts and records only).
Where: `NvtFwCombiner.Bootstrap.Tests.CanonicalSupportMatrixHostTests.QueryReturnsLoadingWhileBackgroundWarmIsInFlight`,
`tests/NvtFwCombiner.Bootstrap.Tests/CanonicalSupportMatrixHostTests.cs` lines 37-71 at `d5770e52e` (37-74 after the fix).
Observed: the attempt-1 TRX (artifact `dotnet-test-bootstrap-evidence-attempt-1`, downloaded with the owner's
approval) shows `Assert.Same() Failure` at line 57 (numbering at `d5770e52e`): expected the query task, actual the one-second `Task.Delay`;
the query task's status was **`WaitingToRun`**, so its `Task.Run` work item had not even started when the delay
elapsed (duration 1.83 s; 2157 of 2158 Bootstrap tests passed; attempt 2 passed). The test blocks a background
catalog load on `AllowLoad`, starts `catalog.Query` with `Task.Run` and requires it to finish within one second
(lines 51-57). The product query did not block; the thread pool did not schedule the work item in time on the
loaded runner.
Expected: the test proves that a query during an in-flight warm returns `Loading` without waiting for the load,
independent of runner load and thread-pool scheduling.
Evidence: the attempt-1 TRX above (`evidence/1.2.1/flake-36799283122/`, test-area relative). Local amplification
(a temporary, uncommitted variant of the test): with the global thread-pool queue saturated by blocking work items
and the query queued from a non-pool thread, as xUnit's own threads do, the `Task.Run` form failed with the same
`Assert.Same() Failure ... Status = WaitingToRun` in 1 of 1 run, while a `TaskCreationOptions.LongRunning` query
passed in the same conditions. Without the saturation both forms passed.
Owner: Claude Code commander, `feature/1.2.1/owner-decisions`.
Resolution: the test runs the query on a dedicated thread (`Task.Factory.StartNew` with
`TaskCreationOptions.LongRunning`) and allows ten seconds instead of one; the load stays blocked until the
`finally` block, so a query that waited for it still could never finish within the bound. No production change.
The fixed class passed 3 of 3 local runs (4 tests each).
