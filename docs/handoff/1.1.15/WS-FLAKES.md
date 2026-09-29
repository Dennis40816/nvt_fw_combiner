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
