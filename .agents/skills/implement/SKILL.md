---
name: implement
description: Implement an approved NFC issue or specification as a bounded, tested, single-writer change.
---

# Implement

Apply [Agent Skill Routing](../../../docs/governance/agent-skill-routing.md) and
the nearest `AGENTS.md`. Implement only owner-approved scope.

1. Pin the integration base, risk, affected authority, acceptance criteria,
   non-goals, mutable surfaces, evidence gates, narrow test, and final gate.
2. Complete the applicable local R1 preflight or recorded design admission in the
   [capability-reuse gate](../../../docs/governance/development-execution-workflow.md#capability-reuse-gate-fail-closed)
   before changing behavior; follow that gate's R1/R2/R3 applicability rules.
   Inventory existing semantic producers, callers,
   ports/adapters, tests, and duplicate-risk helpers; record the owner or exact
   `none-found` evidence and the approved disposition. A projection may
   translate an existing typed result but must not re-derive its fact.
3. Work one observable behavior at a time with this loop:
   - **Red:** add a test that fails for the intended behavior.
   - **Green:** make the smallest production change that passes.
   - **Refactor:** while green, improve naming, locality, duplication, or module
     depth without changing behavior; rerun the same narrow test.
   - **Repeat:** move to the next observable behavior.
4. Prefer stable behavioral seams. Test an internal module directly only when
   it is a stable, named, pure contract in its own right. Firmware expected
   bytes remain independently owned by `$golden-regression`.
5. Create a commit at a stable review checkpoint: coherent, tested, and
   recoverable. Do not require a separate commit for every documentation,
   test, or review correction.
6. Apply `$polytail` and the risk-appropriate review. Finish local units with
   their affected tests and explicit residual integration gates. Run
   `python scripts/verify.py --all` at the frozen integration/release boundary,
   or when the root risk rules require broader verification, not per microcommit.

Do not broaden the ticket, duplicate firmware semantics, stage unrelated
changes, or claim completion while required evidence or human gates remain.
