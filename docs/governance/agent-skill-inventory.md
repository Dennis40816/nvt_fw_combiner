# Agent Skill Inventory

Status: Generated from `.agents/skills/manifest.json`; do not edit the table manually.

Repository validation checks this table, every active skill directory,
frontmatter, Codex metadata, and invocation policy against the manifest.
Removed generic workflows remain available from Git history or user-level
skills; they are not repository authority.

| Skill | Invocation | Authority | Replaces |
| --- | --- | --- | --- |
| `assess-refactor-progress` | implicit | progress-report | — |
| `code-review` | implicit | review | — |
| `composition-experience-change` | implicit | experience | — |
| `crc-worker-contract` | implicit | crc-worker | — |
| `diagnosing-bugs` | implicit | diagnosis | — |
| `dotnet-bootstrap` | implicit | bootstrap | — |
| `firmware-profile-authoring` | implicit | firmware-profile | — |
| `github-review-polling` | explicit | github-review | — |
| `golden-regression` | implicit | golden-evidence | — |
| `grill-with-docs` | implicit | decision-documentation | domain-modeling |
| `grilling` | explicit | decision-interview | — |
| `implement` | implicit | implementation | tdd |
| `nfc-architecture-change` | implicit | architecture | codebase-design, improve-codebase-architecture |
| `polytail` | implicit | quality-gate | — |
| `release-readiness` | implicit | release | — |
| `resolving-merge-conflicts` | implicit | conflict-recovery | — |
| `supervised-branch-development` | implicit | branch-coordination | handoff |
| `to-spec` | implicit | specification-draft | — |
| `to-tickets` | implicit | ticket-planning | — |
| `ui-experience-change` | implicit | presentation | prototype |
