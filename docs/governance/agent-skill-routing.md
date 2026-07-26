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
- Use `$grilling` when the owner explicitly requests a decision interview.
- Use `$grill-with-docs` whenever an NFC specification, architecture, or
  terminology discussion still has owner decisions. It composes `$grilling`,
  `$nfc-architecture-change`, and `$to-spec`, records each accepted result in
  the existing canonical owner, and completes its consistency audit before
  tickets or an implementation goal.
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

The former standalone `$domain-modeling` skill is not active. Its terminology
consistency, concrete IC/workflow/IC Count stress cases, and canonical-document
ownership disciplines are part of `$grill-with-docs`; firmware facts still
route through `$firmware-profile-authoring`.
