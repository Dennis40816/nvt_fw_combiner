# Supported IC / Workflow Matrix Draft

This is a planning inventory, not a support claim. A row becomes supported only after profile validation, integrity disposition, golden regression, and owner sign-off. `Unknown` never means `None`.

Current owner priority as of 2026-07-02:

- focus on normal Merge and normal Replace for DP Replace and CtrlRAM Replace workflows;
- defer AB Code Merge implementation for now;
- include NT51950 and NT51951 normal Merge with the confirmed DP Perspective TP overlay range `0x0A000-0x36FFF (len 0x2D000)`; owner golden fixtures are recorded and firmware-owner sign-off remains required;
- require Replace UI to collect IC num before profile-specific regions are shown. ICs with only single/cascade choices use text labels; ICs with three or more concrete choices such as NT51917/NT51927/NT51928 use numeric count selection, optionally with an Other/custom path later;
- expect CtrlRAM Replace CRC/header recalculation through approved legacy `combiner.exe` postbuild sequences.

The per-IC Merge/Replace flowchart reference is [`ic-workflow-flowcharts.md`](ic-workflow-flowcharts.md). When adding a new IC workflow, follow [`adding-ic-merge-replace-workflow.md`](adding-ic-merge-replace-workflow.md). Update both documents together when IC workflow status changes.

## Current executable inventory

| Area | IC coverage | Source of truth | What this currently means | Not enough for 1.0 until |
| --- | --- | --- | --- | --- |
| Standard Merge profiles | NT51917, NT51919, NT51920, NT51923, NT51926, NT51927, NT51928, NT51929, NT51930, NT51931, NT51932, NT51950, NT51951 | `BuiltInStandardMergeProfiles.ExecutableStandardMergeProfiles` | Executable profiles exist for the golden-backed gen_flash set, owner-confirmed aliases NT51917 -> NT51927 and NT51919 -> NT51929, NT51930 flash-map golden, and NT51950/NT51951 DP Perspective golden cases. NT51950/NT51951 accept only DP input sizes `0x40000`/`0x80000`/`0x100000`, output the selected DP length, overlay TP `0x0A000-0x36FFF (len 0x2D000)`, and preserve customer info. | Firmware-owner sign-off before support claim/exposure; direct owner samples for optional aliases if selected for release. |
| TP flash-map catalog | NT51917, NT51919, NT51920, NT51923, NT51926, NT51927, NT51928 non-NB, NT51929, NT51930, NT51931, NT51932, NT51950, NT51951 | `TpFlashMapCatalog` | UI can display DP, CtrlRAM, customer-info, and IC-count visibility rows from current evidence. | Every released workflow maps these rows through profiles, compiler checks, and report evidence. |
| CtrlRAM postbuild command catalog | NT51917, NT51919, NT51920, NT51923, NT51926, NT51927, NT51928 non-NB, NT51929, NT51930, NT51931, NT51932, NT51950, NT51951 | `LegacyCombinerPostbuildCatalog`, Combiner `1.13.0` | Structured command sequences exist for single/cascade or numeric branches and are tested against argv shape. NT51926/NT51930 select 1.x versus 2.0 postbuild category from base FWConfig Common FW version; NT51930 1.x also distinguishes cascade `2..13` from extended cascade `14..29`. Workbench UI exposes per-region CtrlRAM slots and runs staged Combiner postbuild for Preview/Build reports and output. | Private golden replace outputs, final per-IC support exposure, and firmware-owner parity review. |
| Production Replace profiles | NT51950/NT51951 DP Replace UI/CLI workbench path; CtrlRAM workbench profiles are generated from FlashMap/postbuild evidence; General Replace UI/CLI workbench supports DP-kind mappings and TP/CtrlRAM mappings with postbuild refresh | `WorkbenchCompositionService`, `BuiltInReplaceProfiles`, `TpFlashMapCatalog` | NT51950/NT51951 DP Replace builds by cloning an exact-length base of `0x40000`, `0x80000`, or `0x100000`, replacing the padded selected-length DP container, then restoring TP `0x0A000-0x36FFF (len 0x2D000)` and customer info `0x37000-0x37FFF (len 0x1000)`. Golden-derived self-replacement tests currently lock the owner-approved `0x40000` and `0x80000` DP Perspective fixtures. CtrlRAM workbench runs selected region slots through staged postbuild. General Replace can build explicit mappings inside cataloged DP-kind ranges from UI rows or repeated CLI `--mapping <target-start+length=path>`; TP/CtrlRAM-touching mappings append the selected legacy Combiner postbuild refresh after the explicit mapping. Header/customer/project/backup rows remain protected. Built-in Replace profiles remain contract evidence. | Independent DP Replace expected outputs, real CtrlRAM/General Replace profiles per released IC/mode, TP-touching General Replace golden outputs, and owner-approved safety envelopes are finalized. |

## First-sample gap priority

| Priority | Gap | Current state | Needed before `v1.0.0` |
| --- | --- | --- | --- |
| P0 | Support matrix lock | Candidate IC/workflow rows exist, but candidates are not support claims. | Owner chooses the exact IC/workflow subset to ship; non-selected rows stay hidden or clearly candidate-only. |
| P0 | Standard Merge golden closure | Golden-backed gen_flash profiles are executable; NT51930 and NT51950/NT51951 owner golden outputs from `merge_bin.7z` are covered; NT51917/NT51919 are executable owner-confirmed aliases and are tested against NT51927/NT51929 golden bytes. | Firmware-owner sign-off for every released Standard Merge profile; optional direct NT51917/NT51919 alias samples if desired. |
| P0 | DP Replace production closure | NT51950/NT51951 DP Replace UI/CLI workbench path exists, and golden-derived self-replacement regression covers the current `0x40000`/`0x80000` owner fixtures; other IC DP Replace mappings remain gated. | Independent golden outputs for the 950/951 exact-base/variable-DP rule, the optional `0x100000` exposure case if selected, and per-IC DP/LD maps for any other released DP Replace IC. |
| P0 | CtrlRAM Replace production closure | UI slots, memory layout, staged legacy Combiner execution, postbuild command trace, CLI multi-slot input, and private fixture handoff exist. | Private golden outputs, firmware-owner parity review, and promotion from workbench execution to released support claim. |
| P0 | General Replace production closure | Workbench/UI and CLI Preview/Build can run explicit mappings inside cataloged DP-kind ranges and TP/CtrlRAM ranges that have selected postbuild profiles. Protected/header/customer rows remain blocked. | Owner-approved General Replace safety envelopes, overlap/alignment policy, and golden outputs for released IC/modes. |
| P1 | Unified workflow data model | Merge uses executable profiles; Replace still mixes synthetic profiles, workbench-specific planning, and flash-map facade data. | `0.6.0` should evaluate a unified profile/template/catalog model so UI, CLI, and tests call the same runner contracts. |
| P1 | Report/history completeness | Preview/Build report modal exists; first-peak errors, save flow, output artifact path display, local persistent report history, structured operation evidence cards for status, overlap, normalized ranges, processor/tool id, read/write ranges, and Combiner argv exist. Known truncation diagnostics render as warnings rather than blocking issues, and stale-preview Build gates report `ui.build.preview-required`. | Finish richer persisted history metadata, schema-level severity, and support-ready audit review. |
| P1 | Settings persistence/readiness | Settings surface is catalog-backed; preference persistence is still partial. | Persist execution-affecting settings and show readiness for tool bindings, report location, and support status. |
| P2 | Deferred workflows | AB Merge, saved rules, General profile promotion, and future REG Replace remain planned. | Reactivate only after owner evidence and support priority are explicit. |

## Owner validation package needed

- NT51917/NT51919 Standard Merge: owner confirmation is recorded as NT51917 -> NT51927 and NT51919 -> NT51929; direct IC-specific samples are optional audit evidence rather than an exposure blocker.
- NT51930 Standard Merge: DP input, TP input, expected output, expected file name, input/output SHA-256, and owner approval are recorded in `testdata/golden/standard-merge-gen-flash/manifest.json`.
- NT51950/NT51951 Standard Merge: current owner golden cases cover NT51950 `0x40000` DP and NT51951 `0x80000` DP, with TP input covering `0x0A000-0x36FFF` and customer info preserved from DP. A direct `0x100000` Standard Merge golden remains optional unless selected for release exposure.
- NT51950/NT51951 DP Replace: current committed regression reuses owner-approved Standard Merge `merge_bin.7z` fixtures as base + original DP input and proves self-replacement preserves the golden flash for `0x40000` and `0x80000`; still needed are independent DP Replace expected output hashes, variable-DP cases, the optional `0x100000` case if selected, and rejection cases for invalid base/replacement sizes.
- CtrlRAM Replace per released IC/mode: base firmware, per-region CtrlRAM replacement BINs, expected final output after Combiner postbuild, Combiner version/tool binding, command order, declared read/write ranges, allowed diff ranges, and FWConfig Common FW version when an IC has multiple postbuild categories.
- IC aliases/counts: final owner confirmation for NT51928/NT51929/NT51951 alias behavior, NT51928 NB exclusion, and NT51930 `>13 IC` handling.
- General Replace: protected ranges beyond the current header/customer/project/backup gate, allowed explicit-mapping envelope, overlap/alignment rules, TP-touching postbuild expected outputs, and final release-scope IC/mode approvals.

| IC | Standard Merge | AB Merge | Replace planning | Integrity evidence | Current evidence | 1.0 status |
| --- | --- | --- | --- | --- | --- | --- |
| NT51917 | executable owner-confirmed alias of NT51927 | no evidence | DP/CtrlRAM priority | CtrlRAM Replace uses the NT51927 reference flow | owner alias confirmation + NT51927 golden-byte alias regression | Candidate; postbuild core implemented |
| NT51919 | executable owner-confirmed alias of NT51929 | no evidence | DP/CtrlRAM priority | CtrlRAM Replace uses the NT51929/NT51932 reference flow | owner alias confirmation + NT51929 golden-byte alias regression | Candidate; postbuild core implemented |
| NT51920 | reference candidate | no evidence | DP/CtrlRAM/General TBD | Unknown | `gen_flash_bin_v2` config | Candidate |
| NT51923 | reference candidate | no evidence | DP/CtrlRAM/General TBD | Unknown | `gen_flash_bin_v2` config | Candidate |
| NT51926 | reference candidate | no evidence | DP/CtrlRAM/General TBD | CtrlRAM Replace postbuild category selected by FWConfig Common FW version: `1.4.1` -> `0x32F50` and VN len `0x1660`, `2.0.0` -> `0x32A70` and VN len `0x149E` | `gen_flash_bin_v2` config + 2026-07-05 CtrlRAM fixture + versioned TP Overview rows; expected output pending | Candidate |
| NT51927 | reference candidate | no evidence | DP/CtrlRAM priority | CtrlRAM Replace uses `MERGE_MODE` plus `NT51927BASED_GEN_CRC_MODE CRC32` | `gen_flash_bin_v2` config + IC FlashMap postbuild | Candidate; postbuild core implemented |
| NT51928 | reference candidate + LD | no evidence | DP/CtrlRAM priority | CtrlRAM Replace uses the NT51927 reference flow for non-NB only; NT51928 NB is a separate IC and is not covered | `gen_flash_bin_v2` config + owner alias confirmation | Candidate; postbuild core implemented |
| NT51929 | reference candidate | DP_AB + split-DP concept | DP/CtrlRAM priority | CtrlRAM Replace uses the NT51932 reference flow; TPA/TPB CRC explicitly None for AB evidence; TPB relocation required | verified AB sample + owner alias confirmation | Priority candidate; postbuild core implemented |
| NT51930 | flash-map derived Standard Merge profile | no evidence | CtrlRAM priority; DP/General TBD | Standard Merge golden from `merge_bin.7z`; CtrlRAM Replace uses `NT51930BASED_NORMAL_MODE CRC8`; FWConfig Common FW `1.x.x` selects the archived `1.4.0` command shape, including `2..13` versus `14..29` cascade split, while `2.0.0` selects the newer two-command shape | IC FlashMap postbuild + owner IC-count confirmation + TP Overview category notes; CtrlRAM expected output and firmware-owner sign-off pending | Candidate; postbuild core implemented |
| NT51931 | reference candidate | no evidence | DP/CtrlRAM/General TBD | Unknown | `gen_flash_bin_v2` config | Candidate |
| NT51932 | reference candidate | DP_AB | region inventory TBD | TPA/TPB CRC explicitly None; TPB relocation required | legacy AB reference | Priority candidate |
| NT51950 | normal merge requested; executable DP Perspective profile | uploaded combiner; deferred | DP and CtrlRAM priority | Standard Merge `0x40000` DP golden from `merge_bin.7z`; CtrlRAM Replace postbuild uses `NT51950BASED_NORMAL_MODE CRC8`; DP Replace workbench path requires an exact base of `0x40000`, `0x80000`, or `0x100000`, accepts shorter replacement DP with padding to that selected base length, then restores TP `0x0A000-0x36FFF (len 0x2D000)` and customer info `0x37000-0x37FFF (len 0x1000)` | IC FlashMap postbuild + DP Perspective golden; DP Replace self-replacement regression for `0x40000`; firmware-owner sign-off for support claim | Priority candidate |
| NT51951 | normal merge requested; executable DP Perspective profile shared with 950 | uploaded config; deferred | DP and CtrlRAM priority | Standard Merge `0x80000` DP golden from `merge_bin.7z`; CtrlRAM Replace uses the NT51950 reference flow; DP Replace follows the selected exact-base rule, not a max-container shortcut | DP Perspective golden evidence + DP Replace self-replacement regression for `0x80000` + owner alias confirmation; firmware-owner sign-off for support claim | Candidate |

## Workflow promotion gate per IC/mode

- authoritative memory map, region atomicity, and owner;
- blank/reference initializer and canonical profile;
- explicit integrity disposition for every processor stage;
- valid/invalid fixtures and expected output SHA-256;
- mutation/processor diff review;
- UI catalog visibility and terminology decision;
- release/support owner sign-off.

## Replace-specific evidence still required

- DP Replace: DP partition map and allowed atomicity; NT51950/NT51951 exact-base self-replacement is regression-tested for current `0x40000`/`0x80000` fixtures, but variable-replacement and independent owner expected-output hashes are still required before support promotion;
- CtrlRAM Replace: complete named CtrlRAM regions/groups and post-processing dependencies;
- IC num: UI and request model must bind Replace to the selected IC before presenting region choices; two-option ICs use text choices such as `single`/`cascade`, while three-or-more concrete count ICs use numeric count selection with future room for Other/custom exceptions;
- CRC/header: exact legacy `combiner.exe` version, invocation, read/write ranges, execution order, and golden evidence;
- General: globally forbidden/protected ranges, alignment, overlap, and post-processing trigger rules.
