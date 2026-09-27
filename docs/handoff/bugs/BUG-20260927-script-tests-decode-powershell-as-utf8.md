# BUG-20260927-script-tests-decode-powershell-as-utf8: two script test modules fail on a zh-TW Windows console

Status: open (pre-existing; seen in five of nine local full-verifier logs of batches 2a to 2c; not seen in CI)
Severity: P3
Found: 2026-09-27, Claude Code (Opus 5.5), in `python scripts/verify.py --all` at batch 2c head `e0e330325`
Where: `tests/scripts/test_coverage_ci_contract.py` (`test_required_python_gate_checks_matrix_failure_before_running_worker`)
and `tests/scripts/test_release_smoke_policy.py` (14 cases that run `package.ps1` or `smoke-release.ps1`)
Observed: the tests run `pwsh` and decode its output as UTF-8. On this Windows host (culture zh-TW) PowerShell's console
output encoding is Big5, and its error view renders a truncated line with an ellipsis, written as the Big5 bytes
`0xA1 0x4B`. Decoding fails, `stdout`/`stderr` become `None`, and the assertions raise `TypeError`. The failure depends on
whether the error line is truncated, so the same code passed earlier on the same day. The expected PowerShell error itself
is correct. The same 15 failures reproduce on the trunk without batch 2c; the batch does not touch these files.
Expected: the tests pass regardless of the host console code page, for example by running PowerShell with UTF-8 output or
decoding with the console's encoding and asserting on the decoded text.
Evidence: the batch 2c verifier log (local test area); a rerun of both modules on the board branch (15 failed, 36 passed).
Owner: WS-TEST; an R1 test-only correction.
Resolution:
