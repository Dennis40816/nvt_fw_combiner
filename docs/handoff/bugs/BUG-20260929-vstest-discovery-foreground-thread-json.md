# BUG-20260929-vstest-discovery-foreground-thread-json: an extra xUnit runner line empties the Domain test discovery and fails the core shard

Status: open
Severity: P2
Found: 2026-09-29, Claude Code commander (Claude Opus 5.5), while checking CI on pull request #487 (run
`36562321491`), at `feature/1.1.15/records`@`247473864`
Where: `scripts/verify.py` `parse_vstest_discovery` ("VSTest discovery produced no active inventory"), for
`NvtFwCombiner.Domain.Tests` in the `dotnet / test (core)` shard
Observed: all six core projects passed every test (Domain 475/475, Application 1637/1637, Infrastructure
1572/1572, ProfileContract 484/484, GoldenRegression 15/15, Architecture 277/277), but the shard failed with
"VSTest discovery produced no active inventory" for `NvtFwCombiner.Domain.Tests`. The discovery output
(`vstest ... NvtFwCombiner.Domain.Tests.dll --ListTests`) shows "Exception discovering tests:
System.InvalidOperationException: Test process did not return valid JSON (non-object)". The captured test-process
output was the assembly-info JSON object followed by the line "Waiting 10 seconds for foreground threads to
exit...", raised from `Xunit.v3.TestProcessLauncherAdapter.GetAssemblyInfo`. No test names were listed, so the
verifier found an empty inventory. Pull request #487 changes only documents.
Expected: discovery lists the project's tests on every run, so a shard whose tests all pass is not failed by its
discovery step (the CI failure-evidence contract, ADR 0079).
Evidence: `shards/core/results/NvtFwCombiner.Domain.Tests/discovered-tests.txt` and `shards/core/shard.log` in the
run's `dotnet-test-core-evidence-attempt-1` artifact; `scripts/verify.py` `parse_vstest_discovery`.
Owner: unassigned. First observation, cause unknown. Candidates to check: which foreground thread outlives the
xUnit v3 assembly-info query in `NvtFwCombiner.Domain.Tests`, for example a module initializer or a static
resource, and whether discovery should be retried or its runner output parsed more tolerantly. That choice
belongs to the decision 191 work and its ADR 0079 amendment.
Resolution: not fixed.
