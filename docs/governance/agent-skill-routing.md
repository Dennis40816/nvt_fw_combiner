# Agent Skill Routing

Status: Active repository governance for `0.10.0`.

This document composes repository-native NFC skills with the reviewed workflow
skills adapted from `mattpocock/skills`. It is a routing contract, not another
source of firmware facts.

## Precedence

When instructions overlap, use this order:

1. Root and nearest nested [`AGENTS.md`](../../AGENTS.md).
2. `SPEC.md`, accepted ADRs, contracts, schemas, profiles, and executable tests.
3. The matching repository-native NFC skill.
4. An imported workflow skill.
5. Upstream examples or disclosed reference material.

An imported workflow skill may organize diagnosis, planning, implementation,
review, or communication. It cannot redefine an IC fact, range, CRC/header
rule, golden expectation, release gate, dependency boundary, or permission.

## Repository-native authorities

| Surface | Authoritative skill |
| --- | --- |
| Architecture, layers, public contracts, ADRs | `$nfc-architecture-change` |
| IC profiles, regions, mappings, processors, naming | `$firmware-profile-authoring` |
| CRC/header worker and staged transform protocol | `$crc-worker-contract` |
| Golden bytes, hashes, provenance, promotion | `$golden-regression` |
| Merge/Replace authoring policy and persona access | `$composition-experience-change` |
| Avalonia UI, ViewModels, localization, accessibility | `$ui-experience-change` |
| SDK, packages, restore, solution bootstrap | `$dotnet-bootstrap` |
| Versioning, packaging, release evidence and smoke | `$release-readiness` |
| Multi-agent/version-branch coordination | `$supervised-branch-development` |
| GitHub review polling | `$github-review-polling` |
| Completion and review quality gate | `$polytail` |

These skills remain authoritative after the Matt Pocock workflow adoption.

## Workflow composition

### Diagnose

Use `$diagnosing-bugs` to establish a tight reproduction and the earliest
failing boundary. Then apply the matching NFC authority above. A diagnosis-only
request ends with cause and evidence; it does not authorize a fix.

### Implement

`$implement` organizes an approved issue/spec into reviewable slices. Each slice
still follows the branch, preflight, phase-commit, retry, test-ladder, and human
gate rules in `AGENTS.md`. `$tdd` may drive the narrow behavior loop, while
firmware expected bytes stay independent and `$golden-regression` remains the
authority for golden evidence.

### Review

`$code-review` checks the requested behavior and general engineering quality.
Every non-trivial NFC review also applies `$polytail`. R2 architecture/contract
changes require `$nfc-architecture-change`; R3 firmware semantics require the
matching firmware skill and the declared human/evidence gate.

### Model the domain or architecture

`$domain-modeling`, `$codebase-design`, and
`$improve-codebase-architecture` consume NFC's existing `SPEC.md`,
`docs/adr/`, `docs/architecture/`, and `docs/contracts/`. They do not create a
parallel `CONTEXT.md` authority. A durable new decision is written only in the
canonical NFC document selected by `$nfc-architecture-change`.

### Resolve conflicts

`$resolving-merge-conflicts` first verifies the current branch, operation, merge
base, and intended target. Preserve unrelated changes. If the operation started
on the wrong branch, preserve recoverable evidence and safely abort or restore
the intended state. Stage only resolved files belonging to the active
operation; never stage the whole worktree.

### Prototype, research, learning, and hand off

- `$prototype` answers one design question in an isolated temporary or
  owner-approved branch location. Promotion to production is a separate tested
  implementation phase. The skill does not authorize a branch or commit by
  itself.
- `$research` is read-only unless the user also asks for a durable artifact.
  Sources are cited, private evidence stays private, and research does not
  authorize a branch, issue, or repository write.
- General teaching uses a dedicated workspace selected by the user; the NFC
  production repository does not install the upstream `teach` workflow.
- `$handoff` writes only a redacted temporary handoff and points at existing
  canonical artifacts rather than duplicating them.

## Authority boundaries

- Explicit skill invocation authorizes that workflow, not unrelated state
  changes. External writes must still be requested or be an explicit,
  necessary part of the named workflow.
- Issue and PR mutations use
  [`agent-issue-tracker.md`](agent-issue-tracker.md). Missing state/wayfinder
  labels fail closed; unrelated labels are never substituted.
- A skill that calls for subagents may delegate bounded analysis or work only
  within the current request. Delegation does not expand filesystem, process,
  GitHub, release, or firmware authority.
- The canonical test ladder and retry budget remain
  [`development-execution-workflow.md`](development-execution-workflow.md).
- `$polytail` is mandatory before completion or approval of every non-trivial
  NFC code change.

## Invocation

User-invoked skills are marked
`policy.allow_implicit_invocation: false` in `agents/openai.yaml`. Model-invoked
skills omit that restriction and use narrow trigger descriptions. The reviewed
classification and source pin live in
[`agent-skill-inventory.md`](agent-skill-inventory.md).
