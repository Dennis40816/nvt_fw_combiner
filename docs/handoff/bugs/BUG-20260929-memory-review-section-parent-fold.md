# BUG-20260929-memory-review-section-parent-fold: DP section folding merges by adjacency instead of declared ownership

Status: fixed
Severity: P2
Found: 2026-09-29, independent review and Codex gpt-6-astra correction,
at feature/1.1.15/memory-layout over 8010d7770.
Where: MemoryLayoutProjector.Sections.cs
Observed: A leading command becomes the section identity, adjacent unrelated DP parents merge, orphan commands become DP slices, and large Data partitions appear as fields.
Expected: Decision 192 and the review correction require declared parent/owner attribution, exact field ranges, and neutral undeclared context.
Evidence: review-parent-red.trx and review-parent-fixture-red.trx. Test artifacts are under
`evidence/1.1.15/test-results/`; see the correction checkpoint in WS-MEMLAYOUT.
Owner: Codex, feature/1.1.15/memory-layout.
Resolution: Declared DP code/image ancestors and code siblings under the same declared parent own fields. Only matching section identities coalesce; named field kinds exclude opaque Data partitions. Full Application tests pass, including the non-adjacent DP-parent regression. Included in the correction commit containing this record.
