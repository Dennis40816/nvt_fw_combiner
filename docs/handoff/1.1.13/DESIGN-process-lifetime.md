<a id="1113-wave-5-設計程序取消與視窗生命週期"></a>

# 1.1.13 wave 5 design: process cancellation and window lifetime

Status: revision 6 has received R2 design approval, 2026-09-27, written by Claude Code (Opus 5.5), with Claude
as commander. Review history (`codex/gpt-6-astra`): revision 1 REJECT (F-1 synchronous blocking tree kill);
revision 2 ACCEPT-WITH-CHANGES; revision 3 REJECT (new P1 F-9 and F-4b/F-10/F-11); revision 4
ACCEPT-WITH-CHANGES (F-12 through F-15 remaining); revision 5 ACCEPT-WITH-CHANGES (F-16, F-17 and
ADR wording remaining); revision 6 ACCEPT-WITH-CHANGES, scoped Polytail PASS, recommended for design
approval (only F-18 in an evidence file outside the repository remained; now fixed). The record's `designReview.outcome` is `approved`.
ADR 0081 was accepted by owner decision 97 (2026-09-27).

<a id="0-身分與依據"></a>

## 0. Identity and basis

- Branch `feature/1.1.13/wave5-process-cleanup`, created from `1.1.x` trunk `e6e991af3` (#459 merged),
  worktree `<worktrees>/w5`. The record's `integrationBase` is evidence checkpoint
  `241327de1`. The revision 6 patch was originally produced and validated on `557a9ee6a`; its application
  and test results on the new trunk are recorded in reports after admission and the implementation commit.
- Owner decisions 85–88, 92, and ADR number 0081 are already on the new trunk's board (`docs/handoff/1.1.12.md`,
  `docs/handoff/1.1.13.md`).
- Version allocation follows the `1.1.13` row in the [roadmap](../../architecture/nfc_roadmap.md):
  F03/F06 precede F01/F02/F25. Acceptance criteria: cancellation, termination confirmation, and held-pipe drain are all bounded;
  persistence resumes after handoff failure, and a second Close is allowed; READY cancellation and stale callbacks are isolated.
- Finding contents: the repository retains only summaries and acceptance criteria, namely the roadmap allocation of 2026-09-17
  (`3edb02598`) and AUD-01/AUD-02 in the [audit handoff](../../architecture/post-v1.1.8-audit-handoff.md).
  Neither the original external report nor its attachments entered the repository, so the originals were not read for this design.
  The roadmap records F01/F02/F03 as conditional P2; P1 labels in the external report do not mean they were reproduced locally.
- Summaries in the repository:
  - F03+F06: cancellation callbacks must not leak expected OS errors; execution, termination confirmation, and pipe drain
    must all be bounded; tests must cover child processes holding pipes, refused kills, and timeout/cancel
    races. AUD-02 assigns a single owner and requires preserving F19 ownership.
  - F01+F25: Close → handoff failure → settings and history persistence → second Close must complete;
    ongoing work must finish cleanup before final disposal; stale callbacks must no longer publish results.
  - F02: READY cancellation is isolated.

<a id="1-現況總表"></a>

## 1. Current-state overview

Classification: **reproduced** means observed through deterministic steps in the test area; **code inference** means
judged from code without execution; **not yet reproduced on a real OS** means currently covered only through seams.

| Item | Owner (file:line) | Classification | Evidence |
| --- | --- | --- | --- |
| F03-a Cancellation callback leaks OS errors | `SystemExternalProcessRunner.cs:25-30` (`TryKill` in callback), `:75-84` (catches only `InvalidOperationException`) | Reproduced | When a descendant refuses `PROCESS_TERMINATE`, `Cancel()` throws `AggregateException(Win32Exception 5)`, and `RunAsync` fails with the same exception |
| F03-b Timeout-path leak | `SystemExternalProcessRunner.cs:35-38` | Reproduced | Under the same conditions, timeout does not return `TimedOut`; instead `RunAsync` fails with `AggregateException` |
| F03-c UI/CLI consequences | `MainWindow.axaml.cs:145,184` (`async void OnClosing` calls `CancelActiveRun`), `CompositionRunPresentationViewModel.cs:84-87,197-241`, CLI `Program.cs` `HandleCancel`, `CliApplication.cs:97-106` | Code inference | None of the catch filters include `AggregateException`; `src` has no global `UnhandledException` handler |
| F03-d Race between kill and process exit | Same as F03-a | Code inference | `Kill` may throw `Win32Exception` (access denied) while the process is exiting |
| F06-a Pipe still held after normal exit | `SystemExternalProcessRunner.cs:49-54`, `BoundedProcessOutputReader.cs:30-32` | Reproduced | Timeout 2 seconds, background grandchild holds stdout for about 10 seconds; `RunAsync` waited 10.4 seconds and finally returned success |
| F06-b Cancellation during drain still returns success | Same as above | Reproduced | Canceled at 3 seconds; `RunAsync` returned `exit=0` |
| F06-c Orphan outside the tree walk | `SystemExternalProcessRunner.cs:37-40` | Reproduced | Timeout 2 seconds, orphan holds the pipe; `RunAsync` did not return `TimedOut` until 10.2 seconds |
| F06-d Exit not observed after kill | `SystemExternalProcessRunner.cs:86-95` (`WaitForExitAfterKillAsync` is unbounded) | Code inference; not yet reproduced on a real OS | Covered by seams: blocked termination, ineffective termination, failed exit observation |
| F06-e Live descendant blocks staging | `ExternalStagingDirectory.cs:45-62` (`Dispose` swallows `IOException`) | Code inference (a similar phenomenon was observed in the test fixture) | A live process uses the directory as its current working directory, causing deletion to fail |
| F01 Persistence latch after handoff failure | `LatestSnapshotPersistenceCoordinator.cs:31-34,52-59`; `MainWindow.axaml.cs:141-204,564,631`; `MainWindow.VersionManagement.cs:127-137` | Code inference | See §7.1 |
| F02 Child-side READY cancellation | `MainWindow.axaml.cs:243-273`; `ManagedApplicationStartupCoordinator.cs:35-46`; `AnonymousPipeManagedApplicationProcess.cs:406-407` | Code inference | See §7.2; parent side is already isolated (`:156-170`, with existing tests) |
| F25 Ongoing work not observed before disposal | `MainWindow.axaml.cs:229-240`; `CompositionRunPresentationViewModel.cs:170-270`; `MainWindow.axaml.cs:273` | Code inference | See §7.3 |

Incidentally discovered bug (suspected):
[BUG-20260926-process-start-failure-escapes-typed-result](../bugs/BUG-20260926-process-start-failure-escapes-typed-result.md).
Outside AUD-02 scope; recommend a separate R1 under the same owner. Review addition: stable OS startup
restrictions may also trigger it, so the frequency claim “unlikely in ordinary use” lacks sufficient evidence.

<a id="2-f03f06-的重現方法"></a>

## 2. F03/F06 reproduction method

- Used a temporary detached worktree (`557a9ee6a`) in the test area, plus a console harness outside the repository
  (calling only the public `SystemExternalProcessRunner` API, with no assertions). Each shell first
  loaded `NFC_TEST_AREA_ROOT` and set `TEMP`/`TMP`/`TMPDIR` to its `temp`.
  All `packages.lock.json` files rewritten by the build were restored.
- Scenarios:
  - `cmd /c "start /b ping"`: a pipe remains held after normal exit.
  - outer → inner → ping: after inner exits, ping becomes an orphan outside the tree walk.
  - F03-a/b: a descendant process that refuses termination requests (details retained only in evidence outside the repository).
- Platform probe (not product code): on .NET 10 and Windows, `ReadAsync(token)` on redirected stdout held
  by a grandchild can cancel on time. The review noted that this does not guarantee every reader returns
  immediately, so revision 2 does not depend on it: an uncooperative reader is detached at the deadline (§4.4).
- Complete output is recorded in scratchpad `w5/repro-evidence.txt`; harness source is in
  `w5/repro-harness/`. This batch used no UI host and touched no local state files.

<a id="3-owner-盤點與-capability-reuse-gate"></a>

## 3. Owner inventory and capability-reuse gate

| Concern | Owner | Notes |
| --- | --- | --- |
| External process execution, timeout, cancellation, termination | `Infrastructure.ExternalTools.SystemExternalProcessRunner` | SPEC item 27, ADR 0006, `docs/architecture/external-combiner-tool-runner.md` |
| Pipe drain | `BoundedProcessOutputReader` (runner helper) | Retains diagnostic text within the bound |
| Mapping results to issue codes | `ExternalCombinerProcessor`, `LegacyCombinerPostbuildProcessor`, `RuntimeTrustProbeProcess` | These are the only three production consumers |
| Startup and staging (F19) | `ProcessLaunchGate`, `ExternalStagingDirectory` | Unchanged in this batch |
| Source of run cancellation | `CompositionRunPresentationViewModel.CancelActiveRun`/`CancelRun`; CLI `Program.HandleCancel` | Unchanged in this batch |
| Window close and handoff | `MainWindow.OnClosing`, `MainWindow.VersionManagement`, `SettingsViewModel.HandleLauncherHandoffFailureAsync`, `StableLauncherHandoff` | Second batch |
| Local state persistence | `LatestSnapshotPersistenceCoordinator` (F08 also changes this file in wave 2) | Second batch |
| READY | Child side: `InheritedPipeApplicationReadySignal`, `ManagedApplicationStartupCoordinator`, `MainWindow.RunStartupPreloadAsync` | Second batch |

Gate disposition is `extend-owner`: extend the runner, adopting the same uncertainty classification as
`ManagedProcessTermination`, plus `NotSupportedException`. Review confirmed there is no second external-tool execution
path, all three consumers handle it, the F19 owner is unchanged, and no cross-layer reference to
VersionManagement is needed. Search evidence is in the record's `searchEvidence`.

<a id="4-第一批設計第-6-版process-cleanup-1113-01f03f06"></a>

## 4. First-batch design revision 6: `PROCESS-CLEANUP-1113-01` (F03/F06)

The complete contract is in the ADR 0081 draft (scratchpad `w5/ADR-0081-external-process-cleanup.draft.md`,
`docs/adr/0081-external-process-cleanup.md` in the patch). This section lists only design highlights and their
mapping to review comments.

<a id="41-審查意見的回應"></a>

### 4.1 Responses to review comments

Closed: F-1, F-2, F-5, F-6, F-7 (before round two); F-3, F-4a, F-8 (round three); F-9 (CAS
hard cap), F-4b, F-10, F-11 (round four). This version (revision 5) addresses four items from round four:

| Review comment | Revision 5 disposition |
| --- | --- |
| F-12 [P2] Concurrent test calls `RunAsync` sequentially, so slots are not actually contested | `CapacityIsAHardCapUnderConcurrentStarts` now gives each run its own thread (`TaskCreationOptions.LongRunning`); all call `RunAsync` together after reaching the same `Barrier` (slot reservation occurs before the first await), retaining the exit gate. Assert exactly 3 starts and 5 capacity refusals; after opening the exit gate, wait for each started run to publish `ResourcesReleased`, then assert capacity returns to zero. Added pure test `TryReserveIsAtomicUnderContention`: 32 threads released together by a `Barrier` call `TryReserve` directly, repeated for 200 rounds, validating exactly the cap's number of successes and a value at least the cap observed by every refusal. Updated ADR concurrency-verification claims accordingly |
| F-13 [P2] Exception safety of `Finish()` | `Finish()` tries each handle separately (through the new `DisposeResource` seam), so one failure does not skip the others; disposal exceptions are observed and do not escape; the slot is always returned in `finally`; disposal failure publishes the new `ResourcesReleaseFailed`, not the success signal `ResourcesReleased`; the detached continuation also has fault observation attached. Added fault-injection tests, one each for inline and detached paths: make Process disposal throw, then verify all 5 disposals are attempted, slot usage returns to zero, and only the failure signal is published. ADR item 5 records this design |
| F-14 [P2] Capacity diagnostics incorrectly refer to detached invocations | Renamed exception field `DetachedInvocations` to `InUseInvocations`, holding the in-use count observed by atomic reservation at refusal (no later reread of `Outstanding`); both exception and user messages now say “external-tool runs still executing or cleaning up have filled the capacity,” retaining restart guidance and omitting numbers from the user message. Internal names synchronized: `DetachedCleanupBudget` → `ExternalProcessCapacity`, seam `DetachedCleanup` → `Capacity`, `Outstanding` → `InUse`. The ADR and architecture summary qualify normal-run compatibility: running invocations also occupy slots, so behavior matches the past only while concurrent in-use runs stay below the cap |
| F-15 [P3] Slot lifecycle in this document is outdated | §4.4 now states: reserve before startup → detached work retains the original slot → publish the final signal only after disposal and slot return complete |

Revision 5 also removed an environment-dependent real-OS “termination refused” fixture from the lifetime tests; round five
accepted this without requiring a replacement fixture. The corrected contract is supported by refusal and cancellation seams;
the retained F03-a/b reproduction records demonstrate the base defect. This document does not claim that any revision has
passed the real-OS termination-refusal scenario again.

Revision 6 addresses two items from round five and ADR wording:

| Review comment | Revision 6 disposition |
| --- | --- |
| F-16 [P2] Concurrent test's exit seam treats “gate opened” as “process exited” | The `HeldExitAsync` gate controls only release: after opening, it still waits for the real `process.WaitForExitAsync(observationToken)` before returning success; canceled observation ends canceled rather than becoming success. Added assertions for every admitted run: `ExitCode == 0`, `TimedOut == false`, `Cleanup == Complete`, retaining checks for the final signal and capacity returning to zero. Checked other seams: `TerminationSeam.Block` had the same pattern (reporting success after gate opening without terminating), and now performs a real tree kill after gate opening (slow kill); `Ignore` is intentionally ineffective termination (the test asserts unconfirmed), not a gate; reader, exit-observation fault, and disposal seams all end in faults, without equating gate opening to success |
| F-17 [P3] ADR item 10 runs into the end of item 9 | Restored a newline before `10.`, synchronized between the patch and external draft; all 10 numbered ADR items now start on separate lines |
| ADR wording | Changed “compare-and-set itself observed” to “atomic reservation observed” (when capacity is full initially, the value comes from the initial read and CAS may not have executed); “reported” for `ResourcesReleaseFailed` is limited to an internal phase published only to the test observer, with no production observer, log, or user notification, and does not form a public notification contract. Source comments and the record are synchronized |

<a id="42-時間軸單一-deadlinedecision-86"></a>

### 4.2 Timeline (single deadline, decision 86)

```text
terminal signal (T0) = natural exit | manifest timeout | caller cancellation | exit observation failure
T0            On timeout, cancellation, or exit observation failure, immediately start the sole termination work (background task)
T0 .. T0+2s   Only on natural exit: give streams a held-output grace; if still open when it expires, mark held,
              and start termination (the tree walk can still find direct descendants)
.. T0+4s      Wait for termination, exit observation, and both streams to complete
T0+4s         If streams have not ended, request reader stop (reserve the last 1 second for readers to return)
T0+5s         Make the terminal decision and return; stop waiting for unfinished work
```

- Constants are in `ExternalProcessCleanupTiming.Default` (5 seconds / 2 seconds / 1 second). These are host
  constants, not manifest or profile timeouts; tests inject shorter values through an internal seam.
  Pure function `Schedule(signaledAt)` computes three absolute times (grace, reader-stop,
  deadline); reader-stop falls within the deadline (deadline − reserve), not after it.
- `Cancel()` only calls `TaskCompletionSource.TrySetResult` once, with
  `RunContinuationsAsynchronously` set, and performs no process work.

<a id="42a-呼叫容量decision-92"></a>

### 4.2a Invocation capacity (decision 92)

- The runner has a process-wide `ExternalProcessCapacity` with a fixed **hard cap of 8**, counting
  invocations **still running or still cleaning up**. Through the static Production seam, all runner instances
  (the three consumers and the trust probe's `CreateDefault`) share the same count.
- **Hard cap (F-9)**: before `ProcessLaunchGate.Start`, `RunAsync` atomically reserves a slot through
  `TryReserve(out observedInUse)` (compare-and-set, taking a slot only while the count is below the cap),
  so the in-use count never exceeds the cap, regardless of concurrency. The slot is held throughout the invocation
  and returned exactly once on startup failure, completion, or settlement of detached work (including disposal failure).
  Reservation failure throws `ExternalProcessCleanupCapacityException` before any process starts; its `InUseInvocations`
  is the value observed by atomic reservation at refusal (from the initial read if capacity was full initially, otherwise from a failed CAS).
- Basis for cap 8: external-tool execution is effectively serialized within each workflow (one UI run or one CLI
  invocation at a time; ADR 0075's preload worker is not an external tool), so more than a single-digit number of
  concurrent in-use or stuck invocations represents a host-level fault, not normal load. 8 leaves room for occasional,
  temporarily slow termination while keeping the resource cap a small constant. Cost: when 8 runs are still executing,
  the 9th is refused even if none is stuck; ordinary operation starts one at a time and does not encounter this.
- This complements rather than replaces the 5-second deadline: the deadline bounds each return, and the hard cap
  bounds accumulation. See §4.4 for the slot lifecycle and final signals.
<a id="43-cleanup-值與-terminal-outcome"></a>

### 4.3 Cleanup values and terminal outcome

- `Cleanup` values: `Complete`, `TerminationUnconfirmed`, `OutputStreamHeldOpen`,
  `OutputReadFailed`. Definitions and precedence are in ADR 0081 item 6.
- Cancellation takes priority if requested before the decision point, ending with an `OperationCanceledException`
  carrying the caller's token. Its message includes the observed cleanup value so internal facts are not lost,
  but it is not a contract on which branching logic may depend.
- Cancellation after the decision point does not change the decided result.
- Consumer mappings:
  - Timeout and nonzero exit retain their existing issue codes, with cleanup explanation appended to the message.
  - On exit 0 with `Cleanup != Complete`, fail closed with `external-tool.process.cleanup-incomplete`
    before reading any staged file as a result.
  - Only `Complete` allows captured output to be used as a successful result or protocol input; error diagnostics
    for timeout/nonzero exit may still quote partial output.
  - Trust probe: timeout takes priority, returning `runtime.trust.timeout`; only non-timeout incomplete cleanup
    returns `runtime.trust.probe-failed`.
  - Capacity refusal: staged processors return `external-tool.process.cleanup-capacity`,
    and the trust probe returns `runtime.trust.probe-failed`.

<a id="44-所有權handle逾時工作與晚到的-fault"></a>

### 4.4 Ownership: handles, overdue work, and late faults

- Each call has an `Invocation` custody owning the process handle, both stream readers,
  exit observation, the sole termination work, and all cancellation sources.
- Work still running at the deadline (such as a blocked kill or a reader ignoring stop requests) is detached,
  not forcibly aborted. Only after all background tasks end does custody dispose `StandardOutput`,
  `StandardError`, `Process`, and cancellation sources, while observing all late faults,
  so they neither become unobserved exceptions nor get attributed to the next run.
- The runner's `finally` always calls `Release()`. If the direct child's exit has never been observed
  (for example, an unexpected exception occurs before the terminal signal), `Release()` also starts termination,
  without waiting for it to complete.
- Cost: detached work retains process and pipe handles until it ends or the host process ends.
  Accordingly, the document claims “bounded return, retained ownership, reclamation after settlement, and a bounded
  detached count,” not “no remaining handles within 5 seconds.” This is in ADR 0081's Consequences.
- The decision 92 cap (§4.2a) ensures such remnants cannot accumulate without bound: new runs are refused at the cap.
  Slot lifecycle: **atomically reserve before startup** → if cleanup is ongoing when the run returns,
  the detached path **retains the same slot** (`Detached` phase is informational only) → after background work settles,
  dispose handles one by one and **return the slot**, then publish the final signal: `ResourcesReleased`
  if all disposals succeed, or `ResourcesReleaseFailed` if any disposal fails (the slot is still returned,
  with no success signal). Both final signals are only internal phases published to the test observer; production
  has no observer, writes no log, and gives no user notification, so they are not a public notification contract.
- Final-step exception safety (F-13): try each handle separately so one failure does not skip others; disposal failures
  are observed and do not escape; the slot is always returned in `finally`; the detached continuation itself is also observed.

<a id="45-ui-與-cli-的呈現decision-85q3"></a>

### 4.5 UI and CLI presentation (decision 85, Q3)

- Presentation and CLI code and existing classification channels stay unchanged. New message text, reclassifying
  exit 0 as failure, and bounded waits are all observable behavior changes.
- After cancellation, existing presentation remains: UI publishes no result; CLI prints `error: operation canceled`, exit 70.
  Confirmed that neither has wording such as “all stopped confirmed.” The first batch adds no warning.

| Situation | Now | After this batch |
| --- | --- | --- |
| UI cancellation or Close; descendant refuses termination | `Cancel()` throws on the UI thread, inferred to terminate the application | `Cancel()` returns immediately; run ends canceled within 5 seconds |
| UI timeout; kill refused or blocked | `AggregateException` escapes through the run session | Build/Preview fails with `external-tool.process.timeout`, message includes cleanup explanation |
| Child holds output after tool exit | Waits until the child exits, then succeeds | At most about 5 seconds, then fails with `cleanup-incomplete` |
| CLI Ctrl+C; kill refused or blocked | Unhandled exception in the handler (inferred) | Prints `error: operation canceled` within 5 seconds, exit 70 |
| Still-running or cleaning-up runs reach cap 8 | Unbounded resource accumulation (inferred) | New run fails with `external-tool.process.cleanup-capacity`, prompting restart |
| Ordinary normal run | - | Unchanged |

<a id="46-變更範圍"></a>

### 4.6 Change scope

- 15 `mutablePaths` in total:
  - 10 governed: `docs/adr/0006-external-combiner-tool-runner.md`,
    `docs/adr/0081-external-process-cleanup.md`, and, under
    `src/NvtFwCombiner.Infrastructure/ExternalTools/`,
    `SystemExternalProcessRunner.cs`, `SystemExternalProcessRunner.Invocation.cs` (new),
    `BoundedProcessOutputReader.cs`, `ExternalProcessResult.cs`,
    `ExternalCombinerProcessor.cs`, `LegacyCombinerPostbuildProcessor.cs`,
    `RuntimeTrustProbeProcess.cs`, `ToolchainRuntimeCandidateInspector.cs` (added in revision 4 for F-11).
  - 5 auxiliary tests.
- Revision 4 has one more governed file than revision 3: F-11 carries the capacity reason from trust probe to inspection
  result, changing the existing `ToolchainRuntimeCandidateInspector.cs` (an existing R2 Infrastructure
  file with unchanged owner).
- `docs/architecture/external-combiner-tool-runner.md` becomes a summary pointing to ADR 0081.
  The validator does not classify it as governed, so it is absent from `mutablePaths` but included in the same diff.
- Revision 5 has the same file set as revision 4 (16 patch files, 15 of them `mutablePaths`);
  F-14 only renames within existing files (`ExternalProcessCapacity`, `Capacity` seam, `InUse`,
  `InUseInvocations`); F-13's new `DisposeResource` seam is also in existing files.
- Code size (revision 5): the Infrastructure + Contracts + CRC worker slice grows from 33,195 lines
  to 33,820 lines (+625 nonblank lines). The base itself already exceeds that slice's and several other advisory
  review thresholds; this batch does not change the ratchet.
- Excluded: job object (decision 85, separately scheduled R2), startup-failure classification (bug file), any
  Presentation or CLI code, the VersionManagement termination owner, manifest timeout.

<a id="5-風險分級r2"></a>

## 5. Risk classification: R2

- Reasons for R2:
  - Changes the runner's terminal contract: new public enum and properties, cancellation precedence, host
    deadline.
  - Adds an issue code and reclassifies exit 0 with incomplete cleanup as failure.
  - Adds an ADR and changes ADR 0006. The validator infers a minimum risk of R2 from `docs/adr`.
- Reasons it is not R3:
  - No changes to firmware bytes, ranges, order, CRC/Header, naming, profiles, manifests, release,
    or permissions.
  - Reassess if containment or capabilities requiring platform privileges are added later.
- Non-regression evidence: run existing real-tool smoke and a filtered Golden subset at pre-freeze to prove unchanged
  bytes on normal paths. This is not Golden certification.

<a id="6-測試計畫與目前結果"></a>

## 6. Test plan and current results

<a id="61-lifetime-測試systemexternalprocessrunnerlifetimetests24-個方法30-個案例"></a>

### 6.1 Lifetime tests (`SystemExternalProcessRunnerLifetimeTests`, 24 methods, 30 cases)

Revision 6: the exit seam in `CapacityIsAHardCapUnderConcurrentStarts` now still waits for the real process to exit after gate opening and asserts `ExitCode == 0`, `Cleanup == Complete` for every admitted run (F-16); `TerminationSeam.Block` now performs a real tree kill after gate opening.
Revision 5: `CapacityIsAHardCapUnderConcurrentStarts` (formerly `DetachedBudgetIsAHardCapUnderConcurrentStarts`)
now starts concurrently through independent threads plus `Barrier` (F-12); added `TryReserveIsAtomicUnderContention`
(F-12), `DisposalFailureStillReturnsSlotAndSignalsFailure`, and
`DetachedDisposalFailureStillReturnsSlotAndSignalsFailure` (F-13); removed the environment-dependent real-OS
termination-refusal fixture (classification now relies on refusal-seam tests).
Revision 4: rewrote `OrphanHoldingOutputAfterTimeoutIsBoundedAndReported` with a PID-based
parent-exited handshake (F-4b); changed count assertions in `DetachedCleanupIsBoundedAndRefusesNewRunsAtTheLimit`
to read after waiting for `ResourcesReleased` (F-10). Revision 3 added: a pure-function schedule test,
the `CancelWithinAsync` guard, and `CleanupDiagnosticsDescribeOnlyTheObservation`.

| Test | Type | Verification | Red-test method (on base) |
| --- | --- | --- | --- |
| `CancelReturnsAtOnceAndRunEndsWithinDeadlineWhileTerminationBlocks` | Blocking seam + real ping | `Cancel()` < 1 second; run ends within deadline + 4 seconds; termination called only once; custody not released while termination blocks, released only after unblocking | Code inference: base kills synchronously in the callback, so a blocking kill would also block `Cancel()`; the harness observed only an exception, not blocking |
| `TimeoutWithBlockedTerminationReturnsUnconfirmedWithinDeadline` | Blocking seam | `TerminationUnconfirmed`; TimeoutSignaled → Returning within the bound | Code inference: base kills synchronously on the runner thread, and subsequent waiting is also unbounded |
| `RefusedTerminationIsClassifiedAsUnconfirmed` (×4) | Refusal seam | All four exception types classified as unconfirmed | Harness: `AggregateException` escapes |
| `RefusedTerminationOnCancellationEndsCanceled` | Refusal seam | `Cancel()` does not throw; run ends with OCE | Harness denied-kill-cancel |
| `TerminationWithoutObservedExitIsBoundedAndUnconfirmed` | Ineffective seam | Bounded and unconfirmed | Code inference (base wait is unbounded) |
| `CancellationRightAfterTimeoutSignalEndsCanceled` | Phase handshake | Cancellation immediately after timeout is decided still ends with OCE | Compile level (base has no seam) |
| `CancellationRightAfterExitSignalEndsCanceled` | Phase handshake | Cancellation immediately after natural exit still ends with OCE | Harness held-pipe-normal-cancel: base returns exit 0 |
| `CancellationAfterTerminalDecisionKeepsResult` | Phase handshake | Cancellation after the decision point leaves the result unchanged | Compile level |
| `UncooperativeReaderIsDetachedAtDeadlineAndObservedLater` | Reader seam | Returns `OutputStreamHeldOpen`; custody not released while reader runs; released only after the reader's late fault is observed | Compile level |
| `ReaderFaultWithoutCancellationIsOutputReadFailed` | Reader seam | Reader fault classified as `OutputReadFailed` | Compile level |
| `ReaderFaultWithCancellationEndsCanceled` | Reader seam + handshake | Cancellation takes priority over reader fault | Compile level |
| `ExitObservationFaultIsUnconfirmedOrCanceled` (×2) | Exit-observation seam | Without cancellation: unconfirmed, exit -1, termination only once; with cancellation: OCE | Compile level |
| `HeldOutputAfterNaturalExitIsBoundedAndReported` | Real OS | `OutputStreamHeldOpen`; ExitSignaled → Returning within 5 + 4 seconds | Harness: base takes 10.4 seconds and returns success |
| `CancellationDuringHeldDrainEndsCanceledWithinDeadline` | Real OS + handshake | `Cancel()` < 1 second; run ends with OCE within the bound | Harness: base returns exit 0 |
| `OrphanHoldingOutputAfterTimeoutIsBoundedAndReported` | Real OS | `OutputStreamHeldOpen`, within the bound | Harness: base takes 10.2 seconds |
| `DescendantWithoutRedirectedStreamIsNotObserved` | Real OS | Locks in decision 85's limitation: result is `Complete`, but the descendant is still running | Locks in a limitation, not a fix item |
| `CleanupScheduleKeepsReaderStopWithinTheSingleDeadline` | Pure function | reader-stop = deadline − reserve (within the deadline, not added) | Fails if reserve is added after the deadline |
| `CancelWithinGuardFailsPromptlyOnBlockingCancel` | Guard | A synchronously blocking callback is judged failed within 1 second rather than stalling until the watchdog | Regression test for the guard itself |
| `CleanupDiagnosticsDescribeOnlyTheObservation` (×3) | Pure function | Text for the three cleanup values describes only observations, without "another process" / " kept " | Fails with revision 2 wording |
| `DetachedCleanupIsBoundedAndRefusesNewRunsAtTheLimit` | Blocking seam + cap 1 | At the cap, a new run is refused with `ExternalProcessCleanupCapacityException`; after `ResourcesReleased`, `InUse==0` and a new run succeeds | Compile level (base has no budget) |
| `CapacityIsAHardCapUnderConcurrentStarts` | Independent threads + shared `Barrier` start, capacity shared by multiple runners, blocking exit-observation seam | Cap 3, 8 concurrent `RunAsync` calls: exactly 3 start processes, 5 are refused before startup; after opening the exit gate, every started run publishes `ResourcesReleased` and capacity returns to zero. Contains 10 iterations | Code inference: base prechecks may exceed the cap under concurrency |
| `TryReserveIsAtomicUnderContention` | Pure test, 32 threads released together by `Barrier` | Exactly the cap's number succeed, refusal observations are at least the cap, and usage returns to zero after slots are returned; 200 rounds | Nonatomic read–check–increment allows calls beyond the cap through |
| `DisposalFailureStillReturnsSlotAndSignalsFailure` | Disposal fault injection (inline path) | All 5 disposals attempted, slot usage returns to zero, only `ResourcesReleaseFailed` published; returned slot is reusable | Compile level |
| `DetachedDisposalFailureStillReturnsSlotAndSignalsFailure` | Disposal fault injection (detached path) | Same as above, inside the detached continuation | Compile level |

<a id="62-其他測試"></a>

### 6.2 Other tests

- Reader unit tests: `StoppedDrainKeepsCapturedTextWithoutEndOfStream`,
  `CompletedDrainReportsEndOfStream`.
- `ExternalCombinerProcessorTests.CleanupOutcomeIsClassifiedBeforeAnyStagedRead` (6 rows,
  including a `Complete` control), `TransformMapsCleanupCapacityRefusalToTypedIssue`.
- `LegacyCombinerPostbuildProcessorTests.CleanupOutcomeStopsSequenceBeforeAnyStagedRead`
  (6 rows, a two-command profile, verifying `RunCount == 1`), `CleanupCapacityRefusalMapsToTypedIssue`.
- `ToolchainRuntimeCandidateInspectorTests`: `ProbeWithIncompleteCleanupFailsClosed` (3 rows),
  `ProbeTimeoutKeepsPriorityOverIncompleteCleanup`, `ProbeCleanupCapacityRefusalCarriesRestartGuidance`
  (restart message at the probe layer), `CapacityRefusalDuringProbeReportsRestartGuidance`
  (final inspector-layer message includes Restart, not “Windows could not verify”).

<a id="63-執行結果暫時-worktreebase-557a9ee6a-加上第-6-版-patch不是-w5-分支"></a>

### 6.3 Execution results (temporary worktree, base `557a9ee6a` plus revision 6 patch; not the w5 branch)

| Scope | Result | Time |
| --- | --- | --- |
| Build (Infrastructure and Infrastructure.Tests, analyzers enabled) | 0 warnings, 0 errors | - |
| `SystemExternalProcessRunnerLifetimeTests` | 30/30 passed | 32 seconds |
| Separate consecutive runs of `CapacityIsAHardCapUnderConcurrentStarts` and `TryReserveIsAtomicUnderContention` | 3 additional all-green runs (10 iterations per run for the former, 200 rounds per run for the latter) | About 1 second each |
| Narrow filter: `SystemExternalProcessRunner`, `BoundedProcessOutputReaderTests`, `ExternalCombinerProcessorTests`, `LegacyCombinerPostbuildProcessorTests`, `ToolchainRuntimeCandidateInspectorTests` | 126/126 passed (one complete execution of revision 6) | 31 seconds |
| Architecture.Tests (after document and ADR changes) | 269/269 passed | 15 seconds |

Round-five review noted that `Barrier` does not guarantee a particular instruction interleaving on every run, so repeated
green results are evidence of contention stress, not a formal proof of atomicity; correctness of the cap is separately
supported by reasoning about the CAS code itself.

- 0 analyzer warnings.
- Not yet run: real-tool smoke, Golden subset, `verify.py`. The validator would currently fail because the record
  is not yet staged; this is a known blocker specific to the record, so it was not executed.
- Confirmed no helper processes remained after tests ended.

<a id="64-環境注意事項"></a>

### 6.4 Environment notes

- Helpers for the orphan and “descendant without a connected pipe” tests survive the run, so execution must be outside the workspace.
- No UI host is needed, and no local state files are written.

<a id="7-第二批f01f02f25盤點與設計入口條件"></a>

## 7. Second batch (F01/F02/F25): inventory and design entry conditions

<a id="71-f01handoff-失敗後的恢復"></a>

### 7.1 F01: recovery after handoff failure

- Defects inferred from code:
  - The first Close calls `CompleteAsync()`, permanently setting `_isCompleted` to true
    on both coordinators.
  - After handoff failure the window becomes usable again; changing theme or language or generating a new report
    then throws an unhandled `InvalidOperationException` in a `PropertyChanged` handler.
  - `_isReportHistoryPersistenceComplete` stays true, so the second Close does not
    flush again.
  - `_startupLoadCancellation` has been canceled, so subsequent preload Retry uses a canceled token.
- Dependency: F08 (`feature/1.1.13/f08-save-notice`) also changes
  `LatestSnapshotPersistenceCoordinator.cs` and `MainWindow.axaml.cs`, retaining
  the latch. The second batch must use the version after F08 merges as its base.
- Decision 88: when the launcher cannot start and pending activation cannot be cleared, the second Close offers
  “Retry” and “Close anyway,” explaining that the pending settings remain and have not been reverted.

<a id="72-f02ready-取消"></a>

### 7.2 F02: READY cancellation

- The parent side is already isolated, covered by `CallerCancellationPropagates` and `ReadyTimeoutFailsBoundedly`.
- Child side (code inference): closing the window during the READY write or `InitializeAfterManagedReadyAsync`
  lets OCE escape from `async void OnOpened`; `ApplyVersionSnapshot` may also publish
  after close.

<a id="73-f25dispose-前收尾"></a>

### 7.3 F25: cleanup before disposal

- Code inference: `OnClosing` only calls `CancelActiveRun()` without waiting; `Dispose()` handles only
  startup CTS and preload; `MainWindowViewModel` does not implement `IDisposable`;
  `RunCompositionAsync` still publishes its result when it ends.
- Decision 87: no force-close option; close automatically after a bounded wait, provided all old work has lost
  the right to publish results.

<a id="74-第二批設計入口條件審查-f-7"></a>

### 7.4 Second-batch design entry conditions (review F-7)

The second-batch design must satisfy these conditions before entering review:

1. **Close attempt and session generation**:
   - Create a new close generation for every Close attempt.
   - When recovering after handoff failure, create a new session generation with new startup
     cancellation.
   - Every result publication point (run, inspection, Config, Report, version management, version snapshots
     after READY) must check all three: generation, cancellation state, and lifetime. Checking only
     “the window is still alive” is insufficient: the window is still alive after handoff failure.
2. **Cleanup contract for each task owner**: run, inspection, Config
   (`EventBufferFormatConfigurationSession`, `ToolchainRuntimeConfigurationSession`),
   Report, version installation/switching/deletion, and READY must each define a wait bound, invalidation after
   timeout (loss of publication rights), and fault observation. The first-batch runner's 5-second deadline covers
   only external processes and cannot imply a completion bound for other work.
3. **Decision 87**: before automatic close, all old generations must have lost publication rights, and their
   faults must still be observed rather than lost.
4. **Decision 88** UI and wording require a Settings or dialog design reference (under the owner's
   UI mockup rules).
5. **Required tests**:
   - “Handoff failure → recovery → late old callback”: the old callback must not overwrite the new session.
   - Close → flush → handoff failure → change settings → second Close.
   - Close the window during READY.
   All require a headless Avalonia host. Do not begin writing tests until the “UI tests write real local state files” bug
   ([BUG-20260926-tests-write-real-local-state](../bugs/BUG-20260926-tests-write-real-local-state.md))
   is fixed, or all tests use isolated state files.
6. **Order**: fix and accept F03/F06 → incorporate F08 and the isolated-local-state test prerequisite → shared
   F01+F25 Close state machine → F02. F02 generation and cancellation contracts must be designed together;
   complete lifetime acceptance waits for F02 completion.

<a id="8-owner-決定已決定與剩下的問題"></a>

## 8. Owner decisions (settled) and remaining questions

Settled:

- **85** (2026-09-26): fail closed without reading output when incomplete cleanup is observed; accept “沒有接 pipe 的後代可能看不到” (“descendants without a connected pipe may not be observable”); Job Object is a separate R2 item.
- **86** (2026-09-26): termination confirmation and reader stop share a total 5-second host deadline; cancellation
  callbacks only signal.
- **87** (2026-09-26): no force-close option; close automatically after a bounded wait, but first revoke old work's publication rights.
- **88** (2026-09-26): if the launcher cannot start and pending cannot be cleared, the second Close offers “Retry／
  Close anyway,” explaining that pending settings remain and have not been reverted.
- **92** (2026-09-27): detached, unsettled invocations have a small fixed cap; above it, new runs are refused
  with a typed error prompting restart. This revision implements a fixed hard cap of 8 (atomic reservation).
- **Q3** (per review recommendation): no new warning in the first batch, but do not display “已確認全部停止” (“all stopped confirmed”).

Round-three review explicitly stated that cap 8, allocation within the 5 seconds, and the OCE message approach do not
require asking the owner again. Only items still worth notifying the commander about remain below.

Commander decision or confirmation needed:

1. **Cap value 8**: if the owner wants a smaller value (for example 4) or a larger one, change only
   `ExternalProcessCapacity.DefaultLimit` (the concurrent hard-cap mechanism is independent of the value).
2. **ADR 0006 backlink** (settled (decision 97)): this patch adds
   `Amended by: ADR 0015, ADR 0081` to ADR 0006. ADR 0081 was accepted by owner decision 97 (2026-09-27).
   Recommend retaining it in the patch and confirming it through re-review at admission.
3. **Startup-failure bug ordering**: recommend a separate R1 under the same owner after the first batch merges.

<a id="9-工作量預估"></a>

## 9. Effort estimates

Based on the roadmap's relative units (S=1, M=2, L=3; includes implementation, fault injection, contract tests,
and review; units are not days).

| Batch | Estimate | Basis | Uncertainty |
| --- | --- | --- | --- |
| First batch F03/F06 | M (2), with about 0.5 remaining | Revision 6 draft complete: lifetime 30/30, narrow tests 126/126, Architecture 269/269, 0 analyzer warnings; the two concurrency tests were stable in 3 additional consecutive runs. Round-five F-16, F-17, and ADR wording addressed. Remaining: round-six re-review, admission (rebind checkpoint after batch 2b merges), real-tool and Golden-subset non-regression evidence, fixed-head review, and final evidence | Low: only P2/P3 remained in round five |
| Second batch F01+F25 | L (3), possibly more | Close state machine, generation, cleanup contracts for each task owner, UI tests | High: depends on F08, local-state isolation fix, and decision 88 UI design; Config and Report disposal paths not fully inventoried |
| Second batch F02 | S–M (1–2) | Shares generation contract with F01 | Medium: test control of READY timing |

<a id="10-產出與狀態"></a>

## 10. Outputs and status

- Status: planned. Only documents written locally; no commits and no w5 product-code changes.
- In w5 (uncommitted):
  - This document.
  - [BUG-20260926-process-start-failure-escapes-typed-result](../bugs/BUG-20260926-process-start-failure-escapes-typed-result.md).
- Scratchpad `w5/` (outside the repository):
  - `PROCESS-CLEANUP-1113-01.json`: draft record, schema v2, `design-active`, 15
    `mutablePaths` (10 governed + 5 aux), `designReview` is `codex/gpt-6-astra` /
    `blocked`, recording revision 1 through 5 review conclusions; `integrationBase` is `38b85b15b`; `implementationOwner` is `claude-code`.
  - `PROCESS-CLEANUP-1113-01.proposal.patch`: revision 6 patch, generated from `557a9ee6a`,
    applies cleanly to w5 (`git apply --check`).
  - `ADR-0081-external-process-cleanup.draft.md`: ADR draft, identical to
    `docs/adr/0081-external-process-cleanup.md` in the patch.
  - `repro-evidence.txt`, `repro-harness/`: reproduction evidence for the base and results of each draft revision.
  - SHA-256 for each file is supplied in the report.
