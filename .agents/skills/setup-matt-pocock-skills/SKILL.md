---
name: setup-matt-pocock-skills
description: Audit or refresh this repository's pinned Matt Pocock skill integration, invocation metadata, routing, tracker configuration, and provenance.
---

# Setup Matt Pocock Skills

For NFC repository work, apply
[Agent Skill Routing](../../../docs/governance/agent-skill-routing.md) before
acting.

This repository is already configured. Treat setup as a deterministic audit and
refresh, not a generic scaffold.

## Audit

1. Read `docs/governance/agent-skill-inventory.md`,
   `docs/governance/agent-skill-routing.md`, and
   `docs/governance/agent-issue-tracker.md`.
2. Confirm the upstream repository and exact commit before reviewing an update.
   Do not float the pin or import forks.
3. Compare the pinned upstream active inventory with the locally declared
   inventory. Deprecated, in-progress, personal, and miscellaneous upstream
   directories remain inventory-only unless the owner approves a separate
   adoption decision.
4. For every proposed upstream change, classify it as keep, adapt, merge,
   replace, or exclude. Record NFC authority, invocation mode, inbound links,
   permission impact, and representative validation.
5. Preserve the repository-native NFC skills and root `AGENTS.md` precedence.
   Use the existing `SPEC.md`, ADR, architecture, contract, profile, and test
   authorities; do not create `CONTEXT.md`, `CLAUDE.md`, or a parallel
   `docs/agents/` configuration.
6. Inspect GitHub labels read-only. Missing state or wayfinder labels stay
   owner-gated; never create or substitute them implicitly.
7. Normalize Codex metadata: `SKILL.md` frontmatter has only `name` and
   `description`; user-invoked skills set
   `policy.allow_implicit_invocation: false` in `agents/openai.yaml`; prompts
   use `$skill-name`.
8. Preserve upstream license/provenance, run the skill creator validator for
   every changed skill, then run the repository's narrow and final gates.

Before writing, present any change to the pinned inventory, invocation mode,
authority, tracker state, or license scope. Completion requires the inventory,
files, metadata, routing docs, notices, and validator to agree exactly.
