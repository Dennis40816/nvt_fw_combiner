# BUG-20260929-ci-rerun-unknown-identity-veto: an unrelated placeholder result blocks every retry of its project

Status: fixed (on `feature/1.1.15/ci-rerun`, pending merge)
Severity: P3
Found: 2026-09-29, Claude Opus (independent delta review), at `feature/1.1.15/ci-rerun`@`2c63aa1e6`
Where: `scripts/verify.py`, `ci_trx_method_identities` (selected-method filter) and `ci_retry_selection`
Observed: the selected-method filter called `canonical_vstest_identity` on every result name. One unrelated result
named `<unknown test ID ...>` (a placeholder `parse_trx_test_outcomes` already supports) raised
`invalid VSTest identity`, so the whole project could not be retried. `bc63f9bfe` returned the correct filter for the
same input.
Expected: unrelated placeholder rows do not veto a failed-method filter; they are matched through their TestMethod
definition (decision 191, one retry of the failed tests).
Evidence: the review's probe on both heads; the regression test
`tests/scripts/test_ci_dotnet_retry.py::CiDotnetRetryTests::test_unrelated_unknown_identity_does_not_veto_failed_filter`.
Owner: Claude Code commander, `feature/1.1.15/ci-rerun`.
Resolution: `_ci_result_identity_or_none` returns None for names that are not method identities, so such rows are
skipped by the result filter and still selected through their definition; the regression test passes.
