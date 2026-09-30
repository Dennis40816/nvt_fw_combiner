# BUG-20260929-memory-collision-connector: Proportional connector crosses collision-list choices

Status: resolved
Severity: P3
Found: 2026-09-29, Codex gpt-6-astra, dense-list screenshot inspection.
Where: MemoryCoverageBar.OpenCard
Observed: the reused proportional-strip connector paints through list entries.
Expected: preserve card spacing and clear item labels in the collision list.
Evidence: intermediate `memory-popup-tiny-dense-240.png` inspection.
Owner: Codex, feature/1.1.15/memory-layout.
Resolution: collision entries use the existing non-hit-tested spacer instead
of the proportional connector. Exact card identity and placement are unchanged;
the proportional local strip retains its approved connector.
