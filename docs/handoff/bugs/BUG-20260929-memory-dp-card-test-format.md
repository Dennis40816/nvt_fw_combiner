# BUG-20260929-memory-dp-card-test-format: new UI regression fails formatting gate

Status: open
Severity: P2
Found: 2026-09-29, Codex gpt-6-astra, stage 1 of decision 189,
at feature/1.1.15/memory-layout (uncommitted patch over 99e3efd7e).
Where: tests/NvtFwCombiner.UiSmoke.Tests/MemoryCoverageDpFieldTests.cs:45
Observed: the Window initializer has multiple properties on one line in a
multiline initializer; IDE0055 rejects the UiSmoke build. No selected UI test ran.
Expected: the new test follows the existing formatting contract and then executes
the real DP card template against both canonical banks.
Evidence: final UiSmoke invocation stopped with two IDE0055 diagnostics.
Owner: Codex, feature/1.1.15/memory-layout; resumption controlled by commander/owner.
Resolution: not corrected after the separate route-regression stop condition.
