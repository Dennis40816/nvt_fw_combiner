# BUG-20260929-memory-main-rail-fixture-selector: helper also selects endpoint panel

Status: fixed
Severity: P3
Found: 2026-09-29, Codex gpt-6-astra, stage 2 over ce680fec5.
Where: MemoryCoveragePopupTests.MainPanel
Observed: the migrated legend geometry test uses rail input, exposing a helper
that selects both the main rail and endpoint proportional panel.
Expected: select the named MemoryMainRail before locating its proportional panel.
Evidence: stage2-regression-corrected.trx: 176 passed, 2 failed in this selector.
Owner: Codex, feature/1.1.15/memory-layout.
Resolution: Named MemoryMainRail selection passes both widths in stage2-final-ui.trx (189/189). No product geometry was changed to satisfy this helper.
