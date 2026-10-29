# BUG-20260929-vstest-discovery-foreground-thread-json: an extra xUnit runner line empties the Domain test discovery and fails the core shard

Status: fixed on `feature/1.2.2/ci-xunit-assemblyinfo-guard` by a test-only workaround for xUnit issue 3576; delete the workaround when xunit.v3 is 4.0.0 or later
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
Third observation, 2026-10-29: the `1.2.2` push CI at `160995ff` (run `40648510532`, `dotnet / test (core)`)
failed with "VSTest discovery produced no active inventory" for `NvtFwCombiner.Domain.Tests`, attempt 1, no readable
TRX. The run's `dotnet-test-core-evidence-attempt-1` artifact,
`shards/core/results/NvtFwCombiner.Domain.Tests/attempt-1/discovered-tests.txt`, shows the same output as #487: the
assembly-info JSON, then "Waiting 10 seconds for foreground threads to exit...". The same tree passed in the CI of pull
requests #657 (run `40645733259`) and #619 (run `40744734901`).
Root cause, 2026-10-29: the defect is in xUnit, not in an NFC thread. In xunit.v3 3.2.2,
`ConsoleRunner.Run` (`src/xunit.v3.runner.inproc.console/ConsoleRunner.cs`, tag `v3-3.2.2`, lines 394-422) queues a
thread-pool item after `Main` returns. If the process is still alive one second later, the item writes the waiting
message to standard output. No live foreground thread is needed: a slow process exit on a loaded runner is enough.
During the `-assemblyInfo` query, `TestProcessLauncherAdapter.GetAssemblyInfo` reads all standard output and requires
one JSON object, so the extra line makes discovery list no tests. This explains why 30 local discoveries passed and
why the core and UI shards were both affected. Upstream issue https://github.com/xunit/xunit/issues/3576 reports the
same output. Commit `8b317b7523` (2026-05-12) fixed it by suppressing the message in assembly-info mode. The first
release with the fix is xunit.v3 4.0.0. No 3.2.x release has it.
Fix, 2026-10-29: `tests/Shared/XunitAssemblyInfoOutputGuard.cs`, compiled into every `IsTestProject` through
`Directory.Build.props`. A module initializer detects the assembly-info query, including the `@@ <file>` response-file
form that the xUnit out-of-process launcher uses. Only in that query, it forwards the first standard-output line and
drops later output, the same effect as the upstream fix. Discovery parsing, the retry policy and the decision 191/193
contract are unchanged. Evidence: a scratch xunit.v3 3.2.2 project with a 2.5-second foreground thread failed
`dotnet vstest --ListTests` with this exact output without the guard and listed its test with the guard.
`XunitAssemblyInfoOutputGuardTests` covers the line filter and the argument forms. Upgrading to xunit.v3 4.0.x was not
chosen for `1.2.2`: it is a major version, `Avalonia.Headless.XUnit` 12.0.5 is built on 3.2.2, and the related
repositories pin 3.2.2. Delete the guard and its `Directory.Build.props` entry when xunit.v3 is 4.0.0 or later.
