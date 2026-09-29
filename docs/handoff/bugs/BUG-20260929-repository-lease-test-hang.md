# BUG-20260929-repository-lease-test-hang: the repository launch-lease test hangs until the CI job times out

Status: open; pre-wait timeout gap corrected locally, CI hang cause unproven
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
Additional investigation (2026-09-29, `feature/1.1.15/flaky-fixes`): the archived core log also records xUnit
`[FATAL ERROR] System.IO.IOException: The handle is invalid` immediately before this test's first long-running
report. The ready-wait cancellation source in `AnonymousPipeManagedApplicationProcess.StartUntilReadyAsync` starts
after lifetime lease acquisition and contained process creation, so a pre-wait stall is possible but unproven.
The unchanged test passed 30/30 local runs with Coverlet and a 30-second blame-hang limit; the Infrastructure
project passed 1,572/1,572 twice (default and CI-serialized xUnit settings), and a concurrent six-project core
run passed all projects. The original artifact has no stack or dump showing the blocked step. Local reproduction
and a causal product/test fix remain open; see `docs/handoff/1.1.15/WS-FLAKES.md`.
Owner: unassigned. First observation, cause unknown. Candidates to check: whether `StartUntilReadyAsync` enforces
its timeout on every path; whether install or lease acquisition can block; and the child process and pipe cleanup.
Resolution: decision 194's confirmed pre-wait deadline gap is corrected locally
for both managed Desktop and version Launcher adapters. The committed regression
gate blocks final validation inside `ProcessLaunchGate.StartContained`; before the
production change, the Desktop test failed because start exceeded its 150 ms
deadline. With the correction, both adapters return their existing typed
`ReadyTimeout` while validation is blocked, reject process creation after the
gate is released, and close the local ready and lifetime handles. A creation
whose cleanup cannot be confirmed retains the existing
`TerminationUnconfirmed` outcome. This establishes the pre-wait correction,
not the root cause of #488's CI hang: that incident also had an invalid-handle
fatal error, and the archived artifact has no thread dump or phase trace.

Prior contract review (before decision 194): `SPEC.md` describes a bounded
ready deadline separately from a process that cannot start.
`IManagedApplicationProcess.StartUntilReadyAsync`
calls the parameter a "Bounded ready deadline" without an origin point. ADRs
0051, 0056, 0064 and the launcher bootstrap contract require bounded readiness,
contained creation, fail-closed cleanup and no late start, but do not explicitly
say that the ready deadline includes lifetime-lease acquisition, the global
process-start gate, or final synchronous repository validation. The analogous
`AnonymousPipeManagedLauncherProcess` also starts its timer after launch and
ADMITTED reporting. These sources do not authorize reclassifying a blocked
pre-wait step as `ReadyTimeout`. No production code changed.

Decision 194 in `docs/handoff/1.1.12.md` now owns the whole-start deadline.
Open: capture a stack or phase trace from a real #488 recurrence before
assigning its cause. Protected CI and independent review remain outstanding.
