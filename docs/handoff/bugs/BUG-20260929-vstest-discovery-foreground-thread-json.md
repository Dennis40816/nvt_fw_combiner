# BUG-20260929-vstest-discovery-foreground-thread-json: an extra xUnit runner line empties the Domain test discovery and fails the core shard

Status: open; CI failure observed, local cause not reproduced
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
belongs to the decision 191 work and its ADR 0079 amendment. Update, 2026-09-30: #489 delivered that work without
changing discovery handling; this record is bug work of the `1.2.1` decision 191 residual (decision 214).
Resolution: not fixed. On `feature/1.1.15/flaky-fixes`, the unchanged Release
`NvtFwCombiner.Domain.Tests.dll` completed 30 consecutive `dotnet vstest
<dll> --ListTests` discoveries: exit 0, 475 listed tests, and no foreground-thread
wait or invalid-JSON message in every run. One complete `dotnet test
tests/NvtFwCombiner.Domain.Tests/NvtFwCombiner.Domain.Tests.csproj -c Release
--no-build --no-restore` passed 475/475. The archived #487 output remains the
only red observation; this local sample does not disprove it. Source inspection
found no thread/timer creation, module initializer, assembly fixture, or custom
test-framework extension in Domain.Tests, its linked TestSupport sources, or
Domain. The generated test entry point routes the assembly-info query through
xUnit v3's in-process console runner; the waiting message is present in the
installed `xunit.v3.runner.inproc.console` package. Its appearance does not
identify which foreground thread was alive in #487. No root-cause correction,
runner-output tolerance, or retry has been applied. Obtain a process dump or
thread trace during the failing assembly-info query, then identify and fix the
thread owner before marking this bug fixed.
Second observation, 2026-09-30: pull request #493 (`feature/1.1.15/nav-focus-flake`@`6cc8880d1`, a test-only
change to one UiSmoke test), CI job `109748581586` `dotnet / test (ui)`: "VSTest discovery produced no active
inventory" for `NvtFwCombiner.UiSmoke.Tests`, attempt 1, no readable TRX. The same workflow's UI shard passed on
pull request #492 at `468f24842`. The shard log lives in the run's `dotnet-test-ui-evidence-attempt-1` artifact,
which was not downloaded, so whether the foreground-thread message caused it is unconfirmed. It widens the
affected scope from the core shard to the UI shard.
