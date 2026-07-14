# IC Workflow Flowchart Reference

Status: architecture reference as of 2026-07-02.

This document is an index for the current Merge and Replace flows by IC. It is not a production support claim. A profile is production-ready only after profile validation, golden regression, processor diff review, and owner sign-off. The implementation runbook for adding a new IC workflow is [`adding-ic-merge-replace-workflow.md`](adding-ic-merge-replace-workflow.md).

## Update rule

Update this document in the same change when any of these sources change:

- `src/NvtFwCombiner.Bootstrap/WorkbenchCompositionService.StandardMerge.BuiltInV2.cs`; this is the explicit production Standard Merge registration list.
- `src/NvtFwCombiner.Profiles/BuiltInReplaceProfiles*.cs`; these are synthetic contract fixtures only. Production Replace profiles are manifest-pinned V2 bundles with explicit Bootstrap registrations.
- `src/NvtFwCombiner.Application/ExternalTools/LegacyCombinerPostbuildCatalog.cs`
- `docs/architecture/nt51950-nt51951-dp-length-policy.md`
- `docs/architecture/ctrlram-postbuild-command-matrix.md`
- `docs/architecture/supported-ic-matrix.md`
- `docs/architecture/adding-ic-merge-replace-workflow.md`

The architecture test `IcWorkflowFlowchartReferenceCoversBuiltInIcLists` checks that this document lists every IC from the built-in Standard Merge profiles and the CtrlRAM postbuild catalog. The test is a sync guard only; the C# catalog and owner evidence remain the source of behavior truth.

## Notation

- Ranges use half-open notation: `[start, end)`.
- `Implemented profile` means the repo has an executable built-in profile or command catalog entry. It does not imply production parity.
- `Golden pending` means the repo has an executable profile or command catalog entry, but owner-approved golden output and firmware sign-off are still pending.

## Per-IC flow index

| IC | Standard Merge flow | DP Replace flow | CtrlRAM Replace flow | General Replace flow | Current status notes |
| --- | --- | --- | --- | --- | --- |
| NT51917 | `SM-GENFLASH-V2-ALIAS`: packaged canonical V2 bundle is selected by Bootstrap UI/CLI through its content-hash anchor; map-bound alias of NT51927. | `R-DP-GENERIC`: DP profile wiring pending. | `R-CTRLRAM-927`: follows NT51927, numeric single/2/3 IC and cascade. | `R-GENERAL-POSTBUILD`: explicit mappings use protected-range gates; TP/CtrlRAM mappings run selected postbuild when available. | V2 Standard Merge preserves NT51927 alias golden bytes; firmware-owner review remains required before release support. |
| NT51919 | `SM-GENFLASH-V2-ALIAS`: packaged canonical V2 bundle is selected by Bootstrap UI/CLI through its content-hash anchor; map-bound alias of NT51929. | `R-DP-GENERIC`: DP profile wiring pending. | `R-CTRLRAM-51932`: follows NT51929/NT51932. | `R-GENERAL-POSTBUILD`: explicit mappings use protected-range gates; TP/CtrlRAM mappings run selected postbuild when available. | `AB-51919-CANDIDATE`: fixed `0x80000` V2 plan aliases NT51929; no UI/CLI route or runtime promotion. |
| NT51920 | `SM-GENFLASH-V2`: packaged canonical V2 bundle is selected by Bootstrap UI/CLI through its content-hash anchor. | `R-DP-GENERIC`: DP profile wiring pending. | `R-CTRLRAM-LEGACY-NORMAL`: `CRC_Enable`. | `R-GENERAL-POSTBUILD`: explicit mappings use protected-range gates; TP/CtrlRAM mappings run selected postbuild when available. | V2 Standard Merge preserves legacy golden bytes; firmware-owner review remains required before release support. |
| NT51923 | `SM-GENFLASH-V2`: packaged canonical V2 bundle is selected by Bootstrap UI/CLI through its content-hash anchor. | `R-DP-GENERIC`: DP profile wiring pending. | `R-CTRLRAM-LEGACY-NORMAL`: `CRC_Enable`, cascade split DiffDLM. | `R-GENERAL-POSTBUILD`: explicit mappings use protected-range gates; TP/CtrlRAM mappings run selected postbuild when available. | V2 Standard Merge preserves legacy golden bytes; firmware-owner review remains required before release support. |
| NT51926 | `SM-GENFLASH-V2`: packaged canonical V2 bundle is selected by Bootstrap UI/CLI through its content-hash anchor. | `R-DP-GENERIC`: DP profile wiring pending. | `R-CTRLRAM-LEGACY-NORMAL`: `CRC_Enable`. | `R-GENERAL-POSTBUILD`: explicit mappings use protected-range gates; TP/CtrlRAM mappings run selected postbuild when available. | V2 Standard Merge preserves legacy golden bytes; firmware-owner review remains required before release support. |
| NT51927 | `SM-GENFLASH-V2`: packaged canonical V2 bundle is selected by Bootstrap UI/CLI through its content-hash anchor. | `R-DP-GENERIC`: DP profile wiring pending. | `R-CTRLRAM-927`: numeric single/2/3 IC and cascade. | `R-GENERAL-POSTBUILD`: explicit mappings use protected-range gates; TP/CtrlRAM mappings run selected postbuild when available. | V2 Standard Merge preserves legacy golden bytes; firmware-owner review remains required before release support. |
| NT51928 | `SM-GENFLASH-LD-V2`: hash-anchored V2 route includes TP, DP, and typed auxiliary LD. | `R-DP-GENERIC`: DP/LD profile wiring pending. | `R-CTRLRAM-927`: follows NT51927 for non-NB only. | `R-GENERAL-POSTBUILD`: explicit mappings use protected-range gates; TP/CtrlRAM mappings run selected postbuild when available. | NT51928 NB is not covered and must be a separate IC if approved later. |
| NT51929 | `SM-GENFLASH-V2`: packaged canonical V2 bundle is selected by Bootstrap UI/CLI through its content-hash anchor. | `R-DP-GENERIC`: DP profile wiring pending. | `R-CTRLRAM-51932`: follows NT51932. | `R-GENERAL-POSTBUILD`: explicit mappings use protected-range gates; TP/CtrlRAM mappings run selected postbuild when available. | `AB-51929-CANDIDATE`: fixed `0x80000` full DP -> TPA -> relocated TPB V2 plan; no UI/CLI route or runtime promotion. |
| NT51930 | `SM-FLASHMAP-V2`: packaged canonical V2 bundle is selected by Bootstrap UI/CLI through its content-hash anchor. | `R-DP-GENERIC`: DP profile wiring pending. | `R-CTRLRAM-51930`: current cascade maps to `<=13 IC`. | `R-GENERAL-POSTBUILD`: explicit mappings use protected-range gates; TP/CtrlRAM mappings run selected postbuild when available. | V2 Standard Merge preserves the `merge_bin.7z` golden bytes; firmware-owner review remains required before release support. |
| NT51931 | `SM-GENFLASH-V2`: packaged canonical V2 bundle is selected by Bootstrap UI/CLI through its content-hash anchor. | `R-DP-GENERIC`: DP profile wiring pending. | `R-CTRLRAM-51930`: NT51930-based mode. | `R-GENERAL-POSTBUILD`: explicit mappings use protected-range gates; TP/CtrlRAM mappings run selected postbuild when available. | V2 Standard Merge preserves legacy golden bytes; firmware-owner review remains required before release support. |
| NT51932 | `SM-GENFLASH-V2`: packaged canonical V2 bundle is selected by Bootstrap UI/CLI through its content-hash anchor. | `R-DP-GENERIC`: DP profile wiring pending. | `R-CTRLRAM-51932`: direct postbuild reference. | `R-GENERAL-POSTBUILD`: explicit mappings use protected-range gates; TP/CtrlRAM mappings run selected postbuild when available. | `AB-51932-CANDIDATE`: fixed `0x80000` full DP -> TPA -> relocated TPB V2 plan; no UI/CLI route or runtime promotion. |
| NT51950 | `SM-950-951-DP-PERSPECTIVE-V2`: packaged canonical V2 maps select the exact submitted DP capacity; owner `0x40000` DP golden passes. | `R-DP-950-951`: the workbench UI/CLI routes the supported V2 profile and selects its exact base capacity. | `R-CTRLRAM-51950`: direct postbuild reference. | `R-GENERAL-POSTBUILD`: explicit mappings use protected-range gates; TP/CtrlRAM mappings run selected postbuild when available. | Public deterministic expected hashes plus archived owner-approved legacy comparison cover all capacities with no known deviations. This migration evidence is not a hardware golden or product support claim. Deferred AB shares only the full-DP-first order, not normal-merge ranges or integrity rules. |
| NT51951 | `SM-950-951-DP-PERSPECTIVE-V2`: packaged canonical V2 maps select the exact submitted DP capacity; owner `0x80000` DP golden passes. | `R-DP-950-951`: the workbench UI/CLI routes the supported V2 profile and selects its exact base capacity. | `R-CTRLRAM-51950`: follows NT51950. | `R-GENERAL-POSTBUILD`: explicit mappings use protected-range gates; TP/CtrlRAM mappings run selected postbuild when available. | Public deterministic expected hashes plus archived owner-approved legacy comparison cover all capacities with no known deviations. This migration evidence is not a hardware golden or product support claim. |

## Deferred AB Initializer Policy

NT51919, NT51929, and NT51932 now have executable-candidate V2 profiles with a fixed `0x80000` AB map. They first copy the submitted full DP container, overlay TPA, relocate cloned TPB `u32` values at `0x7164/0x7168/0x716C` by `+0x40000`, then overlay TPB. The profiles are intentionally not routed through Bootstrap, UI, CLI, or Application runtime. NT51950 has a separate repository-only fixed-`0x80000` compilable profile which stages raw full A/B banks for its external Combiner; it is blocked on an exact owner-provided `map.txt` sidecar and does not use C# relocation or CRC. NT51951 remains unimplemented: its normal Merge ranges, accepted sizes, integrity behavior, and IC-count handling cannot be reused. Every IC still needs owner-approved AB input/output, declared processor/integrity behavior where applicable, and firmware-owner review before promotion.

## Standard Merge flowcharts

### SM-GENFLASH and SM-GENFLASH-V2

Used by the executable golden-backed gen_flash profiles: NT51920, NT51923, NT51926, NT51927, NT51929, NT51931, and NT51932. Bootstrap selects the packaged, hash-anchored V2 artifact for NT51917, NT51919, NT51920, NT51923, NT51926, NT51927, NT51929, NT51931, and NT51932. The owner-confirmed aliases are map-bound: NT51917 resolves its physical region-set fact to NT51927, and NT51919 resolves it to NT51929.

```mermaid
flowchart TD
    A["Select built-in Standard Merge profile"] --> B["Create blank output image with profile flash size and fill byte"]
    B --> C{"TP matches declared length (at most 0x40000)?"}
    C -- "no" --> D["Reject input"]
    C -- "yes" --> E{"DP reaches the declared source-range end?"}
    E -- "no" --> D
    E -- "yes" --> F["Extract declared DP range; warn only when total DP size is unexpected"]
    F --> G["Copy TP input range to output, sequence 100"]
    G --> H["Copy DP input range to output, sequence 200"]
    H --> I["Validate declared output ranges and overlap policy"]
    I --> J["Write output artifact"]
    J --> K["Preview/Build report records input hashes, copied ranges, warnings, and output hash"]
```

The DP source-size notes below are expected golden lengths, not exact-size execution gates. A DP input must cover the listed DP range; other total lengths are accepted with a warning. TP input remains exact-length for its declared profile range.

| IC | Output size | TP range | DP range | DP input length note |
| --- | ---: | --- | --- | --- |
| NT51917 | `0x40000` | `[0x00000, 0x35000)` | `[0x3C000, 0x40000)` | Owner-confirmed alias of NT51927; declared source length `0x200000`; alias golden regression uses NT51927 fixtures. |
| NT51919 | `0x40000` | `[0x07000, 0x40000)` | `[0x00000, 0x06000)` | Owner-confirmed alias of NT51929; declared source length `0x40000`; alias golden regression uses NT51929 fixtures. |
| NT51920 | `0x40000` | `[0x00000, 0x30000)` | `[0x3E000, 0x40000)` | Source length equals range end. |
| NT51923 | `0x40000` | `[0x00000, 0x3C000)` | `[0x3E000, 0x40000)` | Source length equals range end. |
| NT51926 | `0x40000` | `[0x00000, 0x3C000)` | `[0x3E000, 0x40000)` | Source length equals range end. |
| NT51927 | `0x40000` | `[0x00000, 0x35000)` | `[0x3C000, 0x40000)` | Declared source length `0x200000`. |
| NT51929 | `0x40000` | `[0x07000, 0x40000)` | `[0x00000, 0x06000)` | Declared source length `0x40000`. |
| NT51931 | `0x40000` | `[0x00000, 0x3C000)` | `[0x3E000, 0x40000)` | Declared source length `0x80000`. |
| NT51932 | `0x40000` | `[0x07000, 0x40000)` | `[0x00000, 0x06000)` | Declared source length `0x40000`. |

### SM-FLASHMAP-V2

Used by NT51930 through its packaged, hash-anchored canonical V2 bundle.

```mermaid
flowchart TD
    A["Select NT51930 flash-map Standard Merge profile"] --> B["Create blank 0x40000 output image"]
    B --> C["Copy TP input [0x07000, 0x40000), sequence 100"]
    C --> D["Copy DP input [0x00000, 0x06000), sequence 200"]
    D --> E["Validate ranges and write artifact"]
    E --> F["Preview/Build report"]
```

| IC | Output size | TP range | DP range | Evidence note |
| --- | ---: | --- | --- | --- |
| NT51930 | `0x40000` | `[0x07000, 0x40000)` | `[0x00000, 0x06000)` | Derived from `IC_FlashMap.xlsx 51930 TP Flashmap`; owner golden added from `merge_bin.7z`. |

### SM-GENFLASH-LD

Used by NT51928 non-NB.

```mermaid
flowchart TD
    A["Select NT51928 Standard Merge profile"] --> B["Create blank 0x80000 output image"]
    B --> C["Copy TP input [0x00000, 0x35000), sequence 100"]
    C --> D["Copy DP input [0x3C000, 0x40000), sequence 200"]
    D --> E["Copy LD input [0x40000, 0x62000), sequence 300"]
    E --> F["Validate ranges and write artifact"]
    F --> G["Preview/Build report"]
```

### SM-950-951-DP-PERSPECTIVE

Used by NT51950 and NT51951. This flow is an executable built-in Standard Merge profile with owner golden output for current `0x40000` and `0x80000` DP Perspective cases.

```mermaid
flowchart TD
    A["Select NT51950/NT51951 DP Perspective merge policy"] --> B{"DP input is 0x40000, 0x80000, or 0x100000?"}
    B -- "no" --> C["Reject DP input length"]
    B -- "yes" --> D["Create output image with selected DP length"]
    D --> E{"TP input contains 0x0A000-0x36FFF?"}
    E -- "no" --> F["Reject TP input length"]
    E -- "yes" --> G["Copy supplied DP bytes at offset 0"]
    G --> H["Overlay TP into 0x0A000-0x36FFF (len 0x2D000)"]
    H --> I["Do not overwrite customer info 0x37000-0x37FFF (len 0x1000) by TP overlay"]
    I --> J["Write artifact with selected DP length"]
```

## DP Replace flowcharts

### R-DP-GENERIC

This is the generic Replace composition shape. Real per-IC DP maps are still pending unless a profile explicitly declares the DP or LD partitions.

```mermaid
flowchart TD
    A["Load reference/base firmware"] --> B["Clone to mutable work image"]
    B --> C["Load DP replacement and optional LD replacement"]
    C --> D{"Replacement input fits declared DP/LD length?"}
    D -- "larger" --> E["Reject oversize input"]
    D -- "shorter or exact" --> F["Pad shorter DP/LD inputs with profile padding byte when declared"]
    F --> G["Replace declared DP and LD partitions"]
    G --> H["No combiner stage unless the profile declares a processor"]
    H --> I["Preview/Build report and history entry"]
```

### R-DP-950-951

Used by NT51950 and NT51951 as the target DP Perspective policy. The V2 profiles are runtime-admitted by archived owner-approved legacy full-byte comparison and public synthetic expected hashes with no known deviations; the workbench route has no legacy fallback. This migration evidence is not an independent hardware golden or a product support claim.

```mermaid
flowchart TD
    A["Load NT51950/NT51951 reference firmware"] --> B{"Reference length is 0x40000, 0x80000, or 0x100000?"}
    B -- "no" --> C["Reject unapproved reference length"]
    B -- "yes" --> D{"Replacement DP length <= reference length?"}
    D -- "no" --> E["Reject oversize replacement"]
    D -- "yes" --> F["Clone exact reference into output work image"]
    F --> G["Pad replacement DP to the selected reference length and replace the full DP container"]
    G --> H["Restore original TP range 0x0A000-0x36FFF (len 0x2D000) from reference firmware"]
    H --> I["Keep customer info 0x37000-0x37FFF (len 0x1000) from replacement DP"]
    I --> J["Write the V2 workbench artifact and record deterministic hashes plus archived legacy-comparison evidence"]
```

## CtrlRAM Replace flowcharts

All CtrlRAM Replace flows require post-replace legacy Combiner 1.13.0 processing before the result can be treated as a finished TDDI firmware image. Command sequences are stored as structured command data, then converted to argv arrays.

### R-CTRLRAM-927

Used by NT51917, NT51927, and NT51928 non-NB.

```mermaid
flowchart TD
    A["Load reference firmware and CtrlRAM replacement bins"] --> B["Clone reference to final output image"]
    B --> C["Split TP work image from the base flash"]
    C --> D["Replace approved CtrlRAM ranges in TP work; truncate oversized CtrlRAM input with warning when profile declares it"]
    D --> E["Stage postbuild BIN files and TP work firmware"]
    E --> F{"IC num selection"}
    F -- "single" --> G["Build 7-command NT51927 MERGE_MODE + CRC32 plan"]
    F -- "2" --> H["Build 10-command NT51927 MERGE_MODE + CRC32 plan"]
    F -- "3 or cascade" --> I["Build 13-command NT51927 MERGE_MODE + CRC32 plan"]
    G --> J["Run Combiner.exe commands in order against TP work"]
    H --> J
    I --> J
    J --> K["Diff transformed TP work against declared processor write ranges"]
    K --> L["Assemble refreshed TP_FW back into the cloned final output image"]
    L --> M["Preview/Build report records warnings, command argv, changed ranges, assembly step, and final hash"]
```

### R-CTRLRAM-LEGACY-NORMAL

Used by NT51920, NT51923, and NT51926.

```mermaid
flowchart TD
    A["Load reference firmware and CtrlRAM replacement bins"] --> B["Clone reference to work image"]
    B --> C["Replace approved CtrlRAM ranges"]
    C --> D["Stage postbuild BIN files and work firmware"]
    D --> E{"IC num selection"}
    E -- "single" --> F["Build CRC_Enable single command plan"]
    E -- "cascade" --> G["Build CRC_Enable cascade command plan"]
    F --> H["Run Combiner.exe commands in order"]
    G --> H
    H --> I["Validate transformed changed ranges"]
    I --> J["Preview/Build report and history entry"]
```

### R-CTRLRAM-51932

Used by NT51919, NT51929, and NT51932.

```mermaid
flowchart TD
    A["Load reference firmware and CtrlRAM replacement bins"] --> B["Clone reference to work image"]
    B --> C["Replace approved CtrlRAM ranges"]
    C --> D["Stage postbuild BIN files and work firmware"]
    D --> E{"IC num selection"}
    E -- "single" --> F["Build NT51932BASED_NORMAL_MODE CRC8 single plan"]
    E -- "cascade" --> G["Build NT51932BASED_NORMAL_MODE CRC8 cascade plan"]
    F --> H["Run Combiner.exe commands in order"]
    G --> H
    H --> I["Validate transformed changed ranges"]
    I --> J["Preview/Build report and history entry"]
```

### R-CTRLRAM-51930

Used by NT51930 and NT51931.

```mermaid
flowchart TD
    A["Load reference firmware and CtrlRAM replacement bins"] --> B["Clone reference to work image"]
    B --> C["Replace approved CtrlRAM ranges"]
    C --> D["Stage postbuild BIN files and work firmware"]
    D --> E{"IC num selection"}
    E -- "single" --> F["Build NT51930BASED_NORMAL_MODE CRC8 single plan"]
    E -- "cascade" --> G["Build NT51930BASED_NORMAL_MODE CRC8 cascade plan"]
    G --> H["For NT51930, cascade uses current <=13 IC DiffDLM branch"]
    F --> I["Run Combiner.exe commands in order"]
    H --> I
    I --> J["Validate transformed changed ranges"]
    J --> K["Preview/Build report and history entry"]
```

### R-CTRLRAM-51950

Used by NT51950 and NT51951.

```mermaid
flowchart TD
    A["Load reference firmware and CtrlRAM replacement bins"] --> B["Clone reference to work image"]
    B --> C["Replace approved CtrlRAM ranges"]
    C --> D["Stage postbuild BIN files and work firmware"]
    D --> E{"IC num selection"}
    E -- "single" --> F["Build NT51950BASED_NORMAL_MODE CRC8 single plan"]
    E -- "cascade" --> G["Build NT51950BASED_NORMAL_MODE CRC8 cascade plan"]
    F --> H["Run Combiner.exe commands in order"]
    G --> H
    H --> I["Validate transformed changed ranges"]
    I --> J["Preview/Build report and history entry"]
```

## General Replace flowchart

### R-GENERAL

General Replace is available as a generic composition model. It becomes safe for a production IC only after protected ranges, allowed explicit-map areas, alignment rules, and processor triggers are known.

```mermaid
flowchart TD
    A["Load reference/base firmware"] --> B["Clone to mutable work image"]
    B --> C["User creates explicit source-to-target mappings"]
    C --> D{"Mappings avoid protected ranges, invalid overlap, and out-of-bounds writes?"}
    D -- "no" --> E["Reject before execution"]
    D -- "yes" --> F["Apply mappings in declared operation order"]
    F --> G{"Profile declares post-processing?"}
    G -- "yes" --> H["Run approved processor and validate changed ranges"]
    G -- "no" --> I["Write output artifact"]
    H --> I
    I --> J["Preview/Build report and history entry"]
```
