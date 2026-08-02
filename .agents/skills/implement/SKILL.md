---
name: implement
description: Implement an approved NFC issue or specification as a bounded, tested, single-writer change.
---

# Implement

Apply [Agent Skill Routing](../../../docs/governance/agent-skill-routing.md) and
the nearest `AGENTS.md`. Implement only owner-approved scope.

1. Pin the integration base, risk, affected authority, acceptance criteria,
   non-goals, mutable surfaces, evidence gates, narrow test, and final gate.
2. Read the relevant code, contract/profile, and existing tests once. Before
   adding production logic, inventory the existing semantic producers on the
   call path—profile/compiler, inspector/normalizer, authoring/session, and
   processor host—and name which typed results the change reuses. A projection
   may translate an existing result; it must not re-derive the same fact in
   Application, Bootstrap, CLI, or UI.
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
6. Apply `$polytail`, obtain the risk-appropriate independent review, and run
   `python scripts/verify.py --all` once on the frozen R1-R3 candidate.

Do not broaden the ticket, duplicate firmware semantics, stage unrelated
changes, or claim completion while required evidence or human gates remain.
