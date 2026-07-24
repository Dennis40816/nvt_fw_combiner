---
name: domain-modeling
description: Build and sharpen NFC domain terminology or durable architectural decisions using the repository's canonical specifications and ADRs.
---

# Domain Modeling

For NFC repository work, apply
[Agent Skill Routing](../../../docs/governance/agent-skill-routing.md) before
acting.

NFC already has canonical domain sources. Read the relevant parts of
`SPEC.md`, `docs/architecture/`, `docs/contracts/`, `docs/adr/`, profiles, and
tests before proposing language. Do not create `CONTEXT.md` or another glossary
authority.

## During the session

1. Challenge ambiguous or conflicting terms against the canonical NFC
   vocabulary. Distinguish authoring persona, composition kind, physical
   region, address space, integrity behavior, processor authority, evidence,
   and certification state.
2. Stress-test the model with concrete IC/workflow/topology scenarios and
   boundary cases. Look up discoverable facts; ask the owner only for decisions
   or unavailable firmware evidence.
3. Cross-check statements against code, profiles, contracts, and tests. Surface
   disagreement instead of silently choosing a source.
4. Record an agreed terminology clarification in the existing canonical
   document that owns it. Use `$nfc-architecture-change` for a public boundary
   or durable architecture decision and the matching firmware skill for
   firmware facts.
5. Offer an ADR only when the decision is hard to reverse, surprising without
   context, and represents a real trade-off. Follow the existing `docs/adr/`
   conventions.

Completion requires one agreed meaning per term, named canonical ownership, and
no documentation/code/profile contradiction left implicit.
