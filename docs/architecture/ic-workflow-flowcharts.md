# IC Workflow Flowchart Reference

Status: architecture reference; evidence dates are recorded by the cited manifests and review history.

This document is an index for the current Merge and Replace flows by IC. It is not a production support claim. A profile is production-ready only after profile validation, golden regression, processor diff review, and owner sign-off. The implementation runbook for adding a new IC workflow is [`adding-ic-merge-replace-workflow.md`](adding-ic-merge-replace-workflow.md).

## Update rule

Update this document in the same change when any of these sources change:

- `src/NvtFwCombiner.Bootstrap/BuiltInV2RegistrationRegistry.cs`; this owns the explicit Standard Merge, AB pilot, and DP Replace registration lists plus non-routed General Merge candidates.
- `src/NvtFwCombiner.Bootstrap/BuiltInV2Bundle.cs`; production profiles are manifest-pinned V2 bundles. Synthetic compiler fixtures remain test-only under `tests/NvtFwCombiner.TestSupport/`.
- `src/NvtFwCombiner.Bootstrap/CtrlRamV2RouteRegistry.cs`; this binds each admitted runtime postbuild profile and typed Number plan to one trusted V2 profile.
- `profiles/built-in/ctrlram-postbuild-v2/catalog.json`; Infrastructure validates its pinned hash and projects typed runtime profiles.
- [`../adr/0031-ctrlram-profile-intervals-and-build-plan-authority.md`](../adr/0031-ctrlram-profile-intervals-and-build-plan-authority.md)
- `docs/architecture/nt51950-nt51951-dp-length-policy.md`
- `docs/architecture/ctrlram-postbuild-command-matrix.md`
- `docs/architecture/supported-ic-matrix.md`
- `docs/architecture/adding-ic-merge-replace-workflow.md`

The architecture test `IcWorkflowFlowchartReferenceCoversBuiltInIcLists` checks that this document lists every IC from the built-in Standard Merge profiles and the CtrlRAM postbuild catalog. The test is a sync guard only; the C# catalog and owner evidence remain the source of behavior truth.

## Notation

- Ranges use half-open notation: `[start, end)`.
- `Implemented profile` means the repo has an executable built-in profile or command catalog entry. It does not imply production parity.
- `Golden pending` means the repo has an executable profile or command catalog entry, but owner-approved golden output and firmware sign-off are still pending.

CtrlRAM production admission follows three independent decisions: requested IC selects the owner-declared family, Common FW selects an effective runtime-profile interval only when the IC has more than one runtime profile, and the requested Number selects a typed build plan. PID, filename, exact golden Common/TP FW version, whole-file SHA, and a golden fixture's observed chip count are report/evidence fields, never family or route gates. FWConfig chip count is cross-checked after plan selection and a contradiction requires an explicit user decision; it does not silently select another plan.

## Per-IC flow index

| IC | Standard Merge flow | DP Replace flow | CtrlRAM Replace flow | General Replace flow | Current status notes |
| --- | --- | --- | --- | --- | --- |
| NT51917 | `SM-GENFLASH-V2-ALIAS`: packaged canonical V2 bundle is selected by Bootstrap UI/CLI through its content-hash anchor; map-bound alias of NT51927. | `R-DP-GENFLASH-V2`: hash-anchored DP Replace profile is routed. | `R-CTRLRAM-927-V2-ALIAS`: one `[1.0.0, infinity)` runtime profile exposes single, exact 2-chip, and exact 3-chip V2 plans. | `R-GENERAL-POSTBUILD`: non-exact General Replace shapes fail closed. | Exact FW/PID/SHA values identify regression cases only. The NT51917 staged identity and 7/10/13-command plans remain support-neutral. |
| NT51919 | `SM-GENFLASH-V2-ALIAS`: packaged canonical V2 bundle is selected by Bootstrap UI/CLI through its content-hash anchor; map-bound alias of NT51929. | `R-DP-GENFLASH-V2`: hash-anchored DP Replace profile is routed. | `R-CTRLRAM-51929-ALIAS`: the `[1.0.0, infinity)` single and generic cascade plans are V2-routed. | `R-GENERAL-POSTBUILD`: non-exact General Replace shapes fail closed. | `AB-51929-FAMILY-PILOT` is runtime/CLI routed through the approved NT51929 fact scope; UI and release gates remain open. |
| NT51920 | `SM-GENFLASH-V2`: packaged canonical V2 bundle is selected by Bootstrap UI/CLI through its content-hash anchor. | `R-DP-GENFLASH-V2`: hash-anchored DP Replace profile is routed. | `R-CTRLRAM-LEGACY-NORMAL`: one `[1.0.0, infinity)` profile; single and generic cascade are V2-routed. | `R-GENERAL-POSTBUILD`: explicit mappings use protected-range gates; TP/CtrlRAM mappings run selected postbuild when available. | Golden values are regression evidence; firmware-owner review remains required before support promotion. |
| NT51923 | `SM-GENFLASH-V2`: packaged canonical V2 bundle is selected by Bootstrap UI/CLI through its content-hash anchor. | `R-DP-GENFLASH-V2`: hash-anchored DP Replace profile is routed. | `R-CTRLRAM-LEGACY-NORMAL`: one `[1.0.0, infinity)` profile; single and generic cascade are V2-routed, with DiffDLM only in cascade. | `R-GENERAL-POSTBUILD`: explicit mappings use protected-range gates; TP/CtrlRAM mappings run selected postbuild when available. | Golden values are regression evidence; firmware-owner review remains required before support promotion. |
| NT51926 | `SM-GENFLASH-V2`: packaged canonical V2 bundle is selected by Bootstrap UI/CLI through its content-hash anchor. | `R-DP-GENFLASH-V2`: hash-anchored DP Replace profile is routed. | `R-CTRLRAM-LEGACY-NORMAL`: `[1.0.0,2.0.0)` uses the 1.4.1-sourced profile and `[2.0.0,infinity)` uses the 2.0.0-sourced profile; both expose single and generic cascade V2 plans. | `R-GENERAL-V2-DP-SLICE`: single/full-Flash/file-backed DP-only mappings use V2; TP/CtrlRAM, patches/fills, other counts and shapes fail closed. | Missing Common FW blocks only because two runtime intervals exist. Exact golden versions do not narrow either interval; support remains neutral. |
| NT51927 | `SM-GENFLASH-V2`: packaged canonical V2 bundle is selected by Bootstrap UI/CLI through its content-hash anchor. | `R-DP-GENFLASH-V2`: hash-anchored DP Replace profile is routed. | `R-CTRLRAM-927`: one `[1.0.0, infinity)` profile exposes single, exact 2-chip, and exact 3-chip V2 plans. | `R-GENERAL-POSTBUILD`: non-exact General Replace shapes fail closed. | PID, exact Common FW, and whole-reference SHA are evidence only; command-plan distinctions come from owner-provided single/2/3 plans. |
| NT51928 | `SM-GENFLASH-LD-V2`: hash-anchored V2 route includes TP, DP, and typed auxiliary LD. | `R-DP-LDC-V2`: DP and the separate LDC `[0x40000,0x62000)` are independent required inputs and routed writes. | `R-CTRLRAM-927-PARTIAL`: owner-approved non-NB single, exact 2-chip, and exact 3-chip plans reuse the matching NT51927 TP branches in a distinct 512 KiB map. | `R-GENERAL-POSTBUILD`: non-exact General Replace shapes fail closed. | NT51928 NB remains excluded; every admitted CtrlRAM plan preserves `[0x34800,0x80000)`, so DP/LDC differences do not alter TP authority. |
| NT51929 | `SM-GENFLASH-V2`: packaged canonical V2 bundle is selected by Bootstrap UI/CLI through its content-hash anchor. | `R-DP-GENFLASH-V2`: hash-anchored DP Replace profile is routed. | `R-CTRLRAM-51932`: the `[1.0.0, infinity)` single and generic cascade plans are V2-routed. | `R-GENERAL-POSTBUILD`: non-exact General Replace shapes fail closed. | `AB-51929-FAMILY-PILOT` has direct golden parity and runtime/CLI routing. UI, final firmware confirmation, and release gates remain open. |
| NT51930 | `SM-FLASHMAP-V2`: packaged canonical V2 bundle is selected by Bootstrap UI/CLI through its content-hash anchor. | `R-DP-GENFLASH-V2`: hash-anchored DP Replace profile is routed. | `R-CTRLRAM-51930`: one `[1.0.0, infinity)` profile exposes exactly single and `2–13` V2 plans; `>=14` is unavailable. | `R-GENERAL-POSTBUILD`: explicit mappings use protected-range gates; TP/CtrlRAM mappings run selected postbuild when available. | Single excludes cascade-only DiffDLM. The 2.0.0 BAT is evidence-only and creates no runtime interval; both plans remain support-neutral. |
| NT51931 | `SM-GENFLASH-V2`: packaged canonical V2 bundle is selected by Bootstrap UI/CLI through its content-hash anchor. | `R-DP-GENFLASH-V2`: hash-anchored DP Replace profile is routed. | `R-CTRLRAM-51930`: the `[1.0.0, infinity)` single and generic cascade plans are V2-routed. | Not Supported. | The cascade-6 case is regression evidence for the generic cascade plan, not an exact-count admission gate; support remains neutral. |
| NT51932 | `SM-GENFLASH-V2`: packaged canonical V2 bundle is selected by Bootstrap UI/CLI through its content-hash anchor. | `R-DP-GENFLASH-V2`: hash-anchored DP Replace profile is routed. | `R-CTRLRAM-51932`: the `[1.0.0, infinity)` single and generic cascade plans are V2-routed. | `R-GENERAL-POSTBUILD`: explicit mappings use protected-range gates; TP/CtrlRAM mappings run selected postbuild when available. | `AB-51929-FAMILY-PILOT` is runtime/CLI routed through the approved NT51929 fact scope; UI and release gates remain open. |
| NT51950 | `SM-950-951-DP-PERSPECTIVE-V2`: packaged canonical V2 maps select the submitted DP capacity. | `R-DP-950-951`: the workbench UI/CLI routes the V2 profile and selects its base capacity; LDC is already packaged in the DP payload. | `R-CTRLRAM-51950`: the `[1.0.0, infinity)` single and generic cascade plans are V2-routed; cascade adds DiffDLM `[0x33200,0x34600)`. | `R-GENERAL-POSTBUILD`: non-exact General Replace shapes fail closed. | Single/cascade share the TP layout and postbuild offsets inside the `0x40000` container. Exact PID/version/SHA values remain evidence only; AB is separate and no support promotion is claimed. |
| NT51951 | `SM-950-951-DP-PERSPECTIVE-V2`: packaged canonical V2 maps select the submitted DP capacity. | `R-DP-950-951`: the workbench UI/CLI routes the V2 profile and selects its base capacity; LDC is already packaged in the DP payload. | `R-CTRLRAM-51950`: the `[1.0.0, infinity)` single and generic cascade plans are V2-routed; cascade adds DiffDLM `[0x33200,0x34600)`. | `R-GENERAL-POSTBUILD`: explicit mappings use protected-range gates; TP/CtrlRAM mappings run selected postbuild when available. | TP layout/postbuild offsets match NT51950 inside the distinct `0x80000` container; the extra tail remains preserved. AB and firmware-owner promotion remain separate. |

## AB Initializer Policy

All five AB V2 profiles copy the complete submitted DP container before their declared TPA/TPB overlays. The `v0.9.14` runtime/CLI pilot admits only NT51919, NT51929, and NT51932: they use the fixed `0x80000` AB map, consume declared prefixes from 512 KiB DP_AB and 256 KiB TPA/TPB inputs, and apply checked cloned-TPB relocations at `0x7164/0x7168/0x716C`. The owner approved NT51929 direct golden applicability for NT51919 and NT51932 through the exact family fact scope; UI and final release gates remain open. NT51950 and NT51951 remain non-routed candidates. They stage immutable A/B banks, apply only the declared TPB DIFF relocation in C#, then invoke Legacy Combiner 1.13.0 with the verified `NT51950BASED_MERGE_AB_MODE` command. That command does not consume `map.txt`; C# never writes the AB header CRC, and Combiner owns the declared B-header ILM/DLM/CRC mutations. NT51950 has two direct owner fixtures with full-byte parity; the owner approved their workflow-logic applicability to NT51951, whose distinct `0x80000` synthetic topology remains locked. Their distinct firmware-owner review remains required before runtime promotion.

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

### R-DP-930

Used by NT51930. This V2 route reuses the exact canonical 256 KiB Standard Merge map. Its checked-in deterministic oracle and owner Standard Merge self-replacement control admit the safety contract, while direct owner DP Replace golden parity remains **Evidence open**.

```mermaid
flowchart TD
    A["Load NT51930 Reference FlashCode"] --> B{"Reference length is exactly 0x40000?"}
    B -- "no" --> C["Reject reference"]
    B -- "yes" --> D{"Replacement covers canonical DP [0x0000, 0x6000)?"}
    D -- "no" --> E["Reject short replacement"]
    D -- "yes" --> F{"Outer length is expected 0x40000?"}
    F -- "no" --> G["Record DP_SIZE_WARNING and extract only the declared DP prefix"]
    F -- "yes" --> H["Use declared DP prefix"]
    G --> I["Clone Reference FlashCode and replace only [0x0000, 0x6000)"]
    H --> I
    I --> J["Preserve gap [0x6000, 0x7000) and TP [0x7000, 0x40000) byte-for-byte"]
    J --> K["Write V2 artifact; report direct golden gate as Evidence open"]
```

## CtrlRAM Replace flowcharts

All CtrlRAM Replace flows require post-replace legacy Combiner 1.13.0 processing before the result can be treated as a finished TDDI firmware image. Command sequences are stored as structured command data, then converted to argv arrays. Production resolves requested IC family, effective Common FW profile interval, and typed Number plan in that order. A trusted V2 profile then scopes map resolution and processor write authority; an absent route fails closed. Firmware metadata is still reported, and FWConfig chip count is cross-checked against the requested plan, but PID, exact golden versions, filename, and SHA never choose a route.

### R-CTRLRAM-927

Used by NT51917, NT51927, and NT51928 non-NB.

NT51917, NT51927, and NT51928 non-NB each expose exactly three owner-provided
command plans: single, 2-chip, and 3-chip. Their one runtime profile covers all
Common FW versions from `1.0.0`; exact FW/PID/SHA values only identify regression
cases. NT51928 reuses NT51927 TP/CtrlRAM authority while keeping its distinct
512 KiB DP/LDC image tail. NT51928 NB is not covered and fails closed.

```mermaid
flowchart TD
    A["Load reference firmware and CtrlRAM replacement bins"] --> B["Clone reference to final output image"]
    B --> C["Split TP work image from the base flash"]
    C --> D["Replace approved CtrlRAM ranges in TP work; truncate oversized CtrlRAM input with warning when profile declares it"]
    D --> E["Stage postbuild BIN files and TP work firmware"]
    E --> F{"Typed Number plan"}
    F -- "single" --> G["Build 7-command NT51927 MERGE_MODE + CRC32 plan"]
    F -- "2" --> H["Build 10-command NT51927 MERGE_MODE + CRC32 plan"]
    F -- "3" --> I["Build 13-command NT51927 MERGE_MODE + CRC32 plan"]
    G --> J["Run Combiner.exe commands in order against TP work"]
    H --> J
    I --> J
    J --> K["Diff transformed TP work against declared processor write ranges"]
    K --> L["Assemble refreshed TP_FW back into the cloned final output image"]
    L --> M["Preview/Build report records warnings, command argv, changed ranges, assembly step, and final hash"]
```

### R-CTRLRAM-LEGACY-NORMAL

Used by NT51920, NT51923, and NT51926. NT51920 and NT51923 each have one
runtime interval `[1.0.0, infinity)`. NT51926 uses `[1.0.0,2.0.0)` for its
1.4.1-sourced profile and `[2.0.0,infinity)` for its 2.0.0-sourced profile.
Every interval exposes single and generic cascade V2 plans. Exact golden
versions, PID, count, filename, and SHA do not narrow these plans.

```mermaid
flowchart TD
    A["Load reference firmware and CtrlRAM replacement bins"] --> B["Clone reference to work image"]
    B --> C["Replace approved CtrlRAM ranges"]
    C --> D{"Select effective Common FW interval; only NT51926 has two"}
    D --> E["Stage postbuild BIN files and work firmware"]
    E --> F{"Typed Number plan"}
    F -- "single" --> G["Compile the interval's V2 single profile"]
    F -- "cascade" --> H["Compile the interval's V2 cascade profile"]
    G --> I["Run Combiner.exe commands in order"]
    H --> I
    I --> J["Validate transformed changed ranges"]
    J --> K["Preview/Build report and history entry"]
```

### R-CTRLRAM-51932

Used by NT51919, NT51929, and NT51932.

Each has one runtime profile from `1.0.0` and routes both single and generic
cascade. Single profiles exclude cascade-only DiffDLM authority; cascade profiles
include the declared DiffDLM range. Direct fixture metadata remains regression
evidence and never narrows either typed plan.

```mermaid
flowchart TD
    A["Load reference firmware and CtrlRAM replacement bins"] --> B["Clone reference to work image"]
    B --> C["Replace approved CtrlRAM ranges"]
    C --> D["Stage postbuild BIN files and work firmware"]
    D --> E{"Requested IC + typed Number plan has a registered V2 route?"}
    E -- "single" --> F["Compile the IC's hash-pinned profile without DiffDLM"]
    E -- "cascade" --> G["Compile the IC's hash-pinned profile with DiffDLM"]
    F --> H["Run Combiner.exe commands in order"]
    G --> H
    H --> I["Validate transformed changed ranges"]
    I --> J["Preview/Build report and history entry"]
```

### R-CTRLRAM-51930

Used by NT51930 and NT51931.

NT51930 has one runtime interval `[1.0.0, infinity)` and exactly two plans:
single and count range `2–13`. The single V2 profile has no DiffDLM input or
write authority; only the `2–13` plan exposes the cascade-only DiffDLM region.
`>=14` is neither shown nor routed. NT51931 routes both single and generic
cascade; its cascade-6 golden is evidence for that plan, not an exact-count
production gate, and its single profile excludes cascade-only DiffDLM authority.

```mermaid
flowchart TD
    A["Load reference firmware and CtrlRAM replacement bins"] --> B["Clone reference to work image"]
    B --> C["Replace approved CtrlRAM ranges"]
    C --> D["Stage postbuild BIN files and work firmware"]
    D --> E{"Requested IC + typed Number plan"}
    E -- "NT51930 single" --> F["Compile single V2 profile without DiffDLM"]
    E -- "NT51930 2–13" --> G["Compile bounded cascade V2 profile with DiffDLM"]
    E -- "NT51931 single" --> H["Compile NT51931 single V2 profile without DiffDLM"]
    E -- "NT51931 cascade" --> L["Compile NT51931 generic cascade V2 profile"]
    E -- "NT51930 >=14" --> X["Fail closed: no registered typed plan"]
    F --> I["Run Combiner.exe commands in order"]
    G --> I
    H --> I
    L --> I
    I --> J["Validate transformed changed ranges"]
    J --> K["Preview/Build report and history entry"]
```

### R-CTRLRAM-51950

Used by NT51950 and NT51951.

Both ICs have one runtime profile covering `[1.0.0, infinity)` and route single
and generic cascade. They use identical TP offsets and processor authority;
NT51950 keeps a 256 KiB container and NT51951 keeps a 512 KiB container. Cascade
adds declared DiffDLM authority. PID, exact FW version, whole-file SHA, and golden
identity are reported but do not select either route. LDC is packaged inside DP,
and AB remains a separate workflow.

```mermaid
flowchart TD
    A["Load reference firmware and CtrlRAM replacement bins"] --> B["Clone reference to work image"]
    B --> C["Replace approved CtrlRAM ranges"]
    C --> D["Stage postbuild BIN files and work firmware"]
    D --> E{"Requested IC + typed Number plan"}
    E -- "single" --> F["Compile the IC's hash-pinned V2 profile without DiffDLM"]
    E -- "cascade" --> L["Compile the IC's hash-pinned V2 profile with DiffDLM"]
    F --> H["Run Combiner.exe commands in order"]
    H --> I["Validate transformed changed ranges"]
    I --> J["Preview/Build report and history entry"]
    L --> H
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
