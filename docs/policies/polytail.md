# Polytail Policy

Status: Mandatory for non-trivial R1-R3 implementation and review.

The executable workflow is `.agents/skills/polytail/SKILL.md`. This policy owns
only the required outcome:

- R0 ordinary non-normative, non-classifier-governed prose: diff and affected-link review; structure/consumer checks only when layout or parsed inputs are affected. Full Polytail is optional. Classifier-governed documents retain their record/integration gates.
- R1: scoped correctness/test review.
- R2: R1 plus architecture/contract review.
- R3: R2 plus authority-specific human and independent evidence gates.
- Verdicts are `PASS`, `PASS-WITH-HUMAN-GATE`, or `FAIL`.
- `PASS` is forbidden with P0/P1 findings, failing required checks, undeclared
  mutations, fake/disabled tests, placeholders, missing mandatory review, or
  hidden private/generated payloads.
- Required CI check: `policy / polytail`.
- Canonical final command for R1-R3: `python scripts/verify.py --all`.

A waiver must identify rule/tool, scope, reason, risk, owner, issue, approver,
creation/expiry date, and removal condition. No waiver may weaken firmware
range safety, processor write ranges, integrity order, secrets/signing,
release allowlists, or independent golden expectations.
