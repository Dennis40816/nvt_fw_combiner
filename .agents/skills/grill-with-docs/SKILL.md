---
name: grill-with-docs
description: Sharpen an NFC plan or design one owner decision at a time while recording agreed terminology and durable decisions in canonical documents.
---

# Grill With Docs

For NFC repository work, apply
[Agent Skill Routing](../../../docs/governance/agent-skill-routing.md) before
acting.

Run `$grilling` as the interview engine and `$domain-modeling` as the
documentation discipline.

1. Look up discoverable facts in code, profiles, contracts, tests, and existing
   documents instead of asking the owner.
2. Ask one decision question at a time, with a recommended answer and concrete
   trade-offs. Wait for the owner's answer before following that branch.
3. As terminology or a durable decision is agreed, identify the existing
   canonical NFC owner (`SPEC.md`, ADR, architecture, contract, profile, or
   other governed document). Do not create `CONTEXT.md`.
4. Present the exact documentation change before writing when it changes a
   public contract, architecture, profile, firmware fact, or human gate. Apply
   the matching repository-native skill.
5. End only when both sides confirm shared understanding. Summarize resolved
   decisions, open evidence gaps, out-of-scope branches, and the canonical
   documents changed or proposed.

This workflow does not authorize implementation, GitHub mutation, or a firmware
semantic change by itself.
