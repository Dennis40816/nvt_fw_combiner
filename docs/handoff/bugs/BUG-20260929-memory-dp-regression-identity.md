# BUG-20260929-memory-dp-regression-identity: route regression expects template identity

Status: open
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
Resolution: not corrected. The same test remained failing after two corrections,
triggering the owner's explicit stop condition. Earlier test code also assumed
the Standard `[0x6000,0x7000)` gap existed in AB; canonical AB declares DP through
`0x7000`, so that gap assertion moved to the Standard route test.
