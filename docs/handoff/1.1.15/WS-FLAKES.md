# WS-FLAKES: decision 191 CI flake fixes

State: local work on `feature/1.1.15/flaky-fixes` from `origin/1.1.x` at `aba286bae`.
Owner: Codex `gpt-6-sol`, single writer. Local commits are authorized; push, PR and GitHub writes are not.
Risk: R1 test corrections; reclassify if a production owner changes.
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
