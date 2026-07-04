# Development Tags and Milestone Nodes

Tags are immutable annotated SemVer tags describing code that exists. Future milestones are reserved here and are not pre-created against the wrong commit.

## Initial node

- `v0.1.0-dev.0` — init/bootstrap and contract definition node. Includes specification, governance, .NET/Avalonia solution skeleton, installers, CI/release skeleton, two Python references, domain proof primitives, external combiner runner contracts, and no firmware-parity claim.
- `v0.5.0-dev.0` — development node for the production-backed settings shell, workflow-scoped Device context, breadcrumb navigation, and normal Replace priority UI state. It does not claim byte-level production parity or golden sign-off.
- `v0.5.0` — baseline candidate for the normal Merge/Replace workbench, Settings shell, report modal workflow, memory coverage visualization, and Replace DP/CtrlRAM priority UI. It does not claim `v1.0.0` full support-matrix sign-off.

## Branch and merge policy

- `0.1.0` is the dev0 contract branch.
- `0.5.0` is the reviewed baseline train and remains the current stable version metadata until the owner chooses the next release version.
- `0.6.0` is the active post-`0.5.0` development train for workflow data-model convergence, UI/report polish, package reference payloads, and postbuild evidence closure.
- `main` is the stable branch.
- Progress to `main` must happen through reviewed merge/PR, not direct unreviewed development pushes.
- Agent/Codex work should stay on the active milestone branch until review gates pass.

## Milestone scope

Current execution priority: normal Merge and normal Replace for DP Replace and CtrlRAM Replace workflows are pulled forward. AB merge remains in the roadmap but is deferred until the owner reactivates it. Standard Merge has executable profiles for the uploaded golden-backed gen_flash set, owner-confirmed NT51917/NT51919 aliases, NT51930 flash-map, and NT51950/NT51951 DP Perspective cases. NT51930 and NT51950/NT51951 now have owner golden fixtures recorded under `testdata/golden/standard-merge-gen-flash`; support exposure still requires firmware-owner sign-off. Replace now has NT51950/NT51951 DP Replace workbench execution and CtrlRAM per-region Preview/Build execution through staged Combiner postbuild; CtrlRAM parity still needs private golden outputs and firmware-owner review.

| Milestone | Scope | Implementation boundary |
| --- | --- | --- |
| `0.1.0-dev.N` | Dev0 contract definition and verification | Small proof primitives only: ranges, diff, write policy, manifest validation, policy scripts. No broad engine implementation. |
| `0.1.0-alpha.N` | Dev0 exit candidates | Contract freeze review, CI green, dev1 backlog ready. |
| `0.1.1-dev.N` | UI design and demo planning | UI documents, low-fidelity demo shell planning, terminal/log/report UX definition. No firmware semantics in UI. |
| `0.2.0-dev.N` | Dev1 non-UI composition core | Profile compiler, composition plan, operation executor, preview/report core, staging workspace, fake external processor runner. |
| `0.3.0-dev.N` | Standard merge parity | First standard IC group, golden tests, naming/version extraction. |
| `0.4.0-dev.N` | Integrity/tool processing | Legacy combiner runner hardening, CRC/Header golden cases, packaging integration. |
| `0.5.0-dev.N` | Normal Replace priority | DP Replace and CtrlRAM Replace workflows, IC num text choices for two-option profiles, numeric count selection for three-or-more concrete count profiles, and post-replace combiner readiness. |
| `0.6.0-dev.N` | Workflow data-model convergence | Evaluate and refactor Merge/Replace data into a unified profile/template/catalog model across ICs. No new byte behavior without evidence. |
| `0.7.0-dev.N` | General Merge/Replace, saved rules, and deferred AB merge | Dynamic mappings, saved rule promotion, preset catalog; AB bank layout resumes only after owner reactivation and golden evidence. |
| `0.8.0-dev.N` | Packaging/security | Release packaging, tool manifests, smoke tests. |
| `0.9.0-rc.N` | UAT/release candidates | UX polish, internal sign-off. |
| `v1.0.0` | stable | Signed-off support matrix. |

## First-sample `v1.0.0` release gate

Before the first sample is tagged as `v1.0.0`, the owner must confirm the exact IC/mode support subset. Anything not signed off remains visible only as candidate/planning data, not as supported behavior.

Required remaining work:

- lock the `v1.0.0` support matrix by IC and workflow, including owner sign-off for NT51930 and NT51950/NT51951 Standard Merge golden fixtures;
- satisfy the owner validation package listed in `docs/architecture/supported-ic-matrix.md` for every released IC/workflow;
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
v0.6.0-dev.N    workflow data-model convergence
v0.7.0-dev.N    General Merge/Replace saved rules and deferred AB merge
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

## `v0.5.0` release candidate gate

`v0.5.0` can be merged to `main` and packaged only after review gates pass on the milestone branch:

- Standard Merge verified against the available owner-approved golden set.
- NT51950/NT51951 DP Replace workbench output verified for the implemented exact-base/variable-DP rule.
- CtrlRAM Replace UI/report trace and staged Combiner Preview/Build output verified, with private golden outputs and firmware-owner review still required before support parity is claimed.
- `python scripts/verify.py --all`, Polytail, Codex review, and required human firmware review notes are complete.
- A Windows x64 self-contained package is produced from the reviewed commit, with version metadata aligned to `0.5.0`.
