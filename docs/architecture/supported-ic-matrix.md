# Supported IC / Workflow Matrix Draft

This is a planning inventory, not a support claim. A row becomes supported only after profile validation, integrity disposition, golden regression, and owner sign-off. `Unknown` never means `None`.

v0.9.12 admission correction: [ADR 0030](../adr/0030-production-firmware-admission-without-golden-hashes.md)
supersedes the older `reference-SHA`, `full-reference-SHA`, and "SHA as build discriminator" wording
retained in evidence-history cells below. Golden input/output hashes continue to lock regression
fixtures and parity claims, but production firmware inputs are admitted by typed profile facts and
must not be accepted or rejected by comparison with a golden whole-file hash.
[ADR 0031](../adr/0031-ctrlram-profile-intervals-and-build-plan-authority.md) likewise supersedes
exact PID/version/count route wording retained in evidence-history cells: requested IC selects the
owner-declared family, effective Common FW intervals select among multiple runtime profiles, and
Number selects an owner-provided typed plan. All 31 currently modeled runtime interval/plan pairs,
including NT51928 non-NB single/2-chip/3-chip and NT51950/NT51951 single/cascade, have trusted V2 routes;
fixture metadata remains report evidence and never narrows that production population.

Current owner priority as of 2026-07-23:

- focus on normal Merge and normal Replace for DP Replace and CtrlRAM Replace workflows;
- open the `v0.9.15` AB runtime/UI/CLI function for NT51919/NT51929/NT51932 under the fixed no-processor plan, NT51950 through explicit `1 IC`/`Cascade` selection, and selector-free NT51951; missing direct golden evidence remains visible support-certification debt rather than a function gate;
- include NT51950 and NT51951 normal Merge with the confirmed DP Perspective TP overlay range `0x0A000-0x36FFF (len 0x2D000)`; owner golden fixtures are recorded and firmware-owner sign-off remains required;
- require Replace UI to collect IC num before profile-specific regions are shown. ICs with only single/cascade choices use text labels; ICs with three or more concrete choices such as NT51917/NT51927/NT51928 use numeric count selection, optionally with an Other/custom path later;
- expect CtrlRAM Replace CRC/header recalculation through approved legacy `combiner.exe` postbuild sequences.

Owner release decision as of 2026-07-21: `v0.9.11` is support-neutral and does
not promote any row below. Existing authoring availability and evidence stages
remain unchanged. Direct golden or firmware-owner gaps continue to gate future
promotion of the selected IC/workflow, but they are not blanket blockers for
the support-neutral `v0.9.11` release.

The per-IC Merge/Replace flowchart reference is [`ic-workflow-flowcharts.md`](ic-workflow-flowcharts.md). When adding a new IC workflow, follow [`adding-ic-merge-replace-workflow.md`](adding-ic-merge-replace-workflow.md). Update both documents together when IC workflow status changes.

The active 0.8.0 cleanup goal and milestone acceptance criteria are tracked in
[`0.8.0-goal-and-acceptance.md`](0.8.0-goal-and-acceptance.md). New IC support should start from `IcSupportCatalog`, then update only the relevant flash-map, TP header, postbuild, family-policy, profile, test, and documentation rows.

## Availability, evidence, and IC-family vocabulary

Workflow availability and golden verification are separate axes:

- **Golden verified** means direct or owner-approved fact-scoped parity is recorded. It is not a product-support promise.
- **Evidence open** means the executable/safety contract exists and authoring remains available, while direct golden or firmware-owner review is still open. A missing golden alone does not ban the workflow.
- **Not available** is reserved for a workflow whose executable profile, ranges, processor authority, or typed plan has not been declared. Its UI badge must expose the reason and opening condition on hover.

Owner-declared family facts are typed in `IcSupportCatalog`; they never expand executable ranges by themselves:

| Family | Canonical IC | Member | Relationship and reusable scope |
| --- | --- | --- | --- |
| `nt51927-family` | NT51927 | NT51917 | Perfect alias across the owner-declared family facts. |
| `nt51927-family` | NT51927 | NT51928 | Partial alias for Replace; LDC differs and NT51928 NB is excluded. |
| `nt51929-nt51932-family` | NT51929 | NT51919 | Perfect owner-declared family alias. |
| `nt51929-nt51932-family` | NT51929 | NT51932 | Perfect family alias for the declared facts; NT51932 still owns its distinct cascade product path. |

## Standard Merge and DP Replace relationship

Standard Merge and DP Replace use the same canonical IC memory-map facts. Therefore, an IC cannot expose DP Replace unless it also exposes Standard Merge. The repository retains 13 isolated trusted Standard Merge/DP Replace bundles and runtime registrations. For the focused v0.9.17 selector policy, 10 ICs are user-selectable; NT51920, NT51930, and NT51931 remain internally modeled for compatibility but are omitted from every user-facing IC selector. NT51925 has no current catalog row. This is an executable authoring statement, not automatic product-support promotion; firmware-owner release review remains separate.

Current DP Perspective input contract for NT51950/NT51951:

- the UI calls `reference-base` **Reference FlashCode**, not `base.bin`;
- it must be one complete final Standard/Normal Merge `.bin` for the same selected IC and exactly one declared capacity: `0x40000`, `0x80000`, or `0x100000` bytes;
- the DP replacement is a DP/FlashCode-shaped `.bin` no larger than that reference capacity; a shorter input is padded with `0x00`, while an oversized input is rejected;
- the output clones Reference FlashCode, replaces the declared full DP container, then restores the reference TP overlay. The existing owner rule keeps customer information from the replacement DP image.

A same-capacity complete Standard/Normal FlashCode is therefore a valid *shape* for the current DP replacement slot, but parity/support claims stay limited to recorded evidence. Future AB FlashCode input must use an AB-specific profile-declared artifact shape/extractor and explicit A/B bank, header-copy, preserved-range, and Legacy Combiner rules. Normal FlashCode offsets must not be guessed or reused for AB input.

NT51930 uses a different profile over its existing canonical 256 KiB Standard Merge map:

- Reference FlashCode is one complete same-IC Standard/Normal Merge output of exactly `0x40000` bytes;
- the replacement is a same-IC DP/FlashCode-shaped BIN containing the complete canonical DP range `[0x0000, 0x6000)`; `0x40000` is the expected outer-container length, any other length that still covers this range produces the declared warning, and shorter input is rejected;
- the output clones Reference FlashCode and replaces only `[0x0000, 0x6000)`. The gap `[0x6000, 0x7000)` and TP `[0x7000, 0x40000)` remain byte-for-byte from Reference FlashCode;
- the executable V2 safety contract and deterministic full-output oracle are present, so authoring is available as **Evidence open**. Direct owner DP Replace golden parity and firmware-owner release review remain open.

The v0.9.11 Gen Flash DP Replace profiles clone an exact Reference FlashCode and have only these canonical write ranges:

- NT51917/NT51927: DP `[0x3C000, 0x40000)` in a `0x40000` base;
- NT51919/NT51929/NT51932: DP `[0x00000, 0x06000)` in a `0x40000` base;
- NT51920/NT51923/NT51926/NT51931: DP `[0x3E000, 0x40000)` in a `0x40000` base; and
- NT51928 non-NB: DP `[0x3C000, 0x40000)` plus a separately supplied LDC `[0x40000, 0x62000)` in a `0x80000` base.

All bytes outside those ranges remain from Reference FlashCode. The DP/LDC profiles are isolated from the existing Standard/General Merge bundle identities, and the all-IC self-replacement regression requires complete output byte/SHA equality to the applicable owner-provided Standard Merge control. NT51917 and NT51919 use only their recorded fact-scoped aliases. These controls admit the authoring routes but do not replace independent firmware-owner release review.

## Current executable inventory

| Area | IC coverage | Source of truth | What this currently means | Not enough for 1.0 until |
| --- | --- | --- | --- | --- |
| IC support exposure | Selectable: NT51917, NT51919, NT51923, NT51926, NT51927, NT51928 non-NB, NT51929, NT51932, NT51950, NT51951. Compatibility-only internal rows: NT51920, NT51930, NT51931. | `WorkbenchCompositionService.GetSupportedIcIds` over `IcSupportCatalog` | The v0.9.17 selector projection exposes 10 ICs while retaining three non-selectable catalog rows so this hot-fix does not perform the later 0.10.x removal/refactor. NT51925 is absent rather than retained. | Every exposed workflow row is backed by the matching detailed catalog, executable profile or workbench path, tests, and owner evidence. |
| Standard Merge profiles | NT51917, NT51919, NT51920, NT51923, NT51926, NT51927, NT51928, NT51929, NT51930, NT51931, NT51932, NT51950, NT51951 | trusted V2 bundles plus `TrustedV2CompositionCompiler` | Executable canonical V2 routes exist for the golden-backed gen_flash set, owner-confirmed aliases NT51917 -> NT51927 and NT51919 -> NT51929, NT51930 flash-map golden, and NT51950/NT51951 DP Perspective. NT51950/NT51951 select the V2 map matching only DP input sizes `0x40000`/`0x80000`/`0x100000`, output the selected DP length, overlay TP `0x0A000-0x36FFF (len 0x2D000)`, and preserve customer info. | Firmware-owner sign-off before support claim/exposure; direct owner samples for optional aliases or remaining 950/951 capacities if selected for release. |
| AB Merge function | NT51919, NT51929, NT51932, NT51950, NT51951 | `nt51919-nt51929-nt51932-ab-merge` and `nt51950-ab-merge` trusted V2 bundles, `IcSupportCatalog`, shared Application runner | `0.9.15` opens the fixed NT51919/29/32 route plus NT51950 `1 IC`/`Cascade` and selector-free NT51951 in UI and CLI. Every AB profile declares its A/B CMI read regions; production naming and UI projection read only the compiled map and have no GenFlash catalog fallback. The 950/951 route copies the complete exact-length DP AB image, copies TPA unchanged, relocates TPB DIFF only, materializes a private A/B Combiner image, then backfills only postbuilt B into the full DP output. TP metadata validates topology but never selects it; TP version/PID/Common FW are report/naming facts, not selectors. Short TP prefixes block; longer TP inputs warn and only the declared prefix has execution authority. | Missing direct golden alone does not close the function. Support certification still requires firmware-owner review of each declared map/postbuild route, direct-golden closure, release EXE smoke, final clean verification, release-owner approval, and the debt ledger below. |
| TP flash-map catalog | NT51917, NT51919, NT51920, NT51923, NT51926, NT51927, NT51928 non-NB, NT51929, NT51930, NT51931, NT51932, NT51950, NT51951 | hash-pinned `profiles/built-in/ctrlram-postbuild-v2/flash-map.json` plus `BuiltInTpFlashMapCatalog` | UI can display DP, CtrlRAM, customer-info, IC-count visibility, and declared TP/full-Flash base shapes from current evidence. Static C# range facts are retired. | Every released workflow maps these rows through profiles, compiler checks, report evidence, and direct TP/full-Flash parity. |
| CtrlRAM postbuild command catalog | NT51917, NT51919, NT51920, NT51923, NT51926, NT51927, NT51928 non-NB, NT51929, NT51930, NT51931, NT51932, NT51950, NT51951 | hash-pinned `profiles/built-in/ctrlram-postbuild-v2/catalog.json`, trusted V2 route bundles, Combiner `1.13.0` | All 31 cataloged runtime interval/build-plan pairs have explicit V2 routes. Production admission is IC + effective Common FW interval (only when multiple profiles exist) + typed Number plan; PID, filename, exact fixture version/count, and whole-file SHA are report/regression facts only. NT51928 reuses NT51927 TP single/2/3 plans while preserving its 512 KiB DP/LDC tail. NT51950/NT51951 single/cascade share TP offsets and processor authority while retaining their distinct full-image capacities. | Direct fixture hashes and exact outputs remain regression evidence; firmware-owner promotion and any route-specific independent output gaps remain support gates, not runtime admission gates. |
| Production Replace profiles | 13 retained trusted V2 DP Replace profiles; 10 ICs are selectable in v0.9.17. CtrlRAM workbench profiles are generated from FlashMap/postbuild evidence; General Replace UI/CLI workbench supports DP-kind mappings and TP/CtrlRAM mappings with postbuild refresh. | `WorkbenchCompositionService`, isolated hash-pinned DP Replace bundles, hash-pinned FlashMap/Postbuild config | Gen Flash profiles clone the exact declared base and replace only the DP ranges listed above; NT51928 additionally requires an independent LDC FlashCode input and operation. NT51930 retains `[0x0000, 0x6000)` internally but is not user-selectable in v0.9.17. NT51950/NT51951 retain capacity-selected container replacement plus TP restore/customer-from-DP behavior. UI/CLI slots come from the compiled profiles, and full-output self-replacement parity covers the applicable owner-provided Standard Merge controls without changing existing Merge bundle identities. CtrlRAM and General Replace behavior is unchanged. | Independent firmware-owner release review/support selection; direct DP Replace output evidence where the owner requires it beyond the admitted same-build controls; real CtrlRAM/General Replace profiles for released modes; TP-touching General Replace golden outputs; and finalized owner-approved safety envelopes. |
| General Merge workbench | Catalog IC ids for selection only; no IC-specific support claim | `WorkbenchCompositionService.RunGeneralMergeAsync`, `general-merge` CLI, Merge page General mode, `saved-rule` CLI | General Merge v1 initializes a caller-declared blank output length with reserved `0x00` bytes, then compiles explicit source-start/target-start/length mappings as `CopyRange` operations through the shared composition planner. It supports Preview/Build from CLI and UI, rejects target overlap and out-of-bounds ranges, writes structured reports, and never invokes postbuild. CLI can validate saved-rule JSON, print normalized mapping fragments, and consume General Merge saved-rule rows via `--rule` plus explicit `--slot` bindings; reports mark those operations as `saved-rule`. | Saved-rule promotion into normal workflows, UI Saved Rules, owner-approved reusable General Merge policies, General Replace saved-rule execution, and any future postbuild-dependent variant must be separately reviewed. |

## AB function availability, direct-golden debt, and progress

`Function open` means that the existing reviewed runtime profile is selectable in
the UI and CLI and can reach the shared Application executor. It is not a
`Supported` product claim. A missing direct golden is an evidence debt, not a
route selector or a reason to stop `0.9.15` implementation. It still prevents
support certification and it never substitutes for an independent R3
map/processor/topology authority review.

| AB IC/topology evidence cell | `0.9.15` function state | Direct AB golden | Remaining non-golden authority |
| --- | --- | --- | --- |
| NT51919 fixed perfect-family route | Open | Missing; uses approved NT51929 fact scope | Support certification review only. |
| NT51929 fixed perfect-family route | Open | Present | Support certification review only. |
| NT51932 fixed perfect-family route | Open | Missing; uses approved NT51929 fact scope | Support certification review only. |
| NT51950 `1 IC` route | Open | Supplied: formal intake/certification closure pending | Firmware-owner approval of the exact staged Combiner sequence. |
| NT51950 `Cascade` route | Open | Missing | Direct vector plus firmware-owner approval of the exact map/postbuild route. |
| NT51951 selector-free route | Open | Missing | Direct vector plus firmware-owner approval of its exact map/postbuild route. |

| Measured progress | Calculation | Current value |
| --- | --- | --- |
| AB function availability by target IC | 5 open ICs (NT51919/NT51929/NT51932/NT51950/NT51951) / 5 planned AB ICs | **100.0%** |
| Direct AB-golden coverage | 2 present cells (NT51929 fixed, NT51950 `1 IC`) / 6 planned IC/topology cells | **33.3%**; **4 missing** |
| Direct AB-golden debt | 4 missing cells / 6 planned IC/topology cells | **66.7%** |
| `0.9.15` delivery checklist | 5 completed local slices (function opening, output naming, TPA/TPB identity inspection, silent perfect-family reconciliation, and delivery-to-review evidence) / 5 local slices | **100.0%**; external golden, firmware-owner, independent-review, packaging, and release-owner gates remain open. |
| AB support certification | 0 fully certified cells / 6 planned IC/topology cells | **0.0%**; function availability must not be presented as certification. |

The four missing direct AB golden cells are NT51919 fixed, NT51932 fixed,
NT51950 `Cascade`, and selector-free NT51951. These counts are AB Merge only;
Standard Merge, DP Replace, and CtrlRAM evidence are not substituted into this
ledger.

## First-sample gap priority

| Priority | Gap | Current state | Needed before `v1.0.0` |
| --- | --- | --- | --- |
| P0 | Support matrix lock | Candidate IC/workflow rows exist, but candidates are not support claims. | Owner chooses the exact IC/workflow subset to ship; non-selected rows stay hidden or clearly candidate-only. |
| P0 | Standard Merge golden closure | Golden-backed canonical V2 profiles are executable; NT51930 and the recorded NT51950/NT51951 owner golden outputs from `merge_bin.7z` are covered; NT51917/NT51919 are executable owner-confirmed aliases and are tested against NT51927/NT51929 golden bytes. NT51950/NT51951 V2 also retain legacy byte parity for all declared capacities. | Firmware-owner sign-off for every released Standard Merge profile; direct owner samples for an additional 950/951 capacity if it is selected for release, and optional direct NT51917/NT51919 alias samples if desired. |
| P0 | DP Replace production closure | All 13 retained IC rows have executable trusted V2 profiles, canonical range tests, and full-output same-build controls; the v0.9.17 user selector exposes 10 of them. NT51928 alone has a required separate LDC input. NT51950/NT51951 retain their deterministic capacity oracle and archived comparison; other routes remain Evidence open in the catalog. | Firmware-owner release/support selection and any independently required direct DP Replace output packages. Non-selectable NT51920/NT51930/NT51931, No-Jira NT51930 content, and NT51928 NB remain outside this admission. |
| P0 | CtrlRAM Replace production closure | UI slots, memory layout, staged legacy Combiner execution, postbuild command trace, CLI multi-slot input, and private fixture handoff exist. | Private golden outputs, firmware-owner parity review, and promotion from workbench execution to released support claim. |
| P0 | General Replace production closure | Workbench/UI and CLI Preview/Build can run explicit mappings inside cataloged DP-kind ranges and TP/CtrlRAM ranges that have selected postbuild profiles. Protected/header/customer rows remain blocked. | Owner-approved General Replace safety envelopes, overlap/alignment policy, and golden outputs for released IC/modes. |
| P1 | General Merge production closure | General Merge v1 can run ad hoc explicit source-to-target mappings over a blank output from CLI and UI without postbuild. Saved-rule JSON validation and General Merge CLI consumption exist, with operation provenance in reports. | UI Saved Rules, normal-workflow promotion, final policy UX, and owner-approved reusable mapping evidence before support-style claims. |
| P1 | Unified workflow data model | Merge uses executable profiles; Replace still mixes synthetic profiles, workbench-specific planning, and flash-map facade data. | `0.6.0` should evaluate a unified profile/template/catalog model so UI, CLI, and tests call the same runner contracts. |
| P1 | Report/history completeness | Preview/Build report modal exists; first-peak errors, save flow, output artifact path display, local persistent report history, single-entry deletion, persisted audit metadata, structured operation evidence cards for status, overlap, normalized ranges, processor/tool id, read/write ranges, and refresh argv exist. Report issues now carry schema-level severity, known truncation diagnostics render as warnings rather than blocking issues, and Build automatically runs validation/report generation without a separate manual Preview gate. | Finish support-ready audit review and visual polish. |
| P1 | Settings persistence/readiness | Settings surface is catalog-backed; theme, strictness, language, and report history persist locally. `Warn only` is recorded as a UI preference but firmware gates still fail closed. | Define any future execution-affecting setting through Application contracts before it can change Preview/Build behavior; continue polishing support-status readiness. |
| P2 | Deferred workflows | AB is function-open for NT51919/NT51929/NT51932/NT51950/NT51951 but all certification and release gates remain open. UI Saved Rules, General profile promotion, General Replace saved-rule execution, and future REG Replace remain deferred. | Close each AB cell's firmware-owner/golden/release gate independently; do not treat function availability as promotion. |

## Owner validation package needed

- NT51917/NT51919 Standard Merge: owner confirmation is recorded as NT51917 -> NT51927 and NT51919 -> NT51929; direct IC-specific samples are optional audit evidence rather than an exposure blocker.
- NT51930 Standard Merge: DP input, TP input, expected output, expected file name, input/output SHA-256, and owner approval are recorded by its direct case under `testdata/golden/canonical/`.
- NT51930 DP Replace: provide the owner-promised exact INX Jira project/variant/topology package with one same-build `0x40000` Reference FlashCode, replacement DP/FlashCode BIN, expected final FlashCode, command/tool identity, and hashes. The checked-in public synthetic oracle and Standard Merge self-replacement are migration controls only and do not close direct golden parity or imply support for no-Jira projects.
- NT51950/NT51951 Standard Merge: canonical V2 map/plan/runtime golden tests cover the owner NT51950 `0x40000` and NT51951 `0x80000` DP cases, with TP input covering `0x0A000-0x36FFF` and customer info preserved from DP. Legacy/V2 parity covers all six declared IC/capacity combinations. Direct owner golden evidence for the other combinations remains required if any is selected for release exposure.
- NT51950/NT51951 DP Replace: public deterministic oracle covers six IC/capacity cases plus customer-padding boundaries, with TP restored from base and customer info from DP. The owner accepts archived pre-V2 comparison plus the checked-in static expected hashes for V2 runtime admission; `knownDeviations` is empty, so full-byte parity is required before routing. This is migration evidence, not an independent hardware golden or a product support claim.
- CtrlRAM Replace per released IC/mode: base firmware, per-region CtrlRAM replacement BINs, expected final output after Combiner postbuild, Combiner version/tool binding, command order, declared read/write ranges, allowed diff ranges, and FWConfig Common FW version when an IC has multiple postbuild categories.
- IC aliases/counts: NT51928/NT51929/NT51951 alias behavior and NT51928 NB exclusion are owner-confirmed. NT51930 validation is limited to the topology in the exact INX Jira golden; no-Jira count branches are neither missing golden cases nor support claims.
- General Replace: protected ranges beyond the current header/customer/project/backup gate, allowed explicit-mapping envelope, overlap/alignment rules, TP-touching postbuild expected outputs, and final release-scope IC/mode approvals.

| IC | Standard Merge | AB Merge | Replace planning | Integrity evidence | Current evidence | 1.0 status |
| --- | --- | --- | --- | --- | --- | --- |
| NT51917 | canonical V2 Standard Merge route; map-bound alias of NT51927 | no evidence | DP Replace plus one `[1.0.0,infinity)` CtrlRAM profile with single/2-chip/3-chip plans | The three TP plans retain NT51917 processor/staged identity and have 7/10/13-command regression controls. Exact versions, PIDs, and hashes identify fixtures only. | owner perfect-family confirmation + direct NT51927 fixtures + separate hash-pinned alias profiles/bundle + V1/V2 full-byte, argv/read-write/report/input-immutability parity | Support-neutral aliases; no public support promotion. |
| NT51919 | canonical V2 Standard Merge route; map-bound alias of NT51929 | support-neutral fixed `0x80000` runtime/CLI pilot through the owner-approved NT51929 fact scope | DP Replace plus one `[1.0.0,infinity)` CtrlRAM profile with single and bounded `2–8` cascade plans | AB uses the exact six-operation no-processor plan; CtrlRAM single regression uses the NT51929 family with NT51919 processor identity, while cascade adds DiffDLM `[0x2D100,0x35D00)` and DLM CRC `[0x7128,0x7144)`. | owner perfect-family confirmation + direct NT51929 AB golden/fact scope + manifest-pinned NT51919 profiles; independent final firmware/release review remains | Support-neutral routes; no public product-support promotion. |
| NT51920 | reference candidate; canonical V2 Standard Merge route | no evidence | DP Replace plus one `[1.0.0,infinity)` CtrlRAM profile with single and generic cascade plans | Single and cascade-2 owner cases each run registered Combiner 1.13.0 with two ordered `CRC_Enable` commands. Exact Common FW, PID, hash, and count identify regression fixtures only. | owner inputs/expected outputs + trusted V2 profiles + full-byte V1/V2 parity + argv/read-write/report/input-immutability evidence; independent support review remains separate | Support-neutral routes; no public support promotion. |
| NT51923 | canonical V2 Standard Merge route | no evidence | DP Replace plus one `[1.0.0,infinity)` CtrlRAM profile with single and generic cascade plans | Single and cascade-3 owner cases each reconstruct the reference and run registered Combiner 1.13.0 with two ordered `CRC_Enable` commands. Exact Common FW, PID, hash, and count identify regression fixtures only. | owner DP/TP/physical inputs and expected outputs + trusted V2 profiles + V1/V2 parity + argv/read-write/report/input-immutability evidence | Support-neutral routes; no public support promotion. |
| NT51926 | canonical V2 Standard Merge route | no evidence | DP Replace; CtrlRAM `[1.0.0,2.0.0)` and `[2.0.0,infinity)` profiles each expose single and generic cascade; DP-only General Replace candidate | The 1.x-sourced plan uses header target `0x32F50`/VN `0x1660`; the 2.x-sourced plan uses `0x32A70`/VN `0x149E`. Common FW selects only this real profile boundary; PID, hash, and fixture count do not. TP/full-Flash bases preserve the Flash tail, and version edit validates the final canonical Backup. | trusted profiles plus 1.4.1/2.0.0 direct regression evidence; the 2.0.0 no-edit V1/V2 results match while owner expected differs only at four approved CRC words | Support-neutral interval/plan routes; no public support promotion. |
| NT51927 | canonical V2 Standard Merge route | no evidence | DP Replace plus one `[1.0.0,infinity)` CtrlRAM profile with single, exact 2-chip, and exact 3-chip plans | Single/2-chip/3-chip regression cases lock the owner-provided 7/10/13-command shapes. Their exact Common FW, PID, hash, and filenames are evidence only. | owner single expected plus owner multi-chip bases/repository-derived replay inputs; profile/map, argv/read-write/report/input-immutability evidence is locked | Support-neutral single/2/3 routes; no public support promotion. |
| NT51928 | canonical V2 Standard Merge route with separate LDC | no evidence | DP Replace requires DP `[0x3C000,0x40000)` and LDC `[0x40000,0x62000)` inputs; CtrlRAM exposes non-NB single/2-chip/3-chip plans | CtrlRAM reuses the matching NT51927 TP layout and postbuild branch inside distinct 512 KiB maps; `[0x34800,0x80000)` stays preserved. The DP/LDC-only difference does not narrow TP authority. NT51928 NB remains excluded. | direct `gen_flash_bin_v2` NT51928 base + owner partial-family confirmation + manifest-pinned 512 KiB family/profiles + two-chip full-byte/process/report/input parity | Support-neutral non-NB routes; single/three-chip independent output review remains a promotion gate. |
| NT51929 | canonical V2 Standard Merge route | support-neutral fixed `0x80000` runtime/CLI pilot with direct owner golden parity | DP Replace plus one `[1.0.0,infinity)` CtrlRAM profile with single and bounded `2–8` cascade plans | AB has no processor and relocates only the three declared TPB scalars; CtrlRAM single locks registered Combiner 1.13 two-command parity, while cascade adds DiffDLM `[0x2D100,0x35D00)` and DLM CRC `[0x7128,0x7144)`. | direct owner AB golden + trusted V2 AB/single/cascade profiles; classified legacy header/CRC differences and final release review remain explicit gates | Support-neutral routes; no public product-support promotion. |
| NT51930 | canonical V2 Standard Merge route | no evidence | DP Replace plus one `[1.0.0,infinity)` CtrlRAM profile with exactly single and `2–13` plans; `>=14` is unavailable | DP Replace reuses the canonical `0x40000` map. CtrlRAM uses `NT51930BASED_NORMAL_MODE CRC8`; single omits DiffDLM and `2–13` consumes it. AUTO_PRJ-302/PID `0x110D`/Common FW 1.3.0/cascade 3 is regression evidence, not admission authority; the inspected 2.0.0 BAT remains evidence-only. | trusted V2 profiles + INX manifest/V1-V2 parity + registered Combiner 1.13 identity; independent single expected-output evidence remains open | Support-neutral single/2–13 routes; independent R3 review and DP direct-golden closure remain separate. |
| NT51931 | canonical V2 Standard Merge route | no evidence | DP Replace plus one `[1.0.0,infinity)` CtrlRAM profile with single and generic cascade plans | DP writes only `[0x3E000,0x40000)`. CtrlRAM cascade-6 is regression evidence for the generic cascade plan; single excludes cascade-only DLM. | hash-pinned DP and CtrlRAM profiles plus cascade full-byte/process parity; independent single output review remains a promotion gate | Support-neutral DP/CtrlRAM candidates; General Replace remains unavailable. |
| NT51932 | canonical V2 Standard Merge route | support-neutral fixed `0x80000` runtime/CLI pilot through the owner-approved NT51929 fact scope | DP Replace plus one `[1.0.0,infinity)` CtrlRAM profile with single and bounded `2–8` cascade plans | AB uses the same exact six-operation no-processor plan; CtrlRAM single uses NF/Normal/VN, while cascade adds DiffDLM `[0x2D100,0x35D00)` and DLM CRC `[0x7128,0x7144)`. | direct cascade regression + owner-approved NT51929 AB family facts; independent final firmware/release review remains | Support-neutral routes; no public product-support promotion. |
| NT51950 | canonical V2 DP Perspective Standard Merge route, exact-capacity selected | function-open AB V2: explicit `1 IC` (`0x80000`) or `Cascade` (`0x100000`) profile map; the operator choice is validated against both TP FWConfig chip counts | DP and CtrlRAM priority: DP Replace has LDC already packaged in DP; `[1.0.0,infinity)` CtrlRAM single and generic cascade plans | AB preserves the complete DP AB input, leaves TPA CRC unchanged, relocates TPB DIFF, and permits the staged Combiner to change only TPB ILM/DLM/CRC bytes. | supplied `1 IC` golden; `Cascade` golden missing; C#/Combiner CRC equivalence and source immutability are regression controls | Function-open, certification pending: golden intake/closure and firmware-owner review remain required. |
| NT51951 | canonical V2 DP Perspective Standard Merge route, exact-capacity selected | function-open selector-free AB V2 map (`0x100000`); no IC-number field is presented | DP and CtrlRAM priority: DP Replace has LDC already packaged in DP; `[1.0.0,infinity)` CtrlRAM single and generic cascade plans | AB preserves the complete DP AB input, leaves TPA CRC unchanged, relocates TPB DIFF, and permits the staged Combiner to change only TPB ILM/DLM/CRC bytes. | synthetic topology/Combiner regression is present; direct golden is missing | Function-open, certification pending: direct golden and firmware-owner review remain required. |

## Workflow promotion gate per IC/mode

- authoritative memory map, region atomicity, and owner;
- blank/reference initializer and canonical profile;
- explicit integrity disposition for every processor stage;
- valid/invalid fixtures and expected output SHA-256;
- mutation/processor diff review;
- UI catalog visibility and terminology decision;
- release/support owner sign-off.

## Replace-specific evidence still required

- DP Replace: all 13 retained IC rows have executable canonical maps and whole-part atomicity, while the v0.9.17 user selector exposes 10. Same-build FlashCode/Initial Code controls prove full-output equality for every admitted route, including separate NT51928 DP/LDC writes; catalog Evidence-open states still require firmware-owner release review and any independently requested direct output package. Non-selectable NT51920/NT51930/NT51931, No-Jira NT51930 content, and NT51928 NB are not admitted evidence rows;
- CtrlRAM Replace: complete named CtrlRAM regions/groups and post-processing dependencies;
- IC num: UI and request model must bind Replace to the selected IC before presenting region choices; two-option ICs use text choices such as `single`/`cascade`, while three-or-more concrete count ICs use numeric count selection with future room for Other/custom exceptions;
- CRC/header: exact legacy `combiner.exe` version, invocation, read/write ranges, execution order, and golden evidence;
- General: globally forbidden/protected ranges, alignment, overlap, and post-processing trigger rules.
