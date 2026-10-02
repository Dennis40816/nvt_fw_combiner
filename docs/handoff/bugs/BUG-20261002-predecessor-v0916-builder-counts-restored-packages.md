# BUG-20261002-predecessor-v0916-builder-counts-restored-packages: the v0.9.16 baseline builder refused the packages its own restore downloads

Status: fixed
Severity: P1 for the v0.9.16 mode (no baseline executor could be built)
Found: 2026-10-02, Claude Code (Opus 5.5), in the first real run of the v0.9.16 mode (R35-09), at
`feature/1.2.2/executor-contract`@`5eb2f9fda`
Where: `scripts/predecessor_comparison.py`, `_build_executor` and `_source_inventory`
Observed: after `dotnet restore` in the fresh worktree of the `v0.9.16` tag the builder stopped with
`PREDECESSOR_EXECUTOR_INVALID: source file delta differs from seven lock rewrites`. The tag's `NuGet.config` sets
`globalPackagesFolder` to `.packages`, a top-level folder that Git ignores, so the restore wrote 716 package files
into the worktree beside the seven lock rewrites. The raw source comparison counts every file outside `bin` and
`obj`, so it saw 723 changed paths. The fake restore of the tests wrote no package.
Expected: the builder reproduces the executor v2 pins from a real restore and build, per
`docs/contracts/v0916-baseline-executor-v2.md`, whose last paragraph leaves the real builders to R35-09.
Evidence: rehearsal runs `v0916-1` and `v0916-2` in the test area (`evidence\1.2.2\p2c\rehearsal`); the second
lists the changed paths.
Owner: Claude Code, `feature/1.2.2/executor-contract`
Resolution: fixed in `d7b5b7c7d`: the executor recipe names the package folder (`executor.restorePackageFolder`); it
must be absent before restore, for both executors, and its files are left out of the source comparison, because the
pinned lock bytes identify the packages. A folder of that name elsewhere and any other new file are still refused.
`tests/scripts/test_predecessor_executor_v2.py` now restores a package as a real restore does and has both refusals.
