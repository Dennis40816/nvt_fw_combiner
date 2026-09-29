# BUG-20260929-memory-tiny-slice-hit: Subpixel memory slices cannot be selected accurately by mouse

Status: open
Severity: P2
Found: 2026-09-29, Codex gpt-6-astra, decision 189 implementation,
at feature/1.1.15/memory-layout@99e3efd7e.
Where: ProportionalStackPanel.cs / MemoryCoverageBar.cs
Observed: F3 of the revised analysis; a three-byte slice rounds into adjacent target geometry.
Expected: Decision 189 O5 adds visible non-overlapping minimum targets or collision lists.
Evidence: revised analysis under `evidence/1.1.15/` and scoped source inspection.
Owner: Codex, feature/1.1.15/memory-layout.
Resolution: implementation stopped at the stage-1 retry limit; no verified fix.
See `../1.1.15/WS-MEMLAYOUT.md`.
