# BUG-20261003-nt51927-drain-entrypoint-names: the native declarations of the output drain have no explicit entry-point names

Status: open
Severity: P3
Found: 2026-10-03, Claude Code commander (Claude Opus 5.5 review of #538), from the independent review of the merged change
(`3d16b8b2d`). Related record: [BUG-20261002-nt51927-fw132-evidence-build-flake](BUG-20261002-nt51927-fw132-evidence-build-flake.md).
Where: `src/NvtFwCombiner.Platform/Processes/WindowsSynchronousReadCancellation.cs` and its callers in
`src/NvtFwCombiner.Infrastructure/ExternalTools/` (a firmware-owner path: a change here is R3).
Owner: unassigned
Expected: the follow-up is closed by its own change.
Observed: the three native declarations in `WindowsSynchronousReadCancellation.cs` have no explicit `EntryPoint` names.
Evidence: code reading of merge `3d16b8b2d`; `WindowsSynchronousReadCancellation.cs` lines 84-91 declare `OpenThread`,
`GetCurrentThreadId` and `CancelSynchronousIo` with `[LibraryImport("kernel32.dll", ...)]` and no `EntryPoint`. Not run.
Resolution: add the explicit names. Pending; no bound, timing, capacity limit or refusal decision changes.
