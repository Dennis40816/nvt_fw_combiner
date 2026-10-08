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
| Architecture, layers, public contracts, ADRs | skill `nfc-architecture-change` |
| IC facts, profiles, regions, mappings, processors | skill `nfc-firmware-profile-authoring` |
| Owner IC evidence drop, new IC or new layout, onboarding order | skill `nfc-onboard-new-ic` |
| Pinned legacy `Combiner.exe` version, rebuild, replacement | skill `nfc-change-legacy-combiner` |
| CRC/header worker or staged transform protocol | skill `nfc-crc-worker-contract` |
| Golden bytes, hashes, provenance, promotion | skill `nfc-golden-regression` |
| Merge/Replace authoring and access | skill `nfc-composition-experience-change` |
| Avalonia, approved visual-reference fidelity, ViewModels, localization, accessibility | skill `nfc-ui-experience-change` |
| SDK, packages, restore, solution bootstrap | skill `nfc-dotnet-bootstrap` |
| Versioning, packaging, release evidence | skill `nfc-release-readiness` |
| Completion/review quality | skill `nfc-review` |

## Workflow routes

- Diagnose with skill `nfc-diagnosing-bugs`; diagnosis alone does not authorize a fix.
- Implement approved scope with skill `nfc-implement`: red-green-refactor for changed
  behavior, characterization for unchanged refactors, applicable document
  checks for prose. Reuse existing evidence when it demonstrates the same case.
- Review changes as a fixed diff with skill `nfc-review`.
  For a current-state audit, pin the commit, subsystem/files and audit question;
  that audit does not replace change admission or fixed-diff review.
- Use skill `nfc-grill-with-docs` whenever an NFC specification, architecture, or
  terminology discussion still has owner decisions. It uses its own interview,
  skill `nfc-architecture-change`, and skill `nfc-to-spec`, records each accepted result in
  the existing canonical owner, and completes its consistency audit before
  tickets or an implementation goal.
- Draft specifications with skill `nfc-to-spec`; only an owner can approve them.
- Split only owner-approved specifications with skill `nfc-to-tickets`; headless
  Application/CLI use-case paths are valid vertical slices.
- Recover conflicts with skill `nfc-resolving-merge-conflicts`.
- Use `docs/handoff/README.md` for multi-agent reconstruction, R3
  migration, release integration, or large conflict resolution.
- Use skill `nfc-github-review-polling` only for an explicitly requested exact-head
  GitHub review wait.

- Locate canonical cases and evidence disposition with skill `nfc-locate-golden-evidence`.
- Open preloaded firmware for owner inspection with skill `nfc-open-firmware-example`.
- Render canonical review screenshots with skill `nfc-capture-firmware-ui`.

## Invocation and mutation

The machine-readable source is
`.agents/skills/manifest.json`. `explicit` entries must disable implicit
invocation in `agents/openai.yaml`; `implicit` entries must remain discoverable.
GitHub mutations follow `agent-issue-tracker.md`. Delegation or skill invocation
never expands the user's filesystem, process, GitHub, release, or firmware
authority.

The former standalone `$domain-modeling` skill is not active. Its terminology
consistency, concrete IC/workflow/IC Count stress cases, and canonical-document
ownership disciplines are part of skill `nfc-grill-with-docs`; firmware facts still
route through skill `nfc-firmware-profile-authoring`.
