# Agent Skill Routing

Status: Active repository authority and invocation contract.

## Precedence

1. Root and nearest nested `AGENTS.md`.
2. `SPEC.md`, accepted ADRs, contracts, schemas, profiles, and tests.
3. The matching NFC authority skill.
4. A workflow skill.

Workflow skills organize work; they never redefine firmware facts, ranges,
CRC/header behavior, evidence, support, release authority, or permissions.

## Authority routes

| Changed surface | Route |
| --- | --- |
| Architecture, layers, public contracts, ADRs | `$nfc-architecture-change` |
| IC facts, profiles, regions, mappings, processors | `$firmware-profile-authoring` |
| CRC/header worker or staged transform protocol | `$crc-worker-contract` |
| Golden bytes, hashes, provenance, promotion | `$golden-regression` |
| Merge/Replace authoring and access | `$composition-experience-change` |
| Avalonia, ViewModels, localization, accessibility | `$ui-experience-change` |
| SDK, packages, restore, solution bootstrap | `$dotnet-bootstrap` |
| Versioning, packaging, release evidence | `$release-readiness` |
| Completion/review quality | `$polytail` |

## Workflow routes

- Diagnose with `$diagnosing-bugs`; diagnosis alone does not authorize a fix.
- Implement approved scope with `$implement`, including its
  red-green-refactor loop.
- Review one fixed diff with `$code-review` and scoped `$polytail`.
- Draft specifications with `$to-spec`; only an owner can approve them.
- Split only owner-approved specifications with `$to-tickets`; headless
  Application/CLI use-case paths are valid vertical slices.
- Recover conflicts with `$resolving-merge-conflicts`.
- Use `$supervised-branch-development` only for explicit multi-agent,
  reconstruction, R3 migration, or release integration.
- Use `$github-review-polling` only for an explicitly requested exact-head
  GitHub review wait.

## Invocation and mutation

The machine-readable source is
`.agents/skills/manifest.json`. `explicit` entries must disable implicit
invocation in `agents/openai.yaml`; `implicit` entries must remain discoverable.
GitHub mutations follow `agent-issue-tracker.md`. Delegation or skill invocation
never expands the user's filesystem, process, GitHub, release, or firmware
authority.
