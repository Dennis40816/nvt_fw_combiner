# BUG-20260929-ci-rerun-annotation-render-limit: GitHub caps per-step warning presentation

Status: open
Severity: P3
Found: 2026-09-29, fresh-session Codex gpt-6-astra/high independent review at
`51b4caa119d18384b0d4d2d8dc7f2f0061a917ea`.
Where: `scripts/verify.py`, `report_ci_flaky_tests`; GitHub runner issue transport.
Observed: reporter emits all 20 warning commands for 20 FQNs, but the runner
retains only ten warnings per step, so later FQNs lack separate PR annotations.
Expected: every recovered identity remains visible under decision 193. This
change meets the explicit per-FQN command requirement and retains the complete
log/summary; separate annotation presentation remains platform-limited.
Evidence: independent direct reporter capture produced 20 warning commands.
Pinned [runner ExecutionContext.cs](https://github.com/actions/runner/blob/5b3c03247427231abb553a8914910c3b4437e7b4/src/Runner.Worker/ExecutionContext.cs#L144)
sets `_maxCountPerIssueType = 10`; its issue-admission gate at line 838 omits
subsequent warning Issues. The same source bounds messages to 4096 characters,
so one unbounded aggregate warning is not a complete remedy.
Owner: commander follow-up for annotation presentation; sole local writer
Codex gpt-6-astra documents the finding without changing workflow or permissions.
Resolution: CI contract and external report disclose the limit. No demonstrated
release bypass: the successful aggregate step has no earlier warning producer,
and its first flaky annotation already rejects release. Full log/summary and
checkout bug enforcement remain active. A future presentation improvement is
outside this correction; real CI annotation transport remains an evidence gate.
