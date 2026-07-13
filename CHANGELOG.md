# Changelog

All notable changes to NVT FW Combiner are documented here. The project follows Semantic Versioning and the Keep a Changelog section model.

## [Unreleased]

Post-`0.9.0` development targets UAT feedback, firmware-owner evidence closure, and `1.0.0` release readiness.

## [0.9.1] - 2026-07-13

### Added

- Trusted profile-bundle loading, canonical firmware-family/map resolution, and V2 profile compilation into the single `CompiledComposition` runtime boundary.
- V2 runtime routing for the existing golden-backed Standard Merge paths and NT51950/NT51951 DP Replace capacity variants, while retaining legacy comparison evidence.

### Changed

- Composition execution now owns mutable work-buffer initialization and accepts one compiled artifact rather than independently supplied profile and plan data.
- Standard Merge and DP Replace display projections derive executable facts from compiled V2 plans; NT51950/NT51951 DP Replace preserves customer information with the DP container.
- Runtime FWConfig metadata is read exclusively from the unambiguous NVT Backup at terminal `T - 0xFFF`; primary flash-map addresses remain inspection and evidence facts only.

### Notes

- This migration preserves existing promotion gates and does not claim new IC support, CtrlRAM parity, or AB Code behavior.

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
