# Changelog

All notable changes to NVT FW Combiner are documented here. The project follows Semantic Versioning and the Keep a Changelog section model.

## [Unreleased]

Post-`0.9.8` development targets `0.9.9` legacy/contract/support convergence
and a separate `0.9.10` end-to-end performance remediation program with
measured before/after parity.

### Changed

- Reassigned legacy/V2 retirement, public contract convergence, IC
  family/evidence semantics, support matrix, and workflow exposure to `v0.9.9`.
  `v0.9.10` now owns the reconciled end-to-end performance baseline and
  remediation; candidate IC intake and the `v0.9.11+` route are unassigned
  until reviewed `v0.9.9` and `v0.9.10` merge.
- Recorded that the audited `0.9.10` lineage was not based on `0.9.9`, and that
  its canonical full gate remained red after one load-sensitive process-tree
  timeout test failed while the isolated test passed. On 2026-07-18 the local
  integration branch was rebased onto the current clean `0.9.9` committed tip
  for parallel development; a final rebase and full verification remain
  required after `0.9.9` closes. This is a planning/integration change, not a
  firmware or support change.
- The `v0.9.10` roadmap now records why Replace performance work is needed: automatic Build currently executes Preview and Build separately, CtrlRAM postbuild repeatedly reads the full staging image, UI inspection rereads selected firmware, and owned byte buffers cross redundant copy boundaries.
- The same roadmap clarifies that `DeclaredReplacement` means an accepted output difference inside a declared Replace write range, not a firmware version change. Any TP FW version-edit explanation must be projected as an Application-owned semantic while preserving or explicitly versioning the stable machine classification.
- The roadmap entries authorize only the measured optimization scope. They do
  not authorize changes to output bytes, ranges, processor commands, profile
  promotion, or firmware support state.
- Added deterministic test instrumentation and synthetic automatic-Build
  baseline coverage for DP lengths `0x40000`, `0x80000`, and `0x100000`, plus
  two-command and 13-command CtrlRAM plans. The baseline records two current
  runs, two CtrlRAM processor sessions, and 4/26 process invocations without a
  new production metrics surface or changes to output bytes, report contracts,
  processor authority, or support state.
- Added the provisional `0.9.10` Application-owned automatic-Build process
  prototype. It commits the accepted output from one authoritative execution
  while keeping explicit Preview-token Build unchanged. Against node A, DP
  drops from two runs/four reads to one/two; CtrlRAM drops from two processor
  sessions and `2C` launches to one session and `C` launches. Complete output,
  SHA-256, mutation summaries, command argv, failed-validation atomicity,
  public DP evidence, and the NT51926 cascade TP-base full-output golden remain
  equal. Final B/C timing and allocation evidence still waits for final
  `0.9.9`.
- Added ADR 0024 and the `0.9.10` goal for truthful UI step progress. The
  current generic Preview/Build bar will become an Application-driven,
  localized and accessible lifecycle indicator for input reading, composition,
  approved postbuild, output validation, commit, and report preparation. It
  remains indeterminate within a phase, honors reduced motion, and does not
  infer firmware semantics or display a fabricated byte percentage.
- Expanded `0.9.10` into the end-to-end performance remediation milestone and
  added ADR 0025. Its explicit scope now includes exact sequential legacy
  Combiner calls and staging readback reduction, Build click/UI-dispatcher
  responsiveness, large change-report background/lazy/virtualized projection,
  UI inspection snapshots, progress animation, allocations, and B/C timing.
  Complete report export, processor trace, golden bytes, host diff/range checks,
  atomicity, and support truth remain unchanged gates.
- Added the provisional ADR 0025 Legacy Combiner readback slice at `67bc5a4e`.
  One automatic Build still launches all `C` commands sequentially, while the
  adapter now carries the last accepted full firmware bytes and performs one
  authoritative staging-firmware readback per command instead of `2C + 1` per
  session. Approved short output is normalized from the preceding accepted
  state. NT51926 full-output golden parity passes; final `0.9.9` replay,
  canonical full verification, and firmware-owner review remain open gates.
- Defined three comparison nodes: pre-rebase historical A, final-`0.9.9`
  unoptimized B, and the same reconciled source as optimized `0.9.10` node C.
  Only B/C may support a performance decision. Deterministic counters and
  golden byte/report parity are gates; p50/p95, allocation, working-set, and
  UI-thread timing remain recorded local evidence rather than CI thresholds.

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
