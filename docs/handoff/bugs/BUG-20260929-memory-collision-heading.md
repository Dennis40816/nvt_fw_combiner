# BUG-20260929-memory-collision-heading: Collision list inherits a proportional-view heading

Status: resolved
Severity: P3
Found: 2026-09-29, Codex gpt-6-astra, stage-3 screenshot inspection.
Where: MemoryCoverageBar.OpenLocal
Observed: the collision list says "Local view · separate scale" although it
shows individual choices rather than a scaled rail.
Expected: describe choices without implying a new proportional range.
Evidence: initial tiny-collision screenshot under `evidence/1.1.15/screens/after/`.
Owner: Codex, feature/1.1.15/memory-layout.
Resolution: reuse localized slice count and navigation hint as list heading;
the proportional local view keeps its original heading. Regression asserts the
collision body does not contain the proportional-view label.
