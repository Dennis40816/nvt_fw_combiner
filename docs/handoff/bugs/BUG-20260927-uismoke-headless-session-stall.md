# BUG-20260927-uismoke-headless-session-stall: a UiSmoke run can stall before the Avalonia headless session is set up

Status: open (observed once; the exact blocking frame is unknown)
Severity: P2
Found: 2026-09-26, Claude Code (Opus 5.5), in run full3 of the `TEST-LOCAL-STATE-1113-01` proposal (UiSmoke, Release, 22:24)
Where: the UiSmoke harness: `tests/NvtFwCombiner.UiSmoke.Tests/AvaloniaHeadlessTestApplication.cs`, Avalonia.Headless.XUnit 12.0.5
session startup and `src/NvtFwCombiner.Presentation.Avalonia/App.axaml(.cs)`; not the local-state code
Observed: 458 thread-pool tests passed, then VSTest blame aborted the run after 15 minutes without progress (exit 1). All 20
tests still in progress were `[AvaloniaFact]`/`[AvaloniaTheory]` tests; the first started at sequence position 74 and no
Avalonia test completed, including one that builds no host (`RunReportsListTests.ReportSaveFailurePreservesReportAndNeverNotifiesSuccess`).
The test-process dump loaded Avalonia.Headless, Avalonia.Skia and libSkiaSharp but never Avalonia.Markup.Xaml,
Avalonia.Themes.Fluent or libHarfBuzzSharp, which a healthy process on the same build loads after one Avalonia test, so
`App.Initialize` never ran. A symbol-free stack scan found every thread waiting and no Avalonia or Skia code on any stack;
no child process was alive.
Expected: the headless session is set up once and every Avalonia test runs, or a setup failure fails the run with its
cause instead of a silent stall.
Evidence: `D:\NvtFwCombiner-TestArea\evidence\tls-local-state\uismoke-hang-20260926-2239` (sequence, both dumps, run log, thread
summary) and `...\tls-local-state\repro` (two later complete runs of the same source, 1672/1672 each, and the healthy
control's module list). Four of five UiSmoke runs of the proposal completed; a later passing run does not close this bug
(ADR 0079 item 8).
Owner: WS-TEST (ADR 0079, U1 hang diagnostics) or the UI test owner. Next step: analyze the retained dump's managed stacks
with dotnet-dump or WinDbg with SOS (neither is installed; installing one downloads a package and needs the owner's approval).
Resolution:
