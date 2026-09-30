# BUG-20260930-release-flaky-masks-admission: flaky evidence masks admission errors

Status: fixed
Severity: P2
Found: 2026-09-30, Codex gpt-6-astra, PR 489 CI diagnosis at
`feature/1.1.15/ci-rerun@449714ac504da99bfef9af70055dfd360aea7df7`.
Where: `scripts/release_promotion_policy.py`, source-CI collection and repository
admission validation.
Observed: missing annotation transport or missing `flakyEvidence` raises before
existing check-runs, rules and thread rejection reasons. Duplicate check-run
workflow tests report `source CI inventory could not be read` instead.
Expected: existing rejection order/messages precede the additional zero-flaky
gate, per the PR 489 correction request; valid admission still requires complete
zero-flaky observations under `docs/ci/release-package.md`.
Evidence: five reported workflow tests failed at the starting head. New
`test_existing_admission_reasons_precede_missing_flaky_evidence` and
`test_exact_candidate_preserves_check_rejection_before_flaky_evidence` both
failed before the policy correction, including after fixture completion.
Owner: Codex gpt-6-astra, sole writer, `feature/1.1.15/ci-rerun`.
Resolution: fixed in the commit containing the PR 489 verified checkpoint.
Existing repository admission validation now precedes mandatory flaky evidence
collection/validation; final output still requires all gates and a closed
payload. Full script suite: 1443 passed, 0 failed, 4 skipped. See
[checkpoint](../1.1.15/WS-CIRERUN.md) for both complete runs and residual gates.
