# BUG-20260929-memory-dp-regression-identity: route regression expects template identity

Status: fixed
Severity: P2
Found: 2026-09-29, Codex gpt-6-astra, stage 1 of decision 189,
at feature/1.1.15/memory-layout (uncommitted patch over 99e3efd7e).
Where: tests/NvtFwCombiner.Bootstrap.Tests/AbCtrlRamMemoryLayoutTests.cs:233
Observed: the new regression expects `cmi-dp-version`, while the accepted
resolved field retains `a-cmi-dp-version`. All A/B/Both cases stop at that assertion.
Expected: compare the exact declared resolved bank identity; retain the template
identity only where that is the actual contract. Do not alter profiles or ranges.
Evidence: `evidence/1.1.15/test-results/stage1-dp-corrected-range.trx`:
3 failed, 1 passed (the independent Standard Unmapped case).
Owner: Codex, feature/1.1.15/memory-layout; resumption controlled by commander/owner.
Resolution: stage 1 completion commit following 37c6c9586, on commander-authorized
resumption. The regression asserts resolved A/B identities, exact flash ranges,
DP membership and absence of standalone field slices. The separate Standard gap
assertion remains. `stage1-resume-bootstrap.trx`: 12 passed, zero failed.
