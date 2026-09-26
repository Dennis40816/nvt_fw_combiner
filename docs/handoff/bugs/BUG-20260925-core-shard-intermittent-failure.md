# BUG-20260925-core-shard-intermittent-failure: CI core test shard fails intermittently

Status: open
Severity: P2
Found: 2026-09-25, Claude Code (commander), on pull request #447 (docs-only), CI run
`36115221320` at `bb327a509`
Where: CI job `dotnet / test (core)`, test assembly `NvtFwCombiner.Infrastructure.Tests`
(`dotnet vstest` returned exit status 1); the aggregate `dotnet / build-test` fails with it.
Observed: A pull request that changes only `docs/handoff/` failed the core shard. The job log
shows only the non-zero vstest exit; the failing test names are in the uploaded trx artifact
(`dotnet-test-core-evidence`), not in the console log. The same pattern happened earlier:
run `36098912021` failed with `VERIFICATION FAILED: core` at `1259c37ba`, and run
`36100314902` passed at the same commit.
Expected: A docs-only change passes every test shard; a shard failure names the failing tests
in the job log.
Evidence: runs `36115221320` (failure) and `36098912021`/`36100314902` (same commit, failure
then success). Candidate area, not confirmed: process and pipe tests under
`tests/NvtFwCombiner.Infrastructure.Tests/VersionManagement/` (timing-sensitive).
Owner: unassigned (triage in 1.1.13 unless it blocks the 1.1.12 release again)
Resolution: not fixed. 2026-09-25: rerunning the failed jobs of run `36115221320` passed
every check at the same commit, confirming the intermittency; PR #447 merged.

H1 local R1 admission (2026-09-26, bounded local R1 path of ADR 0070; recorded
before the implementation commit):
- Authority: owner decision 28 in [the 1.1.12 board](../1.1.12.md) and the
  "CI follow-up" row of [the 1.1.13 board](../1.1.13.md). Base `a600b7cbb` on
  `feature/1.1.13/ci-evidence`; implementer Claude Code.
- Exact path: `tests/NvtFwCombiner.Infrastructure.Tests/VersionManagement/FileSystemVersionManagerWriteLeaseTests.cs`.
  The owner is that test's lease-holder helper; the production writer lease
  (`FileSystemVersionManagerWriteLease`) is unchanged.
- Acceptance: starting the PowerShell child (until it prints `STARTED`) gets its
  own bounded budget; readiness after `STARTED` keeps the 10 s deadline. A slow
  start followed by fast readiness passes. Readiness beyond its deadline, a
  start beyond its budget, and an exit before `STARTED` or before readiness
  still fail with the child diagnostics, and the child is always reaped.
- Narrow tests: `FileSystemVersionManagerWriteLeaseTests` in
  `NvtFwCombiner.Infrastructure.Tests`, then `NvtFwCombiner.Architecture.Tests`.
- No JSON capability-reuse record: the validator rejects a record whose only
  path is a test file ("auxiliary evidence requires a governed path"), and a
  test-only change needs no governed integration coverage.
- Residual: the fix is confirmed only by later CI core-shard runs; H2-H5 and the
  failure-evidence upload are separate items.
- Review fix (2026-09-26, Codex F-1/F-2, P2): the helper keeps the winning start
  event and the monotonic `STARTED` timestamp, so `STARTED` after the start budget
  stays a start timeout, and readiness has a fixed deadline from that timestamp,
  checked before a ready file counts; both new regression tests failed on the
  previous rules and pass now.
