# BUG-20260929-memory-review-marker-outline-contract: Tiny marker outline violates the two-pixel style contract

Status: fixed
Severity: P1
Found: 2026-09-29, independent review and Codex gpt-6-astra correction,
at feature/1.1.15/memory-layout over 8010d7770.
Where: MemoryCoverageStyles.axaml
Observed: Marker and collision-entry full outlines use 1 DIP.
Expected: The existing full-perimeter style contract requires 2 DIP.
Evidence: FullPerimeterThinOutlinesUseTwoPixels. Test artifacts are under
`evidence/1.1.15/test-results/`; see the correction checkpoint in WS-MEMLAYOUT.
Owner: Codex, feature/1.1.15/memory-layout.
Resolution: The shared marker/collision-entry style now uses 2 DIP; theme resource bindings remain intact. Included in the correction commit containing this record.
