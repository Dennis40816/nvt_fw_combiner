# BUG-20260929-memory-review-review-parent-fixture: Adjacent-parent regression initially has invalid equal child bounds

Status: fixed
Severity: P3
Found: 2026-09-29, independent review and Codex gpt-6-astra correction,
at feature/1.1.15/memory-layout over 8010d7770.
Where: MemoryLayoutProjectorTests.SectionParents.cs
Observed: The new fixture makes a code child equal to its image parent, violating proper containment before projection.
Expected: Synthetic maps obey the same Domain region graph constraints.
Evidence: review-parent-red.trx reports ArgumentException for AdjacentDpSectionsKeepDistinctDeclaredParents. Test artifacts are under
`evidence/1.1.15/test-results/`; see the correction checkpoint in WS-MEMLAYOUT.
Owner: Codex, feature/1.1.15/memory-layout.
Resolution: Unneeded equal-bound children were removed; review-parent-fixture-red.trx then reproduces the actual unwanted parent merge. The complete Application project passes. Included in the correction commit containing this record.

The nested-owner fixture also initially omitted the Data children needed for
complete child partitioning (review-nested-red.trx). Completing that partition
without changing the tested field produced the intended missing-field failure
in review-nested-fixture-red.trx. The corrected regression passes in
review-parent-final-green.trx.
