# BUG-20260929-memory-passive-close-no-reopen: Passive close prevents same-slice reopening

Status: open
Severity: P2
Found: 2026-09-29, Codex gpt-6-astra, decision 189 implementation,
at feature/1.1.15/memory-layout@99e3efd7e.
Where: MemoryCoverageBar.cs
Observed: F5 of the revised analysis; Bounds change closes the card and subsequent same-slice moves have no opener.
Expected: Decision 189 M2 reopens once on a new valid move, with controlled-clock regression.
Evidence: revised analysis under `evidence/1.1.15/` and scoped source inspection.
Owner: Codex, feature/1.1.15/memory-layout.
Resolution: implementation stopped at the stage-1 retry limit; no verified fix.
See `../1.1.15/WS-MEMLAYOUT.md`.
