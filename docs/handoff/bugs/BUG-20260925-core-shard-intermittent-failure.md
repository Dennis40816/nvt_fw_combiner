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
