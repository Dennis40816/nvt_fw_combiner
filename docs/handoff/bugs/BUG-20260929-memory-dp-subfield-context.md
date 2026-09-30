# BUG-20260929-memory-dp-subfield-context: Declared DP command field becomes a neutral context slice

Status: fixed
Severity: P2
Found: 2026-09-29, Codex gpt-6-astra, decision 189 implementation,
at feature/1.1.15/memory-layout@99e3efd7e.
Where: MemoryLayoutProjector.Sections.cs
Observed: Section selection includes only Code and DP Image; NT51929 companion declares the three-byte field with Owner Dp and Kind Command.
Expected: Decision 189 folds declared DP fields into the DP section and publishes exact field ranges.
Evidence: revised analysis under `evidence/1.1.15/` and scoped source inspection.
Owner: Codex, feature/1.1.15/memory-layout.
Resolution: Fixed in ce680fec536fea876c87991997985b6ef0b5478a. Canonical Bootstrap bank regressions pass 12/12; actual bilingual DP cards pass inside stage1-final-ui.trx (191/191).
