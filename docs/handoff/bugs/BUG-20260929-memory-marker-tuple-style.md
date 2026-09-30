# BUG-20260929-memory-marker-tuple-style: Marker layout tuple declarations fail analyzers

Status: resolved
Severity: P3
Found: 2026-09-29, Codex gpt-6-astra, stage 3 on memory-layout@20a66dd3b.
Where: MemoryCoverageBar.Markers.cs
Observed: IDE0008/IDE0042 reject implicit tuple variables before behavioral tests run.
Expected: explicit deconstruction compiles under unchanged analyzers.
Evidence: first stage-3 implementation build output.
Owner: Codex, feature/1.1.15/memory-layout.
Resolution: explicit tuple deconstruction compiles with unchanged analyzers;
`stage3-green-markers.trx` passes all 10 selected cases.
