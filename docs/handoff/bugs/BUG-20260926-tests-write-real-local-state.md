# BUG-20260926-tests-write-real-local-state: UI tests can write the developer's real local state files

Status: open (the real file was observed changing during test runs; the code paths were found by reading, not yet pinned by a test)
Severity: P2
Found: 2026-09-26, Claude Code (Opus 5.5), from the rolling-parity P-0.5 spike and a read-only lookup
Where: `src/NvtFwCombiner.Presentation.Avalonia/LocalJsonDocument.cs` (`GetDefaultPath`), `ReportHistoryFileStore.DefaultHistoryPath`,
`ShellPreferenceFileStore.DefaultPreferencesPath`, `src/NvtFwCombiner.Bootstrap/CompositionHostServices.cs` (toolchain runtime and
event-buffer format files); UiSmoke hosts such as `ShellViewModelTestHostFixture` and `PresentationTestHost`
Observed: `%LOCALAPPDATA%\NvtFwCombiner\report-history.v1.json` on the development machine was rewritten at 20:41 and again at 21:14
while several worktrees ran UiSmoke and other suites; no CLI run writes that file. The default paths resolve the real
`LocalApplicationData` folder with no override, and only `ReportControlTestHost` redirects report history and preferences to a
temporary workspace; other hosts use the default services. The file now holds an almost empty history (44 bytes).
Expected: every test runs against an isolated local-state root that production cannot reach by accident, and a guard fails a
test run that touches the real folder. Parallel runs in separate worktrees must not share or overwrite each other's state.
Evidence: file times of the real folder; code reading (lookup report, 2026-09-26). Not yet reproduced by a test.
Owner: 1.1.13, a test-isolation fix (R1 unless the injection point changes production behavior).
Resolution:
