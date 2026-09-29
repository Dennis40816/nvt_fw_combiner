# BUG-20260929-ci-rerun-unrelated-retry-metadata: unrelated retry metadata

Status: fixed
Severity: P3
Found: 2026-09-29, independent review at `bc63f9bfe`; correction baseline
`d836eaf5574610535f82e282d4ce1cc10cdb020d` on `feature/1.1.15/ci-rerun`.
Where: `ci_trx_method_identities` in the CI verifier or release promotion policy.
Observed: Unsupported or duplicate unrelated passing definitions could block a valid failed-method retry.
Expected: owner decision 193 and the amended CI/release contracts.
Evidence: independent `f115-ci-review.md`, P3-5; selected-method definitions only, including all theory siblings; unrelated-invalid positive control.
Owner: Codex gpt-6-astra, high effort, sole correction writer.
Resolution: fixed locally in the commit containing the decision 193 checkpoint;
regressions live in `tests/scripts/test_ci_dotnet_retry.py` and, for release,
`tests/scripts/test_release_promotion_policy.py`. See
[checkpoint](../1.1.15/WS-CIRERUN.md) for exact checks and residual review gates.
Not integrated or published.
