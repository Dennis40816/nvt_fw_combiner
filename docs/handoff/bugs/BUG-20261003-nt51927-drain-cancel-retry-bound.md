# BUG-20261003-nt51927-drain-cancel-retry-bound: the cancel retry loop of the output drain has no bound of its own

Status: suspected
Severity: P3
Found: 2026-10-03, Claude Code commander (Claude Opus 5.5 review of #538), from the independent review of the merged change
(`3d16b8b2d`). Related record: [BUG-20261002-nt51927-fw132-evidence-build-flake](BUG-20261002-nt51927-fw132-evidence-build-flake.md).
Where: `src/NvtFwCombiner.Platform/Processes/WindowsSynchronousReadCancellation.cs` and its callers in
`src/NvtFwCombiner.Infrastructure/ExternalTools/` (a firmware-owner path: a change here is R3).
Owner: unassigned
Expected: the follow-up is closed by its own change.
Observed: a read that cannot be aborted would make the cancel retry loop spin until the deadline.
Evidence: code reading of merge `3d16b8b2d`; `WindowsSynchronousReadCancellation.cs` lines 74-80 loop `while (_completed == 0)`,
call `CancelSynchronousIo` and sleep 1 ms, with no counter or deadline of their own. The unabortable-read case was
reasoned from the code, not reproduced.
Resolution: bound the loop with its own limit (this adds a bound by design; the existing capacity, timing and refusal decisions stay). Pending.
