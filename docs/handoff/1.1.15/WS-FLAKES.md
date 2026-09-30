# WS-FLAKES: decision 191 CI flake fixes

State: local work on `feature/1.1.15/flaky-fixes` from `origin/1.1.x` at `aba286bae`.
Owner: Codex `gpt-6-sol`, single writer. Local commits are authorized; push, PR and GitHub writes are not.
Risk: R3 for the branch diff because `docs/contracts/launcher-bootstrap-v1.md`
is classified R3 by `docs/governance/authority-policy.json`; required roles are
`firmware-owner` and `release-owner`. The earlier test-only phase was R1.
Scope: `BUG-20260929-nav-focus-underline-gap-flake`,
`BUG-20260929-repository-lease-test-hang`, then
`BUG-20260929-vstest-discovery-foreground-thread-json`, one commit per bug.
Authority: ADR 0079, decision 191, and the affected existing test/production owners.

## Checkpoints

### 2026-09-29 navigation focus render

State: verified locally; pending independent exact-head review.
Evidence: archived #486 TRX failed with zero Home focus-ring pixels. The unchanged focused
test passed 30/30 local runs (four parameter cases each); the corrected focused test passed
30/30 runs. The corrected test waits for startup's delayed focus work and asserts the
render-time focus state without changing the pixel assertions. `dotnet test
tests/NvtFwCombiner.UiSmoke.Tests/NvtFwCombiner.UiSmoke.Tests.csproj -c Release --no-build
--no-restore` passed all 1,861 tests once.
Open: independent exact-head review and protected CI remain for integration.

### 2026-09-29 repository launch-lease hang investigation

State: investigated, not fixed. No second commit.
Evidence: #488 core shard log reports xUnit `[FATAL ERROR] System.IO.IOException:
The handle is invalid` at testhost elapsed 56 seconds, immediately before the named
test's first long-running report at elapsed 92 seconds; 50 reports followed until
the job timeout. In `AnonymousPipeManagedApplicationProcess.StartUntilReadyAsync`,
the five-second cancellation source is created only after lifetime lease acquisition
and `ProcessLaunchGate.StartContained`. That scope is a candidate, not a proven
location of the CI hang. The unchanged focused test passed 30/30 local runs with
Coverlet and a 30-second blame-hang limit. The unchanged Infrastructure project
passed 1,572/1,572 twice, once with default settings and once with CI's serialized
xUnit settings. One parallel run of all six core projects also passed, including
Infrastructure 1,572/1,572. The fatal invalid-handle error did not recur, and the
archived CI artifact has no process stack or dump to identify the blocked step.
The internal CI shard entry point refused local execution because it requires an
actual CI run ID and attempt; no CI identity was fabricated.

Open: **stop condition — a bounded hung-test failure requires the decision 191
per-test hang limit in `scripts/verify.py` or `.github/workflows/`, both outside
this workstream's permitted files.** The commander owns that separate change and
obtaining a dump or phase evidence for the #488 incident. Do not call the
repository lease bug fixed from the passing local sample. The VSTest discovery
bug was not started because the ordered task stops here. No push was made.

### 2026-09-29 Domain discovery follow-up

State: investigated locally; not fixed. The #487 discovery artifact is a real
red observation, but 30 unchanged local `dotnet vstest <Domain.Tests.dll>
--ListTests` runs each exited 0 with all 475 tests and without the foreground-
thread message. One complete Domain.Tests run passed 475/475. Inspection found
no explicit foreground-thread owner in Domain.Tests, its linked TestSupport
sources, or Domain. The generated entry point uses xUnit v3's in-process console
runner, whose package contains the waiting message; the failing thread's
identity remains unknown. No source or runner change was made.
Open: capture a dump or thread trace from a failing assembly-info query before
claiming a root cause or a fix. Protected CI and exact-head review remain open.

### 2026-09-29 repository pre-wait deadline follow-up

State: investigated locally; no production fix. A temporary fake lease blocked
`TryValidateForStart` inside `ProcessLaunchGate.StartContained`; the 100 ms
`readyDeadline` had elapsed when the start remained incomplete after 250 ms.
The focused Infrastructure red test failed 1/1 as expected, released its fake
in `finally`, and was removed before commit. This demonstrates an unbounded
pre-wait step, not the cause of #488's CI hang. `SPEC.md`, the Application port,
ADRs 0051/0056/0064, the launcher bootstrap contract and the analogous
Launcher adapter do not establish whether the ready deadline starts before
lease acquisition and process creation.
Open: owner must decide whether this adapter's `readyDeadline` covers the whole
start (with no late process creation and confirmed cleanup) or only readiness
after creation. The real CI hang still needs stack/phase evidence. No push.

### 2026-09-29 decision 194 whole-start deadline implementation

State: local R3 branch diff containing an R2 production correction and an R3
contract path; commit authorized,
push and GitHub writes remain prohibited. Decision 194 resolves the earlier
pre-wait deadline question. The managed Desktop regression failed on the original
source with a blocked final validation and a 150 ms ready deadline (1 failed);
the corrected Desktop and analogous version Launcher tests passed together (2/2).
Both adapters now start one deadline at entry, reject process creation after it
expires, and use the existing typed outcomes and cleanup owner. The launcher
bootstrap contract and the affected port XML docs record the decision.
Admission: base `d27c7c2fa`, risk R3 (`firmware-owner`, `release-owner`), implementation owner Codex `gpt-6-sol`
(single writer). Owner search found the two VersionManagement Infrastructure
process adapters as the current producers, the Application managed-process
ports and activation coordinators as callers, and `ProcessLaunchGate` as the
sole contained native start owner. Disposition: extend the existing adapter
deadline handling, reuse the gate and typed outcomes, reject a second launch
path. No firmware bytes, ranges, integrity, support matrix, or Launcher budget
values change. Narrow gate: the two blocked-validation regressions; final local
gates: Infrastructure VersionManagement 616/616, Bootstrap launcher host 18/18,
Platform process-gate architecture 1/1, and structure verification passed.
Independent fixed-head R2 review of the initial production commit found a
caller-cancellation READY result race (P1) and an unbounded cleanup wait (P2).
Both corrections and three controlled tests are included in the checkpoint;
see `BUG-20260929-managed-start-cancellation-outcome` and
`BUG-20260929-managed-start-cancellation-unbounded` for their evidence.
Open: #488's actual CI hang cause remains unproven without a stack/phase trace.
Independent exact-head R3 review, both named owner approvals on the last push,
and protected CI remain for integration; no push.

### 2026-09-30 independent-review correction

State: local correction to `e20119d71`, single writer; production behavior and
test evidence updated, no push or GitHub write. The existing Application and
Launcher process adapters own the typed start outcomes; `ManagedStartDeadline`
owns the terminal wait, `ProcessLaunchGate` remains the only contained native
start, and Application coordinators own fallback. Disposition: extend these
owners, reuse `ReadyTimeout` and `TerminationUnconfirmed`, reject a second
semantic launch path. No firmware bytes, write ranges, integrity order, profile
support, or release payload change. The branch remains R3 because its contract
path requires `firmware-owner` and `release-owner`; this local checkpoint does
not supply their last-push approval.

P2-1 red: `PreCreationTimeoutReleasesLeaseBeforeImmediateRollback` failed 1/1
at `Assert.False(start.IsCompleted)` after controlled expiry while its fake
lease was still held. Green: 1/1 after `RunAsync` waited for worker cleanup;
the subsequent fallback acquired the lease and returned `Ready`. Both real
adapter blocked-validation tests then returned `ReadyTimeout` only after lease
status became `Exited`, and an immediate second start returned `Ready` (2/2).
If cleanup cannot finish within two five-second termination intervals, the
existing `TerminationUnconfirmed` blocks fallback. No outcome or SPEC change.

P2-2/P2-3: controlled expiry now occurs after the chosen validation/creation
or ADMITTED boundary. Tests no longer depend on a 150/200 ms process-creation
budget or raw handle values; StartFailed budgets are five seconds. Lease status,
worker completion, absent process marker, and successful immediate fallback
verify cleanup. P3: `Interlocked.Exchange` publishes creation, both cleanup
branches allow ten seconds, ADMITTED uses the deadline token, ADR 0056 records
the amendment date, and the affected port docs state the cleanup extension.
The navigation bug record now distinguishes the drained quiet-shell job from
unconfirmed later startup/focus behavior and cites `0700e25f4`; the lease-hang
record names its local correction owner.

Verification at the changed source: Infrastructure 1,578/1,578; Bootstrap
2,142/2,142; NavigationFocusIndicatorTests 13/13; Architecture
RepositoryBoundaryTests 267/267; controlled timeout group 7/7 on each of 30
loaded repeats; `python scripts/verify.py --structure-only` PASS.
Full UiSmoke first ran 1,860/1,861, with the `(scale: 1, dark: True)` navigation
focus row failing; its quiet output omitted the assertion. An unchanged rerun
with TRX logging passed 1,861/1,861. The rerun artifact is under test-area
`evidence/1.1.15/f115-flk-review-correction/`.

Open: the intermittent full-project navigation failure remains unexplained;
the owner requested that test correction itself stay unchanged. #488's CI hang
still needs a stack or phase trace. This checkpoint is not integration-ready:
the observed UiSmoke failure, independent exact-head R3 review, both named
owner approvals on the last push, and protected CI remain open. No push.
