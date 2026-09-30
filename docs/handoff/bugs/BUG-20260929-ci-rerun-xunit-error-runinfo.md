# BUG-20260929-ci-rerun-xunit-error-runinfo: ordinary xUnit failures block the CI retry

Status: fixed
Severity: P1
Found: 2026-09-29, Codex gpt-6-astra independent review, at
`feature/1.1.15/ci-rerun`@`3253841cffc6a6581d732327393b7887cbecd94d`
Where: `scripts/verify.py`, `ci_trx_method_identities`
Observed: the candidate rejects every `RunInfo outcome="Error"` as a platform
error. Retained real xUnit TRX evidence with two tests (one passed, one failed),
zero error/aborted/timeout counters, and an ordinary assertion-failure RunInfo
is rejected before the failed-test retry. Its fake fixtures lacked RunInfo.
Expected: decision 191 reruns ordinary failed-test identities once, preserving
the original TRX. Test-platform failures must be distinguished using structured
terminal status/counters and exact xUnit notifications of known failed cases,
not a diagnostic severity shared by assertions.
Evidence: independent read-only replay of `ci_trx_method_identities` against
retained TRX evidence raised `CI retry cannot hide a test-platform error`;
the sanitized fake-runner regression adds the same ResultSummary/RunInfo shape.
Owner: Codex gpt-6-astra, `feature/1.1.15/ci-rerun`.
Resolution: fixed in the follow-up commit containing this record. Retry permits
only timestamped xUnit `[FAIL]` notifications naming an actually failed TRX case;
unknown Error messages and nonzero fatal terminal counters still reject retry.
The synthetic regression reproduces the prior rejection and now passes,
including ordinary theories, partial recovery and three platform-error negatives.
`python -m unittest tests.scripts.test_ci_dotnet_retry -q` passed 16 tests;
43 existing CI orchestration tests also passed. Independent re-review is tracked
in `WS-CIRERUN.md`; no workflow, release rule or product behavior changed.

Evidence locator confirmed on 2026-09-29 for review P3-8:
`<NFC_TEST_AREA_ROOT>/evidence/f115-dpregions-20260929/recompute-hash.trx`.
SHA-256: `3c67b207e09eae73d5caeeb3eb2c3d34d35edd797e42e1b51b6a7dcb1461dae9`.
The retained real TRX has two results (one passed, one failed) and one Error
RunInfo. The locator is relative to the configured test area; the original
file remains external evidence and is not copied into Git.
