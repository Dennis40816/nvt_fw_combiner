# BUG-20260929-memory-legend-hit-shield: Transparent card corridor intercepts visible rail and legend input

Status: fixed
Severity: P2
Found: 2026-09-29, Codex gpt-6-astra, decision 189 implementation,
at feature/1.1.15/memory-layout@99e3efd7e.
Where: MemoryCoverageBar.cs / MemoryCoverageBar.Legend.cs
Observed: F1 of the revised analysis and independent review; the full card-width transparent frame overlaps visible targets.
Expected: Decision 189 M1 removes the corridor and makes legend passive.
Evidence: revised analysis under `evidence/1.1.15/` and scoped source inspection.
Owner: Codex, feature/1.1.15/memory-layout.
Resolution: Fixed in the stage 2 completion commit following ce680fec5. stage2-corridor-red.trx reproduces the hit shield; stage2-final-ui.trx passes 189/189, including transparent spacing, passive legend and timed transit.
