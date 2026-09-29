# WS-FLAKES: decision 191 CI flake fixes

State: local work on `feature/1.1.15/flaky-fixes` from `origin/1.1.x` at `aba286bae`.
Owner: Codex `gpt-6-sol`, single writer. Local commits are authorized; push, PR and GitHub writes are not.
Risk: R1 test corrections; reclassify if a production owner changes.
Scope: `BUG-20260929-nav-focus-underline-gap-flake`,
`BUG-20260929-repository-lease-test-hang`, then
`BUG-20260929-vstest-discovery-foreground-thread-json`, one commit per bug.
Authority: ADR 0079, decision 191, and the affected existing test/production owners.

## Checkpoints

### 2026-09-29 navigation focus render

State: verified locally; pending independent exact-head review.
Evidence: archived #486 TRX failed with zero Home focus-ring pixels. The unchanged focused
test passed 30/30 local runs (four parameter cases each); the corrected focused test passed
30/30 runs. The corrected test waits for startup's delayed focus work and asserts the
render-time focus state without changing the pixel assertions. `dotnet test
tests/NvtFwCombiner.UiSmoke.Tests/NvtFwCombiner.UiSmoke.Tests.csproj -c Release --no-build
--no-restore` passed all 1,861 tests once.
Open: independent exact-head review and protected CI remain for integration.

### 2026-09-29 repository launch-lease hang investigation

State: investigated, not fixed. No second commit.
Evidence: #488 core shard log reports xUnit `[FATAL ERROR] System.IO.IOException:
The handle is invalid` at testhost elapsed 56 seconds, immediately before the named
test's first long-running report at elapsed 92 seconds; 50 reports followed until
the job timeout. In `AnonymousPipeManagedApplicationProcess.StartUntilReadyAsync`,
the five-second cancellation source is created only after lifetime lease acquisition
and `ProcessLaunchGate.StartContained`. That scope is a candidate, not a proven
location of the CI hang. The unchanged focused test passed 30/30 local runs with
Coverlet and a 30-second blame-hang limit. The unchanged Infrastructure project
passed 1,572/1,572 twice, once with default settings and once with CI's serialized
xUnit settings. One parallel run of all six core projects also passed, including
Infrastructure 1,572/1,572. The fatal invalid-handle error did not recur, and the
archived CI artifact has no process stack or dump to identify the blocked step.
The internal CI shard entry point refused local execution because it requires an
actual CI run ID and attempt; no CI identity was fabricated.

Open: **stop condition — a bounded hung-test failure requires the decision 191
per-test hang limit in `scripts/verify.py` or `.github/workflows/`, both outside
this workstream's permitted files.** The commander owns that separate change and
obtaining a dump or phase evidence for the #488 incident. Do not call the
repository lease bug fixed from the passing local sample. The VSTest discovery
bug was not started because the ordered task stops here. No push was made.
