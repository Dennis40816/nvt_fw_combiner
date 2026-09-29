# BUG-20260929-memory-measurement-test-build: two assertion results lack discards

Status: fixed
Severity: P3
Found: 2026-09-29, Codex gpt-6-astra, stage 2 over ce680fec5.
Where: MemoryCoveragePopupTests.Measurements.cs
Observed: IDE0058 rejects two unused Assert.Single results before test execution.
Expected: explicit discards preserve assertions and the analyzer contract.
Evidence: first stage2-final-ui build failed at lines 101 and 143.
Owner: Codex, feature/1.1.15/memory-layout.
Resolution: Explicit assertion-result discards preserve analyzer enforcement. stage2-final-ui.trx passes 189/189 and emits all eight timing sample files.
