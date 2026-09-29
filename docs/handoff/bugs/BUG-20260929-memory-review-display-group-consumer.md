# BUG-20260929-memory-review-display-group-consumer: Logical coverage throws on missing or inconsistent display groups

Status: fixed
Severity: P1
Found: 2026-09-29, independent review and Codex gpt-6-astra correction,
at feature/1.1.15/memory-layout over 8010d7770.
Where: ReplaceRegionGroupBuilder.cs
Observed: Distinct().Single() gives a generic failure when a fixture supplies incompatible display groups.
Expected: Ungrouped coverage is Common; contradictory typed groups fail with logical identity and a clear diagnostic.
Evidence: Nt51950GoldenCoverageDoesNotRepeatLogicalCtrlRamRegions. Test artifacts are under
`evidence/1.1.15/test-results/`; see the correction checkpoint in WS-MEMLAYOUT.
Owner: Codex, feature/1.1.15/memory-layout.
Resolution: The fixture supplies Common, absent groups are represented explicitly, and inconsistent groups fail closed. Snapshot publishes logical membership; Presentation no longer filters it. Included in the correction commit containing this record.
