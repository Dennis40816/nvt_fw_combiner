# BUG-20261001-support-matrix-loading-query-flake: the support-matrix Loading query test failed once on the 1.2.x push CI

Status: open
Severity: P3
Found: 2026-10-01, Claude Code commander (Claude Opus 5.5), from the R50 workflow data: push CI run `36799283122`
on `1.2.x` at `d5770e52e` (the merge of #503), job `dotnet / test (bootstrap)` (`110169758777`). The test failed on
attempt 1 and passed on the in-job retry, so the decision 193 gate marked it flaky and `dotnet / build-test` failed
with "flaky test has no bug record". #503 changed no product or test code (G0 scripts and records only).
Where: `NvtFwCombiner.Bootstrap.Tests.CanonicalSupportMatrixHostTests.QueryReturnsLoadingWhileBackgroundWarmIsInFlight`,
`tests/NvtFwCombiner.Bootstrap.Tests/CanonicalSupportMatrixHostTests.cs` lines 37-71.
Observed: the job log names only the test; the attempt-1 TRX (in the run's evidence artifact) was not downloaded
yet, so the failing assertion is not confirmed. The test blocks a background catalog load on `AllowLoad`, starts
`catalog.Query` with `Task.Run` and requires it to finish within a one-second `Task.Delay` (lines 51-57) before it
checks `Loading`. Hypothesis: on a loaded runner the `Task.Run` work item, or the query behind the in-flight load,
does not complete within one wall-clock second.
Expected: the test proves that a query during an in-flight warm returns `Loading` without waiting for the load,
independent of runner load and thread-pool scheduling.
Evidence: to collect: the attempt-1 TRX of run `36799283122`, then a local amplification (parallel load or a
saturated thread pool) that reproduces the failure before any change.
Owner: unassigned; `1.2.1` flake bug work (decision 214, beside the other open flake records).
Resolution: pending.
