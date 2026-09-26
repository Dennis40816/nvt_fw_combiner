# BUG-20260927-uismoke-headless-session-stall: a UiSmoke run stalled with no Avalonia test completing

Status: open (observed once; suspected Avalonia headless-session startup stall; root cause and relationship to
`TEST-LOCAL-STATE-1113-01` remain undetermined)
Severity: P2
Found: 2026-09-26, Claude Code (Opus 5.5), in run full3 of the `TEST-LOCAL-STATE-1113-01` proposal (UiSmoke, Release, 22:24)
Where: suspected in the UiSmoke Avalonia startup path: `tests/NvtFwCombiner.UiSmoke.Tests/AvaloniaHeadlessTestApplication.cs`,
Avalonia.Headless.XUnit 12.0.5 session setup and `src/NvtFwCombiner.Presentation.Avalonia/App.axaml(.cs)`. No direct causal
link to the local-state change was identified by static review; this does not exclude it.
Observed: 458 thread-pool tests passed, then VSTest blame aborted the run after 15 minutes without progress (exit 1). All 20
tests still in progress were `[AvaloniaFact]`/`[AvaloniaTheory]` tests; the first started at sequence position 74 and no
Avalonia test completed, including one that builds no host (`RunReportsListTests.ReportSaveFailurePreservesReportAndNeverNotifiesSuccess`).
The test-process dump had loaded Avalonia.Headless, Avalonia.Skia and libSkiaSharp but not Avalonia.Markup.Xaml,
Avalonia.Themes.Fluent or libHarfBuzzSharp, which a healthy process on the same build loads after one Avalonia test; this
is consistent with the application's XAML initialization not having completed, but does not prove it was never entered.
A symbol-free heuristic scan, which cannot resolve managed or JIT-compiled frames, found every thread's instruction pointer
in ntdll and no Avalonia or Skia module values on the scanned stacks; no child process was alive. There is no failing run
without the change and no established wait chain, so neither a pre-existing defect nor an environment cause is proven.
Expected: the headless session is set up once and every Avalonia test runs, or a setup failure fails the run with its
cause instead of a silent stall.
Evidence: `D:\NvtFwCombiner-TestArea\evidence\tls-local-state\uismoke-hang-20260926-2239` (sequence, both dumps, run log,
thread summary; SHA-256 in the `TEST-LOCAL-STATE-1113-01` record) and `...\tls-local-state\repro` (two later complete runs
of the same proposal, 1672/1672 each, and the healthy control's module list). The stall occurred in one of five UiSmoke
runs of the proposal. Later passing runs do not close this bug (ADR 0079 item 8).
Owner: WS-TEST (ADR 0079, U1 hang diagnostics) or the UI test owner. Next step: analyze the retained dump's managed stacks
with dotnet-dump or WinDbg with SOS (neither is installed; installing one downloads a package and needs the owner's
approval), then decide whether the cause lies in the harness, the change, or the environment.
Resolution:
