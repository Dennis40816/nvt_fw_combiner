# BUG-20260929-memory-review-legend-focus-consumer: CtrlRAM consumer still expects focusable legend

Status: fixed
Severity: P1
Found: 2026-09-29, independent review and Codex gpt-6-astra correction,
at feature/1.1.15/memory-layout over 8010d7770.
Where: CtrlRamMemoryLayoutTests.cs
Observed: The three-chip window test expects passive legend focus to succeed.
Expected: Decision 189 and analysis section 7.1 require a non-focusable legend with rail keyboard access.
Evidence: ThreeChipWindowShowsFirmwareOverviewAndSeparatePhysicalLanes. Test artifacts are under
`evidence/1.1.15/test-results/`; see the correction checkpoint in WS-MEMLAYOUT.
Owner: Codex, feature/1.1.15/memory-layout.
Resolution: The regression asserts legend focus fails and the corresponding rail opens the exact card. Included in the correction commit containing this record.
