# BUG-20260929-ci-rerun-flaky-bug-gate: flaky bug gate

Status: fixed
Severity: P2
Found: 2026-09-29, independent review at `bc63f9bfe`; correction baseline
`d836eaf5574610535f82e282d4ce1cc10cdb020d` on `feature/1.1.15/ci-rerun`.
Where: `scripts/verify.py`, `finalize_ci_dotnet_evidence` and `require_ci_flaky_bug_records`.
Observed: A recovered failure could pass without a checkout bug record.
Expected: owner decision 193 and the amended CI/release contracts.
Evidence: the independent fixed-head review of `bc63f9bfe` on `feature/1.1.15/ci-rerun` (recorded on its pull request), P2-2; exact full-FQN bug gate, prefix negatives and visible annotations.
Owner: Codex gpt-6-astra, high effort, sole correction writer.
Resolution: fixed locally in the commit containing the decision 193 checkpoint;
regressions live in `tests/scripts/test_ci_dotnet_retry.py` and, for release,
`tests/scripts/test_release_promotion_policy.py`. See
[checkpoint](../1.1.15/WS-CIRERUN.md) for exact checks and residual review gates.
Not integrated or published.
