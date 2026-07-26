---
name: grilling
description: Stress-test a plan, decision, or idea one owner decision at a time. Use when the user says grill me, asks to be questioned before implementation, or wants assumptions and trade-offs challenged before action.
---

# Grilling

For NFC repository work, apply
[Agent Skill Routing](../../../docs/governance/agent-skill-routing.md) before
acting.

1. Discover facts from the repository, tools, contracts, profiles, tests, and
   evidence instead of asking the owner to recall them.
2. Build the decision tree and resolve prerequisite decisions first.
3. Ask exactly one owner decision at a time. Include the recommended answer,
   concrete trade-offs, and any fact/evidence boundary.
4. Wait for the answer before following that branch. Record accepted,
   rejected, deferred, and evidence-blocked branches.
5. Periodically state the remaining decision count when the interview is long.
6. End only after the owner confirms shared understanding and no unresolved
   decision is being guessed.

This workflow does not authorize implementation, GitHub mutation, release
state, firmware changes, or documentation edits by itself. Compose it with an
authoritative workflow such as `$grill-with-docs` when accepted decisions must
be recorded.
