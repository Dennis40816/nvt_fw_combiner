# BUG-20260928-replace-selection-shows-type-name: Replace selection lists the view-model type name instead of the selected replacements

Status: fixed (merged into `1.1.x` by #470, merge `93da007af`, 2026-09-28)
Severity: P2
Found: 2026-09-28, Codex (gpt-6-sol) full-screen visual pass of the 1.1.14 display-convention inventory at trunk `47e01ebab`
Where: `src/NvtFwCombiner.Presentation.Avalonia/Views/ReplaceSelectionModal.axaml:50` uses
`StaticResource ReportSelectionRowTemplate`, which is defined in `Resources/MainWindowReportTemplates.axaml`; that
dictionary is not among the main window's merged dictionaries (`MainWindow.axaml:22-28`).
Observed: on the first entry into CtrlRAM Replace with three targets, the "Selected replacements" rows each show
`NvtFwCombiner.Presentation.Avalonia.ViewModels.ReportLineViewModel` (the object's `ToString()`), in English and
Traditional Chinese, light and dark; the counts above the list are correct.
Expected: each row shows its title and detail through the report selection row template on every path, including before
any report view has been opened; the type name never appears in the UI.
Evidence: `<test-area>/evidence/ui-inv-screens/` (Replace selection, both variants) and the inventory's TXT-15.
Owner: 1.1.13 follow-up (R1: make the template available to the modal on every path, with a first-entry UI test).
Resolution:
