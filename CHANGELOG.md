# Changelog

All notable changes to NVT FW Combiner are documented here. The project follows Semantic Versioning and the Keep a Changelog section model.

## [Unreleased]

No unreleased changes.

## [0.10.1] - 2026-08-01

### Summary

This stable release completes the `0.10.x` headless canonical foundation: all
78 admitted headless routes now resolve through reviewed capability definitions
and one exact compiled composition per accepted authoring revision. It also
ships the approved NT51928, NT51929-family, NT51950, and NT51951 firmware
contracts, retires four unsupported IC families, and hardens deterministic
verification and release promotion. The deferred desktop UI, compatibility
deletion, and Core convergence waves are not part of this release.

### Product changes

#### Canonical headless capability and compilation authority

- Before → After: headless Merge and Replace consumers could resolve through
  parallel catalogs, route-specific bridges, or a fingerprint that conflated
  reviewed policy with one compiled plan; all 78 admitted routes now use the
  canonical capability catalog and retain exactly one immutable
  `CompiledComposition` for Preview, Build, Memory Layout, naming, and reports.
- Affected: headless Standard, AB, and General Merge plus DP, CtrlRAM, and
  General Replace for the ten selectable ICs: NT51917, NT51919, NT51923,
  NT51926, NT51927, NT51928, NT51929, NT51932, NT51950, and NT51951.
- Support status: support-neutral canonicalization. `CapabilityFingerprint`
  identifies the reviewed route, allowed map variants, selection groups, and
  compiler semantics; `CompilationFingerprint` additionally identifies the
  selected map/slots, General mappings, initializer, and processor plan.
- Compatibility: the existing UI and CLI continue to call the same shared
  Application planner/executor. Authoring/publication/evidence policy binds the
  capability fingerprint, while Preview/Build/Report bind the compilation
  fingerprint; selecting an already approved variant no longer creates policy
  churn.
- Verification: capability census, dynamic compilation, exact processor
  binding, report/naming identity, architecture, firmware-semantic, and full
  repository tests cover every admitted headless route.
- Limitations: desktop presentation migration and removal of Workbench/legacy
  runtime compatibility owners remain in the next ticket wave. The frozen
  runtime metric is 72,004 nonblank lines, so the 25,000-line target is not a
  claim of this release.

#### Isolated authoring, General workflows, metadata, and Saved Rule v2

- Before → After: authoring state, General mappings, selected-file identity,
  blank-output initialization, occupancy, reusable rules, and plan-only
  diagnostics had separate or incomplete owners; Merge/Replace sessions now
  isolate state, General inputs bind immutable content snapshots, and Saved
  Rule v2 uses strict identity, controlled storage, exact Trusted Parent
  binding, and typed diagnostic readiness.
- Affected: all headless Merge/Replace authoring sessions, General Merge,
  DP-only General Replace Saved Rules, metadata inspection, output naming,
  Memory Layout projection, CLI Preview/Build, and composition reports.
- Support status: support-neutral. General Merge remains blank-image plus
  explicit mappings; General Replace remains immutable-reference plus bounded
  mappings. Diagnostic Preview produces no firmware output when required
  processor authority or runtime readiness is unavailable.
- Compatibility: Saved Rule v1 remains read-only import compatibility and is
  not silently rewritten. Saved Rule v2 semantic edits invalidate stale
  compilation identity; existing explicit output paths and normal firmware BIN
  inputs remain unchanged.
- Verification: authoring-session, content-snapshot, initializer, occupancy,
  exact-parent, controlled-storage, CLI, report, metadata, and no-output
  diagnostic tests run through the canonical verifier.
- Limitations: stage-bearing TP/POSTBUILD Saved Rule execution remains closed
  pending its own exact processor-range, byte-golden, and firmware-owner gates;
  native macOS Saved Rule persistence is implemented but not runtime-validated.

#### Reviewed firmware routes and exact preservation semantics

- Before → After: NT51928 capacity selection, NT51919/29/32 AB symmetry,
  NT51950/NT51951 AB placement, and active DiffNF preservation depended on
  route-specific or incomplete lowering; they now compile typed map variants,
  source projections, region-instance deltas, and exact processor/write-view
  authority through the shared engine.
- Affected: NT51928 Standard Merge and DP Replace resolve `0x40000` without LDC
  or `0x80000` with LDC; NT51919/NT51929/NT51932 use symmetric AB compilation;
  NT51950 `1 IC`/`Cascade` and selector-free NT51951 use exact DP seed and TP
  placement contracts; NT51950/NT51951 two-IC CtrlRAM Replace copies only the
  active Diff CtrlRAM span while preserving active DiffNF and inactive records.
- Support status: function-open and evidence-explicit, not certified. NT51928
  remains `ContractOnly`. AB certification remains 0 of 6 cells even though
  the reviewed routes are executable and admitted evidence stays attached to
  its declared fact scope.
- Compatibility: firmware inputs remain immutable, Replace clones the required
  reference, Merge initializes the declared blank image, and external tools
  write only host staging copies within exact declared half-open ranges. The
  new canonical contracts intentionally fail closed on topology, capacity,
  source-view, processor, or write-range drift.
- Verification: exact range, topology, source-view, DiffDLM/DiffNF
  preservation, registered Combiner, whole-output, golden, and DP-tail tests
  cover the admitted contracts without inferring support from filenames,
  versions, or hashes.
- Limitations: direct AB goldens remain missing for NT51919, NT51932, NT51950
  Cascade, and NT51951; the NT51950 Cascade DP-tail vector is preservation
  evidence, not direct product parity. A complete NT51928 project golden is
  also unavailable.

#### Retirement of unsupported production capabilities

- Before → After: NT51920, NT51925, NT51930, and NT51931 retained selectable or
  registered compatibility surfaces despite lacking the approved `0.10.x`
  production authority; their selectors, profiles, registrations, package
  entries, and publication rows are now removed.
- Affected: every production Merge and Replace route for NT51920, NT51925,
  NT51930, and NT51931. Historical evidence remains reference-only.
- Support status: retired and unavailable in `v0.10.1`; historical notes are
  not an admission, migration, or support claim.
- Compatibility: operators who still require one of these four ICs must retain
  an untouched earlier portable package. No remaining family alias or nearby
  memory map is used as a replacement.
- Verification: selector, catalog, registration, profile-bundle, package, and
  architecture tests fail if a retired route reappears.
- Limitations: this release provides no automatic project migration or
  substitute route for a retired IC.

#### Verification and stable-release hardening

- Before → After: repository verification was sequential and stable-tag text
  transport or maintenance-source recovery could produce avoidable release
  ambiguity; the canonical verifier now uses bounded lanes and coverage/code
  size ratchets, while protected promotion binds exact source/workflow SHAs,
  normalizes only CRLF transport differences, and keeps historical maintenance
  sources behind a closed allowlist.
- Affected: repository CI, Windows x64 packaging, SBOM/provenance generation,
  annotated stable tags, recovery of `0.9.17`/`0.9.18`, and downloaded-package
  smoke verification.
- Support status: release-process hardening only; it does not promote a
  firmware route or widen executable authority.
- Compatibility: `python scripts/verify.py --all`, the portable-folder layout,
  and the five-asset stable Release contract remain the canonical interfaces.
- Verification: release-policy, package-allowlist, manifest, checksum,
  provenance, review/CI identity, tag/release recovery, and newline regression
  tests run before the protected release environment can create a tag.
- Limitations: visible clean-Windows UI launch, private-runner golden evidence,
  and final organizational license/legal approval remain separate release
  attestations and are not inferred from a green headless package smoke.

### Security

- Firmware inputs and reference images remain immutable; external processors
  receive only host-created staging copies and every mutation is checked
  against exact declared ranges before atomic output promotion.
- Canonical capability publication fails closed on stale definition identity,
  missing typed semantics, undeclared processor write views, or mismatched
  compilation proof.
- The stable workflow keeps candidate jobs read-only, grants `contents: write`
  only inside the protected release environment, publishes a closed five-asset
  set, and executes downloaded-package smoke without a GitHub write token.

### Known issues

- Headless canonical foundation is complete, but the full `0.10.x` restructuring
  program is 55.0% complete. Deferred UI, compatibility deletion, legacy
  runtime deletion, and Core convergence remain after this tag.
- Runtime production size is 72,004 nonblank lines. Reaching 25,000 requires
  deleting or simplifying 47,004 lines through the approved deletion and Core
  convergence sequence; `v0.10.1` does not claim that target.
- Direct AB golden coverage is 2 of 6 cells and support certification is 0 of
  6. NT51919, NT51932, NT51950 Cascade, and NT51951 direct product evidence
  remains missing; NT51928 remains `ContractOnly` without a complete project
  golden.
- Stage-bearing TP/POSTBUILD Saved Rule execution, native macOS persistence
  validation, and the deferred desktop UI wave remain unavailable or
  unvalidated as described above.

### Upgrade and rollback

- Upgrade from `v0.10.0` or `v0.9.18` by replacing the complete portable folder
  with `NvtFwCombiner-v0.10.1-win-x64`; do not mix files across versions.
- Preserve an earlier package if NT51920, NT51925, NT51930, or NT51931 is still
  required. Saved Rule v1 documents remain read-only imports; make a separate
  copy before saving a rule as strict v2.
- Roll back by restoring the untouched prior portable folder. Firmware outputs
  remain ordinary BIN files and require no database migration.

### Downloads and integrity

- The stable GitHub Release publishes exactly
  `NvtFwCombiner-v0.10.1-win-x64.zip`,
  `NvtFwCombiner-v0.10.1-win-x64.spdx.json`,
  `NvtFwCombiner-v0.10.1-win-x64.provenance.json`,
  `NvtFwCombiner-v0.10.1-candidate.json`, and
  `NvtFwCombiner-v0.10.1-assets.sha256`. GitHub also provides tag-derived
  source ZIP and TAR.GZ downloads.
- Verify the outer checksum list, candidate source SHA/tree, provenance, and
  package manifest before distribution. The Windows x64 ZIP is self-contained
  and does not require a separately installed .NET or Python runtime.

## [0.10.0] - 2026-07-25

### Summary

This planning release establishes the reviewed IC-first maintainability program
for the next `0.10.x` implementation slices. It ships no production refactor
and does not change firmware bytes, supported routes, processor authority, or
AB certification status.

### Product changes

#### Maintainability program and execution plan

- Before → After: maintainability decisions, IC/topology vocabulary, artifact
  metadata planning, authoring-session isolation, Memory Layout/Hex seams, and
  migration gates were dispersed across workshop discussion and documents; they
  now have one approved specification, canonical terminology, lifecycle rules,
  and a repository-anchored ticket dependency plan.
- Affected: future `0.10.x` implementation work only. Existing Merge, Replace,
  Hex Editor, reports, UI behavior, CLI behavior, profiles, and firmware bytes
  are unchanged by this release.
- Support status: support-neutral. The release does not promote an IC, workflow,
  topology, golden alias, or firmware evidence route.
- Compatibility: no input, output, saved-data, profile, protocol, or package
  migration is required.
- Verification: the canonical full verifier passed locally; the formal release
  workflow still supplies exact-main CI, package, provenance, asset, and
  downloaded-package evidence.
- Limitations: this release records the approved implementation program only;
  no ticketed production refactor or new firmware capability is included.

#### FlashMap evidence and ADR lifecycle

- Before → After: the owner-updated `IC_FlashMap_20260725.xlsx` reference and
  ADR lifecycle decisions had no single release provenance/terminology record;
  they are now hash-pinned and lifecycle-classified.
- Affected: evidence intake and architecture governance only; existing
  firmware inputs, profiles, executable routes, and user data are unchanged.
- Support status: support-neutral. No IC, topology, golden alias, or evidence
  route is promoted by the reference intake.
- Compatibility: no input, output, saved-data, profile, protocol, or package
  migration is required.
- Verification: the workbook size and SHA-256 are recorded in the reference
  manifest; the canonical repository verifier checks the documentation links.
- Limitations: this records evidence provenance and decision lifecycle only;
  later tickets must still turn approved plans into executable capabilities.

### Security

Firmware inputs remain immutable, release assets are integrity-manifested, and
this release introduces no new processor, network, or file-write authority.

### Known issues

- #170 through #197 remain dependency-gated implementation tickets. Publishing
  this plan does not start or complete any of them.
- The clean-Windows visible UI smoke and release-workflow annotated-tag newline
  hardening remain explicit later `0.10.x` work. This release does not claim
  either as complete.
- The final `0.10.x` 50% production-code reduction target becomes executable
  only after #171 integrates the ADR 0021 measurement into the canonical
  verifier; #197 applies it at program integration.

### Upgrade and rollback

- Upgrade by replacing the complete previous portable folder with
  `NvtFwCombiner-v0.10.0-win-x64`; do not copy individual files into an older
  package folder.
- No firmware data or saved settings migration is required. Roll back by
  restoring the untouched `v0.9.16` portable folder.

### Downloads and integrity

- The stable GitHub Release publishes `NvtFwCombiner-v0.10.0-win-x64.zip`, its
  SPDX SBOM, provenance, candidate manifest, and outer SHA-256 list. GitHub
  also provides tag-derived source ZIP and TAR.GZ downloads.
- Verify the outer checksum list and provenance source identity before
  distribution. The Windows x64 package is self-contained and does not require
  a separately installed .NET or Python runtime.

## [0.9.16] - 2026-07-24

### Summary

This hot-fix aligns CtrlRAM Replace postbuild authority with the IC header
workbook, fixes three operator-visible Merge/Replace state projections, and adds
direct NT51929 single Normal CtrlRAM regression evidence. Existing Standard
Merge, Customized Merge/Replace, DP Replace, AB byte execution, output naming,
and support-certification state remain unchanged.

### Product changes

#### Exact CRC/header authority for CtrlRAM Replace

- Before → After: a legacy Combiner postbuild could update real CRC words that were not represented by the selected profile, causing an otherwise valid CtrlRAM Replace to fail the host-side write-range check; profiles now authorize only their classified Header CRC, Header Copy CRC, CtrlRAM/MP CRC, and topology-applicable DiffDLM/DLM CRC words.
- Affected: Replace → CtrlRAM for NT51919, NT51920, NT51923, NT51926, NT51929, NT51930, NT51931, NT51932, NT51950, and NT51951. NT51929-family cascade is explicitly bounded to 2–8 IC. NT51932 uses the Type A/B header model. NT51950/NT51951 DIFF CRC fields are identified as DLM1 through DLM19.
- Support status: Corrective and support-neutral. The change does not add an IC/mode route, promote a candidate profile, infer one family's map for another family, or certify an AB route.
- Compatibility: Single-IC profiles continue to reject cascade-only DiffDLM/DLM CRC writes. Header copy remains the declared copy target; CRC words are separate exact postbuild write ranges. External processing remains confined to host-created staging copies and inputs remain immutable.
- Verification: Profile closure, catalog/trust-anchor, exact-range, topology, real-tool smoke, and owner-evidence tests run through the canonical verifier. An owner-supplied NT51929 single Andes cross-replacement differs from its owner output at exactly 16 bytes: four bytes in each declared Header/Header Copy CRC word and zero bytes in the cascade-only DLM CRC range.
- Limitations: The new NT51929 evidence covers that single-IC Normal CtrlRAM cross-replacement only. It does not provide cascade evidence or authorize redistribution of the private fixture.

#### AB Memory coverage clarity for NT51950/NT51951

- Before → After: the AB Memory coverage panel could show staging, CRC, and work-buffer rows that looked like broad writable output ranges; it now shows only `DP AB`, `TPA`, and `TPB`.
- Affected: Merge → AB Code for NT51950 `1 IC`/`Cascade` and selector-free NT51951. `DP AB` reflects the selected DP container length; TPA and TPB retain their fixed profile-declared spans.
- Support status: Presentation-only and support-neutral. AB execution ranges, postbuild authority, topology selection, and certification state are unchanged.
- Compatibility: Existing inputs, output sizes, reports, automatic names, and CLI behavior are unchanged. CRC rows are intentionally absent from this operator summary.
- Verification: Bootstrap coverage-role and UI smoke tests lock the exact three-row projection and selected-input sizing.
- Limitations: Direct AB certification debt for NT51950 Cascade and NT51951 remains open.

#### Replace context and TP-firmware inspection fixes

- Before → After: returning from AB Code to Replace could show the AB page's IC/IC-number selection on the first Replace device dialog, and CtrlRAM Replace could continue into DP version/CMI inspection after identifying a TP firmware input; Replace now restores its own device context immediately, and TP firmware bypasses DP-only metadata inspection.
- Affected: Merge → AB Code navigation back to Replace, plus Replace → CtrlRAM firmware inspection for profile-classified TP inputs.
- Support status: Support-neutral UI/application correction; composition bytes and profile admission do not change.
- Compatibility: Merge and Replace retain independent IC/number selections. Full FlashCode inputs still receive their declared DP and TP inspection, while TP firmware continues to expose TP facts and build through the same composition service.
- Verification: UI device-context, navigation, number-mismatch, firmware-inspection, and Bootstrap inspection regressions run in the canonical verifier.
- Limitations: The broader shared Support Matrix and error/report experience remain planned for `0.10.0`.

### Security

- This release does not change a CRC algorithm, command order, executable binding, arbitrary-command policy, or source/final-output immutability boundary.
- Postbuild write authority is narrower than the legacy header span and is expressed as exact half-open ranges; bytes between classified CRC words remain unauthorized.
- The NT51929 private golden is repository test evidence only, is excluded from the release reference selector, and is not packaged for redistribution.

### Known issues

- AB function availability remains a certification-neutral `0.9.15` state. Missing per-route direct AB evidence and firmware-owner promotion review still block support-certification claims.
- The protected workflow's deterministic package smoke uses `-SkipUiLaunch`. Visible clean-Windows UI smoke and annotated-tag CRLF/LF comparison hardening remain tracked for `0.10.0`; this release does not claim those deferred checks passed.

### Upgrade and rollback

- Upgrade by replacing the complete previous portable folder with `NvtFwCombiner-v0.9.16-win-x64`; do not copy only the EXE or merge package contents into an older or OneDrive-synchronized profile tree.
- Saved preferences and report history require no migration. Roll back by restoring the untouched `v0.9.15` portable folder; firmware outputs remain ordinary BIN files.

### Downloads and integrity

- The stable GitHub Release publishes `NvtFwCombiner-v0.9.16-win-x64.zip`, its SPDX SBOM, provenance, candidate manifest, and outer SHA-256 list. GitHub also provides tag-derived source ZIP and TAR.GZ downloads.
- Verify the outer checksum list and provenance source identity before distribution. The Windows x64 package is self-contained and does not require a separately installed .NET or Python runtime.

## [0.9.15] - 2026-07-24

### Summary

This release opens the profile-declared AB Code workflow for NT51950 and NT51951,
makes the resulting FlashCode identity auditable from accepted DP/TP metadata, and
adds an optional A-only FlashCode delivery for the NT51929 family. It also improves
CtrlRAM Replace handling of TP firmware bases for NT51950/NT51951 and adds
release-review evidence automation. Standard Merge, Customized Merge, DP Replace,
and existing CtrlRAM workflows retain their established execution contracts.

### Product changes

#### AB Code function opening and output identity

- Before → After: AB Code exposed only the fixed NT51919/NT51929/NT51932 route; it now also exposes profile-owned NT51950 `1 IC`/`Cascade` choices and selector-free NT51951 in Merge UI and CLI. Every route declares its A/B CMI readers in its compiled map, so output naming and input inspection have no IC-specific GenFlash catalog fallback. The selected canonical IC, compiled map, and—only for NT51950—the explicit topology token select execution; TP version, PID, Common FW, project ID, filename, and hash never select a route.
- Affected: Merge → AB Code. NT51950 `1 IC` outputs 512 KiB; NT51950 `Cascade` and NT51951 output 1 MiB. Every TPA/TPB input must cover `[0x00000,0x37000)` and contributes only `[0x0A000,0x37000)`.
- Support status: Function open but not certified. NT51950 `1 IC` has evidence pending formal closure; direct AB vectors for NT51950 `Cascade` and NT51951 remain unavailable. Evidence from one IC or normal Merge/Replace/CtrlRAM does not promote another AB route.
- Compatibility: Existing explicit output paths continue to win and remain the report identity. Automatic names use `NT519xx_FlashCode_A_DmmmmTvvvv_B_DmmmmTvvvv_yyyyMMdd.bin` with a UTC date. Output/input aliases fail closed; a different pre-existing output is atomically replaced.
- Verification: Profile, runtime, UI, CLI, source-immutability, output-override, stale-tail, topology-mismatch, and output-preservation coverage runs through the canonical repository verifier.
- Limitations: NT51950/NT51951 AB availability is support-neutral until direct evidence and firmware-owner review close the route-specific promotion gates.

#### Optional NT51929 A-only FlashCode delivery

- Before → After: an NT51929 AB build produced only the combined A/B container; the operator can now choose before Build whether to also write the profile-declared A bank as a separate FlashCode, with a separate save choice for each output.
- Affected: Merge → AB Code for the NT51919/NT51929/NT51932 family. NT51950 and NT51951 intentionally do not expose this A-only delivery.
- Support status: Available within the existing AB pilot scope; it does not certify additional ICs or change AB evidence status.
- Compatibility: Declining the optional delivery preserves the prior one-output flow. Automatic and explicit output names remain distinct, and every reported artifact uses its effective saved name.
- Verification: UI and Bootstrap tests cover the two-save-dialog order, automatic-name re-resolution, output/input alias rejection before execution, A-bank range declaration, primary-output preservation, and partial-delivery reporting.
- Limitations: The optional A-only artifact is a profile-declared NT51929-family delivery only; it is not inferred for other AB layouts.

#### CtrlRAM Replace TP firmware-base recognition

- Before → After: CtrlRAM Replace could present an NT51950/NT51951 TP firmware base as a FlashCode-shaped input; it now recognizes the declared TP-only form and names a successful output as `NT519xx_TPFW_Tvvvv_yyyyMMdd.bin`, including an operator-selected TP version update.
- Affected: Replace → CtrlRAM for NT51950 and NT51951. TP-only input is the declared `[0x00000,0x37000)` prefix; full FlashCode bases retain their complete image and their FlashCode naming.
- Support status: Support-neutral routing and naming improvement. It does not promote AB Code or reuse one IC's evidence for another.
- Compatibility: Existing full FlashCode CtrlRAM Replace continues to preserve the tail outside the TP prefix. Other ICs classify TP firmware from their profile-declared DP regions rather than from a shared size shortcut.
- Verification: Candidate profile, map-closure, output-naming, full-flash tail-preservation, TP version propagation, and canonical repository verification tests cover the behavior.
- Limitations: The postbuild tool runs only over the declared TP prefix; its write authority and all firmware support claims remain profile-declared and review-gated.

#### Delivery-to-review automation

- Before → After: reviewers had to reconstruct baseline, changed-file, and residual-gate evidence manually; the read-only `collect_review_handoff.py` collector now fails closed for dirty or ambiguous lineage and records the exact annotated baseline, peeled SHA, branch/tree, committed diff, supplied CI state, verification state, impact, unchanged boundaries, and required human gates.
- Affected: Version-branch review handoff only.
- Support status: Release-process improvement; firmware support and byte behavior are unchanged.
- Compatibility: The collector is evidence-only and cannot build, merge, tag, push, publish, or promote support.
- Verification: Review-handoff contract coverage and canonical verification validate clean-lineage and release-evidence behavior.
- Limitations: Direct AB evidence closure is planned certification work and remains separate from this automation.

### Security

- AB inputs remain immutable. TPA is copied unchanged; TPB is processed only through the declared host-owned staging path, and the complete DP tail is preserved.
- Automatic output names and optional A-only delivery are checked against selected inputs before composition. Explicit output paths remain allowed only when they do not alias protected inputs.
- This release does not broaden arbitrary command execution, change checksum algorithms, or allow an external processor outside its declared read/write ranges.

### Known issues

- No function-open AB route is certified. NT51929 requires firmware-owner promotion review; NT51919 and NT51932 still require their own direct-product golden closure beyond the approved NT51929 fact scope; NT51950 `1 IC` has a supplied vector awaiting formal intake/certification; NT51950 `Cascade` and NT51951 still require direct vectors. Firmware-owner promotion remains outstanding for every route. Availability is not a certification claim.
- All-IC/all-mode workflow documentation, shared presentation convergence, Settings Support Matrix, error-experience unification, IC family/rule authoring, customized-plan import, and report-layout redesign remain scheduled for later owner-selected releases.

### Upgrade and rollback

- Upgrade by replacing the complete previous portable folder with `NvtFwCombiner-v0.9.15-win-x64`; do not copy only the EXE or merge package contents into a OneDrive-synchronized profile tree.
- Saved preferences and report history require no migration. Roll back by restoring the untouched `v0.9.14` portable folder; firmware outputs remain ordinary BIN files.

### Downloads and integrity

- The stable GitHub Release publishes `NvtFwCombiner-v0.9.15-win-x64.zip`, its SPDX SBOM, provenance, candidate manifest, and outer SHA-256 list. GitHub also provides tag-derived source ZIP and TAR.GZ downloads.
- Verify the outer checksum list and provenance source identity before distribution. The Windows x64 package is self-contained and does not require a separately installed .NET or Python runtime.

## [0.9.14] - 2026-07-22

### Summary

This release makes the NT51919/NT51929/NT51932 AB Code pilot available through
the shared composition workbench, reports file-size health as soon as inputs are
loaded, fixes the requested targeted UI interactions, and replaces manual stable
tagging with a protected CI-owned release promotion. It does not admit
NT51950/NT51951 AB Code or perform the broader cross-workflow UI redesign planned
for `v0.9.15`.

### Product changes

#### AB Code pilot and typed input health

- Before → After: AB Code was hidden and rejected; the three approved 51929-family ICs now expose DP_AB, TPA, and TPB authoring, Preview, Build, report evidence, and independent DP1/DP2/TPA/TPB facts through the shared V2 composition path.
- Affected: Merge → AB Code for NT51919, NT51929, and NT51932. DP_AB requires/uses 512 KiB; TPA and TPB each require/use 256 KiB.
- Support status: Available as an owner-scoped pilot; exact six-operation/no-processor confirmation and firmware-owner support promotion remain human gates. NT51950 and NT51951 remain closed.
- Compatibility: Standard Merge, Customized Merge, Replace, saved reports, CLI tokens, and existing profile schemas are unchanged. Inputs shorter than the required span block; longer inputs warn and ignore the declared trailing range without mutating the source.
- Verification: Boundary tests cover one byte short, exact, oversized, large ignored tails, mixed/unknown TP versions, immutability, shared runtime execution, and the three-IC visibility fence.
- Limitations: The AB page intentionally reuses the released Merge layout. Full header/status/coverage unification and additional AB families are deferred.

#### Load-time diagnostics, IC details, and targeted UI correctness

- Before → After: input problems appeared too late or as noisy page badges, loaded slots could lose their accepted green state, empty Replace targets could look changed, dense base hatching could overpower changed ranges, coverage could leave unused width, filename tips could lose the full path or obstruct clicks, byte hover required touching glyphs, the action rail could overlap modals, and refreshing IC-dependent Merge choices could mistake a binding update for navigation away from Replace; loaded slots now show a green accepted state overridden only by pending/warning/error health, IC facts live in a compact icon-first pass-through hover/focus card that closes immediately after selection, sparse hatching plus a fixed legend distinguishes every kept range from solid changed ranges, bars fill the available width, filename hover shows the stable absolute path away from its clickable target, full byte cells hover, modal state removes background actions from hit testing, and Merge mode changes no longer leave Replace unless the user invokes navigation.
- Affected: Merge/Replace slot cards, IC selector, Output layout, Hex Editor byte cells, filename reveal, and the bottom-right `View file`/Build actions.
- Support status: Support-neutral presentation and typed Bootstrap projection; no IC range, processor, operation order, or support stage changes.
- Compatibility: Existing file selection, Explorer reveal-and-focus, Build/report commands, keyboard focus, English/Traditional Chinese resources, coverage proportions, and typed Changed decisions remain intact. Replace hatching now consistently means Kept, including unselected colored regions.
- Verification: UI smoke tests cover AB readiness, hidden AB cache clearing, semantic slot severity, Changed/Kept pattern transitions and legend bindings, exact 300-unit normalization, pointer pass-through, focus-tooltip selection lifecycle, mode-binding navigation isolation, pending-route accessibility, modal hit testing, and expanded action-rail spacing contracts. IC evidence now names verified/open workflows instead of presenting workflow counts as golden-case counts.
- Limitations: A global Button interaction/state rollout, shared Hex/Changes viewport, and Report issue-triage redesign remain scheduled for `v0.9.15`.

#### Canonical toolchain and CI-owned stable promotion

- Before → After: callers could use the wrong PowerShell/.NET entry and stable publication still depended on a manually created tag; the canonical Windows entry re-enters under PowerShell 7, bootstraps the pinned SDK, avoids duplicate package workflows, and the protected release workflow creates the immutable tag and complete Release only after exact-main/review/check/environment validation.
- Affected: repository bootstrap/test/package entry points, pull-request CI, `main` package preview, and stable GitHub Release promotion.
- Support status: Release infrastructure only; firmware support and byte behavior are unchanged.
- Compatibility: `python scripts/verify.py --all` remains the sole full verifier. The portable self-contained Windows x64 ZIP, SHA-256, SBOM, provenance, and GitHub source archives remain the distribution contract.
- Verification: Release-policy tests cover exact SHA/tree/review binding, protected-main context, unused/recoverable tag and Release states, closed asset manifests, digest validation, release-note rendering, and downloaded-package smoke.
- Limitations: The protected `release` environment, branch protection, clean-machine UI smoke, legal review, and human release/security approval must exist outside the repository before publication.

### Security

- AB inputs remain immutable; only the declared DP_AB/TPA/TPB prefixes enter the compiled operations, ignored tails remain report evidence, and short inputs produce no output.
- This release changes no CRC/header algorithm, external-processor authority, arbitrary command policy, or OneDrive/reparse-point fail-closed boundary.
- Stable promotion receives write permission only after the read-only candidate job and protected environment approval. It binds the reviewed PR tree, final `main` commit, annotated tag, release notes, asset names, sizes, hashes, SBOM, and provenance.

### Known issues

- NT51950/NT51951 AB Code is not available. Cross-workflow header/status/coverage unification, global white secondary-button feedback, shared Hex/Changes rendering, and one-glance Report issue summaries are deferred to `v0.9.15`.
- Firmware-owner confirmation of the exact pilot route and clean Windows x64 validation without development runtimes remain release gates; this changelog does not claim those external checks have passed.

### Upgrade and rollback

- Upgrade by replacing the complete previous portable folder with `NvtFwCombiner-v0.9.14-win-x64`; do not copy only the EXE or merge package contents into a OneDrive-synchronized profile tree.
- Saved preferences and report history require no migration. Rollback restores the untouched `v0.9.13` portable folder; outputs created by either version remain ordinary BIN files.

### Downloads and integrity

- The stable GitHub Release publishes `NvtFwCombiner-v0.9.14-win-x64.zip`, SPDX SBOM, provenance, candidate manifest, and outer SHA-256 list. GitHub also provides tag-derived source ZIP and TAR.GZ downloads.
- Verify the outer checksum list and provenance source identity before distribution. The package is self-contained for Windows x64 and does not require a separate .NET or Python installation.

## [0.9.13] - 2026-07-22

### Changed

- Firmware filenames are concise again instead of always showing an ambiguous partial or noisy absolute path. Clicking a selected source filename, generated-output filename, or recent-output action now opens Windows Explorer with that exact existing BIN selected; inaccessible or missing files report a visible failure without invoking a shell command string.
- The bottom-right action rail now overlays only its own compact footprint instead of reserving a full-width bottom band, so Build remains reachable without covering or shortening the lower workflow content. The report Inputs workspace owns its vertical scrolling, including when an expanded summary makes the page taller than the modal.
- User-facing workflow labels are now `Standard Merge`, `Customized Merge`, and `Customized Replace`. Existing `Normal` and `General` contract tokens, CLI behavior, saved data, profiles, and automation remain unchanged.
- NT51951 DP slot descriptions now state `(Initial Code + LDC)` for both Merge and DP Replace. This is guidance for the existing packaged payload shape and does not add a new input, range, or support claim.
- Base-firmware memory coverage keeps its gray fill and adds black diagonal hatching, preserving the existing byte meaning while making retained base regions visibly distinct from the panel background.

### Fixed

- Restored the bottom-right action icons and kept primary blue-button text white during hover so Build, dialog, and report actions remain readable. The TP-version confirmation now presents the keep/edit choice and base value with clearer visual hierarchy.
- CtrlRAM Replace output naming now uses the edited TP firmware version selected for the output backup; previously the bytes changed while the generated filename could retain the base version.
- A profile bundle rejected because its package path crosses a Windows reparse point now explains that OneDrive Files On-Demand can trigger the safety boundary and instructs users to re-extract the complete portable folder to a local non-synchronized directory such as `C:\Tools`. The fail-closed path validation is intentionally retained.
- Home workflow cards now use the same Standard/Customized terminology as their selectors, and the action-rail architecture contract matches the non-reserving overlay row.

### Security

- This release changes no firmware range, operation order, padding/truncation rule, CRC/header algorithm, processor authority, profile admission, report schema, or support stage. Source artifacts remain immutable, Explorer reveal accepts only an existing fully qualified file, and reparse-point profile roots remain rejected.
- AB Code candidates remain hidden and rejected at the Application run boundary. NT51919/NT51929/NT51932/NT51950/NT51951 evidence is not promoted by this UI stabilization release.

### Notes

- Upgrade by replacing the complete previous portable folder with `NvtFwCombiner-v0.9.13-win-x64`; do not copy only the EXE into an older or OneDrive-synchronized profile tree. Rollback restores the untouched `v0.9.12` folder and requires no saved-data or report migration.
- AB Code production re-admission plus aligned Merge/Replace Mode placement and compact evidence-status icons are scheduled for `v0.9.14`. The shared Raw Hex Editor/Change Report viewport, redesigned Changes workspace, and full global Button pressed rollout remain scheduled for `v0.9.15`; the latter still requires routed pointer/keyboard, disabled-state, reduced-motion, and effective high-contrast evidence.
- The release package is a self-contained Windows x64 ZIP with SHA-256, SBOM, and provenance sidecars. Verify those files before distribution. Clean-machine validation, accessibility assistive-technology review, legal review, and firmware-owner support promotion remain organizational gates and are not claimed by the local candidate.

## [0.9.12] - 2026-07-22

### Added

- Profile-driven CtrlRAM production routing for all 31 runtime profile/build-plan pairs across the 13 selectable ICs. Requested IC selects family, effective Common FW interval selects only among genuinely different postbuild profiles, and a typed Number selector chooses single, generic cascade, exact-count, or bounded count-range plans.
- Support-neutral CtrlRAM routes for NT51919/NT51929 cascade, NT51931/NT51932 single, NT51928 non-NB single/2-chip/3-chip, and NT51950/NT51951 cascade. NT51928 keeps DP and LDC as separate required DP Replace inputs; NT51950/NT51951 package LDC inside the DP payload.
- A bilingual IC Number mismatch confirmation that can switch to the detected plan without rereading or discarding compatible selections. Cancel keeps the current UI context and the authoritative Build path still blocks a contradictory exact plan.
- A bilingual navigation confirmation before leaving Merge or Replace with selected files. Cancel retains the page; confirm clears only that workflow's file/mapping selections while keeping device context, mapping addresses, and Settings.
- Typed failure summaries that automatically open on Build failure or missing output and show the primary reason, failed step, affected output, and next action.

### Changed

- Golden identities are regression evidence rather than production admission. PID, filename, exact fixture version, whole-file SHA-256, and a fixture's observed generic-cascade count remain visible in reports but do not choose a family or route.
- Common FW profiles now use half-open effective intervals from `1.0.0`. NT51926 uses the 1.4.1-sourced plan for `[1.0.0,2.0.0)` and the 2.0.0-sourced plan for `[2.0.0,infinity)`; every other single-profile IC accepts missing or later informational Common FW without inventing another boundary.
- NT51930 exposes exactly `1 IC` and `2–13 IC`; count 14 and above remains unavailable because no owner command plan exists. NT51927 and NT51928 non-NB expose only their owner-provided single/2-chip/3-chip plans.
- Selected CtrlRAM BIN cards use the completed/green state. Base firmware and Common/topology sections retain consistent width and reusable spatial padding.
- The bottom-right actions use a vertical action rail. Circular icons expand labels to the left on hover or keyboard focus without tooltip interception, Build remains the bottom primary action, and reduced motion retains an immediate static state cue.
- Pre-1.0 source-size governance retains the 75,000-line production ceiling and exact duplicate-JSON gate while replacing two brittle exact-equality partial ratchets with reviewed 4,500-line ceilings for `WorkbenchCompositionService` and `MainWindowViewModel`.

### Fixed

- CtrlRAM routing no longer returns size-zero/no-output merely because production input metadata does not match a hash-pinned golden tuple.
- CtrlRAM FWConfig reads and optional TP firmware-version edits use the canonical source/copy model rather than treating legacy copy/backup destinations as the user-edit source.
- Perfect-family filename hints can retain the selected IC without granting cross-family authority; partial-family and incompatible IC hints still require explicit confirmation.
- Build failures now publish a visible reason instead of silently leaving the user without an output file.

### Security

- Input/reference artifacts remain immutable; Legacy Combiner still operates only on host-created staging copies, and final output is independently rejected outside declared half-open write ranges.
- The route expansion changes no CRC/header algorithm, command order, processor authority, output naming, report schema, runtime dependency, AB behavior, or public support stage. All new profile routes remain support-neutral pending their normal direct-output and firmware-owner promotion gates.
- NT51928 NB remains excluded. NT51950/NT51951 AB remains a separate workflow and is not inferred from matching normal CtrlRAM/DP layouts.

### Notes

- Owner decision on 2026-07-22 supersedes the earlier visibility schedule: the repository remains Public through the stable `v1.0.0` release and becomes Private afterward. Public history, tracked owner-approved golden payloads, caches, and existing forks cannot be retracted by the later transition. This closure adds no new BIN/archive/secret payload; the exact existing public inventory and packaging boundary are recorded separately.
- Focused production tests cover actual Workbench output for the added routes, including DiffDLM presence/absence, 256/512 KiB container preservation, NT51928 DP/LDC tail preservation, report identity, and immutable sources. Clean-commit canonical verification and independent R3 code review pass. A real NT51926 Windows CLI Build matches its manifest expected output exactly, and the Number-mismatch run publishes no output; see [`v0.9.12-ctrlram-build-evidence.md`](docs/references/v0.9.12-ctrlram-build-evidence.md).
- The existing unsupported owner-handoff `.7z` inventory entry is not modified or silently accepted. It still blocks verification in the primary worktree, while the same reviewed commit passes `verify.py --all` in a clean detached worktree. Protected CI, clean-machine package smoke, per-plan firmware-owner promotion decisions, and final release packaging remain open gates.
- The shared Hex viewport, redesigned read-only Changes workspace, global Button pressed acknowledgement, and AB Code architecture re-admission are deferred to `v0.9.13`. All existing AB candidates remain hidden, support-neutral, and rejected by the Application run boundary in this release.
- No saved-data or report-schema migration is required. Upgrade by replacing the previous portable application folder; rollback uses the untouched `v0.9.11` package and does not require profile conversion.
- Stable downloads will include `NvtFwCombiner-v0.9.12-win-x64.zip` plus GitHub-generated source `.zip`/`.tar.gz`. Verify the published SHA-256, SBOM, and provenance before distribution; final values are generated only from the tagged `main` commit.

## [0.9.11] - 2026-07-21

### Added

- Profile-driven DP Replace authoring for every declared selectable IC, including the distinct NT51928 non-NB LDC FlashCode input. Authoring availability does not promote an IC/workflow to production support without its existing golden and firmware-owner gates.
- Opt-in startup traces with per-stage cumulative managed-allocation traffic, first-frame synchronous UI intervals, bounded background page-materialization intervals, and process working-set observations.
- Post-first-frame warm-up for immutable catalogs and the common Device Context, Replace, Merge, Settings, and Hex Editor visual trees without navigation, user-file reads, processor execution, profile mutation, or Build authority.

### Changed

- Reconstructed the release candidate from the exact final `v0.9.10` predecessor and retained its output-publication, immutable-input, CtrlRAM postbuild, report-delivery, firmware-identity, Build-completion, package-authority, stale-search, and final Changes behavior.
- IC Number remains a choice selector after Home navigation and warm-up. CtrlRAM inputs and Memory layout use profile-projected Common/Cascade or Common/Master/Slave topology groups rather than assigning DiffDLM to a generic single-IC group.
- Replace spacing is owned by the reusable `SpaciousPanel` default; nested input/group sections retain deliberate padding. The Build action stays in a fixed bottom-right rail with restrained hover/pressed/focus states, no tooltip interception, reduced-motion behavior, and no Memory layout occlusion.
- The Windows package remains self-contained and untrimmed while using compressed composite ReadyToRun. The measured candidate main EXE is 69,990,762 bytes and the complete ZIP is 75,358,293 bytes, both below the owner-approved 80,000,000-byte ceilings.

### Security

- No firmware range, operation order, CRC/header algorithm, processor authority, support stage, report schema, or runtime dependency is changed by the startup, package, and spatial UI work.
- The shared Raw Hex Editor/Change Report Hex viewport and redesigned range-only Changes workspace remain deferred to `0.9.13`.

### Notes

- On the recorded Windows machine, one warm-up plus five packaged Home launches measured a 908.146 ms process-to-window median. The one-second goal passes; the aspirational 0.8-second target does not and is not a universal claim.
- By owner decision on 2026-07-21, `0.9.11` is support-neutral: no IC/workflow support stage is promoted. Existing per-IC golden and firmware-owner gaps remain future support-promotion gates rather than blanket blockers for this release.
- Clean Windows x64 execution without separately installed .NET/Python, representative firmware UAT, accessibility/visual review, protected CI, final reviewed-main packaging, signing, and immutable release publication remain release gates.

## [0.9.10] - 2026-07-20

### Added

- Application-owned typed composition progress with stable phases, step counts, workflow context, accessible announcements, and a persisted reduced-motion mode that keeps status visible while removing non-essential animation.
- A typed post-commit delivery boundary that announces a usable atomic Build output before complete Change Report, Hex Diff, and history projection finish in the background; Preview and uncommitted failures expose no artifact.
- A bounded read-only Hex Diff for complete in-session reports: 16-byte output/ASCII rows, optional original rows, typed reason and expected/review-required verdict, before/after hashes, half-open output ranges, address jump, and a review-first range navigator. Persisted reports without verified before/output bytes degrade explicitly instead of inventing a comparison.
- Deterministic B/C performance surfaces for Automatic Build, Legacy Combiner sessions/launches/full reads, failed-input zero commit, fragmented reports, report history, startup restoration, immutable firmware inspection, and Raw Hex Editor search reuse.

### Changed

- One Build now performs one authoritative composition and report publication. DP changes from two runs/four artifact reads to one run/two reads; CtrlRAM changes from two processor sessions and `2C` launches to one session and the same exact `C` sequential launches.
- Legacy Combiner commands remain exact, sequential, hidden-window, hash-pinned, and staging-confined, while complete staged-firmware reads change from `2C+1` to one final read. Final independent diffing still rejects changes outside the union of declared write ranges; only the evidenced `MERGE_MODE` short-output path may read a selective predecessor tail.
- Report generation, JSON/history sizing, bounded history retention, large-report projection, startup report restoration, preference persistence, firmware inspection, output-name projection, Raw Hex Editor change tracking, and Search/Next reuse avoid repeated full scans/copies or dispatcher I/O while preserving their existing contracts.
- Successful Build keeps the UI responsive after atomic output commit and retains run ownership until the complete report is ready, preventing overlapping composition and report-history publication.
- Change review now opens in the focused Hex Diff workspace instead of presenting a long byte-change list as the primary view; JSON export, operation/postbuild/issues/raw evidence, and history reopen behavior remain available.

### Security

- External processor executable identity, command order/argv, per-command failure gates, private staging, immutable source artifacts, final write-range enforcement, atomic output promotion, and report evidence remain fail closed. No arbitrary executable path or script authority was added.
- The release changes no IC support promise, profile range, CRC/header algorithm, processor command, or product golden. NT51931 Replace remains Not available, and 13-command evidence remains count/order-only rather than an independent full-output golden claim.

### Notes

- Stable Node B is protected-main `v0.9.9` commit `32c37e25`; optimized production Node C is `6f3698dd`. The exact NT51926 two-command output and 10,000-difference report/JSON hashes remain identical across the comparison.
- PR #150's runtime integration baseline is `e968a310`; subsequent reviewed UAT, safety, release-manifest, progress, and release-review fixes remain part of the current `0.9.10` branch without redefining the Node B/C measured samples.
- The small physical two-command fresh-testhost case was slightly slower on Node C, so v0.9.10 claims deterministic execution/read/allocation reduction, not a Legacy Combiner wall-clock improvement for that case.
- The exact review-fix tree contains 60,050 nonblank production C#/AXAML lines under the owner-approved temporary maximum of 60,100; the 50-line increase above the earlier ceiling retains same-path firmware identity, coverage fallback, and final save-dialog revalidation. Code size is a maintainability gate, not the release performance KPI.
- The repository remains Public through `0.9.11` by owner decision on 2026-07-20 and is scheduled to become Private after that milestone completes.

## [0.9.9] - 2026-07-19

### Added

- Canonical, manifest-pinned direct and fact-scoped golden evidence for the reviewed Standard Merge, DP Replace, and CtrlRAM Replace cases, including the final 2026-07-18 owner intake and exact provenance, size, and SHA-256 identities.
- Exact V2 CtrlRAM routes for the admitted owner cases, with ordered postbuild commands, declared processor read/write ranges, immutable inputs, output hashes, and replacement-versus-header/CRC difference classification locked by regression tests.
- Closed canonical golden inventory validation under `IC/workflow/variant-or-version/topology/case/{inputs,expected,provenance}`, with diagnostics separated from expected outputs and link/junction escapes rejected.

### Changed

- Retired the production V1 composition compiler and its remaining runtime authority after exact V2 route and fail-closed coverage; the constrained Legacy Combiner executable and runner remain the deliberate exception.
- CtrlRAM replacement inputs now follow the physical Postbuild model and copy at most the declared section maximum while accepting shorter immutable sources; oversized sources still fail closed, and section expected/maximum byte facts are projected to the workbench.
- DP Replace requires the same-IC canonical Standard Merge map and a complete compatible Reference FlashCode. AB FlashCode remains a separate evidence-gated profile concern and is not inferred from Standard Merge offsets.
- Kept NT51931 Replace unavailable while preserving its reproducible evidence-only Combiner investigation; non-exact or release-excluded shapes remain fail closed rather than being promoted from family similarity.
- Consolidated the owner-approved canonical golden and external-tool inventories without changing runtime routes, profile facts, firmware bytes, expected outputs, or support status.
- Moved the generated CRC Worker 0.1.0 payload from the release root to the closed `external-tools/crc-worker/0.1.0/` package path while preserving Protocol 1.0, its manifest hash, and the packaged `123456789` self-test.

### Security

- External processors remain hash-pinned, package-allowlisted, staged, and independently diff-constrained. DiffNFMerge is repository evidence only: it is not runtime-registered, executed, or included in the release ZIP.
- Production C#/AXAML remains below the owner-approved 54,000-nonblank-line ceiling without removing required safety, evidence, range, CRC/header, or fail-closed checks.

### Notes

- Exact golden verification and runtime availability remain distinct from product-support promotion; R3 firmware-owner review continues to gate promotion.
- The earlier `v0.9.9` tag incorrectly pointed to an internal milestone tree that still reported version `0.9.8`. The owner explicitly approved replacing that tag only after this metadata-aligned tree passes review and required CI and is merged to `main`; `v0.9.9.5` remains an internal predecessor tag and is not stable package authority.

## [0.9.8] - 2026-07-16

### Added

- Exact canonical ratchets for hand-written production C#/AXAML, byte-identical profile/schema JSON, oversized partial-type aggregates, and the Windows release ZIP.
- Deterministic package smoke enforcement for the owner-approved 58,076,715-byte maximum, including manifest, hash, SBOM, provenance, external-tool allowlist, worker self-test, and desktop startup checks.

### Changed

- Reduced production C#/AXAML from the 60,237-line `v0.9.7` baseline to the owner-accepted 56,742-line final ratchet without changing firmware behavior or support state.
- Reduced exact duplicate profile/schema JSON from 10,781 to 1,156 lines, `WorkbenchCompositionService` from 6,033 to 4,483 lines, and `MainWindowViewModel` from 3,035 to 2,847 lines.
- Consolidated canonical schema ownership, built-in bundle registration, external Combiner tool resolution, byte-range/string helpers, CLI/UI run lifecycles, local JSON persistence, and immutable report projections.
- Retired only verified test-only, unbound, pass-through, synthetic, or exactly replaced compatibility surfaces; required validation, tests, evidence manifests, and external processing boundaries remain intact.

### Security

- Legacy Combiner 1.13.0 and its constrained staged runner remain the explicit legacy exception; no executable path, command, read/write range, CRC/header behavior, or runtime support state is promoted by this release.
- AB and CtrlRAM candidates retain their direct or fact-scoped evidence gates, and C# does not write AB header CRC bytes.

### Notes

- The reviewed local Windows candidate ZIP remains below the owner-approved 1% growth ceiling; exact bytes and SHA-256 belong to the immutable package evidence for its source commit.
- CtrlRAM golden byte parity/output sign-off, protected remote CI, signing/legal approval, and an independent clean-machine package run remain explicit release/support gates.

## [0.9.7] - 2026-07-15

### Added

- Evidence-gated AB Merge candidates for NT51919, NT51929, NT51932, NT51950, and NT51951, including direct or fact-scoped applicability records, Python-reference parity, and exact legacy Combiner 1.13.0 command coverage where established.
- Manifest-driven candidate IC intake, logical-output General Merge compilation, runtime reference-replace compilation, and compiled final-output validation reports.
- A non-routed NT51926 Common FW 1.4.1 CtrlRAM V2 candidate with canonical NVT FWConfig Backup resolution and closed external-processor write authority.
- Closed release-package evidence sidecars, external-tool allowlist verification, and deterministic package smoke checks.

### Changed

- Retired exactly replaced Standard Merge, DP Replace, and General Merge legacy definitions after their V2 parity gates, while preserving unsupported and owner-review blockers.
- Retired the duplicate DP Perspective C# fact catalog; NT51950/NT51951 capacities, TP/customer-information ranges, operations, and surfaced IC membership now project from the trusted V2 bundle and compiled plan.
- Consolidated audited Avalonia slot, firmware-fact, icon, and coverage-state colors into one semantic token owner without moving firmware decisions into Presentation code.
- Final-output validation failures now block output publication and report precise stable issue codes and validation outcomes.

### Security

- AB and CtrlRAM candidate profiles remain outside Application runtime admission until their per-IC product golden and firmware-owner gates close.
- External Combiner processing remains confined to host-created staging artifacts with declared read/write ranges; no user firmware source is mutated in place.

### Notes

- This cumulative integration advances repository and package metadata directly from `0.9.2` to `0.9.7`; no retroactive `0.9.3` through `0.9.6` release tags are created.
- NT51919 still requires owner approval of its fact-scoped alias, NT51932 and NT51951 still require direct or approved alias/product evidence, and the selected CtrlRAM branches still require real expected outputs and firmware-owner review before runtime promotion.
- Stable package publication, signing, and support claims remain separate release gates.

## [0.9.2] - 2026-07-13

### Added

- Content-addressed source-schema inventory and deterministic materialization for every built-in V2 profile bundle.
- A closed allowlist for built-in bundle materialization, plus the 0.9.x completion roadmap and retirement gates for later workflow convergence.

### Changed

- Standard Merge runtime loading uses only the materialized V2 bundle registrations; duplicate per-bundle schema snapshots and the legacy runtime fallback were removed.
- The trusted bundle loader boundary, normal Merge/Replace byte behavior, promotion state, and AB Code behavior remain unchanged.

### Notes

- This is a local-verification milestone tag for profile-bundle consolidation, not a GitHub package release or product-support promotion.

## [0.9.1] - 2026-07-13

### Added

- Trusted source profile-bundle loading, canonical firmware-family/map resolution, and V2 profile compilation into the single `CompiledComposition` runtime boundary.
- V2 runtime routing for the existing golden-backed Standard Merge paths and NT51950/NT51951 DP Replace capacity variants, while retaining legacy comparison evidence.

### Changed

- Composition execution now owns mutable work-buffer initialization and accepts one compiled artifact rather than independently supplied profile and plan data.
- Standard Merge and DP Replace display projections derive executable facts from compiled V2 plans; NT51950/NT51951 DP Replace preserves customer information with the DP container.
- Runtime FWConfig metadata is read exclusively from the unambiguous NVT Backup at terminal `T - 0xFFF`; primary flash-map addresses remain inspection and evidence facts only.

### Notes

- This migration preserves product-support gates and does not claim new IC support, CtrlRAM parity, AB Code behavior, or packaged-install trust closure.

## [0.9.0] - 2026-07-11

### Added

- Standalone raw-BIN Hex Editor under Util Tools. It keeps one in-memory work copy, supports direct byte editing, overwrite/fill, insert/delete, ASCII search, original-row comparison, undo/redo, and Save As-only export without applying IC, profile, CRC, or postbuild rules.
- IC metadata facade and catalog-backed FlashCode version decoding for the DP main/sub bytes and TP FW/sub-version output tokens.
- NVT-copy FWConfig validation for all current Standard Merge golden outputs, plus catalog-backed CMI DP/Jira display metadata. The NT51950 CMI branch reads the validated TP ChipNumber and fails closed when it is unavailable.

### Changed

- Hex Editor rendering now uses one bounded custom viewport, immediate document extent, symmetric Hex/ASCII hover and selection, structural shift-block navigation, and blank-area context hit testing without creating one control per byte.
- ASCII search runs on a bounded memory snapshot, keeps complete result counts without retaining an unbounded highlight list, and supports cancellation without blocking the desktop UI.
- Unsaved state now reflects the actual in-memory bytes and source-address identity, including no-op edits, restored values, and insert/delete reversals.
- Canonical verification now terminates its repository SDK compiler servers after every .NET verification run, including a failed run, so idle MSBuild/Roslyn processes do not persist.
- External processor cancellation, timeout, desktop-window close, and CLI Ctrl+C now terminate the host-started process tree before returning control.

## [0.8.0] - 2026-07-09

### Added

- Home now exposes General Merge as a first-class workflow shortcut alongside Normal Merge and the reserved AB Code entry.
- Report review now groups inputs, changes, operation flow, postbuild evidence, issues, and raw JSON into clearer human-review sections.
- Output-difference review now groups accepted and review-required byte changes with readable section labels, range summaries, and byte previews.

### Changed

- Home workflow rows now provide stronger hover and pressed feedback so full-row navigation is visually discoverable.
- Report history rows reserve space for per-report delete actions so the right border and scrollbar no longer collide with the delete button.
- Primary report status avoids exposing SHA-256 details by default while keeping the full evidence in report details.

## [0.7.5] - 2026-07-08

### Added

- The Avalonia shell now uses English and Traditional Chinese text resources for the primary Home, Settings, Merge, Replace, and Report surfaces.
- Settings language selection now switches the visible UI immediately and is restored through the existing preference store.

### Changed

- Settings preference, catalog, tool, diagnostics, and report-history rows now describe the real current state instead of showing placeholder or pending wording.
- Report and workflow action labels are routed through the shared text-resource model so bilingual polish does not fork the XAML or ViewModel flow.

## [0.7.4] - 2026-07-08

### Added

- DP BIN slots now show gen_flash-derived DP version badges where evidence exists, with a warning badge when a selected IC still lacks a DP version rule.
- Report review now uses a wider evidence modal with Inputs, Changes, Operations, Postbuild, Issues, and Raw sections, including table-style operation range evidence.

### Changed

- Merge and Replace workbench pages now use a Build-first interaction model; Build validates current inputs automatically and disabled states surface the blocking reason in-page.
- Home workflow cards no longer show IC/Number selection hints that are only relevant inside Merge or Replace.
- The reference folder formerly named combiner info is now exposed as TDDI Flash Header for clearer owner-facing terminology.

## [0.7.3] - 2026-07-06

### Added

- Operation reports now carry structured provenance so each operation can be traced to a built-in profile, runtime General mapping, or saved rule.
- Saved composition rules can be validated from CLI with strict field checks that reject hidden command/script fields.
- General Merge CLI can consume reviewed saved-rule mapping rows through `--rule <rule.json>` and explicit `--slot <slot-id=path>` bindings, compiling them back through the shared General Merge planner.

### Changed

- Report review operation cards now show operation source alongside status, overlap, ranges, processor/tool details, and reasons.
- Saved-rule support remains data-first: General Replace saved-rule execution, UI Saved Rules, and promotion into normal workflows still require separate review.

## [0.7.2] - 2026-07-06

### Added

- General Merge v1 execution from CLI and UI, using explicit source-start, target-start, and length mappings over a caller-declared blank output image.
- General Merge workbench reports now include structured operation, input, output size/hash, and committed output evidence.
- General Merge memory coverage preview now shows reserved output bytes and explicit Source BIN writes before Preview/Build.

### Changed

- Merge mode selection now separates Standard Merge, General Merge, and reserved AB Code state in the desktop workbench.
- General Merge profile versioning follows the repository `VERSION`/assembly informational version instead of duplicating a literal version in code.

## [0.7.1] - 2026-07-06

### Added

- Replace reports now include final output-vs-reference difference rows, with accepted classifications for declared replacement ranges and IC/IC-number-specific postbuild CRC/header ranges.
- Report review UI now surfaces the accepted output-difference table alongside operations, external Combiner commands, and mutation evidence.
- Golden self-replacement tests now verify Replace report differences: DP self-replacement must produce no output-difference rows, while CtrlRAM postbuild self-replacement may produce only accepted CRC/header rows.

## [0.7.0] - 2026-07-06

### Added

- IC FlashMap workbook, postbuild BAT, mmap, and combiner-info reference documentation for CtrlRAM postbuild investigation.
- Release package reference payload plan for approved human-review evidence and manifest-declared Standard Merge golden fixtures.
- Real-tool CtrlRAM postbuild smoke coverage for accepted 16-byte self-replacement cases.
- General Replace execution path for explicit mappings, including TP-touch postbuild refresh and structured report evidence.
- Local report history persistence for Preview/Build reports, including reopenable report metadata and warning severity display.

### Changed

- Clarified current CtrlRAM postbuild gaps: empty `map.txt` staging is implemented for no-overlay smoke cases, while overlay map content, NT51926 copy-header initialization, NT51930 hidden DiffDLM mutation, and NT51931 Combiner 1.13.0 crash remain evidence gates.
- Updated owner handoff notes so already recorded NT51930 and NT51950/NT51951 Standard Merge golden fixtures are no longer listed as required uploads.
- Clarified that `refcode/` remains exactly two Python snapshots; FlashMap evidence lives under `docs/references/` and approved runtime binaries live under `external-tools/`.
- Split shared workbench run context from workflow-specific Merge/Replace planning, including General Replace, CtrlRAM Replace, and DP Replace adapters.
- Centralized product version metadata on the repository `VERSION` file for MSBuild assembly/package metadata and shell display.
- NT51950/NT51951 DP Replace now derives the displayed and executable DP range from the selected base image instead of a fixed maximum length.
- NT51926 and NT51930 CtrlRAM postbuild selection is category-aware from FWConfig Common FW version, with TP Overview notes aligned to the selected category.

## [0.5.0] - 2026-07-02

### Added

- Bootstrap workbench facade for desktop Settings, memory-map, IC catalog, and Standard Merge run contracts.
- First-sample `v1.0.0` readiness gates for support matrix and package release evidence.
- Executable Standard Merge profiles for the full v1 IC merge list, including NT51930 flash-map ranges and NT51950/NT51951 DP Perspective variable-length DP input padding plus TP overlay.
- Replace selection overview chip and modal so collapsed CtrlRAM groups still show how many replacement targets are selected and what Preview requires.

### Changed

- Presentation and Golden Regression test dependencies now route through Bootstrap instead of directly referencing Application/Profile/Infrastructure firmware catalogs.
- Supported IC matrix now separates executable profiles, flash-map catalog coverage, CtrlRAM postbuild command coverage, and production Replace profile gaps.
- Desktop app, CLI, and package metadata are aligned to stable version `0.5.0`.

## [0.5.0-dev.0] - 2026-07-01

### Added

- Catalog-backed Settings surface for profiles, tool bindings, diagnostics, preferences, and readiness.
- Shell breadcrumb/history navigation that can return to an earlier page level.

### Changed

- Updated app, assembly, CLI, and desktop shell version display to `0.5.0-dev.0`.
- Scoped Device Context to Merge and Replace workflow pages instead of showing it globally.

## [0.1.0-dev.0] - 2026-06-25

### Added

- Final `Dennis40816/nvt_fw_combiner` private repository identity and MIT baseline.
- Root canonical `SPEC.md`, .NET SDK 10.0.301 pin, Avalonia 12.0.4 solution scaffold, bootstrap/install scripts and active CI/release workflows.
- Immutable official `dotnet/install-scripts` source pin for repository-local .NET installation.
- Orthogonal composition kind, initializer, experience, audience, layout and region-access model.
- Display, TP HW, TP FW and General Replace experiences; General Merge with extensible BIN/mapping support.
- Development tag policy with initial node `v0.1.0-dev.0`.
- Tester Codex agent, golden-regression and UI-experience skills, plus nested layer instructions.
- Initial Domain range value objects and architecture tests without claiming firmware parity.

### Changed

- Replaced closed workflow-family execution semantics with experience/access authoring policy.
- Moved the canonical implementation spec to repository root and converted the old path to a compatibility pointer.

## [0.0.3] - 2026-06-25

### Added

- Separate versioned Composition Request and Composition Report contracts with JSON Schemas.
- Explicit MIT license-scope rules for proprietary/reference material.
- Input-slot/address-space instancing, profile support status, processor purpose, and stronger operation metadata in the profile schema.

### Changed

- Separated firmware integrity outcome (`none`, `verify-existing`, `recalculate-and-write`) from external worker authority (`calculate`, `transform`).
- Expanded report evidence for blank/reference initialization, custom mappings, processor claims, host-verified byte diffs, and atomic output commitment.
- Refined the `nvt_fw_combiner` recommendation and repository-creation gate.

## [0.0.2] - 2026-06-25

### Added

- Recommended private repository name `nvt_fw_combiner` and root MIT License.
- Canonical composition variable model and JSON Schema.
- One-engine ADR: Merge uses blank initialization; Replace uses a reference-image clone.
- Replace workflows for AE/CtrlRAM, DP plus whole TP, and Arbitrary Segment mappings.
- Custom Merge layout editor requirements with drag and exact table/manual input.
- Per-profile-stage integrity dispositions and an explicit IC integrity evidence matrix.
- Reserved Protocol 2 staged Python CRC/header transform with independent host diff validation.
- Repository `polytail` Agent Skill to block low-quality AI-generated code.

### Changed

- External Python authority now permits mutation only of a host-owned staging copy, never original/final firmware.
- Codex handoff, milestones, release package, repository gate, AGENTS, and acceptance criteria now follow the unified composition architecture.

## [0.0.1] - 2026-06-25

### Added

- Initial implementation specification and repository seed.
- Clean Architecture and pure external Python CRC calculation proposal.
- CRC worker Protocol 1.0 prototype and tests.
- Layered AGENTS, Codex configuration, initial repository skills, CI/release templates, and two Python reference snapshots.
