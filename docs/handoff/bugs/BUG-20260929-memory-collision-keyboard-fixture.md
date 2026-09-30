# BUG-20260929-memory-collision-keyboard-fixture: New keyboard fixture uses incomplete headless API arguments

Status: resolved
Severity: P3
Found: 2026-09-29, Codex gpt-6-astra, stage-3 wider regression build.
Where: MemoryCoveragePopupTests.TinyMarkers.cs
Observed: missing PhysicalKey/text parameters fail CS7036; Any assertion fails xUnit2012.
Expected: use the existing full headless key sequence and collection assertion.
Evidence: first `stage3-ui` build diagnostics; no tests executed.
Owner: Codex, feature/1.1.15/memory-layout.
Resolution: full press/release arguments, existing PressEscape helper, and
DoesNotContain assertion applied. First correction verified: the keyboard
case passes in `evidence/1.1.15/test-results/stage3-ui.trx`.
