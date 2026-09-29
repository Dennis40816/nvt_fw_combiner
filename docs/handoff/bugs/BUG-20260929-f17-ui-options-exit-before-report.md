# BUG-20260929-f17-ui-options-exit-before-report: recoverable UI options stop desktop startup

Status: fixed locally
Severity: P1
Found: 2026-09-29, Codex (gpt-6-sol), reproducing independent review on `feature/1.1.14/bounded-diagnostics` at `e3994fdbe`
Where: `src/NvtFwCombiner.Presentation.Avalonia/DesktopApplication.cs:28-36`
Observed: `UiLaunchOptions.Issues` caused exit 64 before host construction for an invalid page, missing UI value, or invalid report path, so the existing startup report stage could not show the issue. A plain duplicate `--page` was not recorded as an issue.
Expected: managed/inherited context failures stop before host construction; recoverable UI option issues reach `MainWindow.ApplyStartupReportAsync`. Repeated `--page` is reported there too.
Evidence: the new `DesktopStartupArgumentTests` had 4 failures and 1 pass against `e3994fdbe` production behavior: three recoverable cases exited before the host factory, and duplicate page produced no issue. The managed-host process test failed 1/1 after temporarily substituting the predecessor `Program.cs`, then the exact file bytes were restored. `ReportFileLoadingTests` already covers mixed invalid page plus valid report loading and modal opening. After correction, the selected F17/managed-host/report checks passed 8/8, full UiSmoke passed 1841/1841, and `python scripts/verify.py --structure-only` passed on 2026-09-29.
Owner: Codex (gpt-6-sol), `feature/1.1.14/bounded-diagnostics`.
Resolution: fixed locally in `b013b8b9b`; UI issues continue into the existing report stage, duplicate page is reported, and the `launch-options.parsed` trace marker moved to after the trace session starts (integration commit `94c8caa83`, `DesktopApplication.cs` after `StartFromEnvironment()`). Integrated in the 1.1.14 integration branch A.
