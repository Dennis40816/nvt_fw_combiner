---
name: grill-with-docs
description: Close unresolved NFC specification, architecture, terminology, or planning decisions one at a time and record each accepted result in its canonical document. Use before to-spec, to-tickets, or an implementation goal when owner decisions remain.
---

# Grill With Docs

Apply [Agent Skill Routing](../../../docs/governance/agent-skill-routing.md).
Use `$grilling` as the interview engine, `$nfc-architecture-change` for durable
boundaries, and `$to-spec` for canonical specification synthesis. Apply the
matching firmware, contract, UI, evidence, or release authority when the
decision touches that surface.

1. Read the relevant issue, `SPEC.md`, architecture/ADR/contract owner,
   profiles, code paths, and tests once. Discover facts rather than asking the
   owner.
2. Identify terminology conflicts and the canonical owner for each term.
   Distinguish authoring, execution, evidence, publication, runtime readiness,
   firmware geometry, metadata, and processor authority.
3. Stress-test the model with concrete IC, workflow, IC Count, topology/map,
   pending-input, failure, and migration scenarios. Do not generalize one IC
   route into another without evidence.
4. Ask one owner decision at a time with a recommendation and trade-offs. Wait
   before following the branch.
5. After each confirmation, update the existing canonical document immediately
   when the user authorized documentation work. Do not create `CONTEXT.md`, a
   parallel glossary, or a second specification authority.
6. Use an ADR only for a durable, surprising, difficult-to-reverse trade-off.
   Firmware facts and public contract changes retain their normal human gates.
7. After the last question, audit the complete document set for duplication,
   stale terminology, conflicting authority, and unrecorded evidence gaps.
8. Only after owner approval of the consolidated specification may
   `$to-tickets` synchronize issue bodies, dependencies, and readiness. Do not
   create an implementation goal or begin code while the grill remains open.

Completion requires one agreed meaning per term, explicit canonical ownership,
concrete boundary examples, recorded non-goals/evidence gaps, and no implicit
documentation/code/profile contradiction. This workflow does not independently
authorize implementation, GitHub mutation, firmware changes, or release state.
