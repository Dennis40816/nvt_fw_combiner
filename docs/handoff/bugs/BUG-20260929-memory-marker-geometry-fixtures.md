# BUG-20260929-memory-marker-geometry-fixtures: Geometry fixtures omit the accepted marker row and fixed border

Status: resolved
Severity: P3
Found: 2026-09-29, Codex gpt-6-astra, stage-3 wider regression.
Where: MemoryCoverageBarGeometryTests.cs; MemoryCoveragePopupTests.LegendRows.cs
Observed: six tests expect the entire control to remain 34 DIP after adding a
24-DIP marker row with spacing; four expect the legend square at the outer edge
without the new reserved two-DIP border. All 228 other tests pass.
Expected: preserve the proportional rail's 34-DIP geometry and exact weights;
assert the separate marker row and border inset without weakening those checks.
Evidence: `evidence/1.1.15/test-results/stage3-ui.trx` (228 passed, 10 failed).
Owner: Codex, feature/1.1.15/memory-layout.
Resolution: first fixture correction keeps all proportional bounds and weights,
asserts the rail and marker heights separately, and accounts for the reserved
legend border. `stage3-geometry-green.trx`: 10 passed, 0 failed. Production
source is unchanged from the wider run's 228 passes.
