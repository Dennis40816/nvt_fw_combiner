---
name: code-review
description: Review a fixed NFC diff or a scoped repository snapshot for correctness, architecture, and test evidence.
---

# Code Review

Apply [Agent Skill Routing](../../../docs/governance/agent-skill-routing.md).
For a change review, pin the exact commit or merge-base diff and originating
issue/spec; an empty or unresolved diff cannot establish change correctness.
For a current-state audit, pin the source commit, exact files or subsystem,
and the owner's audit question. This needs no new diff and does not substitute
for a change's admission or fixed-diff review. Keep unrelated findings separate.

Use three lenses in one findings list:

1. **Spec correctness** — missing, incorrect, or unrequested behavior.
2. **Runtime, safety, and architecture** — dependency direction, firmware
   authority, ranges/order, immutable inputs/staging, compatibility, security,
   and release impact.
3. **Tests and evidence** — behavior and failure coverage, non-mirrored tests,
   golden independence, and residual human evidence.

Apply `$polytail` to the same fixed scope and add the matching NFC authority
skill when its surface is touched. Spawn read-only subagents only when the diff
has genuinely independent, read-heavy areas; ordinary reviews stay in one
pass. Tooling output does not replace semantic review.

Report findings first in this form:

```text
[P0-P3] Title
Path:line
Observed behavior
Why it matters
Required correction
Evidence/test
```

Then give the Polytail verdict, commands inspected, and remaining human gates.
Do not duplicate the same finding under multiple review headings.
