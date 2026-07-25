---
name: code-review
description: Review a fixed NFC diff for specification correctness, runtime safety and architecture, and test evidence.
---

# Code Review

Apply [Agent Skill Routing](../../../docs/governance/agent-skill-routing.md).
Pin an exact commit or merge-base diff and its originating issue/spec before
reviewing. An empty or unresolved diff is not reviewable.

Use three lenses in one findings list:

1. **Spec correctness** — missing, incorrect, or unrequested behavior.
2. **Runtime, safety, and architecture** — dependency direction, firmware
   authority, ranges/order, immutable inputs/staging, compatibility, security,
   and release impact.
3. **Tests and evidence** — behavior and failure coverage, non-mirrored tests,
   golden independence, and residual human evidence.

Apply `$polytail` to the same fixed diff and add the matching NFC authority
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
