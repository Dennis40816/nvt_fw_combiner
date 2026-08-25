# Supported IC / Workflow Matrix

This document separates exact-route publication from evidence kind and release
readiness. Catalog `1.10.0` records the 2026-08-25 owner decision: all 64 exact
Standard Merge, AB Merge, and CtrlRAM Replace routes are `Supported` and
ordinary authoring is `Available`. This is the current support claim for those
exact routes. It does not waive a route's honest `ContractOnly` evidence,
firmware/release-owner review, package/signing, or clean-machine smoke. The
canonical manifest now cross-links all 89 policy routes exactly; that closed
repository gate is not a release approval. `Unknown` never means `None`.

Older dated `support-neutral`, `candidate`, or `function-open` statements below
are retained as evidence history. Where they conflict with catalog `1.10.0`,
the 2026-08-25 exact-route policy supersedes them.

> **0.10.x target scope:** NT51920, NT51925, NT51930, and NT51931 are retired
> production capabilities under `SPEC.md`, ADR 0042, and #221. They have no
> selector, route, registration, package entry, or Support Matrix publication
> row. Explicitly labelled historical notes below preserve provenance only and
> cannot act as admission or migration authority.

v0.9.12 admission correction: [ADR 0030](../adr/0030-production-firmware-admission-without-golden-hashes.md)
supersedes the older `reference-SHA`, `full-reference-SHA`, and "SHA as build discriminator" wording
retained in evidence-history cells below. Golden input/output hashes continue to lock regression
fixtures and parity claims, but production firmware inputs are admitted by typed profile facts and
must not be accepted or rejected by comparison with a golden whole-file hash.
[ADR 0031](../adr/0031-ctrlram-profile-intervals-and-build-plan-authority.md) likewise supersedes
exact PID/version/count route wording retained in evidence-history cells: requested IC selects the
owner-declared family, effective Common FW intervals select among multiple runtime profiles, and
Number selects an owner-provided typed plan. The pre-#221 compatibility
baseline's 31 modeled runtime interval/plan pairs,
including NT51928 non-NB single/2-chip/3-chip and NT51950/NT51951 single/cascade, have trusted V2 routes;
fixture metadata remains report evidence and never narrows that production population.

Current owner priority as amended on 2026-08-25:

- keep DP Replace profiles and executable regression semantics but hide its
  ordinary UI/CLI authoring surface in the initial `1.0.0`; all 14 DP routes
  are `Unavailable` and `Internal`, and retirement or reopening is decided at
  `1.1.0`;
- publish every current exact Standard Merge, AB Merge, and CtrlRAM Replace
  route as `Supported` and `Available` without rewriting weaker evidence into
  direct Golden evidence;
- include the `v0.9.15` AB runtime/UI/CLI function for NT51919/NT51929/NT51932 under the fixed no-processor plan, NT51950 through explicit `1 IC`/`Cascade` selection, and selector-free NT51951; missing direct golden evidence remains visible release-certification debt rather than a function gate;
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
[`0.8.0-goal-and-acceptance.md`](0.8.0-goal-and-acceptance.md). New IC support starts from a manifest-pinned family/profile bundle and an exact route in `canonical-capability-policy-v1.json`, then updates only the relevant flash-map, TP header, postbuild, evidence, test, and documentation rows.

## Availability, evidence, and IC-family vocabulary

Workflow availability and golden verification are separate axes:

- **Golden verified** means direct or owner-approved fact-scoped parity is recorded. It is not a product-support promise.
- **Evidence open** means the executable/safety contract exists and authoring remains available, while direct golden or firmware-owner review is still open. A missing golden alone does not ban the workflow.
- **Not available** is reserved for a workflow whose executable profile, ranges, processor authority, or typed plan has not been declared. Its UI badge must expose the reason and opening condition on hover.

Owner-declared family facts are typed in the resolved canonical map's `FamilyRelationships`; they never expand executable ranges by themselves:

| Family | Members | Relationship and reusable scope |
| --- | --- | --- |
| `nt51927-family` | NT51917, NT51927 | Perfect symmetric relationship across the owner-declared family facts. |
| `nt51927-family` | NT51927, NT51928 | Partial symmetric relationship for Replace; LDC differs and NT51928 NB is excluded. |
| `nt51929-nt51932-family` | NT51919, NT51929, NT51932 | Perfect symmetric relationship for the declared facts; NT51932 still owns its distinct cascade product path. |

## Standard Merge and DP Replace relationship

Standard Merge and DP Replace use the same canonical IC memory-map facts.
Therefore, an IC cannot expose DP Replace unless it also exposes Standard
Merge. The ten selectable ICs retain isolated trusted DP Replace bundles,
runtime registrations, and profile-derived inputs for regression. Catalog
`1.10.0` keeps every DP Replace authoring route unavailable and publication
internal, so the initial `1.0.0` does not enumerate the workflow in ordinary
UI/CLI authoring.
This is an exposure decision, not deletion or support promotion; firmware-owner
review remains separate and the owner will decide the feature at `1.1.0`.

Current DP Perspective input contract for NT51950/NT51951:

- the UI calls `reference-base` **Reference FlashCode**, not `base.bin`;
- it must be one complete final Standard/Normal Merge `.bin` for the same selected IC and exactly one declared capacity: `0x40000`, `0x80000`, or `0x100000` bytes;
- the DP replacement is a DP/FlashCode-shaped `.bin` no larger than that reference capacity; a shorter input is padded with `0x00`, while an oversized input is rejected;
- the output clones Reference FlashCode, replaces the declared full DP container, then restores the reference TP overlay. The existing owner rule keeps customer information from the replacement DP image.

A same-capacity complete Standard/Normal FlashCode is therefore a valid *shape* for the current DP replacement slot, but parity/support claims stay limited to recorded evidence. Future AB FlashCode input must use an AB-specific profile-declared artifact shape/extractor and explicit A/B bank, header-copy, preserved-range, and Legacy Combiner rules. Normal FlashCode offsets must not be guessed or reused for AB input.

Historical pre-#221 evidence recorded a different NT51930 profile over a
canonical 256 KiB Standard Merge map:

- Reference FlashCode is one complete same-IC Standard/Normal Merge output of exactly `0x40000` bytes;
- the replacement is a same-IC DP/FlashCode-shaped BIN containing the complete canonical DP range `[0x0000, 0x6000)`; `0x40000` is the expected outer-container length, any other length that still covers this range produces the declared warning, and shorter input is rejected;
- the output clones Reference FlashCode and replaces only `[0x0000, 0x6000)`. The gap `[0x6000, 0x7000)` and TP `[0x7000, 0x40000)` remain byte-for-byte from Reference FlashCode;
- the retired executable V2 safety contract and deterministic full-output
  oracle remain immutable characterization evidence only; they do not make
  authoring available.

The v0.9.11 Gen Flash DP Replace profiles clone an exact Reference FlashCode and have only these canonical write ranges:

- NT51917/NT51927: DP `[0x3C000, 0x40000)` in a `0x40000` base;
- NT51919/NT51929/NT51932: DP `[0x00000, 0x06000)` in a `0x40000` base;
- NT51923/NT51926: DP `[0x3E000, 0x40000)` in a `0x40000` base; and
- NT51928 non-NB: one capability resolves `0x40000` without LDC or
  `0x80000` with LDC `[0x40000,0x62000)`. DP/Initial Code, TP, and LDC
  section sources are address-bearing views; Reference is an exact complete
  container variant.

All bytes outside those ranges remain from Reference FlashCode. The DP/LDC profiles are isolated from the existing Standard/General Merge bundle identities, and the all-IC self-replacement regression requires complete output byte/SHA equality to the applicable owner-provided Standard Merge control. NT51917 and NT51919 use only their recorded fact-scoped aliases. These controls admit the authoring routes but do not replace independent firmware-owner release review.

## Current executable inventory

| Area | IC coverage | Source of truth | What this currently means | Not enough for 1.0 until |
| --- | --- | --- | --- | --- |
| IC support exposure | NT51917, NT51919, NT51923, NT51926, NT51927, NT51928 non-NB, NT51929, NT51932, NT51950, NT51951 | exact routes in `canonical-capability-policy-v1.json` joined to compiled capabilities | Catalog `1.10.0` contains 89 exact routes. Its 64 Standard/AB/CtrlRAM routes are `Supported + Available`; 14 DP routes are `Internal + Unavailable`; General retains 10 internal and one test-only route. Authoring, publication, and evidence remain independently pinned; manifest schema `1.1` supplies the exact 89-route evidence join. | Keep the exact join and validator green through the frozen-tree verifier, package/signing, clean-machine, and release-owner gates. |
| Standard Merge profiles | NT51917, NT51919, NT51923, NT51926, NT51927, NT51928, NT51929, NT51932, NT51950, NT51951 | trusted V2 bundles plus `TrustedV2CompositionCompiler` | All 14 exact routes are supported. Evidence is seven Direct Golden, two Approved Alias, four Synthetic Oracle, and one Contract Only. NT51950/NT51951 select the V2 map matching only DP input sizes `0x40000`/`0x80000`/`0x100000`, output the selected DP length, overlay TP `0x0A000-0x36FFF (len 0x2D000)`, and preserve customer info. | Publication and exact evidence cross-link are closed; weaker evidence remains visibly weaker, and release still requires firmware review, final verification, and release-owner approval. |
| AB Merge function | NT51919, NT51929, NT51932, NT51950, NT51951 | `nt51919-nt51929-nt51932-ab-merge` and `nt51950-ab-merge` trusted V2 bundles, exact canonical policy routes, shared Application runner | All six exact AB routes are supported. Evidence is two Direct Golden, two Approved Alias, one Synthetic Oracle, and one Contract Only. Every AB profile declares its A/B CMI read regions; production naming and UI projection read only the compiled map and have no GenFlash catalog fallback. The canonical 950/951 route copies the complete exact-length DP AB image, projects TPA/TPB from the shared TP-native window, relocates TPB DIFF by the resolved `+0x40000` or `+0x80000` instance delta, materializes a private A/B Combiner image, and imports only the verified B ILM/DLM/CRC fields. It never backfills a whole bank into the DP-seeded output. TP metadata validates topology but never selects it; TP version/PID/Common FW are report/naming facts, not selectors. Short TP prefixes block; longer TP inputs warn and only the declared prefix has execution authority. | Missing direct Golden alone does not revoke support. The exact evidence join is closed; release still requires firmware review of declared map/postbuild authority, packaged EXE smoke, final clean verification, and release-owner approval. |
| TP flash-map catalog | NT51917, NT51919, NT51923, NT51926, NT51927, NT51928 non-NB, NT51929, NT51932, NT51950, NT51951 | hash-pinned `profiles/built-in/ctrlram-postbuild-v2/flash-map.json` plus `BuiltInTpFlashMapCatalog` | UI can display DP, CtrlRAM, customer-info, IC-count visibility, and declared TP/full-Flash base shapes from current evidence. Static C# range facts are retired. | Every released workflow maps these rows through profiles, compiler checks, report evidence, and direct TP/full-Flash parity. |
| CtrlRAM postbuild command catalog | NT51917, NT51919, NT51923, NT51926, NT51927, NT51928 non-NB, NT51929, NT51932, NT51950, NT51951 | hash-pinned `profiles/built-in/ctrlram-postbuild-v2/catalog.json`, trusted V2 route bundles, Combiner `1.13.0` | All 44 exact CtrlRAM routes are supported. Nineteen exact route variants use a shortened `tp-work` map; a typed TP artifact that already spans the complete image capacity instead reuses the capacity-matched map without becoming FlashCode. Evidence is 19 Direct Golden, five Approved Alias, and 20 Contract Only. Production admission is IC + effective Common FW interval (only when multiple profiles exist) + typed IC Count plan; PID, filename, exact fixture version/count, and whole-file SHA are report/regression facts only. Shortened TP and larger full-Flash bases remain separate exact-capacity routes even though their effective CtrlRAM/TP range is identical. NT51950 single plus NT51951 single/two-IC TP builds are executable, but await independent TP-only expected outputs because their owner full-Flash prefixes contain DP-origin bytes. | The manifest's exact 44-route evidence join is closed; release still requires current packaged Combiner trust/smoke, firmware review of weaker-evidence routes, final verification, and release-owner approval. |
| Production Replace profiles | All ten selectable ICs retain trusted V2 DP Replace profiles behind unavailable authoring policy; CtrlRAM profiles are generated from FlashMap/postbuild evidence; General Replace UI/CLI supports DP-kind mappings and TP/CtrlRAM mappings with postbuild refresh | `CanonicalCapabilityCatalog`, focused authoring/planning/execution adapters, isolated hash-pinned DP Replace bundles, hash-pinned FlashMap/Postbuild config | Gen Flash DP profiles and full-output self-replacement regression remain intact, but ordinary UI/CLI authoring does not enumerate DP Replace in `0.10.7` or the initial `1.0.0`. NT51928 retains its `0x40000`/`0x80000` DP/LDC contract and NT51950/NT51951 retain capacity-selected behavior as regression authority. CtrlRAM and General Replace behavior are separate and unchanged. | Owner decision at `1.1.0` to retire or reopen DP Replace; if reopened, independent firmware-owner release review/support selection, ADR 0045 section-source migration, remaining direct output evidence, naming contracts, and finalized safety envelopes still apply. |
| General Merge workbench | Catalog IC ids for selection only; no IC-specific support claim | `CompositionExecutionAdapter.RunGeneralMergeAcceptedSessionWithProgressAsync`, `general-merge` CLI, Merge page General mode, `saved-rule` CLI | General Merge initializes one caller-authored positive output capacity with a typed `0x00..0xFF` fill byte (`0x00` only when omitted), then compiles explicit source-start/target-start/length mappings as `CopyRange` operations through the shared composition planner. It supports Preview/Build from CLI and UI, rejects target overlap and out-of-bounds ranges, writes structured reports, and never invokes postbuild. General Merge `--rule` consumes a Saved Rule v2 initializer plus explicit `--slot` bindings and forbids `--size`/`--fill` overrides; reports mark those operations as `saved-rule`. Standalone saved-rule inspection accepts only v2 and rejects v1 with explicit migration guidance. | Saved-rule promotion into normal workflows, UI Saved Rules, owner-approved reusable General Merge policies, General Replace saved-rule execution, and any future postbuild-dependent variant must be separately reviewed. |

## AB support, direct-golden debt, and release progress

Catalog `1.10.0` publishes all six exact AB routes as `Supported` and
`Available`. `Function open` in the historical table means the reviewed runtime
profile is selectable in UI/CLI and reaches the shared Application executor; it
is no longer the publication ceiling. A missing direct Golden remains an
honest evidence debt and cannot be rewritten from an alias or synthetic case.
It does not revoke the owner support decision, but it remains relevant to the
independent R3 map/processor/topology and release review.

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
| AB publication support | 6 supported exact routes / 6 planned IC/topology cells | **100.0%** by the 2026-08-25 owner decision; evidence kind remains separate. |
| AB release-certification closure | 0 cells with every external release gate closed / 6 planned IC/topology cells | **0.0%**; local support policy does not self-approve firmware, package, signing, or release-owner gates. |

The four missing direct AB golden cells are NT51919 fixed, NT51932 fixed,
NT51950 `Cascade`, and selector-free NT51951. These counts are AB Merge only;
Standard Merge, DP Replace, and CtrlRAM evidence are not substituted into this
ledger.

## First-sample gap priority

| Priority | Gap | Current state | Needed before `v1.0.0` |
| --- | --- | --- | --- |
| P0 | Support matrix lock | Closed by catalog `1.10.0`: all 64 exact Standard/AB/CtrlRAM routes are Supported + Available; DP remains Internal + Unavailable and General keeps its current internal/test-only states. Manifest `1.1` cross-links all 89 routes exactly. | Keep the independent denominator, route-evidence join, and policy hash pins green through release. |
| P0 | Standard Merge golden closure | Golden-backed canonical V2 profiles are executable; the recorded NT51950/NT51951 owner golden outputs from `merge_bin.7z` are covered; NT51917/NT51919 are executable owner-confirmed aliases and are tested against NT51927/NT51929 golden bytes. NT51950/NT51951 V2 also retain legacy byte parity for all declared capacities. | Firmware-owner sign-off for every released Standard Merge profile; direct owner samples for an additional 950/951 capacity if it is selected for release, and optional direct NT51917/NT51919 alias samples if desired. |
| P0 | DP Replace production closure | All ten selectable ICs have executable trusted V2 profiles, profile-derived UI/CLI slots, canonical range tests, and full-output same-build controls. NT51928 uses one Reference-resolved `0x40000`/`0x80000` capability and an applicable Initial Code/LDC selection group. NT51950/NT51951 retain their deterministic capacity oracle and archived comparison; other routes remain Evidence open in the catalog. | Firmware-owner release/support selection, ADR 0045 section-source migration, and any independently required direct DP Replace output packages. NT51928 NB remains outside this admission. |
| P0 | CtrlRAM Replace production closure | UI slots, memory layout, staged legacy Combiner execution, postbuild command trace, CLI multi-slot input, and private fixture handoff exist. | Private golden outputs, firmware-owner parity review, and promotion from workbench execution to released support claim. |
| P0 | General Replace production closure | Workbench/UI and CLI Preview/Build can run explicit mappings inside cataloged DP-kind ranges and TP/CtrlRAM ranges that have selected postbuild profiles. Protected/header/customer rows remain blocked. | Owner-approved General Replace safety envelopes, overlap/alignment policy, and golden outputs for released IC/modes. |
| P1 | General Merge production closure | General Merge v1 can run ad hoc explicit source-to-target mappings over a blank output from CLI and UI without postbuild. Saved-rule JSON validation and General Merge CLI consumption exist, with operation provenance in reports. | UI Saved Rules, normal-workflow promotion, final policy UX, and owner-approved reusable mapping evidence before support-style claims. |
| P1 | Unified workflow data model | Merge uses executable profiles; Replace still mixes synthetic profiles, workbench-specific planning, and flash-map facade data. | `0.6.0` should evaluate a unified profile/template/catalog model so UI, CLI, and tests call the same runner contracts. |
| P1 | Report/history completeness | Preview/Build report modal exists; first-peak errors, save flow, output artifact path display, local persistent report history, single-entry deletion, persisted audit metadata, structured operation evidence cards for status, overlap, normalized ranges, processor/tool id, read/write ranges, and refresh argv exist. Report issues now carry schema-level severity, known truncation diagnostics render as warnings rather than blocking issues, and Build automatically runs validation/report generation without a separate manual Preview gate. | Finish support-ready audit review and visual polish. |
| P1 | Settings persistence/readiness | Settings surface is catalog-backed; theme, strictness, language, and report history persist locally. `Warn only` is recorded as a UI preference but firmware gates still fail closed. | Define any future execution-affecting setting through Application contracts before it can change Preview/Build behavior; continue polishing support-status readiness. |
| P2 | Deferred workflows | AB is function-open for NT51919/NT51929/NT51932/NT51950/NT51951 but all certification and release gates remain open. UI Saved Rules, General profile promotion, General Replace saved-rule execution, and future REG Replace remain deferred. | Close each AB cell's firmware-owner/golden/release gate independently; do not treat function availability as promotion. |

## Owner validation package needed

- NT51917/NT51919 Standard Merge: owner confirmation is recorded as NT51917 -> NT51927 and NT51919 -> NT51929; direct IC-specific samples are optional audit evidence rather than an exposure blocker.
- NT51950/NT51951 Standard Merge: canonical V2 map/plan/runtime golden tests cover the owner NT51950 `0x40000` and NT51951 `0x80000` DP cases, with TP input covering `0x0A000-0x36FFF` and customer info preserved from DP. Legacy/V2 parity covers all six declared IC/capacity combinations. Direct owner golden evidence for the other combinations remains required if any is selected for release exposure.
- NT51950/NT51951 DP Replace: the 2026-08-02 owner decision requires replacement length to exactly equal the selected `0x40000`, `0x80000`, or `0x100000` base capacity. Exact-pair execution restores TP from base and keeps customer info from replacement DP; shorter and larger inputs fail closed. The older short-input public hashes remain immutable historical migration evidence and no longer authorize production padding. Direct hardware golden is still missing.
- CtrlRAM Replace per released IC/mode: base firmware, per-region CtrlRAM replacement BINs, expected final output after Combiner postbuild, Combiner version/tool binding, command order, declared read/write ranges, allowed diff ranges, and FWConfig Common FW version when an IC has multiple postbuild categories.
- IC aliases/counts: NT51928/NT51929/NT51951 alias behavior and NT51928 NB exclusion are owner-confirmed.
- General Replace: protected ranges beyond the current header/customer/project/backup gate, allowed explicit-mapping envelope, overlap/alignment rules, TP-touching postbuild expected outputs, and final release-scope IC/mode approvals.

| IC | Standard Merge | AB Merge | Replace planning | Integrity evidence | Current evidence | 1.0 status |
| --- | --- | --- | --- | --- | --- | --- |
| NT51917 | canonical V2 Standard Merge route; map-bound alias of NT51927 | no evidence | DP Replace plus one `[1.0.0,infinity)` CtrlRAM profile with single/2-chip/3-chip plans | The three TP plans retain NT51917 processor/staged identity and have 7/10/13-command regression controls. Exact versions, PIDs, and hashes identify fixtures only. | owner perfect-family confirmation + direct NT51927 fixtures + separate hash-pinned alias profiles/bundle + V1/V2 full-byte, argv/read-write/report/input-immutability parity | Standard and all full/TP CtrlRAM routes are Supported; single is Approved Alias and multi-IC remains Contract Only. |
| NT51919 | canonical V2 Standard Merge route; map-bound alias of NT51929 | fixed `0x80000` route through the owner-approved NT51929 fact scope | DP Replace plus one `[1.0.0,infinity)` CtrlRAM profile with single and bounded `2–8` cascade plans | AB uses the exact six-operation no-processor plan; CtrlRAM single regression uses the NT51929 family with NT51919 processor identity. Cascade compiles `N-1` records from `0x2D100`, writes each `0x0B90` DLM prefix, preserves each `0x0870` NF tail and every inactive record, and authorizes DLM CRC `[0x7128,0x7144)`. Postbuild-owned FWConfig Backup placement is checked against the count-derived aligned expectation. | owner perfect-family confirmation + direct NT51929 AB golden/fact scope + manifest-pinned NT51919 profiles; independent final firmware/release review remains | Standard, AB, and both CtrlRAM routes are Supported; Standard/AB/single CtrlRAM are Approved Alias and cascade is Contract Only. |
| NT51923 | canonical V2 Standard Merge route | no evidence | DP Replace plus one `[1.0.0,infinity)` CtrlRAM profile with single and generic cascade plans | Single and cascade-3 owner cases each reconstruct the reference and run registered Combiner 1.13.0 with two ordered `CRC_Enable` commands. Exact Common FW, PID, hash, and count identify regression fixtures only. | owner DP/TP/physical inputs and expected outputs + trusted V2 profiles + V1/V2 parity + argv/read-write/report/input-immutability evidence | Standard plus both full and TP-prefix CtrlRAM routes are Supported with Direct Golden evidence. |
| NT51926 | canonical V2 Standard Merge route | no evidence | DP Replace; CtrlRAM `[1.0.0,2.0.0)` and `[2.0.0,infinity)` profiles each expose single and generic cascade; DP-only General Replace candidate | The 1.x-sourced plan uses header target `0x32F50`/VN `0x1660`; the 2.x-sourced plan uses `0x32A70`/VN `0x149E`. Common FW selects only this real profile boundary; PID, hash, and fixture count do not. TP/full-Flash bases preserve the Flash tail, and version edit validates the final canonical Backup. | trusted profiles plus 1.4.1/2.0.0 direct regression evidence; the 2.0.0 no-edit V1/V2 results match while owner expected differs only at four approved CRC words | Standard and all eight full/TP CtrlRAM interval/plan routes are Supported with Direct Golden evidence. |
| NT51927 | canonical V2 Standard Merge route | no evidence | DP Replace plus one `[1.0.0,infinity)` CtrlRAM profile with single, exact 2-chip, and exact 3-chip plans | Single/2-chip/3-chip regression cases lock the owner-provided 7/10/13-command shapes. Their exact Common FW, PID, hash, and filenames are evidence only. | owner single expected plus owner multi-chip bases/repository-derived replay inputs; profile/map, argv/read-write/report/input-immutability evidence is locked | Standard and all six full/TP CtrlRAM routes are Supported; single full/TP is Direct Golden and 2/3-chip routes remain Contract Only. |
| NT51928 | One Standard Merge capability; no LDC resolves the shared `0x40000` candidate, while selected structurally valid LDC resolves `0x80000` without invalid-input fallback | no evidence | DP Replace accepts exact `0x40000` or `0x80000` Reference and applies one Initial Code/LDC `1..2` selection group; separately declared CtrlRAM profiles expose non-NB single/2-chip/3-chip plans | CtrlRAM profiles explicitly reference their matching TP layout/postbuild facts inside the NT51928 container; this route/processor authority is not inherited by the Initial Code/TP shared-fact relationship. NT51928 NB remains excluded. | direct `gen_flash_bin_v2` NT51928 base + owner typed shared-fact confirmation + manifest-pinned current family/profiles + dual-capacity changed-input evidence | Standard and all six non-NB full/TP CtrlRAM routes are Supported with honest Contract Only evidence; NB remains excluded. |
| NT51929 | canonical V2 Standard Merge route | fixed `0x80000` route with direct owner Golden parity | DP Replace plus one `[1.0.0,infinity)` CtrlRAM profile with single and bounded `2–8` cascade plans | AB has no processor and relocates only the three declared TPB scalars; CtrlRAM single locks registered Combiner 1.13 two-command parity. Cascade compiles `N-1` records from `0x2D100`, writes each `0x0B90` DLM prefix, preserves each `0x0870` NF tail and every inactive record, and authorizes DLM CRC `[0x7128,0x7144)`. Postbuild-owned FWConfig Backup placement is checked against the count-derived aligned expectation. | direct owner AB golden + trusted V2 AB/single/cascade profiles; classified legacy header/CRC differences and final release review remain explicit gates | Standard, AB, and both CtrlRAM routes are Supported; Standard/AB/single CtrlRAM are Direct Golden and cascade remains Contract Only. |
| NT51932 | canonical V2 Standard Merge route | fixed `0x80000` route through the owner-approved NT51929 fact scope | DP Replace plus one `[1.0.0,infinity)` CtrlRAM profile with single and bounded `2–8` cascade plans | AB uses the same exact six-operation no-processor plan; CtrlRAM single uses NF/Normal/VN. Cascade compiles `N-1` records from `0x2D100`, writes each `0x0B90` DLM prefix, preserves each `0x0870` NF tail and every inactive record, and authorizes DLM CRC `[0x7128,0x7144)`. Postbuild-owned FWConfig Backup placement is checked against the count-derived aligned expectation. | direct cascade regression + owner-approved NT51929 AB family facts; independent final firmware/release review remains | Standard, AB, and both CtrlRAM routes are Supported; Standard/cascade CtrlRAM are Direct Golden, AB is Approved Alias, and single CtrlRAM is Contract Only. |
| NT51950 | canonical V2 DP Perspective Standard Merge route, exact-capacity selected | supported AB V2: explicit `1 IC` (`0x80000`) or `Cascade` (`0x100000`) profile map; the operator choice is validated against both TP FWConfig chip counts | DP and CtrlRAM priority: DP Replace has LDC already packaged in DP; `[1.0.0,infinity)` CtrlRAM single and exact 2-IC cascade plans | AB preserves the complete DP AB input, leaves TPA CRC unchanged, relocates TPB DIFF, and permits the staged Combiner to change only TPB ILM/DLM/CRC bytes. CtrlRAM Cascade writes only the active `0x0910` DLM prefix, preserves its `0x0AF0` NF tail, and uses fixed Backup `0x36000`. | direct CtrlRAM single golden plus owner-approved NT51951 fact-scoped Cascade geometry/postbuild alias; the NT51951 output identity/capacity is excluded. AB `Cascade` golden remains missing. | All Standard, AB, and CtrlRAM routes are Supported; weaker capacity/mode evidence remains Synthetic, Approved Alias, or Contract Only as declared. |
| NT51951 | canonical V2 DP Perspective Standard Merge route, exact-capacity selected | supported selector-free AB V2 map (`0x100000`); no IC-number field is presented | DP and CtrlRAM priority: DP Replace has LDC already packaged in DP; `[1.0.0,infinity)` CtrlRAM single and exact 2-IC cascade plans | AB preserves the complete DP AB input, leaves TPA CRC unchanged, relocates TPB DIFF, and permits the staged Combiner to change only TPB ILM/DLM/CRC bytes. CtrlRAM Cascade uses the same masked record and fixed Backup contract in the distinct `0x80000` container. | direct CtrlRAM single plus direct AUTO_PRJ-599 exact-2-IC Cascade golden; registered Combiner drift is confined to four approved CRC words. AB uses the declared public-host synthetic oracle. | All Standard, AB, and CtrlRAM routes are Supported; alternate Standard capacities and AB remain Synthetic Oracle, both CtrlRAM full-Flash routes are Direct Golden, and both TP-only routes remain Contract Only pending independent TP expected outputs. |

## Workflow promotion gate per IC/mode

- authoritative memory map, region atomicity, and owner;
- blank/reference initializer and canonical profile;
- explicit integrity disposition for every processor stage;
- valid/invalid fixtures and expected output SHA-256;
- mutation/processor diff review;
- UI catalog visibility and terminology decision;
- release/support owner sign-off.

## Replace-specific evidence still required

- DP Replace: all ten selectable ICs have executable canonical maps and whole-part atomicity. Same-build FlashCode/Initial Code controls prove full-output equality for every admitted route, including separate NT51928 DP/LDC writes; catalog Evidence-open states still require firmware-owner release review and any independently requested direct output package. NT51928 NB is not admitted;
- CtrlRAM Replace: complete named CtrlRAM regions/groups and post-processing dependencies;
- IC num: UI and request model must bind Replace to the selected IC before presenting region choices; two-option ICs use text choices such as `single`/`cascade`, while three-or-more concrete count ICs use numeric count selection with future room for Other/custom exceptions;
- CRC/header: exact legacy `combiner.exe` version, invocation, read/write ranges, execution order, and golden evidence;
- General: globally forbidden/protected ranges, alignment, overlap, and post-processing trigger rules.
