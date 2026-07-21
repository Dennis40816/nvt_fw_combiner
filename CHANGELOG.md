# Changelog

All notable changes to NVT FW Combiner are documented here. The project follows Semantic Versioning and the Keep a Changelog section model.

## [Unreleased]

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

- Focused production tests cover actual Workbench output for the added routes, including DiffDLM presence/absence, 256/512 KiB container preservation, NT51928 DP/LDC tail preservation, report identity, and immutable sources. Clean-commit canonical verification and independent R3 code review pass. A real NT51926 Windows CLI Build matches its manifest expected output exactly, and the Number-mismatch run publishes no output; see [`v0.9.12-ctrlram-build-evidence.md`](docs/references/v0.9.12-ctrlram-build-evidence.md).
- The existing unsupported owner-handoff `.7z` inventory entry is not modified or silently accepted. It still blocks verification in the primary worktree, while the same reviewed commit passes `verify.py --all` in a clean detached worktree. Protected CI, clean-machine package smoke, per-plan firmware-owner promotion decisions, and final release packaging remain open gates.
- The shared Hex viewport, redesigned read-only Changes workspace, global Button pressed acknowledgement, and AB Code architecture re-admission are deferred to `v0.9.13`. All existing AB candidates remain hidden, support-neutral, and rejected by the Application run boundary in this release.

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
