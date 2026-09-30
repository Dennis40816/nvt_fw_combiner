# BUG-20260929-ci-rerun-dump-rejection-drops-attempt: invalid dump drops valid attempt diagnostics

Status: fixed
Severity: P2
Found: 2026-09-29, Codex gpt-6-astra independent review, at
`feature/1.1.15/ci-rerun`@`3253841cffc6a6581d732327393b7887cbecd94d`
Where: `scripts/verify.py`, `collect_ci_failed_project_evidence`
Observed: an empty or oversized regular dump makes the collector throw before
retaining the same attempt's otherwise valid original TRX and log. The
`retry-invalid-attachment` fake runner leaves three regular attempt-2 files on
disk but none in the manifest, while its attempt-1 evidence is retained.
Expected: decision 191 retains both attempts' available TRX/log. Invalid dump
bounds reject the attachment with an omission diagnostic; they do not discard
independent regular test evidence. Reparse/non-regular trees remain rejected.
Evidence: independent bounded fake-runner reproduction at the stated head;
the regression asserts attempt-2 TRX/log retention with its dump excluded.
Owner: Codex gpt-6-astra, `feature/1.1.15/ci-rerun`.
Resolution: fixed in the follow-up commit containing this record. After the
whole tree passes regular-file custody, attachment bounds failures produce a
fixed omission diagnostic without discarding the attempt's TRX/log/discovery
or normalized coverage. Reparse/non-regular trees still fail closed.
The regression now asserts both attempts' TRX/log retention and invalid-dump
exclusion; all 16 retry tests and 43 existing CI orchestration tests passed.
Independent re-review is tracked in `WS-CIRERUN.md`; no workflow, release rule
or product behavior changed.
