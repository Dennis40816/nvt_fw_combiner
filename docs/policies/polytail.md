# Polytail Policy

Status: Mandatory for non-trivial R1-R3 implementation and review.

Apply the root [risk-adaptive gates](../../AGENTS.md#risk-adaptive-gates) for
review depth, independence, human evidence and local/full-test applicability.
Use the [Polytail skill](../../.agents/skills/polytail/SKILL.md) to perform the
audit. This policy owns the required verdict and waiver rules:

- Verdicts are `PASS`, `PASS-WITH-HUMAN-GATE`, or `FAIL`.
- `PASS` is forbidden with P0/P1 findings, failing required checks, undeclared
  mutations, fake/disabled tests, placeholders, missing mandatory review, or
  hidden private/generated payloads.
- Required CI check: `policy / polytail`.
- Canonical integration/release command for R1-R3: `python scripts/verify.py --all`.

Name the reviewed stage. A scoped local R1 verdict evaluates the
[bounded local correction](../governance/development-execution-workflow.md#bounded-local-r1-continuation),
not the complete integration candidate. Record-only integration blockers remain
explicit without being relabeled as local correctness failures or silently
passed. A local verdict never certifies CI, a checkpoint or release readiness.

A waiver must identify rule/tool, scope, reason, risk, owner, issue, approver,
creation/expiry date, and removal condition. No waiver may weaken firmware
range safety, processor write ranges, integrity order, secrets/signing,
release allowlists, or independent golden expectations.
