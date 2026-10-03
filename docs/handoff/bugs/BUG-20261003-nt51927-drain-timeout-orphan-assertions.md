# BUG-20261003-nt51927-drain-timeout-orphan-assertions: the timeout-orphan test of the output drain does not assert enough

Status: open
Severity: P3
Found: 2026-10-03, Claude Code commander (Claude Opus 5.5 review of #538), from the independent review of the merged change
(`3d16b8b2d`). Related record: [BUG-20261002-nt51927-fw132-evidence-build-flake](BUG-20261002-nt51927-fw132-evidence-build-flake.md).
Where: `tests/NvtFwCombiner.Infrastructure.Tests/ExternalTools/SystemExternalProcessRunnerLifetimeTests.cs` line 479 (the
test) and the drain it exercises, `src/NvtFwCombiner.Platform/Processes/WindowsSynchronousReadCancellation.cs` and its callers in
`src/NvtFwCombiner.Infrastructure/ExternalTools/` (a firmware-owner path: a change here is R3).
Owner: unassigned
Expected: the follow-up is closed by its own change.
Observed: the timeout-orphan test does not assert the kept text, that no `Detached` state is reported, and that the in-use count is 0.
Evidence: code reading of merge `3d16b8b2d`; `tests/NvtFwCombiner.Infrastructure.Tests/ExternalTools/SystemExternalProcessRunnerLifetimeTests.cs`
line 479 `OrphanHoldingOutputAfterTimeoutIsBoundedAndReported` asserts `TimedOut`, `Cleanup == OutputStreamHeldOpen` and the
cleanup time, and asserts neither the kept text, nor the absence of a `Detached` state, nor the in-use count. Not run.
Resolution: add the three assertions. Pending; no production change.
