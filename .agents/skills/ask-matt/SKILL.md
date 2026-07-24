---
name: ask-matt
description: Route an NFC task to the smallest useful combination of installed Matt Pocock workflows and repository-native authority skills.
---

# Ask Matt

For NFC repository work, apply
[Agent Skill Routing](../../../docs/governance/agent-skill-routing.md) before
acting.

Choose the smallest flow that covers the request.

## Build or change

1. Use `$grilling` only when the remaining uncertainty is an owner decision.
   Use `$grill-with-docs` when the decision also needs canonical terminology or
   durable documentation.
2. Use `$to-spec` for a multi-session requirement that should become one issue,
   then `$to-tickets` when it needs independently verifiable tracer bullets.
3. Use `$implement` for the approved slice. It routes to `$tdd`, the matching
   repository-native NFC authority, phase commits, and the canonical gate.
4. Finish with `$code-review` and mandatory `$polytail`.

## Diagnose

Use `$diagnosing-bugs` to build a tight failing loop and locate the earliest
failure. A diagnosis-only request stops at cause/evidence. If a fix is approved,
continue through `$tdd`, the matching NFC authority, and `$code-review`.

## Architecture

Use `$improve-codebase-architecture` to survey candidates and
`$codebase-design` for deep-module vocabulary or alternative interfaces.
`$nfc-architecture-change` remains authoritative for layers, contracts,
ports/adapters, and ADRs.

## Focused supporting workflows

- `$prototype`: answer one logic or UI design question without implicitly
  creating production code, a branch, or a commit.
- `$research`: establish external facts from primary sources; repository writes
  require explicit artifact scope.
- `$resolving-merge-conflicts`: recover or resolve an active operation while
  preserving branch intent and unrelated changes.
- `$handoff`: create a redacted temporary continuation note.
- `$setup-matt-pocock-skills`: audit or update the pinned integration.
- `$writing-great-skills`: maintain skill quality and invocation design.

GitHub triage and wayfinder flows are deferred until the owner provisions their
required labels and dependency behavior. General teaching belongs in a
dedicated teaching workspace, not this production repository.
