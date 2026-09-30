# BUG-20260929-ci-rerun-ci-memory-dump: ci memory dump

Status: fixed
Severity: P2
Found: 2026-09-29, independent review at `bc63f9bfe`; correction baseline
`d836eaf5574610535f82e282d4ce1cc10cdb020d` on `feature/1.1.15/ci-rerun`.
Where: `scripts/verify.py`, `local_dotnet_vstest_command` and `collect_ci_hang_attachments`.
Observed: CI collected and staged memory dumps that could contain test firmware payloads.
Expected: owner decision 193 and the amended CI/release contracts.
Evidence: the independent fixed-head review of `bc63f9bfe` on `feature/1.1.15/ci-rerun` (recorded on its pull request), P2-5; HangDumpType=None and sequence-only staging; an unexpected dump is excluded from upload.
Owner: Codex gpt-6-astra, high effort, sole correction writer.
Resolution: fixed locally in the commit containing the decision 193 checkpoint;
regressions live in `tests/scripts/test_ci_dotnet_retry.py` and, for release,
`tests/scripts/test_release_promotion_policy.py`. See
[checkpoint](../1.1.15/WS-CIRERUN.md) for exact checks and residual review gates.
Not integrated or published.
