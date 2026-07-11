# Development Tags and Milestone Nodes

Tags are immutable annotated SemVer tags describing code that exists. Future milestones are reserved here and are not pre-created against the wrong commit.

## Initial node

- `v0.1.0-dev.0` — init/bootstrap and contract definition node. Includes specification, governance, .NET/Avalonia solution skeleton, installers, CI/release skeleton, two Python references, domain proof primitives, external combiner runner contracts, and no firmware-parity claim.
- `v0.5.0-dev.0` — development node for the production-backed settings shell, workflow-scoped Device context, breadcrumb navigation, and normal Replace priority UI state. It does not claim byte-level production parity or golden sign-off.
- `v0.5.0` — baseline candidate for the normal Merge/Replace workbench, Settings shell, report modal workflow, memory coverage visualization, and Replace DP/CtrlRAM priority UI. It does not claim `v1.0.0` full support-matrix sign-off.
- `v0.7.0` — stable milestone for Standard Merge, DP Replace, CtrlRAM Replace, General Replace traceability, report history, and workbench run-context refactoring. It does not claim `v1.0.0` full support-matrix sign-off or complete private golden parity.
- `v0.7.1` — patch milestone for Replace report output-difference traceability. It classifies final output-vs-reference differences as declared replacement, IC-number-specific postbuild CRC/header, or unexpected; it does not expand firmware support scope.
- `v0.7.2` — patch milestone for General Merge v1. It adds CLI/UI explicit source-to-target mappings over a caller-declared blank output image and does not add saved-rule promotion, postbuild behavior, or new IC support claims.
- `v0.7.3` — patch milestone for saved-rule validation, operation provenance, and General Merge saved-rule CLI consumption. It does not promote saved rules into normal workflows or enable General Replace saved-rule execution.
- `v0.7.4` — patch milestone for report review readability, Build-first workbench interaction, DP version badges from gen_flash evidence, and TDDI Flash Header reference naming. It does not expand firmware support scope or private golden parity claims.
- `v0.7.5` — patch milestone for bilingual English/Traditional Chinese UI resources and functional Settings state. It does not expand firmware support scope or private golden parity claims.
- `v0.8.0` — milestone for report readability, Home workflow discoverability, General Merge shortcut exposure, report-history spacing, and repository structure cleanup. It does not claim `v1.0.0` full support-matrix sign-off or complete private golden parity.
- `v0.8.1-dev.0` candidate — Home `Util Tools` raw-BIN Hex Editor: one source read into memory, overwrite/fill/insert/delete/undo/redo, and confirmed Save As-only output. It has no IC, profile, Flash Map, CRC, postbuild, General Replace, or report behavior and makes no firmware-validity claim.

## Branch and merge policy

- `0.1.0` is the dev0 contract branch.
- `0.7.1` is the patch train for Replace report output-difference traceability on top of the reviewed `0.7.0` stable milestone.
- `0.7.2` is the patch train for General Merge v1 on top of `0.7.1`.
- `0.7.3` is the patch train for saved-rule validation and General Merge rule consumption on top of `0.7.2`.
- `0.7.4` is the patch train for report readability, Build-first workbench UI, DP version badges, and reference naming on top of `0.7.3`.
- `0.7.5` is the patch train for bilingual UI resources and Settings functionality on top of `0.7.4`.
- `0.8.0` is the tagged post-`0.7.5` milestone for repository structure consolidation, report readability, workflow discoverability, and remaining release evidence closure.
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
| `0.7.0-dev.N` | General Merge/Replace, saved rules, and deferred AB merge | Dynamic mappings, saved-rule validation/preset projection, and deferred promotion catalog; AB bank layout resumes only after owner reactivation and golden evidence. General Merge v1 ships in `0.7.2`; saved-rule validation and General Merge CLI consumption ship in `0.7.3`; normal-workflow promotion remains separately reviewed. |
| `0.8.0-dev.N` | Structure, catalog ownership, packaging/security | IC onboarding catalogs, large-file containment, release packaging, tool manifests, smoke tests. |
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
v0.7.2          General Merge v1
v0.7.3          saved-rule validation and General Merge CLI consumption
v0.7.4          report readability and Build-first workbench UI
v0.7.5          bilingual UI resources and Settings functionality
v0.8.0          report readability, workflow discoverability, structure cleanup
v0.8.x          stabilization, catalog ownership, packaging/security
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

## `v0.7.0` release gate

`v0.7.0` can be merged to `main` and packaged after review gates pass on the milestone branch:

- Standard Merge, DP Replace, CtrlRAM Replace, and General Replace workbench paths have local verification coverage.
- CtrlRAM postbuild version-category behavior for NT51926 and NT51930 is documented and tested, with private parity outputs still gated.
- NT51950/NT51951 DP Replace selected-base behavior is covered for the available approved base lengths.
- Report history and structured operation evidence are covered by UI smoke tests.
- During the 2026-07-06 GitHub Actions billing outage, owner-approved local `python scripts/verify.py --all` plus Codex review can temporarily substitute for remote CI, but the release evidence must record that remote CI did not execute.
- A Windows x64 self-contained package is produced from the reviewed commit, with version metadata aligned to `0.7.0`.

## `v0.7.2` release gate

`v0.7.2` can be merged after the General Merge v1 branch passes review gates:

- General Merge Preview/Build is available from CLI and UI over a blank output image.
- General Merge uses explicit `CopyRange` mappings through the shared profile compiler and rejects overlapping or out-of-bounds mappings.
- General Merge reports include input summaries, operation details, output size/hash, and committed output path when built.
- General Merge does not invoke legacy Combiner postbuild; TP-touching CRC/header refresh remains a Replace responsibility.
- Saved-rule promotion remains out of scope for this patch and must be reviewed separately before exposure.
- `python scripts/verify.py --all`, Polytail, Codex review, and required local verification are complete before tagging.

## `v0.7.3` release gate

`v0.7.3` can be merged after the saved-rule validation branch passes review gates:

- Operation reports expose operation provenance for built-in profile operations, runtime General mappings, and saved-rule mappings.
- `saved-rule validate` rejects unknown fields, command/script hooks, invalid compatibility, invalid ranges, duplicate ids, and unsafe source/target row shapes.
- `saved-rule mappings` prints normalized mapping rows and CLI fragments without reading or writing firmware bytes.
- General Merge CLI accepts `--rule <rule.json>` plus explicit `--slot <slot-id=path>` bindings and compiles resulting rows through the same General Merge planner/executor.
- General Replace saved-rule build, UI Saved Rules, and normal-workflow promotion remain out of scope until their processor/range policy and review gates are defined.
- `python scripts/verify.py --all`, Polytail, Codex review, and required local verification are complete before tagging.

## `v0.7.4` release gate

`v0.7.4` can be tagged after the report/build-first UI branch passes local verification and review gates:

- Report review uses a wide, sectioned evidence modal with readable operation range tables and concise output-difference summaries.
- Merge and Replace workbench pages expose Build as the primary action, with Build performing validation on current inputs before writing output.
- Home workflow cards avoid IC/Number hints that only apply inside workflow pages.
- DP version badges are derived from gen_flash evidence when rules exist; unsupported ICs show an explicit warning badge rather than a silent or guessed value.
- TDDI Flash Header reference naming is used for owner-facing reference evidence.
- `python scripts/verify.py --all`, Polytail, Codex review, and required local verification are complete before tagging.

## `v0.7.5` release gate

`v0.7.5` can be tagged after the bilingual Settings branch passes local verification and review gates:

- Primary Home, Settings, Merge, Replace, and Report surfaces use the shared English/Traditional Chinese text-resource model instead of duplicated XAML or ViewModel literals.
- Settings Language changes the active UI language immediately and persists through the local preference store.
- Theme, Strictness, Language, catalog/tool, diagnostics, and report-history rows show implemented state or explicit limitations, with no placeholder pending wording.
- English and Traditional Chinese UI smoke coverage verifies Settings persistence and representative navigation/report labels.
- Visual inspection confirms Home, Settings, Merge, Replace, and Report remain aligned and readable in both languages.
- `python scripts/verify.py --all`, Polytail, Codex review, and required local verification are complete before tagging.
