# BUG-20260929-memory-marker-test-format: Marker fixture collection syntax fails IDE0300

Status: resolved
Severity: P3
Found: 2026-09-29, Codex gpt-6-astra, stage 3 on memory-layout@20a66dd3b.
Where: MemoryCoveragePopupTests.TinyMarkers.cs
Observed: explicit array initializer violates the repository collection-expression rule.
Expected: the fixture compiles with analyzers enabled.
Evidence: stage-3 build output; subsequent `evidence/1.1.15/test-results/stage3-red.trx`.
Owner: Codex, feature/1.1.15/memory-layout.
Resolution: use a collection expression; the next run compiles and reaches all
ten intended behavioral red assertions. No analyzer suppression.
