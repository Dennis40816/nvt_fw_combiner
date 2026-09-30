# BUG-20260930-release-drift-test-message: new regression expected wrong SHA error

Status: fixed (merged into `1.1.x` through #489, merge `e5e93f635`)
Severity: P3
Found: 2026-09-30, Codex gpt-6-astra, first full script-suite verification of the
PR 489 local correction based on `449714ac5`.
Where: `tests/scripts/test_release_promotion_policy.py`,
`test_source_ci_completion_rejects_run_drift_after_annotations`.
Observed: the new test expected a generic run-drift message for a wrong SHA;
the existing exact-source identity guard correctly rejects it earlier.
Expected: each case checks its established exact rejection message.
Evidence: first full suite: 1442 passed, 1 failed, 4 skipped. This new test is
absent from `aba286bae`; an in-memory probe of base and current identity guards
confirmed identical exact-source mismatch rejection. The independent reviewer
confirmed all three messages without changing production code.
Owner: Codex gpt-6-astra, sole writer, `feature/1.1.15/ci-rerun`.
Resolution: one test-only correction replaces the inaccurate shared regex with
per-case exact equality. The commit containing the
[checkpoint](../1.1.15/WS-CIRERUN.md) records narrow and full re-verification.
