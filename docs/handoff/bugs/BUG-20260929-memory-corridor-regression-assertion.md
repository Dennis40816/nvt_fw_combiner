# BUG-20260929-memory-corridor-regression-assertion: regression uses rejected assertion API

Status: fixed
Severity: P3
Found: 2026-09-29, Codex gpt-6-astra, stage 2 over ce680fec5.
Where: MemoryCoveragePopupTests.Passive.cs
Observed: xUnit2032 rejects IsAssignableFrom before the corridor regression runs.
Expected: use IsType with exactMatch false, without analyzer suppression.
Evidence: first stage2-corridor-red build diagnostic.
Owner: Codex, feature/1.1.15/memory-layout.
Resolution: Corrected assertion API; the intended red is retained in stage2-corridor-red.trx, and final regression passes 189/189 in stage2-final-ui.trx.
