---
name: to-spec
description: Synthesize the current NFC discussion and repository evidence into a draft specification for owner approval.
---

# To Spec

Apply [Agent Skill Routing](../../../docs/governance/agent-skill-routing.md) and
use canonical NFC terminology from `SPEC.md`, ADRs, contracts, profiles, and
tests.

Produce or update a **draft specification** only. Never apply
`ready-for-agent`; that label means the owner has approved both the
specification and implementation intake.

Do not ask the user for facts that can be established from repository or
approved evidence. If an unresolved owner decision affects a public,
firmware, support, security, or release contract, put it under **Open
decisions**. Do not guess it and do not call the draft implementation-ready.

Include:

1. Problem and intended outcome.
2. Three to eight necessary user stories.
3. Scope and explicit non-goals.
4. Canonical owners and affected layers/workflows/ICs.
5. Functional and failure requirements.
6. Compatibility, firmware, support, security, and release impact.
7. Testing/evidence plan at stable behavioral seams.
8. Open decisions and owner approvals still required.

Publish or update a named issue only when the user authorized that GitHub
mutation and `agent-issue-tracker.md` permits it. The result remains a draft
until explicit owner approval.
