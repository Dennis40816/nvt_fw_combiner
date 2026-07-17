# CtrlRAM Replace Status Report

Status: living investigation report, not a production support claim.
Owner gate: firmware-owner confirmation is still required before CtrlRAM Replace is declared OK for release.
Last updated: 2026-07-17.

This file is the current single place to update CtrlRAM Replace experiment results and conclusions until the workflow is formally accepted. Lower-level notes may keep raw details, but status, blockers, and final interpretation should be reflected here.

## Scope

This report tracks CtrlRAM Replace only:

- named CtrlRAM slot mapping;
- staged legacy `Combiner.exe` postbuild execution;
- header/CRC/integrity byte drift after self-replacement;
- IC-specific exceptions that block an "OK" claim.

It does not claim Standard Merge parity, DP Replace parity, AB behavior, or General Replace behavior.

## Current Summary

CtrlRAM Replace is implemented as a workbench path with staged postbuild execution, but it is not yet production-cleared for all released ICs.

The owner-confirmed base contract is a TP BIN work image. Product evidence is
assembled as Initial DP + original TP, TP-only Replace, then Initial DP +
updated TP. A full FlashCode must not be passed through TP-relative operations.

Highlighted conclusion:

```text
NT51926 and NT51930 are versioned postbuild-category cases. Use the base BIN FWConfig Common FW version before selecting a postbuild profile: NT51926 Common FW 1.4.1 uses the 1.4.1 reference, NT51926 Common FW 2.0.0 uses the 2.0.0 reference, NT51930 Common FW 1.x.x uses the archived 1.4.0 reference, and NT51930 Common FW 2.0.0 uses the 2.0.0 reference.
```

The currently visible version-crossing issue is concentrated in ICs whose postbuild changed between 1.4.x/1.x.x and 2.0.0. The known differences are header-copy range/length/command-count changes:

- NT51926: `1.4.1` copies `0x0 -> 0x32F50`, length `0x100`; `2.0.0` copies `0x0 -> 0x32A70`, length `0x100`.
- NT51930: `1.4.0` copies `0x7000 -> 0x28FB0`, length `0x100` once; `2.0.0` copies `0x7000 -> 0x28FB0`, length `0x200` and then runs a second header-only command.

FWConfig can now be used as the preferred postbuild-category signal for the base/TP work image:

- The owner-provided `ap_fwconfig.c` reference is preserved at `docs/references/ic-flashmap/common-fw/ap_fwconfig.c`; only its sanitized source label and SHA-256 hash are recorded in `SOURCE_MANIFEST.json`.
- `ST_PUB_FW_CONFIG.gstFwSettings` keeps Common FW version at offsets `+0x01A/+0x01B/+0x01C`, FW version/bar at `+0x000/+0x001`, FW sub-version at `+0x011`, and Project ID at `+0x022` as little-endian `UINT16`.
- Current golden reads confirm NT51926 is Common FW `1.4.1`; 2.0.0-family golden cases read as `2.0.0`.
- This supports treating the 1.x.x vs 2.0.0 mismatch as historical MD/postbuild codebase drift, not a Replace overlay-size bug.

Known stable conclusions:

- The current normalized postbuild catalog matches the inspected owner BAT files for the ICs listed below.
- The adapter stages selected CtrlRAM replacement BINs as virtual `BIN/*.bin` sources, runs the approved legacy combiner on a host-created work image, and imports only declared writes.
- Empty `map.txt` is sufficient for the observed no-overlay normal/NT-based postbuild smoke runs. This is a practical staging conclusion for the current golden cases, not a proof for overlay-enabled firmware.
- For currently covered normal/NT-based no-overlay NT519xx references, header-copy size is correct when matched to the same codebase family: `0x100` for the observed 1.x.x/CRC_Enable-style headers and `0x200` for the observed 2.0.0 NT-based headers. NT51927/NT51928 are MERGE_MODE header/copy/backup flows and are tracked separately from this no-overlay size conclusion.
- Representative tests show staged-source pasteback matches the older pre-pasted work-image model for NT51920, NT51923, NT51926, NT51927, and NT51950.
- For NT51920, NT51923, NT51929, NT51932, NT51950, and NT51951, the Standard Merge sample matrix has the expected 16-byte CRC/header-word self-replacement pattern. The same-product NT51929 AB first half separately produces 15 changed bytes because one byte in the second CRC word already matches; both observations remain valid for their own fixture.
- For NT51927 and NT51928, drift is still CRC/header-word based, but more than 16 bytes because the flow updates multiple header/copy/backup windows.
- Keep the CRC-changing postbuild behavior for production. It is acceptable only when the diff is constrained to declared CRC/header words or documented header-copy windows for the selected category.
- NT51926 and NT51930 postbuild-category selection is implemented in Preview/Build and in workbench/UI slot/range display after a base BIN is loaded, but not production-closed until matching expected golden outputs and firmware-owner parity review are complete.
- NT51930 Common FW 1.x keeps numeric `2..29` on the approved cascade command shape with `DiffDLM` length `0xFE00`. The earlier `0x23000` section is archived evidence only and must not execute without new owner approval.
- NT51931 is not closed.
- The 2026-07-17 owner-approved snapshot supplies single/cascade evidence for
  NT51920/NT51923 and 1.4.1 NT51926, single evidence for NT51927/NT51929, and
  diagnostic inputs for NT51930/NT51931/NT51932. It does not promote support.

Do not use this stronger conclusion:

```text
All IC CtrlRAM self-replacement differences are known-good header CRC drift.
```

Use this narrower conclusion instead:

```text
All currently classified CtrlRAM self-replacement differences are either known header/CRC drift or explicitly documented exceptions. NT51926, NT51930, and NT51931 remain blockers for a blanket CtrlRAM Replace OK claim.
```

## Evidence Sources

Primary files:

- `src/NvtFwCombiner.Application/ExternalTools/LegacyCombinerPostbuildCatalog.cs`
- `src/NvtFwCombiner.Application/FlashMaps/TpFlashMapCatalog.cs`
- `src/NvtFwCombiner.Infrastructure/ExternalTools/LegacyCombinerPostbuildProcessor.cs`
- `external-tools/legacy-combiner/1.13.0/Combiner.exe`
- `docs/references/ic-flashmap/postbuild/*.bat`
- `docs/references/ic-flashmap/mmap/*.h`
- `docs/references/ic-flashmap/common-fw/ap_fwconfig.c`
- `docs/references/ic-flashmap/IC_FlashMap_20260705.xlsx`
- `testdata/golden/standard-merge-gen-flash/manifest.json`
- `testdata/golden/ctrlram-replace/manifest.json`
- `testdata/golden/ctrlram-replace/manifest.20260717.json`

Supporting notes:

- `docs/architecture/ctrlram-postbuild-command-matrix.md`
- `docs/architecture/ctrlram-postbuild-original-pasteback.md`
- `docs/architecture/ctrlram-postbuild-investigation-reference.md`
- `docs/architecture/integrity-processing-matrix.md`

## Execution Model

Current workbench model:

```text
base TP work image
-> stage selected CtrlRAM replacement BIN bytes as virtual BIN/*.bin sources
-> stage unselected postbuild BIN bytes from the base TP work image
-> run normalized legacy Combiner.exe commands
-> Combiner performs pasteback plus header/CRC refresh
-> host validates changed ranges and imports only approved output bytes
```

The current base input is the combiner TP work image size used by the postbuild command offsets. If a future UI or CLI accepts a larger full-flash container, the flow must explicitly slice the owner-confirmed TP range, run this postbuild model on that TP slice, and reinsert the processed TP slice into the full-flash output.

## Postbuild Catalog Status

| IC | Current command family | Source evidence | Current status |
| --- | --- | --- | --- |
| NT51917 | NT51927 alias: `MERGE_MODE` + `NT51927BASED_GEN_CRC_MODE CRC32` | owner alias confirmation | Alias only; direct CtrlRAM Replace golden still optional evidence. |
| NT51919 | NT51929/NT51932 alias: `NT51932BASED_NORMAL_MODE CRC8` | owner alias confirmation | Alias only; direct CtrlRAM Replace golden still optional evidence. |
| NT51920 | `CRC_Enable` | inspected BAT + 2026-07-17 owner snapshot | Single/cascade formal payload bytes match their declared targets; owner command/range review remains. |
| NT51923 | `CRC_Enable` | inspected BAT + 2026-07-17 owner snapshot | Single/cascade formal payload bytes match their declared targets; owner command/range review remains. |
| NT51926 | `CRC_Enable` | inspected 1.4.1/2.0.0 BAT + owner snapshots | Workbench selects from TP FWConfig. The 1.4.1 single/cascade payloads match; 2.0.0 evidence remains separate. |
| NT51927 | `MERGE_MODE` + `NT51927BASED_GEN_CRC_MODE CRC32` | inspected BAT + owner snapshots | Single has direct real-tool evidence and TP-only product replay; two/three-chip residuals remain confined to header/CRC ranges. |
| NT51928 non-NB | NT51927 alias flow | owner alias confirmation | Non-NB only; NB is not covered. |
| NT51929 | `NT51932BASED_NORMAL_MODE CRC8` | inspected BAT + 2026-07-17 owner snapshot | A true non-AB single expected output now exists; its NF/Normal/VN target bytes match. The AB half is not the single golden. |
| NT51930 | `NT51930BASED_NORMAL_MODE CRC8` | inspected BAT + 2026-07-17 owner snapshot | NF/Normal/MP/VN match, but one DiffDLM 4 KiB slot has 4,090 mismatches; not closed. |
| NT51931 | owner BAT: `NT51931BASED_NORMAL_MODE CRC8` | 2026-07-17 owner BAT | BAT consumes only `0xC800` DiffDLM bytes and calls missing `InsertSID.py`; prior run still has 108 unexplained bytes. Replace stays Not Supported. |
| NT51932 | `NT51932BASED_NORMAL_MODE CRC8` | inspected BAT + 2026-07-17 owner snapshot | Normal/VN/NF prefix match, but DiffDLM has a 4,095-byte mismatch and the completed DiffNFMerge NF is not proven. |
| NT51950 | `NT51950BASED_NORMAL_MODE CRC8` | inspected BAT | Catalog and 16-byte drift understood. |
| NT51951 | NT51950 alias flow | owner alias confirmation | Catalog and 16-byte drift understood through alias. |

## Experiment Log

### 1. Direct VN-only self-pasteback

Purpose: verify that per-region staged source/destination/length wiring can be no-op when only the selected CtrlRAM source is passed through combiner.

Result summary:

| IC | Result | Interpretation |
| --- | --- | --- |
| NT51920 | 0 diff bytes | Expected no-op. |
| NT51923 | 0 diff bytes | Expected no-op. |
| NT51926 | 0 diff bytes | Expected no-op. |
| NT51927 | 0 diff bytes after host-like normalization | Expected no-op with merge normalization. |
| NT51928 | 0 diff bytes after host-like normalization | Expected no-op with merge normalization. |
| NT51929 | 0 diff bytes | Expected no-op. |
| NT51930 | 2853 diff bytes | Postbuild-version mismatch candidate: current `2.0.0` catalog uses a longer header-copy/second-command shape than the 1.4.0-like golden. |
| NT51931 | access violation with official BAT-shaped family | Investigation blocker. |
| NT51932 | 0 diff bytes | Expected no-op. |
| NT51950 | 0 diff bytes | Expected no-op. |
| NT51951 | 0 diff bytes | Expected no-op. |

This experiment is diagnostic only. Real CtrlRAM Replace still must run the full IC-specific postbuild sequence.

### 2. Full single-branch self-replacement using Standard Merge golden output

Purpose: run the full single-branch postbuild sequence on an already-final golden image, with CtrlRAM bytes sliced from that same image.

| IC | Diff bytes | Classification | Key ranges |
| --- | ---: | --- | --- |
| NT51920 | 16 | Expected CRC-generation drift | `0x1C..0x20`, `0xFC..0x100`, `0x2669C..0x266A0`, `0x2677C..0x26780` |
| NT51923 | 16 | Expected CRC-generation drift | `0x1C..0x20`, `0xFC..0x100`, `0x3032C..0x30330`, `0x3040C..0x30410` |
| NT51926 | 251 | Postbuild-version mismatch candidate | `2.0.0` run changes `0x1C..0x20`, `0xFC..0x100`, most of `0x32A70..0x32B70`; supplied base already matches the `1.4.1` target near `0x32F50` except CRC words |
| NT51927 | 24 | Expected CRC/header-word drift | six 4-byte header words near `0x1E26C`, `0x1E27C`, `0x32FDC..0x33010` |
| NT51928 | 64 | Expected CRC/header-word drift | 4-byte words near `0x23C`, `0x24C`, `0x1E24C..0x1E2B0`, `0x32FDC..0x33040` |
| NT51929 | 16 | Expected CRC-generation drift | `0x7100..0x7104`, `0x7118..0x711C`, `0x27FF0..0x27FF4`, `0x28008..0x2800C` |
| NT51930 | 2901 | Postbuild-version mismatch candidate | `2.0.0` run includes `0x7100`, `0x7118`, `0x28FB0` header area, and extensive `0x32000` changes; 51930 golden header-copy target matches `1.4.0` length `0x100` much more closely than `2.0.0` length `0x200` |
| NT51931 | no output diff | Official command crashes | exit `0xC0000005` with no stdout/stderr |
| NT51932 | 16 | Expected CRC-generation drift | `0x7100..0x7104`, `0x7118..0x711C`, `0x27FF0..0x27FF4`, `0x28008..0x2800C` |
| NT51950 | 16 | Expected CRC-generation drift | `0xA11C..0xA120`, `0xA130..0xA134`, `0x2D428..0x2D42C`, `0x2D43C..0x2D440` |
| NT51951 | 16 | Expected CRC-generation drift | same as NT51950 |

The NT51929 AB case is intentionally recorded separately from that Standard Merge table. Its tracked 512 KiB expected output has SHA-256 `c7e1e263...3d66abe2`; `[0x00000,0x40000)` has SHA-256 `e257e734...1127c12`. NF `[0x1FC00,0x21B90)`, Normal `[0x21B90,0x26590)`, and VN `[0x26590,0x27EF0)` match the same-product TPFW byte-for-byte. Running the exact pinned Combiner 1.13.0 NT51929 single command changes 15 bytes at `[0x7100,0x7104)`, `[0x7118,0x711B)`, `[0x27FF0,0x27FF4)`, and `[0x28008,0x2800C)`; the owner-approved allowed-diff classes are Header CRC and Header Copy CRC. This does not establish full-byte single parity and does not create a standalone `expected.bin`.

### 3. Workbench self-replacement with committed 2026-07-05 CtrlRAM fixtures

Owner supplied NT51926/NT51927 CtrlRAM Replace fixtures on 2026-07-05. The payloads are committed under `testdata/golden/ctrlram-replace/fixtures/20260705`.

| Case | Workbench result | Diff classification | Current conclusion |
| --- | --- | --- | --- |
| NT51926 cascade | Build succeeds; FWConfig reads Common FW `1.4.1` and report trace uses the `1.4.1` `0x32F50` header-copy target | postbuild-category now matches the fixture codebase; final expected output still absent | Postbuild execution and version selection work. The committed self-replacement VN input is sliced to the archived `1.4.1` length `0x1660`; parity promotion still needs owner expected output. |
| NT51927 2-chip | Build succeeds; 100 changed bytes across 25 ranges | all observed ranges are header/integrity words in main/header-copy/final-backup areas | CtrlRAM payload placement looks correct; final byte parity needs matching owner expected output for the 2-chip branch. |
| NT51927 3-chip | Build succeeds; 105 changed bytes across 30 ranges | all observed ranges are header/integrity words in master/right/left header-copy and final-backup areas | CtrlRAM payload placement looks correct; final byte parity needs matching owner expected output for the 3-chip branch. |

Report verification gate: committed golden-backed self-replacement tests now inspect the generated Replace report, not only the final output bytes. DP self-replacement must leave `OutputDifferences` empty. CtrlRAM postbuild self-replacement may emit only accepted `PostbuildCrcHeader` rows, with no `report.output-difference.unexpected` issue; a second self-replacement from the postbuild-clean output must return to an empty difference table.

The 2026-07-05 NT51927 synthetic sentinel run replaced every selected 2-chip and 3-chip CtrlRAM slot with non-golden byte patterns. The 2-chip branch stayed within the existing declarations. The 3-chip branch exposed additional CRC-only main-header word writes at `[0x22C,0x230)`, `[0x29C,0x2A0)`, and `[0x2AC,0x2B0)` that self-replacement did not necessarily surface. These ranges align with the `51927_1.4.1_mmap.h` cascade header CRC offsets and descriptor/header CRC-word rule below, so the allowed-write catalog declares them only for the 3-chip/cascade branch. This remains R3 firmware evidence and requires firmware-owner review before production-support promotion.

NT51930 Standard Merge golden cross-check:

- `PostbuildSetup_51930_1.4.0.bat` uses `output\nt51930_fw.bin 0x7000 0x28FB0 256`.
- `PostbuildSetup_51930_2.0.0.bat` derives `HEADER_SZ = 0x200` from `51930_2.0.0_mmap.h` and runs both a merge command and a second header-only command.
- In the current 51930 golden output, comparing `[0x7000,0x7200)` to `[0x28FB0,0x291B0)` gives 1 differing byte for the first `0x100`, but 40 differing bytes for `0x200`. This supports treating the golden as 1.4.0-era evidence.

No-overlay/header-copy size cross-check:

| IC family | Matching reference | Header-copy size conclusion |
| --- | --- | --- |
| NT51920 | `PostbuildSetup_51920_1.3.1.bat` | `0x100`; golden header table also reports `0x100`. |
| NT51923 | `PostbuildSetup_51923_1.4.1.bat` | `0x100`; golden header table also reports `0x100`. |
| NT51926 | `PostbuildSetup_51926_1.4.1.bat` and `PostbuildSetup_51926_2.0.0.bat` | Size remains `0x100`; mismatch is target address/codebase, not size. |
| NT51929 / NT51932 | `PostbuildSetup_51932_2.0.0.bat` | `0x200`; this is the 2.0.0 NT-based header size. |
| NT51930 | `PostbuildSetup_51930_1.4.0.bat` and `PostbuildSetup_51930_2.0.0.bat` | `0x100` for 1.4.0 evidence; `0x200` for 2.0.0 evidence. Use FWConfig/category before choosing. |
| NT51931 | `PostbuildSetup_51931_1.3.0.bat` | `0x100` in official reference, but official execution still crashes with Combiner 1.13.0. |
| NT51950 / NT51951 | `PostbuildSetup_51950_2.0.0.bat` | `0x200`; this is the 2.0.0 NT-based header size. |

Current conclusion: for normal/NT-based no-overlay postbuild, the size data is consistent when matched to the correct codebase family. Auto mode may use FWConfig Common FW version to choose the postbuild category and may keep the empty `map.txt` staging model for this no-overlay category. It must still fail closed for overlay-enabled or unclassified firmware.

FWConfig golden reads:

| IC | FWConfig start | Common FW | FW/bar | PID |
| --- | ---: | --- | --- | --- |
| NT51920 | `0x22000` | `1.2.0` | `0x01/0xFE` OK | `0x1404` |
| NT51923 | `0x22000` | `1.3.0` | `0x06/0xF9` OK | `0x1606` |
| NT51926 | `0x22000` | `1.4.1` | `0x01/0xFE` OK | `0x5102` |
| NT51927 | `0x16000` | `1.4.1` | `0x02/0xFD` OK | `0x1348` |
| NT51928 | `0x16000` | `1.3.2` | `0x84/0x7B` OK | `0xF206` |
| NT51929 | `0x1F200` | `2.0.0` | `0x01/0xFE` OK | `0x1707` |
| NT51930 | `0x1F200` | `1.3.0` | `0x04/0xFB` OK | `0x110D` |
| NT51931 | `0x16000` | `1.3.0` | `0x82/0x7D` OK | `0x131B` |
| NT51932 | `0x1F200` | `2.0.0` | `0x80/0x7F` OK | `0x4801` |
| NT51950 | `0x22200` | `2.0.0` | `0x04/0xFB` OK | `0x135E` |
| NT51951 | `0x22200` | `2.0.0` | `0x03/0xFC` OK | `0x5901` |

TP Overview evidence notes:

- The owner workbook now carries explicit postbuild codebase/category notes for ICs where Common FW `1.x.x` and `2.0.0` use different header copy behavior.
- NT51926 now has two documented TP Overview sections: Common FW `1.4.1` uses header copy `0x0 -> 0x32F50`, length `0x100`, VN length `0x1660`, and FWConfig backup length `0x800`; Common FW `2.0.0` uses `0x0 -> 0x32A70`, length `0x100`, VN length `0x149E`, and FWConfig backup length `0x780`. The current golden/base evidence reads Common FW `1.4.1`.
- NT51926 `1.4.1` committed BIN evidence has one little-endian end-flag marker (`00 4E 56 54`) at `0x3BFFC`; `0x34FFC` is `00 00 00 00` in the 2026-07-05 base, Standard Merge TP input, and expected flash. Treat `0x34FFC` as the 2.0.0 mmap/TP Overview `FLASHMAP_ENDFLAG` row, not as the actual marker location for the current `1.4.1` fixture.
- NT51930 has documented TP Overview category notes: Common FW `1.4.0/1.x.x` evidence uses `0x7000 -> 0x28FB0`, length `0x100`, single postbuild command, consumes `MP_Ctrlram.bin`, and uses VN length `0x195E`; numeric cascade `2..29` uses the approved `DiffDLM` length `0xFE00`. Common FW `2.0.0` uses length `0x200`, includes a second header-only command, and does not currently consume `MP_Ctrlram.bin`. The current Standard Merge golden reads Common FW `1.3.0`, so it must not be validated against the 2.0.0 row.
- The workbench uses default TP Overview rows before a base image is loaded, then refreshes visible replaceable CtrlRAM slots after FWConfig category selection so NT51930 `1.x.x` exposes MP as consumed and NT51926 `1.4.1` exposes the correct VN/FWConfig lengths.
- TP Overview should include the primary `FLASHMAP_FW_REGISTER` start per IC because UI traceability and postbuild-category selection now read Common FW/FW/PID from FWConfig.
- A 2026-07-06 TP Overview end-flag audit checked the row immediately before each `end_flag (0x00,N,V,T)` section:

  | IC / section | TP Overview row before end flag | Half-open range | Catalog status |
  | --- | --- | --- | --- |
  | NT51920 | FW Config Backup | `[0x2F000,0x2F780)` | `fw-config-backup`, protected traceability row |
  | NT51923 | FW Config | `[0x3B000,0x3B800)` | `fw-config-backup` block id for postbuild alignment, display label stays `FW Config` because the workbook does not call it backup |
  | NT51926 `2.0.0` / `1.x.x` | Workbook row shows `0x34FFC` as the end flag, but the committed `1.4.1` BIN marker is only at `0x3BFFC`; FW Config Backup is at `0x3B000` | category-specific `[0x3B000,0x3B780)` or `[0x3B000,0x3B800)` | documented exception, selected by Common FW category; do not infer the actual 1.4.1 marker from the 2.0.0 mmap row |
  | NT51927 / NT51928 / NT51917 | FW Config/Reg Backup | `[0x34000,0x34800)` | `fw-config-reg-backup`, protected traceability row |
  | NT51929 / NT51932 / NT51919 | FW Information | `[0x3F000,0x3FFFC)` | `fw-information`, protected traceability row; not backup |
  | NT51930 | FW Information For Host | `[0x3F000,0x3FFFC)` | `fw-information-host`, protected traceability row; not backup |
  | NT51931 | FW Config Backup | `[0x3B000,0x3B800)` | `fw-config-backup`, protected traceability row |
  | NT51950 / NT51951 | FW Information for Host | `[0x36000,0x36FFC)` | `fw-information-host`, protected traceability row; not backup |

- Allowed-write ranges must follow the selected postbuild command's full declared CRC/header/header-copy blocks. Do not carve a PID byte out of a declared header-copy block; CtrlRAM Replace does not run a separate Insert PID stage, and any PID-byte drift inside a wrong-version header-copy target is part of the postbuild-version mismatch evidence.
- NT51931 remains fail-closed. The newest owner BAT corrects the intended mode
  to `NT51931BASED_NORMAL_MODE` and the DiffDLM consumption to `0xC800`, but
  also depends on a missing `InsertSID.py`; the existing 108-byte drift is not
  resolved.

NT51927 flash-header cross-check:

- `IC_FlashMap_20260705.xlsx` records `0x00200` Common Header, `0x00220` Flash Header, `0x1E230` Master header copy, `0x27230` Slave R header copy, `0x30230` Slave L header copy, and `0x32DC0` Header backup.
- `51927_1.4.1_mmap.h` records cascade header CRC offsets.
- Observed changed words align with 16-byte descriptor CRC positions (`descriptor + 0x0C`), including split 3-chip diff ranges where one byte in a 4-byte word happened to match.
- Synthetic sentinel replacement confirmed the real Combiner 1.13.0 path can update additional 3-chip main-header CRC words that self-replacement did not necessarily surface.

### 4. NT51931 corrected command evidence, incomplete toolchain

Current repo reference for NT51931 is `1.3.0`, not `2.0.0`:

- `docs/references/ic-flashmap/mmap/51931_1.3.0_mmap.h`
- `docs/references/ic-flashmap/postbuild/PostbuildSetup_51931_1.3.0.bat`

The earlier repository BAT-shaped command was:

```text
Combiner.exe NT51930BASED_NORMAL_MODE CRC8 ...
```

Observed with Combiner 1.13.0 and the current NT51931 Standard Merge golden:

```text
ExitCode=-1073741819
Hex=0xC0000005
STDOUT=<empty>
STDERR=<empty>
DiffBytes=0
```

The 2026-07-17 owner BAT instead declares:

```text
Combiner.exe NT51931BASED_NORMAL_MODE CRC8 ...
```

Observed:

```text
ExitCode=0
FW Merge is OK
DiffBytes=108
```

Changed ranges from the diagnostic run:

```text
0x1C-0x20
0xFC-0x100
0x1DA48-0x1DA50
0x1DA6C-0x1DA70
0x1DA7C-0x1DA80
0x1DA8C-0x1DA90
0x1DA9C-0x1DAE8
0x1DB2C-0x1DB30
```

Interpretation:

- `NT51931BASED_NORMAL_MODE` avoids the crash with Combiner 1.13.0.
- The owner BAT consumes `0xC800` bytes from DiffDLM rather than the previously
  modeled `0x17C00` and calls `InsertSID.py` before Combiner.
- `InsertSID.py` and its produced input state are absent from the evidence.
- The 108-byte drift is in main header / `0x1DA30` header-copy area, but it is not CRC-only.
- This resembles a header-copy target state issue, not a finished proof that production should use `NT51931BASED_NORMAL_MODE`.
- Correcting diagnostic facts does not unlock runtime support. NT51931
  CtrlRAM Replace remains Not Supported.

### 5. Header-copy `0xFF` prefill mode assessment

Owner question: should CtrlRAM Replace support a mode that pre-fills header-copy target windows with `0xFF` before running postbuild, so self-replacement can become byte-identical?

Current decision:

```text
Do not implement header-copy prefill as a production mode.
```

Reasoning:

- It does not answer the real NT51926/NT51930 issue. Current evidence points to postbuild-version mismatch: 1.x.x-era golden files are being tested with 2.0.0 postbuild shape.
- Combiner header-copy commands overwrite the target before CRC/header recalculation. When the correct postbuild version is selected, target prefill should usually be irrelevant.
- If any byte in the prefilled target is not overwritten, the mode can erase meaningful prior-generation header state and weaken golden evidence.
- It would add a visible mutation before the external processor, so reports, allowed-write ranges, and golden explanations must all account for it.

Allowed future use:

- Diagnostic-only experiment against a staging copy.
- Output only a report comparing baseline postbuild vs prefilled preimage postbuild.
- Never produce a production Build artifact, never update expected golden hashes from this mode, and never relax allowed-write ranges.

If owner later insists on production behavior, treat it as R3 firmware behavior: add an ADR/contract, declare the prefill as an ordered profile operation over a half-open range, require matching owner golden expected outputs for each postbuild version, and prove wrong-version cases fail closed.

## Current Blockers Before "CtrlRAM Replace OK"

| Blocker | Impact | Needed evidence/decision |
| --- | --- | --- |
| NT51926 2.0.0 | 1.4.1 single/cascade expected outputs and inputs are present. | 2.0.0 single/cascade evidence or explicit v0.9.9 scope exclusion. |
| NT51930 DiffDLM | One 4 KiB target slot has 4,090 mismatching bytes. | Same-run DiffDLM or command/log explaining the supplied variants. |
| NT51931 incomplete toolchain | Corrected owner BAT depends on missing `InsertSID.py`; existing output has 108 unexplained bytes. | Keep Replace Not Supported. |
| NT51932 DiffDLM/DiffNFMerge | One 4 KiB target slot has 4,095 mismatching bytes; NF composite is not proven. | Same-run DiffDLM and completed NF output or equivalent log/hash proof. |
| Full FlashCode input | The owner-confirmed Replace base is TP BIN only. | Reject full FlashCode unless a separate TP slice/reinsert contract is approved. |
| Remaining release-scope cases | NT51950 cascade and NT51951 corrected single/cascade remain missing or invalid. | Matching owner inputs/final, command/tool authority, and R3 review. |

## Update Rules

Update this report whenever any of these change:

- a postbuild command family, argument order, command count, or IC alias;
- a flash-map, mmap, header-copy, CRC/header, or allowed-write range;
- a real-tool crash, timeout, or stdout/stderr diagnostic;
- a new owner CtrlRAM Replace golden fixture or expected output;
- a decision about NT51926, NT51930, NT51931, or full-flash slicing;
- a support claim moves from investigation to release scope.

Each update should record:

- IC and IC number branch;
- source evidence file or fixture manifest entry;
- exact combiner mode and version/hash;
- input/output sizes and hashes when available;
- changed ranges and classification;
- whether firmware-owner review is still required.

## Current Release Statement

As of this report:

```text
CtrlRAM Replace execution is traceable and its base contract is TP BIN only, but it is not globally OK for release. The 2026-07-17 owner snapshot closes several completely-missing sample gaps. NT51930/NT51932 composition gaps remain, NT51931 Replace stays Not Supported, and every migrated family still needs owner R3 review before runtime promotion.
```
