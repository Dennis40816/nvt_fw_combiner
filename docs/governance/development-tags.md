# Development Tags and Milestone Nodes

Tags are immutable annotated SemVer tags describing code that exists. Future
milestone scope, sequencing, and dates are maintained only in
[NFC Roadmap](../architecture/nfc_roadmap.md); historical milestone descriptions
here are not active planning authority and are not pre-created against the wrong
commit.

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
- `v0.9.0` — stable Util Tools raw-BIN Hex Editor milestone: one source read into memory, direct byte and range edits, overwrite/fill/insert/delete, ASCII search, structural diff navigation, undo/redo, and confirmed Save As-only output. It has no IC, profile, Flash Map, CRC, postbuild, General Replace, or report behavior and makes no firmware-validity claim.
- `v0.9.1` — firmware-model-v2 migration milestone: trusted profile bundles, canonical family/map selection, compiled-composition runtime admission, and Normal/Standard Merge routing retain existing byte evidence. It does not promote IC support without firmware-owner review.
- `v0.9.2` — profile-bundle consolidation milestone: content-addressed schema source inventory and deterministic closed-root materialization reduce repeated schema snapshots without changing loader trust boundaries or firmware behavior.
- `v0.9.3` — AB Code and CtrlRAM version-edit milestone: owner-approved AB Code profiles and golden evidence may extend the shared composition engine for the approved IC subset; CtrlRAM Build may offer an explicit TP FW major/sub-version edit choice. Neither feature promotes support without its normal firmware-owner evidence.
- `v0.9.4` — automated IC intake milestone: a deterministic, manifest-driven intake interface may produce candidate bundles and validation reports from declared evidence. It must not infer firmware behavior or automatically promote an IC/mode.
- `v0.9.5` — V2 workflow convergence milestone: one reviewed workflow family at a time may retire an exactly replaced legacy profile/catalog only after direct V2 runtime and golden evidence prove parity.
- `v0.9.6` — support and release consolidation milestone: retire completed compatibility projections, reconcile the support matrix, and close package/release evidence without expanding firmware behavior.
- `v0.9.7` — UI token consolidation and code-size discipline milestone: unify exactly equivalent visual, text, and spacing tokens; remove only proven duplicate UI or compatibility code while retaining UI behavior, accessibility, tests, and firmware boundaries.
- `v0.9.8` — feature-frozen code-size convergence milestone: hold the owner-accepted 56,742-line final production ratchet and portable-package growth to at most 1% without expanding support or weakening trust-boundary checks.
- `v0.9.9` — legacy convergence and patch-closure milestone: reduce production C#/AXAML to at most 54,000 nonblank lines, retire only exactly replaced legacy paths, preserve the Legacy Combiner executable/constrained runner exception, and close verified security gaps.
- `v0.9.10` — end-to-end performance and Change Report remediation milestone: preserve exact firmware/process/report evidence while reducing repeated execution, I/O, allocation, history, and UI-thread costs; add typed progress and a read-only Hex Diff without changing support truth.
- `v0.9.11` — reconstructed stabilization milestone: start only from the exact final `v0.9.10` predecessor; retain its safety and report behavior while adding DP/LDC authoring coverage, measured first-frame/background warm-up, bounded self-contained packaging, topology-aware CtrlRAM grouping, spatial padding ownership, and the fixed bottom-right Build rail without support promotion.
- `v0.9.12` — CtrlRAM production-routing and interaction-stabilization milestone: start only from exact stable `v0.9.11`; replace golden-identity admission with IC/profile/typed-plan authority; improve failure feedback and navigation safety; add complete release notes and deterministic branch/review governance; and retain support-neutral firmware gates.
- `v0.9.13` — urgent UI and release-stabilization milestone: correct action feedback/layout, exact-file reveal, TP-version UX/naming, Inputs scrolling, OneDrive diagnostics, NT51951 DP guidance, and Standard/Customized display labels without changing firmware behavior or support truth.
- `v0.9.14` — AB architecture re-admission, targeted UI correctness, and release-automation milestone: re-audit the first owner-approved AB family under ADR 0032, add typed load diagnostics and the minimum AB authoring UI, add IC detail disclosure, fix file-hover hit testing, modal/action-rail layering, action-rail consistency, and empty Replace coverage semantics, reduce duplicate Actions work, and move stable tag/release creation behind one protected CI promotion.
- `v0.9.15` — AB Code function-open, delivery-readiness, and review-automation milestone: start only from the official `v0.9.14` release commit, open the reviewed NT51919/NT51929/NT51932 route plus NT51950 `1 IC`/`Cascade` and selector-free NT51951 in UI/CLI, complete owner-authorized AB input/output usability and reviewer-facing delivery automation, and retain code size as a non-blocking review metric. Direct-golden debt remains visible for support certification; it does not block these function-open routes. The shared Hex viewport, Changes redesign, and global Button-feedback backlog are deferred to a later owner-selected milestone.
- `v0.9.16` — exceptional CRC/header-authority hot-fix from official `v0.9.15`: classify owner-approved CRC write windows, correct Replace/AB UI state and coverage, skip DP metadata inspection for TP firmware, and add the owner-approved NT51929 single Normal CtrlRAM golden. The former NT51950 AB certification assignment moved to the active `0.11.0` evidence track.
- `v0.9.17` — separately approved DiffDLM active-Diff-NF preservation hot-fix
  from official `v0.9.16`: mask only the 929-like and 950-like Cascade routes,
  preserve their active NF bytes before existing CRC/Postbuild processing,
  rename the selector to `DiffDLM`, make every independent NF slot unavailable
  on those Cascade routes, and hide NT51920/NT51925/NT51930/NT51931 from the
  `0.9.17` user-facing IC lists. It does not backport the `0.10.x` architecture
  or certify the 950-like route without its direct golden.
- `v0.9.18` — independently reviewed NT51928 compatibility hot-fix from
  `v0.9.17`: make LDC optional in Standard Merge, admit the isolated `0x40000`
  no-LDC output, and allow Initial Code, LDC, or both against the exact
  `0x80000` DP Replace reference. It is support-neutral and does not import the
  `0.10.x` canonical architecture.
- `v0.10.0` — support-neutral maintainability planning release from official
  `v0.9.16`: publish the approved IC-first architecture, terminology, FlashMap
  evidence provenance, ADR lifecycle, validation standards, and the canonical
  `0.10.x` dependency plan. It starts no production refactor and makes no
  firmware or support-promotion claim.
- `v0.10.1` — headless canonical foundation release: integrate the reviewed
  `0.10.x` implementation line after all 78 admitted headless routes converge
  on canonical capability resolution and one exact compiled composition. It
  includes the reviewed firmware contracts and retirements allocated through
  #194, keeps missing golden/support gates explicit, and leaves the desktop UI,
  compatibility deletion, legacy runtime deletion, and Core convergence waves
  for later releases.
- `v0.10.2` — complete canonical-refactoring release: tag only after deferred UI
  adoption, superseded compatibility and legacy-runtime deletion, all four Core
  Convergence slices, and the hard #197 production-size gate are complete and
  reviewed. It is the end of the currently approved refactoring program, not a
  partial checkpoint.
- `v0.10.3` — post-refactor simplification audit: measure the `v0.10.2` result
  again and review the new architecture for remaining removable ownership or a
  simpler expression. The milestone does not pre-authorize speculative
  abstractions, safety removal, or a line-count-only rewrite.
- `v0.10.4` — unified preload performance-control release: consolidate startup
  and background preload scheduling behind one observable, cancellable,
  bounded, and user-controllable lifecycle. Performance work must retain
  deterministic results, current cache identity, and the canonical verifier.
- `v0.10.5` — reserved path-based update experience: add a screen that can
  obtain reviewed update artifacts from a configured path so routine delivery
  no longer depends on repackaging and email. Trust source, rollback, version
  selection, share/network behavior, and release authorization are intentionally
  deferred to a later owner discussion; no implementation ticket is implied by
  this reservation.

The original `0.9.17` certification and `0.9.18` family-model proposals were
superseded before release. The owner subsequently reused those version numbers
for the bounded DiffDLM and NT51928 compatibility hot-fixes above; neither
assignment revives the former certification/family-model scope or becomes
alternate `0.10.x` architecture authority.

The active closure goal, completed-milestone audit, and the exact scope fence
for `v0.9.3` through `v0.9.10` are maintained in
[`0.9.x Completion Roadmap`](../architecture/0.9.x-completion-roadmap.md).

## Branch and merge policy

- `0.1.0` is the dev0 contract branch.
- `0.7.1` is the patch train for Replace report output-difference traceability on top of the reviewed `0.7.0` stable milestone.
- `0.7.2` is the patch train for General Merge v1 on top of `0.7.1`.
- `0.7.3` is the patch train for saved-rule validation and General Merge rule consumption on top of `0.7.2`.
- `0.7.4` is the patch train for report readability, Build-first workbench UI, DP version badges, and reference naming on top of `0.7.3`.
- `0.7.5` is the patch train for bilingual UI resources and Settings functionality on top of `0.7.4`.
- `0.8.0` is the tagged post-`0.7.5` milestone for repository structure consolidation, report readability, workflow discoverability, and remaining release evidence closure.
- `0.9.0` is the integration branch for the standalone raw-BIN Hex Editor milestone and subsequent UAT fixes after the reviewed stable tag.
- `0.9.1` is the integration branch for the firmware-model-v2 migration after `0.9.0`.
- `0.9.2` starts from the owner-approved locally verified `v0.9.1` tag and is limited to trust-preserving profile-bundle consolidation before AB Code work resumes. PR #94 records the temporary local-verification integration; remote CI and Codex review remain required before any later `main` integration.
- `0.9.3` integrates and releases from the reviewed `v0.9.2` tag and contains the separately evidence-gated AB Code and CtrlRAM version-edit work. Candidate AB commits may begin earlier only under the pre-tag policy below.
- `0.9.4` integrates and releases after reviewed `0.9.3`, and may automate declared IC-evidence intake only through the standardized 0.9.2 bundle contract. Candidate intake work may begin earlier under the pre-tag policy.
- `0.9.5` integrates and releases after reviewed `0.9.4` and performs staged V2 workflow convergence using the [Legacy Retirement Matrix](legacy-retirement-matrix.md); it does not treat a legacy name as evidence that code is removable. Candidate convergence work may begin earlier under the pre-tag policy.
- `0.9.6` integrates and releases after reviewed `0.9.5` and closes only release/support evidence and compatibility items whose matrix gates are complete. Candidate release-evidence work may begin earlier under the pre-tag policy.
- `0.9.7` integrates and releases after reviewed `0.9.6` and consolidates UI tokens and exactly replaced duplication. Candidate token/code-size work may begin earlier under the pre-tag policy; it does not introduce firmware behavior, relax release evidence, or use line-count reduction as a reason to remove required tests or compatibility paths.
- `0.9.8` integrates and releases after reviewed `0.9.7` and is feature-frozen and support-neutral. It enforces the reviewed production-line and package-size ceilings while retaining required validation, golden evidence, and tests.
- `0.9.9` integrates and releases after reviewed `0.9.8` and retires only legacy paths with an exact reviewed replacement. The Legacy Combiner executable and constrained runner remain; its static command declarations are replaced by the hash-pinned typed data catalog without changing support claims. R3 evidence still gates IC/mode promotion.
- `0.9.10` integrates and releases after reviewed `0.9.9` and performs measured end-to-end performance and Change Report remediation under ADRs 0026 and 0027. Candidate intake, new profile/runtime registration, support promotion, firmware ranges, and processor behavior remain outside this milestone unless separately reassigned and reviewed.
- `0.9.11` integrates and releases only after reviewed final `v0.9.10`. Its predecessor tag and peeled SHA must be recorded before feature work; a branch created from an earlier performance candidate is invalid release lineage.
- `0.9.12` starts from stable `v0.9.11` at peeled commit `14470f95eafe810de08db03d3e0370e81d086338`. Its integration and feature branches follow [Branch, Version, and Release Governance](branch-version-and-release-governance.md); no stale `0.9.11` feature branch is a valid substitute baseline. CtrlRAM runtime/profile work remains support-neutral until its normal R3 evidence and firmware-owner gates close.
- `0.9.13` starts from the exact latest reviewed `0.9.12` head and is limited to the support-neutral UI/release stabilization scope recorded in its changelog. It does not admit AB or alter firmware composition authority.
- `0.9.14` starts from stable `v0.9.13`/`main` commit `f9f8dbcd979ecdef43f432016787e57763819492`. It owns AB re-admission as an R3 track under ADR 0032, typed input diagnostics, the minimum AB authoring surface, IC detail disclosure, targeted hover/modal/action-rail/coverage UI fixes, canonical toolchain/CI efficiency, and protected CI-owned stable release promotion. Existing AB candidates remain hidden until their exact route, evidence, review, and firmware-owner gates close.
- `0.9.15` starts from official `v0.9.14` tag `58f5bbf4cdbfb4e02036c8c1b40c48aa88fa21f7`, peeled commit `9b15d8757ccb44167c471ca4e602036066bcdea9`. It opens the reviewed NT51919/NT51929/NT51932 AB function plus NT51950 `1 IC`/`Cascade` and selector-free NT51951 in UI/CLI, and owns output-name/input-inspection correctness plus delivery-to-review automation. All five routes remain function-open but support certification is not promoted. Direct-golden debt is explicit support-certification evidence, not a function gate. Cross-workflow header/Evidence/Memory coverage unification, the shared viewport, Changes redesign, and global Button feedback must be reassigned to a later owner-selected milestone before implementation.
- `0.9.16` starts only from the official reviewed `v0.9.15` release tag. It owns only the exceptional CRC/header-authority, Replace/AB UI correctness, TP inspection, and NT51929 golden hot-fix; it does not certify an AB route or absorb the former NT51950 AB certification assignment.
- `0.9.17` starts only from the official reviewed `v0.9.16` release tag. It owns
  only the DiffDLM active-NF preservation, selector rename, and selector-list
  retirement compatibility work declared above; it does not broaden support,
  alter the full-replace routes, or import the `0.10.x` architecture.
- `0.9.18` is the independently reviewed NT51928 optional-input maintenance
  branch whose exact head was published as `v0.9.18`; it does not merge its
  product commits into `main` or redefine the `0.10.x` implementation line.
- `0.10.0` starts only from the official reviewed `v0.9.16` release tag. It is
  a planning and governance release; later `0.10.x` version allocation follows
  the canonical dependency plan rather than this tag.
- `0.10.1` integrates the exact reviewed `0.10.x` head into the protected-main
  lineage after the headless foundation and its R3 review gates complete. It
  does not silently promote `ContractOnly`/missing-golden routes. Owner decision
  on 2026-08-02 allocates the remaining UI, deletion, Core-convergence, and
  #197 integration graph to `0.10.2`; that later tag requires the complete
  refactoring result rather than an intermediate wave.

The former `0.9.17` certification and `0.9.18` family-model delivery roadmap
remains historical evidence only. The bounded maintenance releases that reused
those version numbers do not restore the superseded scopes. Active
post-`0.10.x` sequencing is maintained in the
[NFC Roadmap](../architecture/nfc_roadmap.md).

Pre-tag `0.9.x` feature development is permitted for candidate-only work under
the [roadmap policy](../architecture/0.9.x-completion-roadmap.md#pre-tag-candidate-development-policy).
It never waives evidence, support-promotion, integration, or release-tag gates.
- `main` is the stable branch.
- Progress to `main` must happen through reviewed merge/PR, not direct unreviewed development pushes.
- Agent/Codex work should stay on the active milestone branch until review gates pass.
- Starting with `v0.9.12`, complete user-facing release notes are a stable-release gate; generated commit lists are supporting material only.

### Temporary local integration exception

Until `2026-08-01 00:00` Asia/Taipei, the owner permits a feature PR to merge into its exact version integration branch when remote Actions fail before allocating executable steps or logs, provided that the exact feature head has passed `python scripts/verify.py --all`, `git diff --check`, and an independent review with no P0/P1 findings. The PR must record the unavailable remote job evidence and the local verifier result.

This exception does not permit direct `main` merges, automatic tags, support promotion, release publishing, or bypassing required R3 firmware-owner/golden evidence. Those gates remain explicit.

## Milestone scope

Legacy implementation state before the `0.10.x` retirement: normal Merge and
normal Replace had executable routes across the former 13-IC inventory. The
`0.9.15` AB Merge function remains open in UI/CLI for NT51919/NT51929/NT51932,
NT51950 `1 IC`/`Cascade`, and selector-free NT51951; its profile/IC/topology
route is never selected from TP metadata. This remains a function-availability
statement, not a support claim. ADR 0042/#221 remove NT51920, NT51925,
NT51930, and NT51931 from the `0.10.x` target instead of migrating that entire
legacy inventory. Historical golden and FlashMap evidence stays support-neutral
and cannot re-admit a retired IC.

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
| `v0.9.0` | Stable raw-BIN utility milestone | Standalone bounded Hex Editor, internal sign-off, and release packaging. |
| `0.9.x` | UAT stabilization | Corrective UX and reliability patches without expanding firmware support claims. |
| `v0.9.1` | Firmware-model-v2 migration | Canonical V2 profile/family/map compilation and runtime routing retain existing Normal/Replace parity; owner promotion gates remain explicit. |
| `v0.9.2` | Profile-bundle consolidation | Content-addressed schema source inventory materializes the same closed runtime roots; no firmware semantics or AB behavior changes. |
| `v0.9.3` | AB Code and CtrlRAM version edit | Evidence-gated AB composition profiles plus an explicit pre-Build TP FW major/sub-version edit choice for CtrlRAM Replace. |
| `v0.9.4` | Automated IC intake | Manifest-driven candidate-bundle and validation-report generation; no inferred firmware rules or automatic support promotion. |
| `v0.9.5` | V2 workflow convergence | Retire only legacy definitions with an exact V2 runtime replacement, direct tests, and golden/evidence closure recorded in the retirement matrix. |
| `v0.9.6` | Support/release consolidation | Remove completed compatibility projections and close support/package evidence without adding firmware behavior. |
| `v0.9.7` | UI token consolidation and code-size discipline | Consolidate exact-equivalent UI tokens, remove only proven duplicate UI/compatibility code, and retain behavioral/accessibility coverage without adding firmware behavior. |
| `v0.9.8` | Feature-frozen code-size convergence | Enforce the owner-accepted 56,742-line final ratchet and 1%-package ceiling without support expansion or safety-check removal. |
| `v0.9.9` | Legacy convergence and patch closure | Enforce the 54,000-line and 1%-package ceilings, retire exactly replaced legacy paths except Legacy Combiner, and close evidence-backed security gaps. |
| `v0.9.10` | End-to-end performance and Change Report remediation | Keep byte/process/report parity while delivering one authoritative Build, typed progress, scalable reports/history, and a read-only virtualized Hex Diff. |
| `v0.9.11` | Reconstructed stabilization | Preserve final `v0.9.10` behavior while completing DP/LDC authoring, measured startup/background warm-up, package-size bounds, IC Number/topology grouping, spatial padding, and bottom-right Build interaction without support promotion. |
| `v0.9.12` | CtrlRAM routing and interaction stabilization | Use IC/effective profile/typed plan production authority, surface actionable failures, require complete release notes and deterministic branch/review governance, and preserve support-neutral R3 gates. |
| `v0.9.13` | Urgent UI and release stabilization | Deliver support-neutral action feedback/layout, file reveal, TP-version UX/naming, Inputs scrolling, OneDrive diagnostics, NT51951 guidance, and terminology corrections. |
| `v0.9.14` | AB re-admission and targeted UI correctness | Deliver separately gated typed AB production authority, the minimum AB authoring UI, IC details, and owner-approved hover, modal/action-rail, and empty Replace coverage fixes. |
| `v0.9.15` | AB function open, delivery readiness, and review automation | Open NT51919/NT51929/NT51932 plus NT51950 `1 IC`/`Cascade` and selector-free NT51951 AB routes. Direct-golden debt blocks support certification, not function availability. Shared viewport/Changes/Button Presentation work is deferred pending a new owner-selected milestone. |
| `v0.9.16` | Exceptional CRC/header-authority hot-fix | Classify owner-approved CRC write windows, correct Replace/AB UI behavior and TP inspection, and lock the NT51929 single Normal CtrlRAM golden; former NT51950 AB certification work moved to `0.11.0`. |
| `v0.9.17` | Exceptional DiffDLM active-NF preservation hot-fix | Preserve active reference NF for 929-like and 950-like Cascade DiffDLM routes, rename the selector, make every independent NF slot unavailable on those routes, hide the four retired IC choices, and retain Single/full-replace behavior. |
| `v0.9.18` | NT51928 optional-input compatibility hot-fix | Make LDC optional in Standard Merge and allow Initial Code, LDC, or both in DP Replace while retaining exact declared capacities and support-neutral status. |
| `v0.10.0` | Maintainability planning and governance | Publish the approved IC-first architecture, terminology, FlashMap provenance, ADR lifecycle, validation standards, and canonical dependency-gated execution plan without changing firmware behavior or support truth. |
| `v0.10.1` | Headless canonical foundation | Complete all 78 admitted headless routes, reviewed capability/per-compilation identity, allocated firmware contracts, and explicit retirements without claiming deferred UI, deletion, Core convergence, or support certification. |
| `v0.10.2` | Complete canonical refactoring | Complete the approved deferred UI adoption, compatibility and legacy-runtime deletion, four Canonical Core Convergence slices, and the hard 22,607-line integration gate before tagging. |
| `v0.10.3` | Post-refactor simplification audit | Re-measure the completed canonical architecture, identify any remaining removable or simplifiable ownership, and accept further changes only with preserved behavior, evidence, and descending size gates. |
| `v0.10.4` | Unified preload performance control | Optimize startup and background work by making every preload use one observable, cancellable, bounded, and user-controllable lifecycle without changing firmware results or support truth. |
| `v0.10.5` | Path-based update experience | Reserve a user-facing update flow that obtains reviewed artifacts from a configured path so routine delivery does not require repackaging and email. Exact trust, rollback, version, network/share, and release-policy behavior remains owner-deferred and unimplemented until a later specification review. |
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
v0.9.0          stable raw-BIN Hex Editor utility milestone
v0.9.1          firmware-model-v2 foundation
v0.9.2          profile-bundle consolidation
v0.9.3          AB Code and CtrlRAM version edit
v0.9.4          automated IC intake candidate workflow
v0.9.5          V2 workflow convergence and legacy retirement
v0.9.6          support/release consolidation
v0.9.7          UI token consolidation and code-size discipline
v0.9.8          feature-frozen code-size convergence
v0.9.9          legacy convergence and verified patch closure
v0.9.10         end-to-end performance and Change Report remediation
v0.9.11         reconstructed stabilization and release hygiene
v0.9.12         CtrlRAM production routing, interaction stabilization, and release governance
v0.9.13         urgent UI and release stabilization
v0.9.14         separately gated AB re-admission and targeted UI correctness fixes
v0.9.15         AB function open, input/output usability, and review automation
v0.9.16         exceptional CRC/header-authority and UI-correctness hot-fix
v0.9.17         exceptional DiffDLM active-NF preservation hot-fix
v0.9.18         NT51928 optional-input compatibility hot-fix
v0.10.0         maintainability planning and dependency-gated execution program
v0.10.1         headless canonical foundation and reviewed firmware contracts
v0.10.2         complete canonical refactoring and hard integration-size gate
v0.10.3         post-refactor simplification and ownership audit
v0.10.4         unified controllable preload performance lifecycle
v0.10.5         reserved configured-path update experience
v1.0.0          stable
```

## Rules

- Create a tag only after its commit passes every gate available at that milestone.
- Never move or reuse a tag; corrections receive a new prerelease number.
- `VERSION`, changelog, assembly/worker versions, manifest, commit, and tag must agree.
- Development tags do not trigger stable publishing; only exact `vX.Y.Z` tags do.
- Stable release tags are signed once the signing policy and key custody are approved.
- `v0.9.12` and later stable releases require complete feature-level release notes using [Branch, Version, and Release Governance](branch-version-and-release-governance.md).

## `v0.5.0` release candidate gate

`v0.5.0` can be merged to `main` and packaged only after review gates pass on the milestone branch:

- Standard Merge verified against the available owner-approved golden set.
- NT51950/NT51951 DP Replace workbench output verified for the then-implemented exact-base/variable-DP rule. This historical `v0.5.0` gate is superseded for current production admission by the 2026-08-02 exact base/replacement capacity-pair decision.
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
