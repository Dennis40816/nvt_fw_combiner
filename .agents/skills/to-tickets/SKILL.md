---
name: to-tickets
description: Split an owner-approved NFC specification into reviewable dependency-ordered implementation tickets.
---

# To Tickets

Apply [Agent Skill Routing](../../../docs/governance/agent-skill-routing.md) and
`agent-issue-tracker.md`. Refuse to ticket an unapproved draft as
implementation-ready.

Each ticket must complete one observable, independently reviewable use-case
path through only the layers that use case needs. A headless path through
canonical facts, resolution/compilation, Application and CLI/report evidence
is vertical without UI. Existing UI may remain behind a one-way compatibility
adapter with a named deletion criterion.

For every ticket record:

- outcome, acceptance criteria, and non-goals;
- owning layer/authority and mutable surfaces;
- exact blockers and parallel-safe relationships;
- risk, evidence, and human gates;
- narrow and final verification;
- compatibility adapter and deletion criterion, when applicable.

Choose ticket size by independent review, verification, ownership, and ability
to keep CI green—not by a model context window. Use
`.tmp/agent/to-tickets/<timestamp>/` only for local drafts.

Create or update GitHub tickets only when authorized. Apply
`ready-for-agent` only to tickets whose specification and individual scope the
owner explicitly approved; unresolved tickets remain drafts.
