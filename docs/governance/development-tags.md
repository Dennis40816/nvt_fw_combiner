# Development Tags and Milestone Nodes

Tags are immutable annotated SemVer tags describing code that exists. Future milestones are reserved here and are not pre-created against the wrong commit.

## Initial node

- `v0.1.0-dev.0` — init/bootstrap and contract definition node. Includes specification, governance, .NET/Avalonia solution skeleton, installers, CI/release skeleton, two Python references, domain proof primitives, external combiner runner contracts, and no firmware-parity claim.
- `v0.5.0-dev.0` — current working version for the production-backed settings shell, workflow-scoped Device context, breadcrumb navigation, and normal Replace priority UI state. It does not claim byte-level production parity or golden sign-off.

## Branch and merge policy

- `0.1.0` is the dev0 contract branch.
- `0.5.0` is the active normal Replace/settings milestone branch.
- `main` is the stable branch.
- Progress to `main` must happen through reviewed merge/PR, not direct unreviewed development pushes.
- Agent/Codex work should stay on the active milestone branch until review gates pass.

## Milestone scope

Current execution priority: normal Merge and normal Replace for DP Replace and CtrlRAM Replace workflows are pulled forward. AB merge remains in the roadmap but is deferred until the owner reactivates it. Standard Merge now has executable profiles for the full v1 merge IC list, including NT51930 and NT51950/NT51951 DP Perspective variable-length DP input handling; NT51930/NT51950/NT51951 still need golden evidence. CtrlRAM Replace has the first Combiner 1.13.0 postbuild command core but still needs production profile wiring and golden evidence.

| Milestone | Scope | Implementation boundary |
| --- | --- | --- |
| `0.1.0-dev.N` | Dev0 contract definition and verification | Small proof primitives only: ranges, diff, write policy, manifest validation, policy scripts. No broad engine implementation. |
| `0.1.0-alpha.N` | Dev0 exit candidates | Contract freeze review, CI green, dev1 backlog ready. |
| `0.1.1-dev.N` | UI design and demo planning | UI documents, low-fidelity demo shell planning, terminal/log/report UX definition. No firmware semantics in UI. |
| `0.2.0-dev.N` | Dev1 non-UI composition core | Profile compiler, composition plan, operation executor, preview/report core, staging workspace, fake external processor runner. |
| `0.3.0-dev.N` | Standard merge parity | First standard IC group, golden tests, naming/version extraction. |
| `0.4.0-dev.N` | Integrity/tool processing | Legacy combiner runner hardening, CRC/Header golden cases, packaging integration. |
| `0.5.0-dev.N` | Normal Replace priority | DP Replace and CtrlRAM Replace workflows, IC num text choices for two-option profiles, numeric count selection for three-or-more concrete count profiles, and post-replace combiner readiness. |
| `0.6.0-dev.N` | AB merge | Bank layout, relocation, compare rules, AB golden parity; deferred until the owner reactivates it. |
| `0.7.0-dev.N` | General Merge/Replace and saved rules | Dynamic mappings, saved rule promotion, preset catalog. |
| `0.8.0-dev.N` | Packaging/security | Release packaging, tool manifests, smoke tests. |
| `0.9.0-rc.N` | UAT/release candidates | UX polish, internal sign-off. |
| `v1.0.0` | stable | Signed-off support matrix. |

## First-sample `v1.0.0` release gate

Before the first sample is tagged as `v1.0.0`, the owner must confirm the exact IC/mode support subset. Anything not signed off remains visible only as candidate/planning data, not as supported behavior.

Required remaining work:

- lock the `v1.0.0` support matrix by IC and workflow, including owner sign-off for NT51930 and NT51950/NT51951 Standard Merge golden outputs;
- add executable production profiles for every released DP Replace and CtrlRAM Replace IC/mode, not only the current synthetic Replace contracts and postbuild command catalog;
- complete owner-approved golden outputs for every released Standard Merge and Replace profile, including private golden evidence and firmware-owner review where ranges, CRC/header, or command order matter;
- finish report modal/history behavior for Preview/Build so users can inspect input/output hashes, normalized ranges, combiner argv, warnings, and output artifact paths;
- complete Settings persistence/readiness behavior for catalog/tool/preference values that affect execution or support claims;
- pass `python scripts/verify.py --all`, private golden regression, clean Windows x64 package smoke, Polytail, Codex review, and required human review;
- align `VERSION`, assembly metadata, changelog, release manifest, stable tag `v1.0.0`, SBOM/provenance, signing policy, and package hashes.

## Progression

```text
v0.1.0-dev.N    dev0 contract and verification
v0.1.0-alpha.N  dev0 exit candidates
v0.1.1-dev.N    UI design/demo planning and terminal/log UX
v0.2.0-dev.N    dev1 composition core
v0.3.0-dev.N    standard merge parity
v0.4.0-dev.N    worker/tool integrity
v0.5.0-dev.N    normal Replace priority for DP/CtrlRAM
v0.6.0-dev.N    AB merge
v0.7.0-dev.N    General Merge/Replace saved rules
v0.8.0-dev.N    packaging/security
v0.9.0-rc.N     UAT/release candidates
v1.0.0          stable
```

## Rules

- Create a tag only after its commit passes every gate available at that milestone.
- Never move or reuse a tag; corrections receive a new prerelease number.
- `VERSION`, changelog, assembly/worker versions, manifest, commit, and tag must agree.
- Development tags do not trigger stable publishing; only exact `vX.Y.Z` tags do.
- Stable release tags are signed once the signing policy and key custody are approved.
