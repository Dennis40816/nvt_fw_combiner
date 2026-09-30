# BUG-20260929-repository-lease-test-hang: the repository launch-lease test hangs until the CI job times out

Status: open
Severity: P2
Found: 2026-09-29, Claude Code commander (Claude Opus 5.5), while checking CI on pull request #488 (run
`36549856597`), at `feature/1.1.15/bootstrap-flake`@`a5b7019f5`
Where: `tests/NvtFwCombiner.Infrastructure.Tests/VersionManagement/FileSystemManagedVersionRepositoryLaunchLeaseTests.cs:132`,
`FileSystemManagedVersionRepositoryTests.RepositoryLeaseRemainsStableThroughManagedProcessStart`
Observed: in the `dotnet / test (core)` shard, xUnit reported this test as a long-running test every 30 seconds from
about 00:01:32 (elapsed 00:00:35) until the 30-minute job timeout cancelled the shard (50 reports). vstest never exited,
so `build-test` failed on the missing core evidence. Pull request #488 changes only Bootstrap tests and a bug record,
and the separate bootstrap shard passed. The test installs a package, acquires the launch lease, starts the ready-probe
process through `AnonymousPipeManagedApplicationProcess.StartUntilReadyAsync` with a 5-second ready timeout, and then
checks that the lease blocks `File.Move`. It runs in the `ReadyProbeProcessSerialGroup` collection with the other
ready-probe tests.
Expected: the test finishes, passing or failing, within its own bounds. A 5-second ready timeout, or any other step,
never lets one test hold the shard for 25 minutes (the adapter's ready-wait contract and the test's own assertions).
Evidence: the `shard.log` in the run's `dotnet-test-core-evidence-attempt-1` artifact (the repeated long-running-test
lines, then `Command timing: 1602.8s`); the job log shows `verify.py` waiting on vstest until the cancellation.
Owner: unassigned. First observation, cause unknown. Candidates to check: whether `StartUntilReadyAsync` enforces
its timeout on every path; whether install or lease acquisition can block; and the child process and pipe cleanup.
Resolution: not fixed.
