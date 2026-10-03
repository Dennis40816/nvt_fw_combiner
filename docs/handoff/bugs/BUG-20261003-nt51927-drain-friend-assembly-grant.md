# BUG-20261003-nt51927-drain-friend-assembly-grant: the friend-assembly grant of the output drain gives Infrastructure every Platform internal

Status: open
Severity: P3
Found: 2026-10-03, Claude Code commander (Claude Opus 5.5 review of #538), from the independent review of the merged change
(`3d16b8b2d`). Related record: [BUG-20261002-nt51927-fw132-evidence-build-flake](BUG-20261002-nt51927-fw132-evidence-build-flake.md).
Where: `src/NvtFwCombiner.Platform/Processes/WindowsSynchronousReadCancellation.cs` and its callers in
`src/NvtFwCombiner.Infrastructure/ExternalTools/` (a firmware-owner path: a change here is R3).
Owner: unassigned
Expected: the follow-up is closed by its own change.
Observed: the friend-assembly grant gives Infrastructure every Platform internal, which is wider than the one adapter it needs.
Evidence: code reading of merge `3d16b8b2d`; `src/NvtFwCombiner.Platform/NvtFwCombiner.Platform.csproj` lines 6-7 declare
`InternalsVisibleTo` for `NvtFwCombiner.Infrastructure` and `NvtFwCombiner.Infrastructure.Tests`. Not run.
Resolution: make the adapter public or a public facade and drop the grant. Pending; no bound, timing, capacity limit or refusal decision changes.
