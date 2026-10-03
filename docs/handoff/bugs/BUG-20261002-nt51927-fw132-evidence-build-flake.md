# BUG-20261002-nt51927-fw132-evidence-build-flake: the NT51927 FW 1.3.2 two-chip evidence Build failed once on a pull-request CI run

Status: open (scheduling correction committed as `def8e3af3`; held-pipe review correction locally tested; original CI trigger remains inferred)
Severity: P2 (a CtrlRAM Replace Build of an owner-certified case reported failure once)
Found: 2026-10-02, Claude Code commander (Claude Opus 5.5): pull request #535 (records only: handoff documents),
CI run `37013137906`, job `dotnet / build-test` (`110861530924`). The test failed on attempt 1 and passed on the
in-job retry, so the decision 193 gate marked it flaky and the job failed with "flaky test has no bug record".
The pull request changes no product, test or profile file.
Where: `NvtFwCombiner.Bootstrap.Tests.Nt51927CtrlRamFw132TwoChipEvidenceTests.ExactExpectedDerivedCaseProducesLockedV2EvidenceAsync`
(`icId: "NT51927"`, `expectedProfileId: "nt51927-ctrlram-replace-fw132-twochip"`,
`expectedProcessorId: "nfc.nt51927.ctrlram-postbuild-v1"`),
`tests/NvtFwCombiner.Bootstrap.Tests/Nt51927CtrlRamFw132TwoChipEvidenceTests.cs` line 87 at `2ea530361`.
Original observation (2026-10-02, preserved; later issue detail below supersedes the missing report detail):
`Assert.True(v2.Succeeded, CompositionRunReportJson.Serialize(v2))` failed: the CtrlRAM Replace Build of
the case returned a result that did not succeed. The run report in the message starts at 13:32:51 UTC and
completes at 13:32:54 UTC (3.5 seconds); the job log prints only the first lines of the message (the report's
header and inputs), so the issue that made the run fail is not visible there. The attempt-1 evidence
(artifact `dotnet-test-bootstrap-evidence-attempt-1` of that run) holds the full message; it has not been
downloaded yet (a download needs the owner's approval).
Expected: the Build succeeds on every run and its output has the locked hash; a failure of this test must be
explained, because it compares a certified case's complete output.
Evidence: the job log of run `37013137906`. Not yet established: which issue the report carries (an external
postbuild processor failure or timeout, a staging or output file conflict between parallel tests, or something
else), and whether the output bytes were affected. The retry in the same job passed, including the locked output
hash.
Owner: Claude Code commander.
Resolution: scheduling correction in PR #538, commit `def8e3af3` on base `8479dee8e`, verified by the modeled
same-issue regression and historical 247/247 ExternalTools tests; review-fix evidence follows below.
No tool retry or timing-bound change. The status stays open conservatively because the original CI report
has no scheduling trace proving whether pool delay or a briefly held pipe triggered that particular occurrence.
Next: commander reviews/integrates the review correction under the existing gates; preserve this distinction
if closing the historical CI flake on the strength of the modeled scheduling mechanism.

## Local investigation admission (2026-10-03)

- Source: `feature/1.2.6/external-tool-drain`, PR #538, commit
  `def8e3af39bfeb6c02e805dab699e209dca06d07` on base `8479dee8e`.
  Implementation owner: Codex/GPT-6, primary session. The earlier local patch became that commit;
  ExternalTools is identical between the earlier investigation base and this base, and the frozen hashes match.
- Outcome: model the same refusal and correct the drain scheduling defect through the current owner.
  Non-goals: firmware bytes/ranges/order, processor/profile contracts, staging/diffing, bound changes and retries.
- Behavioral risk: bounded runner correction; path policy floor: R3 (`firmware-owner`, Infrastructure ExternalTools).
  Publication/integration still require independent exact-head review, applicable firmware evidence and owner approval.
- Semantic owner: `SystemExternalProcessRunner.Invocation` owns terminal classification and cleanup;
  `BoundedProcessOutputReader` owns bounded diagnostic capture. Disposition: `extend-owner` for pipe-drain scheduling,
  `reuse` for classification and consumer fail-closed handling. No second terminal or firmware execution path.
- Search evidence: runner/seams, bounded reader, `ExternalProcessResult`, `IExternalProcessRunner`,
  `LegacyCombinerPostbuildProcessor`, `ExternalCombinerProcessor`, `RuntimeTrustProbeProcess`, runner lifetime/output
  tests and processor cleanup mapping tests; authority: accepted ADR 0081.
- Owned mutable surfaces: those runner/reader files, Infrastructure ExternalTools regression tests and this bug record.
  Narrow gate: delayed-read reproduction; final local gate: Infrastructure.Tests `ExternalTools` classes.
- Owner-supplied attempt-1 report (not independently downloaded in this no-network session):
  `external-tool.process.cleanup-incomplete`, redirected stream did not reach its end; nine replace operations
  skipped, `postbuild-twochip` failed, output size 0 and committed false. 2,287/2,288 Bootstrap tests passed;
  retry passed. This supersedes the original lack of issue detail while preserving that original observation above.

## Cause and bounds

Review-fix admission (2026-10-03, PR #538): source `def8e3af39bfeb6c02e805dab699e209dca06d07`
on base `8479dee8e`; local patch only, with no commit/push/network or report file. The owner requests P1-P3
corrections under ADR 0081, without firmware changes or new cleanup bounds. `BoundedProcessOutputReader`
remains the capture/stop owner (`extend-owner`); `Invocation` remains the classification/custody/capacity
owner (`reuse`). Native cancellation belongs in `NvtFwCombiner.Platform/Processes`, already referenced
by Infrastructure, alongside the existing Windows process adapter (`extend-owner`). Search: production
`LibraryImport`/`DllImport` declarations, Platform process adapters, `RepositoryBoundaryTests` and
`HostInfrastructureBoundaryTests.ProductionProcessStartsAreOwnedOnlyByPlatformGate`; no new native
declaration in Infrastructure. Mutable surfaces: the bounded reader, Platform cancellation adapter and
friend access, Infrastructure ExternalTools regression tests, and this record. Gates: real-orphan
red/green lifetime class, reader/processor classes, full ExternalTools class filter and RepositoryBoundaryTests;
existing R3 integration gates remain with the commander. No new public result/firmware contract or ADR decision.

Classification: (a), a runner drain scheduling defect, modeled locally. Attribution of the original CI incident
to thread-pool starvation is a supported inference, not a captured scheduling trace.

On Windows, the runtime's redirected process readers wrap synchronous `FileStream` pipe handles (the regression
checks `IsAsync == false` on both). Their original `ReadAsync` path uses async-over-sync reads on the shared pool;
a queued blocking read, or its continuation waiting to run, can miss EOF even though the tool already closed
its pipe. The runner starts both drains before `WaitForExitAsync`, but it does not join them to exit observation.
`WaitForExitAsync` observes the direct child; it does not await these manually started readers (there is no
`BeginOutputReadLine`/`BeginErrorReadLine` event reader). Exit and reader completion therefore race.

`SystemExternalProcessRunner.Invocation.cs:55` samples the cleanup schedule after choosing the terminal signal;
at lines 61-75, an unsettled reader at the natural-exit grace check latches `heldAfterExit`. Line 257 keeps that
refusal even if both readers later reach EOF before the total deadline. This sticky refusal is intentional for
real held pipes; removing it would weaken the accepted drain bound. The defect is letting a pool-queued read
be mistaken for the held-pipe condition, even when a dedicated drain can promptly observe the real pipe's EOF.

All production bounds remain unchanged:

| Bound | Value | Measurement / signal |
| --- | --- | --- |
| Tool execution | Manifest seconds; packaged Combiner 30 s, schema 1-120 s; trust probe 30 s | `Task.Delay(timeout)` starts in `WaitForTerminalSignalAsync`, after launch, drain startup and exit-observer setup; races exit and caller cancellation. It does not cover process launch. |
| Natural-exit drain grace | 2 s | Cleanup schedule timestamp, sampled after the terminal decision; only natural exit takes this grace. |
| Reader stop | At 4 s | Same timestamp: 5 s deadline minus 1 s reserve, not an additional budget. |
| Total host cleanup | 5 s | Same timestamp; all termination/exit/drain/reader-stop waits share absolute Stopwatch deadlines. Actual return has scheduling latency, as ADR 0081 states. |
| Reader-stop reserve | Last 1 s of those 5 s | From the scheduled reader-stop point to the same deadline. |
| Invocation capacity | 8, including detached cleanup | Atomic reservation before process launch; retained until all background work settles and disposal is attempted. No unbounded queue of admitted runs. |
| Diagnostic capture | 65,536 UTF-16 characters per stream | Cumulative characters read; 32,768 prefix and 32,768 tail ring, with truncation marker included in the returned cap. Capture is bounded; total drained data is not capped. |
| Read buffer | 4,096 characters requested per read | Each read; pooled allocation may be larger. |
| Dedicated drains after correction | At most 2 per admitted invocation, hence 16 | Same invocation capacity and detached custody; no new process-wide switch or capacity. |

Timing validation also requires every timing value to be positive, grace plus reserve not to exceed the deadline,
and the deadline not to exceed `int.MaxValue` milliseconds (2,147,483,647 ms); these guards are unchanged.

A redirected stream may actually stay open because a descendant/grandchild or another inherited/duplicated writer
handle remains open. It may only *appear* unfinished because buffered output is still being drained, the reader
has not run, a synchronous pipe read occupies a pool worker, or a read/EOF continuation has not run. Observing
exit before the independently scheduled readers finish is sufficient; starting readers first does not prevent it.
A read fault has its separate `OutputReadFailed` classification, rather than this stream-held message.

The 3.5 s observation fits a short tool plus the 2 s grace and a delayed reader that then finishes (the latched
failure remains), on a busy pool. A real holder that closes just after grace also fits. A pipe staying open for
the entire cleanup period would normally require at least 5 s after the terminal decision and does not fit
this 3.5 s report. The historical report alone cannot distinguish a briefly held pipe from delayed reader work.

## Modeled regression and committed scheduling correction

Regression: `LegacyCombinerPostbuildProcessorTests.CleanExitWithDelayedAsyncPipeReadDoesNotRefuseStagedOutput`.
It reuses the existing fake runner/processor workspace and the production system runner with its drain/phase seams.
A short real `cmd.exe` helper writes exact stdout/stderr and exits 0, without descendants. A reader wrapper gates
only async reads until `OutputHeldAfterExit`; synchronous reads use the same real pipe immediately. This models
unavailable async-over-sync workers deterministically, without saturating or reconfiguring the test host pool.
The existing processor maps the runner result, so the red is the actual consumer issue, not a timeout or an
assertion against a fabricated issue constant.

- Red, trunk production code at the stated base: 0 passed / 1 failed in 2 s, with
  `external-tool.process.cleanup-incomplete` and the same redirected-stream message. Exit 0, no timeout and
  exact stdout/stderr assertions passed before that failure, demonstrating that the readers eventually reached EOF.
- Green: 1 passed / 0 failed in 312 ms; exact unchanged input/output bytes, empty changed ranges/issues, complete
  cleanup, one invocation/command and the capacity reservation returned. The tool is never retried.
- Committed correction (`def8e3af3`): `BoundedProcessOutputReader.DrainProcessStreamAsync` runs each process
  drain on a dedicated `LongRunning` task using synchronous reads, sharing the original capture/truncation
  algorithm. This fixes the modeled scheduling defect, but independent review found a P1 regression: unlike
  the old Windows async-over-sync path, plain `Read` cannot stop an in-flight held-pipe read. It can detach,
  lose captured diagnostics from the returned result and retain capacity until the orphan closes the pipe.
  The earlier claim that observable capacity/refusal behavior was unchanged was incorrect for this case.
  The reader comment's original claim that this was pre-existing behavior was also incorrect on Windows.
  The review correction below restores stoppability without changing classification, grace/deadline values,
  capture/truncation, capacity limit or consumer fail-closed policy.
- Existing cleanup-mapping rows additionally assert empty output and changed ranges. Their locked staged file
  still proves incomplete cleanup prevents staged reads and a second command. Domain failure propagation and
  `CompositionRunService.cs:316` permit output commit only on success; neither path was changed.

Verification uses Release, no restore, disabled shared compilation/build servers, and the owner's configured
test-area environment. Initial red-test build: 0 warnings/errors. An intermediate correction build failed
CA1068 (CancellationToken parameter order); that was fixed, not suppressed. Corrected builds have 0 warnings/errors.
Commands:

```text
dotnet build tests/NvtFwCombiner.Infrastructure.Tests/NvtFwCombiner.Infrastructure.Tests.csproj -c Release --no-restore -v q -nologo -p:UseSharedCompilation=false -nodeReuse:false
dotnet test tests/NvtFwCombiner.Infrastructure.Tests/NvtFwCombiner.Infrastructure.Tests.csproj -c Release --no-build -nologo --filter "FullyQualifiedName~CleanExitWithDelayedAsyncPipeReadDoesNotRefuseStagedOutput"
dotnet test tests/NvtFwCombiner.Infrastructure.Tests/NvtFwCombiner.Infrastructure.Tests.csproj -c Release --no-build -nologo --filter "FullyQualifiedName~ExternalTools"
```

The first corrected ExternalTools run passed 247/247 (0 skipped) in 35 s, including real held-pipe/orphan, timeout,
cancellation, reader-fault, uncooperative-reader, termination/capacity and fail-closed consumer cases. Final
verification after strengthening the empty-output assertions: build passed (0 warnings/errors, 19.68 s),
ExternalTools passed 247/247 (0 failed/skipped, 44 s). Across development: three successful builds and one
intermediate CA1068 failure; one red regression, one green regression and two green ExternalTools runs.
`git diff --check` passed; only the four admitted files changed, no `packages.lock.json` changes or new report file.

Frozen source SHA-256 for the historical reviewed/tested scheduling correction, now commit `def8e3af3`
on base `8479dee8e` (these match the earlier local patch; not hashes of the review correction below):

- `BoundedProcessOutputReader.cs`: `8e5a40f8f8210f0a745d131fda040d204625a91b4d65be28047417208e4b71d7`
- `SystemExternalProcessRunner.cs`: `affb48f7a1a463b990f053ce7fb02f292eb8cb855ca0dc5b22ccac6acded6c7b`
- `LegacyCombinerPostbuildProcessorFailureTests.cs`: `69dd1bc03a2e3f252d8b6b4a733ad8d91e4e8c933b26e1c8df20ac415d8d7cb4`

Local scoped review under `nfc-review`/Polytail: the current owner is extended, no firmware semantics, profile,
staging, write-range, order, public API or Golden expectation changes; no retry or widened timing/capacity bound.
The committed scheduling fix's only analyzer suppression was the narrow CA1849 sync-read branch, required
to avoid async-over-sync pool I/O. This review correction adds a scoped CA1031 catch to classify startup faults.
The first local review reported no correctness findings; independent Claude Opus review subsequently found
the held-pipe P1 and related P2/P3 corrections. That earlier verdict is superseded by this review round.
Independent exact-head review, protected CI, applicable
Golden execution and firmware-owner approval remain integration/publication gates for the commander; this local
investigation supplies none of those gates. The initial investigation performed no commit, push, Git configuration
change or network access; PR #538 subsequently recorded the scheduling correction as commit `def8e3af3`.
The current review-fix session makes only a local patch and performs none of those external/Git mutations.

## PR #538 review correction (2026-10-03)

- P1: retain the dedicated Windows drain threads and use the internal Platform adapter
  `Processes/WindowsSynchronousReadCancellation.cs` for native calls. It opens a non-inheritable handle to
  the current dedicated thread with `THREAD_TERMINATE` access, and registers stop separately around each read.
  `CancelSynchronousIo` is retried until that read completes, including when cancellation precedes entry into
  the kernel. An inline already-cancelled registration does not wait on its own thread; the token check prevents
  the read. The read's finally marks completion and joins the callback before any subsequent I/O or handle close.
  The scope cannot read on another thread, and its `SafeFileHandle` is closed only after all read registrations
  are disposed. No native declaration was added outside Platform.
- Only Windows `ERROR_OPERATION_ABORTED` (995) while stop is requested is treated as a stopped read;
  unrelated read faults remain faults. Captured text and `ReachedEndOfStream=false` flow through the existing
  `Invocation` classification, so a held pipe remains `OutputStreamHeldOpen` and fails closed. Invocation also
  joins the reader-stop dispatch within the existing total deadline, avoiding detachment for just-finishing
  cancellation work. In the real orphan case readers settle and the capacity slot is returned before the run
  returns, even though the orphan remains alive. No termination guarantee is added.
- P2: a genuine Windows pipe holder alone no longer leaves the dedicated read running past stop/deadline.
  Readers that truly ignore stop, blocked termination and other unsettled work still use ADR 0081 bounded
  detachment and retain their slot until settling. The 8-slot policy itself is unchanged; its incorrect held-pipe
  retention introduced by `def8e3af3` is corrected. Bounds in the table above remain unchanged.
- Non-Windows: the prior code used `TextReader.ReadAsync(..., stopToken)` on every platform. This correction
  retains that path on a dedicated drain task outside Windows, preserving the runtime's platform-specific
  interruptibility rather than introducing Unix native cancellation or assuming `CancelSynchronousIo` exists
  there. If the runtime reader does not honor stop, the existing bounded detachment remains. No non-Windows
  execution is claimed by the Windows test results.
- P3: scheduling evidence consistently says modeled; source/status now name the committed scheduling fix.
  `FirstReader` uses the production drain for stderr. Null validation/thread-start faults return faulted tasks
  for the existing read-failure classification. The scheduling regression asserts that synchronous reads do
  not run on pool threads, so moving blocking `Read` back to `Task.Run` fails; its Windows-only scenario now
  explicitly skips with a reason elsewhere. It models delayed async work without altering the global pool.
- Required real-orphan regression: `OrphanHoldingOutputAfterExitStopsWithoutDetachingAndKeepsText`, beside
  the existing held-output tests (their bodies are unchanged), uses cmd -> PowerShell -> ping, confirms the
  intermediate parent exited and the pipe-holding orphan survives, then asserts reader stop, preserved stdout,
  no `Detached`, `ResourcesReleased` and zero in-use capacity on return. Against the `def8e3af3` production
  drain the lifetime class was 30 passed / 1 failed: the new test failed on `Detached` (line 555 in that snapshot; with the final test body the first failing assertion on that drain is the empty stdout near line 580).
  From the old code, a still-pending drain also makes `TextOf` empty and retains its original capacity slot;
  whether stdout was already closed does not affect the test's no-detachment requirement.
- Additional tests cover production startup failure as a task/classified result, already-requested stop and
  cancellation after entering the read wrapper but before the kernel pipe read.

Review-fix verification (Windows, Release, owner-prescribed environment/options, no restore):

- `dotnet build tests/NvtFwCombiner.Infrastructure.Tests/NvtFwCombiner.Infrastructure.Tests.csproj -c Release --no-restore -v q -nologo -p:UseSharedCompilation=false -nodeReuse:false`:
  final build passed, 0 warnings / 0 errors (57.46 s), including touched Platform and Infrastructure.
- `dotnet test tests/NvtFwCombiner.Infrastructure.Tests/NvtFwCombiner.Infrastructure.Tests.csproj -c Release --no-build -nologo --filter "FullyQualifiedName~NvtFwCombiner.Infrastructure.Tests.ExternalTools"`:
  252 passed / 0 failed / 0 skipped (52 s). The preceding three-class green run was 85/85 (41 s), before
  adding the pre-kernel cancellation test and joining stop dispatch; the final 252 run covers both changes.
- Architecture.Tests could not be built in the implementer's sandbox (`NETSDK1004`, no restore attempted). The
  commander ran it outside the sandbox on the committed head `e77eeeff2` (2026-10-03, analyzers as errors, heavy
  machine load): Infrastructure.Tests 1,601, Application.Tests 1,654, Architecture.Tests 277, Bootstrap.Tests 2,288
  and GoldenRegression.Tests 15 passed, 0 failed. Earlier outside runs without this change failed one Bootstrap
  test each with `external-tool.process.cleanup-capacity` under the same load (branches `r54b` and `r54c`).
- Intermediate build failures were corrected: IDE0022 method-body style and CS0619 accidental selection
  of the obsolete async `Record.Exception` overload. An initial test was mistakenly launched before its build
  completed, causing DLL-copy locks (MSB3026/MSB3027/MSB3021); its old-binary run was 29 passed / 1 failed
  and is not red/green evidence. After both processes ended, a sequential build passed 0/0, then the valid
  30/1 lifetime-class red described above was obtained. All later builds/tests were ordered correctly.
- `git diff --check` passed. Eight scoped files changed (including the new Platform adapter), no
  `packages.lock.json`, generated report, firmware/Golden payload, Git index/config or other worktree changes.

Frozen review-fix source SHA-256 (the review-fix patch on `def8e3af3`, as committed in `e77eeeff2`):

- `WindowsSynchronousReadCancellation.cs`: `0bf659befa7df691b53bf57b2cc37f0f9e43cbf04c8040440d8fadf835cf37c5`
- `NvtFwCombiner.Platform.csproj`: `2ff4119a101c96f27e1cad06f96b1242387b405b24885bb797747b68f04feb49`
- `BoundedProcessOutputReader.cs`: `5f4b99579f6017ac2472dbfb4ac57084ffc6a4dc1a457c3dd9d29c3fbf5d9826`
- `SystemExternalProcessRunner.Invocation.cs`: `3d1ecf6d41040a15e4efcc12226cb67fb782bf0cee93f98ce8f664bfdeb4359f`
- `BoundedProcessOutputReaderTests.cs`: `c7b5363a52f9e1328c7f8f6c0742e3a46beab755ca0ea5b1027438108d341fad`
- `SystemExternalProcessRunnerLifetimeTests.cs`: `9e9b77c6ac5fd9433fbf884170b61e33ef25e9d46d376de974a9c9eddd37a6cb`
- `LegacyCombinerPostbuildProcessorFailureTests.cs`: `de4fb25bc18c6b740021e6e18f3f08808a8b4cb73cd7a9779f5ac0a07ad346fe`

Scoped author review under `nfc-review`/Polytail and the native-boundary lens of `nfc-architecture-change`:
no residual P0/P1 found in this fixed source scope; existing semantic owners are extended/reused, inward
dependencies and internal Platform ownership are preserved. No firmware bytes/ranges/order/integrity/support,
profiles, staged writes, public API or Golden expectations changed; no retry or widened bounds. Local Polytail
verdict at that time: `FAIL` (Architecture.Tests could not be built in the sandbox; the outside run above passed).
This author review does not substitute for independent review (pull request 538). After the blocker is resolved, independent
exact-head review, protected CI, applicable Golden execution and firmware-owner approval remain integration
gates. Open product/design questions: none.
