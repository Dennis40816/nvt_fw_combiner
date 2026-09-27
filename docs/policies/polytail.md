# Polytail Policy

Status: Mandatory for non-trivial R1-R3 implementation and review.

Apply the root [risk-adaptive gates](../../AGENTS.md#risk-adaptive-gates) for
review depth, independence, human evidence and local/full-test applicability.
Use the [Polytail skill](../../.agents/skills/polytail/SKILL.md) to perform the
audit. This policy owns the required verdict rules:

- Verdicts are `PASS`, `PASS-WITH-HUMAN-GATE`, or `FAIL`.
- `PASS` is forbidden with P0/P1 findings, failing required checks, undeclared
  mutations, fake/disabled tests, placeholders, missing mandatory review, or
  hidden private/generated payloads.
- Required CI check: `policy / polytail`.
- Canonical integration/release command for R1-R3: `python scripts/verify.py --all`.

Name the reviewed stage; a local verdict never certifies CI or release readiness.

Waivers follow the [execution workflow](../governance/development-execution-workflow.md#waivers).
