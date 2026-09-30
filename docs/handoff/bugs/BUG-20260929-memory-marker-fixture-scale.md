# BUG-20260929-memory-marker-fixture-scale: Narrow fixture accidentally clusters the first large field

Status: resolved
Severity: P3
Found: 2026-09-29, Codex gpt-6-astra, stage-3 initial behavioral run.
Where: MemoryCoveragePopupTests.TinyMarkers.cs
Observed: at 240 DIP the first 0x401A-byte field is 15 DIP, so its marker
correctly clusters with the three-byte field; the test wrongly expects a
single-field card. At 420 DIP all three cases pass.
Expected: the isolated-field fixture keeps the first neighbor at least 24 DIP
at both tested widths; a separate adjacent-field cluster remains explicit.
Evidence: `evidence/1.1.15/test-results/stage3-green-initial.trx`, 7 passed / 3 failed.
Owner: Codex, feature/1.1.15/memory-layout.
Resolution: reduce the fixture's final endpoint to 0x28000 (24.038 DIP first
neighbor at width 240); the exact three-byte fields and their assertions remain
unchanged. First fixture correction verified by `stage3-green-markers.trx`
(10 passed, 0 failed); all 24 width/DPI/theme/language cases also pass in
`stage3-ui.trx`.
