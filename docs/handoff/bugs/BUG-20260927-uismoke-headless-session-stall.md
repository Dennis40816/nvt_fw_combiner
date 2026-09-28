# BUG-20260927-uismoke-headless-session-stall: a UiSmoke run stalled with no Avalonia test completing

Status: open (root cause identified from the retained dump on 2026-09-27: Avalonia.Headless 12.0.5 turned a
per-test setup failure into a silent permanent stall; the setup failure was a race in which a non-headless UiSmoke
test on a parallel xUnit worker claimed the Avalonia UI dispatcher; no evidence links it to
`TEST-LOCAL-STATE-1113-01`; not yet reproduced deterministically or fixed)
Severity: P2
Found: 2026-09-26, Claude Code (Opus 5.5), in run full3 of the `TEST-LOCAL-STATE-1113-01` proposal (UiSmoke, Release, 22:24)
Where: Avalonia.Headless 12.0.5 `HeadlessUnitTestSession` setup-failure handling, triggered by non-headless UiSmoke
tests that use Avalonia objects on parallel xUnit workers; not `AvaloniaHeadlessTestApplication.cs` or `App`
initialization.
Observed: 458 thread-pool tests passed, then VSTest blame aborted the run after 15 minutes without progress (exit 1).
All 20 tests still in progress were `[AvaloniaFact]`/`[AvaloniaTheory]` tests and no Avalonia test completed. Dump
analysis with dotnet-dump/SOS (`analysis-20260927.md` and `sos-*.txt` in the evidence directory) found the
Avalonia.Headless session loop (`HeadlessUnitTestSession.StartNew`, a `Task.Run` work item) Faulted, with 19 dispatch
items queued and no consumer, while the 20 xUnit workers blocked without timeout in `AvaloniaTestCase.Run`, holding
all 20 parallel slots (24 further test collections waited for a slot). The loop died while setting up the first
dequeued test, `CtrlRamMemoryDisplayFailureTests.InvalidDisplayBindingDoesNotBlockValidCascadeInputs(dark: False)`:
`EnsureIsolatedApplication` -> `AppBuilder.SetupUnsafe` -> `AvaloniaHeadlessPlatform.Initialize` -> `Compositor` ->
`DefaultRenderLoop.Add` -> `Dispatcher.VerifyAccess` threw `InvalidOperationException` ("The calling thread cannot
access this object because a different thread owns it."). `DispatchCore` runs this setup outside the `try` that
reports to the test's completion source, so the exception ended the loop silently instead of failing the test. The
process UI dispatcher belonged to an ordinary xUnit worker (managed thread 15): in Avalonia 12 every `AvaloniaObject`
constructor calls `Dispatcher.CurrentDispatcher`, and the first dispatcher created after
`Dispatcher.ResetBeforeUnitTests()` becomes `Dispatcher.UIThread`, which setup had just cleared. An unreachable
`ControlTheme` bound to that dispatcher shows Avalonia objects were constructed on the worker outside the headless
session; the dump does not identify which test did so. The failure preceded `App` construction, consistent with the
missing `Avalonia.Markup.Xaml`/`Avalonia.Themes.Fluent` modules. The `EventBufferFormatConfigurationFormatException`
recorded on another worker is the expected, handled exception of the plain `[Fact]`
`AbMergeLaunchTests.InvalidConfigurationStopsAutomaticSelection` (sequence 477 of 478) and is unrelated; no frame
involves local-state code.
Expected: the headless session is set up for every Avalonia test, or a setup failure fails the run with its cause
instead of a silent stall.
Evidence: `D:\NvtFwCombiner-TestArea\evidence\tls-local-state\uismoke-hang-20260926-2239` (sequence, both dumps, run log,
`analysis-20260927.md` with evidence strength per finding, and the `sos-*.txt` outputs) and `...\tls-local-state\repro`
(two later complete runs of the same proposal, 1672/1672 each). The stall occurred in one of five UiSmoke runs of the
proposal. Later passing runs do not close this bug (ADR 0079 item 8).
Owner: WS-TEST (ADR 0079, U1 hang diagnostics) or the UI test owner. Next step: identify the plain UiSmoke tests that
use Avalonia objects off the headless session, add fail-fast headless setup diagnostics, confirm with a deterministic
reproduction, and report the harness defect upstream after checking newer Avalonia releases.
Resolution: Pending. Closure requires (1) a headless setup failure to fail the UiSmoke run promptly with its exception
instead of stalling until the blame timeout (U1 hang diagnostics); (2) no non-headless UiSmoke test constructing
Avalonia objects or touching the Avalonia dispatcher on an xUnit worker, or an Avalonia.Headless version that binds the
UI dispatcher to the session and reports setup failures; and (3) a deterministic reproduction (concurrent off-session
Avalonia object construction during `[AvaloniaFact]` setup) that stalls before the fix and fails or passes cleanly
after it.
