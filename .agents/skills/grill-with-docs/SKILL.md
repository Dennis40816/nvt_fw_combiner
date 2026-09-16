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

1. Identify the unresolved material decision. Read its canonical owner and
   the directly affected evidence; consult profiles, code or tests when that
   decision depends on them. Discover facts rather than asking the owner, and
   reuse accepted decisions instead of reopening settled questions.
2. Identify terminology conflicts and the canonical owner for each term.
   Distinguish authoring, execution, evidence, publication, runtime readiness,
   firmware geometry, metadata, and processor authority.
3. Stress-test the affected model with concrete examples and counterexamples.
   For firmware decisions, cover the affected IC/workflow/IC Count/topology and
   pending-input/failure cases; add migration cases when relevant. Do not
   generalize one IC route into another without evidence.
4. Ask one owner decision at a time with a recommendation and trade-offs. Wait
   before following the branch.
5. After each confirmation, update the existing canonical document immediately
   when the user authorized documentation work. Do not create `CONTEXT.md`, a
   parallel glossary, or a second specification authority.
6. Use an ADR only for a durable, surprising, difficult-to-reverse trade-off.
   Firmware facts and public contract changes retain their normal human gates.
7. After the last question, audit the affected canonical documents and direct
   references for duplication, stale terms, authority conflicts and evidence
   gaps. Expand when a concrete dependency or contradiction requires it, not
   to perform a repository-wide audit for every discussion.
8. Only after owner approval of the consolidated specification may
   `$to-tickets` synchronize issue bodies, dependencies, and readiness. Do not
   create an implementation goal or begin code while the grill remains open.

Completion requires one agreed meaning per term, explicit canonical ownership,
concrete boundary examples, recorded non-goals/evidence gaps, and no implicit
documentation/code/profile contradiction. This workflow does not independently
authorize implementation, GitHub mutation, firmware changes, or release state.
