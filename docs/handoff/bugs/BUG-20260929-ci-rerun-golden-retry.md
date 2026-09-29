# BUG-20260929-ci-rerun-golden-retry: golden retry

Status: fixed
Severity: P2
Found: 2026-09-29, independent review at `bc63f9bfe`; correction baseline
`d836eaf5574610535f82e282d4ce1cc10cdb020d` on `feature/1.1.15/ci-rerun`.
Where: `verify_ci_dotnet_test_shard / finalize_ci_dotnet_evidence` in the CI verifier or release promotion policy.
Observed: GoldenRegression initial failure could be accepted after retry.
Expected: owner decision 193 and the amended CI/release contracts.
Evidence: independent `f115-ci-review.md`, P2-4; unconditional Golden project exclusion, retaining original failed evidence.
Owner: Codex gpt-6-astra, high effort, sole correction writer.
Resolution: fixed locally in the commit containing the decision 193 checkpoint;
regressions live in `tests/scripts/test_ci_dotnet_retry.py` and, for release,
`tests/scripts/test_release_promotion_policy.py`. See
[checkpoint](../1.1.15/WS-CIRERUN.md) for exact checks and residual review gates.
Not integrated or published.
