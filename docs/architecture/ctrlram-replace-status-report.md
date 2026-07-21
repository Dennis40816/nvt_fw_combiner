# CtrlRAM Replace Status Report

Status: living investigation report, not a production support claim.
Owner input gate: closed by the final 2026-07-18 intake; remaining parity,
tool, route, and R2/R3 review gates are agent-owned.
Last updated: 2026-07-21.

v0.9.12 admission correction: [ADR 0030](../adr/0030-production-firmware-admission-without-golden-hashes.md)
supersedes every `reference-SHA`, `full-reference-SHA`, or "exact reference SHA" runtime-admission
statement retained in the historical experiment rows below. Those hashes remain exact golden/parity
evidence only. Production routes select declared IC, processor, Common FW, topology, project,
capacity, structural, and range facts; a different complete firmware hash is not by itself a blocker.

This file is the current single place to update CtrlRAM Replace experiment results and conclusions until the workflow is formally accepted. Lower-level notes may keep raw details, but status, blockers, and final interpretation should be reflected here.

## Scope

This report tracks CtrlRAM Replace only:

- named CtrlRAM slot mapping;
- staged legacy `Combiner.exe` postbuild execution;
- header/CRC/integrity byte drift after self-replacement;
- IC-specific exceptions that block an "OK" claim.

It does not establish Standard Merge, DP Replace, AB, or General Replace runtime
support or parity. Standard Merge results cited below are used only to prove the
CtrlRAM reference-base provenance for golden regression; AB hashes are cited only to keep those
fixtures separate from non-AB CtrlRAM evidence.

## Current Summary

CtrlRAM Replace is implemented as a workbench path with staged postbuild execution, but it is not yet production-cleared for all released ICs.

The owner-confirmed base contract accepts either a TP BIN work image or a full
Flash BIN. Both forms execute the same TP-relative replacement and postbuild
semantics. The current Workbench runs the processor against a host-created
base clone and enforces declared write ranges, preserving the full-Flash tail.
The schema 2.9 NT51926 executable candidate narrows this further by staging
only the TP prefix and reinserting it into the full clone. Common FW 1.4.1
cascade and Common FW 2.0.0 single/cascade without a version edit now use
exact routes; undeclared container lengths and every other version/count/edit
stay outside them.

Highlighted conclusion:

```text
NT51926 and NT51930 are versioned postbuild-category cases. Use the base BIN FWConfig Common FW version before selecting a postbuild profile: NT51926 Common FW 1.4.1 uses the 1.4.1 reference, NT51926 Common FW 2.0.0 uses the 2.0.0 reference, and NT51930 accepts only Common FW 1.x.x through the archived 1.4.0 reference. The inspected NT51930 2.0.0 BAT is evidence-only because the owner confirmed that no stable 2.0.0 project exists.
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
- NT51926 and NT51930 postbuild-category selection is implemented in Preview/Build and in workbench/UI slot/range display after a base BIN is loaded. NT51926 2.0.0 current/V2/expected parity is closed for exact single/cascade cases; NT51930 still requires exact parity and independent R3 byte review.
- NT51930 Common FW 1.x keeps numeric `2..29` on the approved cascade command shape with `DiffDLM` length `0xFE00`. The earlier `0x23000` section is archived evidence only and must not execute without new owner approval.
- NT51931 now has a direct exact-case intake and a closed diagnostic record.
  InsertSID remains outside the retirement parity boundary, but registered
  Combiner 1.13 still access-violates on the official command; runtime remains
  Not available.
- The 2026-07-18 final owner-approved intake adds exact NT51926 2.0.0
  single/cascade, NT51930 INX cascade, NT51931 cascade, NT51932 cascade, and
  NT51951 single inputs/expected outputs. The NT51951 Phase B experiment closes
  exact-case V1/V2 parity while retaining support-neutral/no-promotion status.

Do not use this stronger conclusion:

```text
All IC CtrlRAM self-replacement differences are known-good header CRC drift.
```

Use this narrower conclusion instead:

```text
All currently classified CtrlRAM self-replacement differences are either known header/CRC drift or explicitly documented exceptions. NT51926 2.0.0 single/cascade, NT51930 INX 1.3.0 cascade 3, NT51931 1.3.0 cascade 6, NT51932 2.0.0 cascade 3, and NT51951 2.0.0 single exact-case V1/V2 parity are closed without support promotion. NT51931 has zero expected-derived replacement-payload drift and 108 classified header/header-copy CRC bytes; its exact V1 and V2 outputs are byte-identical at SHA `f38fdecd...c594` using registered Combiner 1.13.0/51931-based.
```

## Evidence Sources

Primary files:

- `profiles/built-in/ctrlram-postbuild-v2/catalog.json`
- `src/NvtFwCombiner.Infrastructure/ExternalTools/BuiltInPostbuildProfileCatalog.cs`
- `profiles/built-in/ctrlram-postbuild-v2/flash-map.json`
- `src/NvtFwCombiner.Infrastructure/FlashMaps/BuiltInTpFlashMapCatalog*.cs`
- `src/NvtFwCombiner.Infrastructure/ExternalTools/LegacyCombinerPostbuildProcessor.cs`
- `external-tools/legacy-combiner/1.13.0/Combiner.exe`
- `docs/references/ic-flashmap/postbuild/*.bat`
- `docs/references/ic-flashmap/mmap/*.h`
- `docs/references/ic-flashmap/common-fw/ap_fwconfig.c`
- `docs/references/ic-flashmap/IC_FlashMap_20260705.xlsx`
- `testdata/golden/canonical/manifest.json`
- `testdata/golden/ctrlram-replace/manifest.json`
- `testdata/golden/ctrlram-replace/manifest.20260717.json` (remaining
  diagnostics/cross-workflow duplicates only)

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

The base may be the Combiner TP work image or a declared full-Flash container. The V2 prefix contract explicitly slices the owner-confirmed zero-based TP range, runs this postbuild model on that TP slice, and reinserts only the audited TP result into the full clone.

## Postbuild Catalog Status

| IC | Current command family | Source evidence | Current status |
| --- | --- | --- | --- |
| NT51917 | NT51927 perfect-family alias: `MERGE_MODE` + `NT51927BASED_GEN_CRC_MODE CRC32` | owner alias confirmation plus NT51927 exact single/two-/three-chip fixtures | Exact full-reference-SHA V2 aliases preserve the NT51927 V1/V2 bytes and 7/10/13-command process evidence; other shapes fail closed and no support promotion is made. |
| NT51919 | NT51929/NT51932 alias: `NT51932BASED_NORMAL_MODE CRC8` | owner perfect-family confirmation plus NT51929 AUTO_PRJ-594 exact fixture | Exact FW 2.0.0/PID `0x4703`/single/reference-SHA V2 alias preserves NT51929 V1/V2 output SHA `d23f53a1...198f`, one ordered two-command session, NT51919 staged identity, input immutability, and report identity. Other shapes fail closed; no support promotion is made. |
| NT51920 | `CRC_Enable` | inspected BAT + 2026-07-17 owner snapshot | Single/cascade formal payload bytes match their declared targets; owner command/range review remains. |
| NT51923 | `CRC_Enable` | inspected BAT + 2026-07-17 owner snapshot | Single/cascade formal payload bytes match their declared targets; owner command/range review remains. |
| NT51926 | `CRC_Enable` | inspected 1.4.1/2.0.0 BAT + owner snapshots | Workbench selects from TP FWConfig. Exact 1.4.1 cascade and 2.0.0 single/cascade routes are V2 candidates; 2.0.0 V1/V2 outputs are identical and differ from owner expected only at four approved CRC words. |
| NT51927 | `MERGE_MODE` + `NT51927BASED_GEN_CRC_MODE CRC32` | inspected BAT + owner snapshots | Single direct plus two- and three-chip full-reference-SHA engineering routes have exact V1/V2 process parity. Two-chip residuals are 25 expected-derived header/CRC words; three-chip residuals are 29 header/CRC words plus four declared VN replacement ranges. |
| NT51928 non-NB | NT51927 alias flow | owner partial-family confirmation plus canonical NT51928 Standard Merge golden | Exact FW 1.3.2/PID `0xF206`/two-chip/reference SHA `5064b313...7e0e` routes through V2. V1/V2 output SHA is `fbe011c7...f24c`, one ten-command session matches, and the distinct 512 KiB DP/LDC tail `[0x34800,0x80000)` is preserved. NB and other shapes fail closed; no support promotion is made. |
| NT51929 | `NT51932BASED_NORMAL_MODE CRC8` | inspected BAT + 2026-07-17 owner snapshot | Exact AUTO_PRJ-594/PID `0x4703`/Common FW 2.0.0/single routes through V2. Standard Merge reconstructs the true non-AB expected SHA `d3c958d2...3910`; V1 and V2 both produce `d23f53a1...198f`, with zero NF/Normal/VN drift and 15 bytes confined to four CRC words. The AB image remains separate. |
| NT51930 | `NT51930BASED_NORMAL_MODE CRC8` | inspected BAT + final 2026-07-18 direct INX intake | Earlier DiffDLM mismatch is historical diagnostic evidence. The exact AUTO_PRJ-302 case now proceeds through command/NF reconstruction and three-way parity. |
| NT51931 | selected: registered 1.13.0 `NT51931BASED_NORMAL_MODE CRC8` | final 2026-07-18 direct AUTO_PRJ-158 intake plus 2026-07-19 mode experiment | The 2026-07-17 BAT is 51931-based and the 2026-07-18 BAT is 51930-based. On the same base/inputs, registered 1.13.0/51931-based equals owner 1.2.0.4/51930-based at SHA `f38fdecd...c594`. The expected-derived control has zero payload drift and 108 header/header-copy CRC bytes. InsertSID is a nonblocking out-of-scope pre-step. |
| NT51932 | `NT51932BASED_NORMAL_MODE CRC8` | inspected BAT + final 2026-07-18 direct AUTO_PRJ-525 intake | Exact PID `0x5601`/Common FW 2.0.0/cascade 3 routes through V2 with V1/V2 full-byte parity. The direct NF composite is the route input; it equals `NF_Diff_0.bin`, so no DiffNFMerge derivation is claimed. |
| NT51950 | `NT51950BASED_NORMAL_MODE CRC8` | 2026-07-17 direct AUTO_PRJ-676 single intake | Exact PID `0x4A06`/Common FW 2.0.0/single/reference-SHA routes through V2. V1 and V2 produce SHA `a32e6896...d5c4`; the owner output differs only at four CRC words, and cascade is excluded from v0.9.9 scope. |
| NT51951 | NT51950 alias flow | final 2026-07-18 direct single intake | Exact AUTO_PRJ-695/PID `0x5901`/Common FW 2.0.0/single routes through V2 with V1/V2 full-byte parity. The 1.11-produced expected versus registered 1.13 differs only at four CRC words; cascade is excluded from v0.9.9 scope. |

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
| NT51931 | selected registered 1.13.0/51931-based succeeds; rejected 1.13.0/51930-based access-violates | Owner 1.2.0.4/51930-based control and registered 1.13.0/51931-based output are byte-identical with empty `output/map.txt`; 1.2.0.4 is not packaged. |
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
| NT51926 | 16 | Exact 2.0.0 allowed CRC drift | Final single/cascade V1 and V2 bytes are identical. Owner expected differs only at `0x1C..0x20`, `0xFC..0x100`, `0x32A8C..0x32A90`, and `0x32B6C..0x32B70`; CtrlRAM payload diff is zero. |
| NT51927 | 24 | Expected CRC/header-word drift | six 4-byte header words near `0x1E26C`, `0x1E27C`, `0x32FDC..0x33010` |
| NT51928 | 64 | Expected CRC/header-word drift | 4-byte words near `0x23C`, `0x24C`, `0x1E24C..0x1E2B0`, `0x32FDC..0x33040` |
| NT51929 | 16 | Expected CRC-generation drift | `0x7100..0x7104`, `0x7118..0x711C`, `0x27FF0..0x27FF4`, `0x28008..0x2800C` |
| NT51930 | 2901 | Postbuild-version mismatch candidate | `2.0.0` run includes `0x7100`, `0x7118`, `0x28FB0` header area, and extensive `0x32000` changes; 51930 golden header-copy target matches `1.4.0` length `0x100` much more closely than `2.0.0` length `0x200` |
| NT51931 | 108 / 8 ranges in expected-derived control | Header/header-copy CRC only; replacement payload 0 diff | Registered 1.13 exits `0xC0000005` without mutation, so this is not production parity. |
| NT51932 | 16 | Expected CRC-generation drift | `0x7100..0x7104`, `0x7118..0x711C`, `0x27FF0..0x27FF4`, `0x28008..0x2800C` |
| NT51950 | 16 | Expected CRC-generation drift | `0xA11C..0xA120`, `0xA130..0xA134`, `0x2D428..0x2D42C`, `0x2D43C..0x2D440` |
| NT51951 | 16 | Expected CRC-generation drift | same as NT51950 |

The NT51929 AB case is intentionally recorded separately. Its tracked 512 KiB expected output has SHA-256 `c7e1e263...3d66abe2`; `[0x00000,0x40000)` has SHA-256 `e257e734...1127c12` and must not be substituted for a single golden. The independent 256 KiB non-AB single expected is `d3c958d2...3910`; existing DP/TP inputs reconstruct it byte-for-byte. Running the exact pinned Combiner 1.13.0 command through both V1 and V2 produces `d23f53a1...198f` and changes 15 bytes at `[0x7100,0x7104)`, `[0x7118,0x711B)`, `[0x27FF0,0x27FF4)`, and `[0x28008,0x2800C)`. Those differences are only Header CRC and Header Copy CRC; NF, Normal, and VN match their physical input projections.

### 3. Workbench self-replacement with committed 2026-07-05 CtrlRAM fixtures

Owner supplied NT51926/NT51927 CtrlRAM Replace fixtures on 2026-07-05. The
NT51927 two-/three-chip direct-input cases now live under
`testdata/golden/canonical/NT51927/ctrlram-replace/`; the remaining NT51926
controls stay in the dated fixture root until diagnostics separation. The
owner-confirmed NT51917 perfect-family scope reuses those NT51927 exact facts
through canonical fact-scoped aliases without copying payloads: NT51917 V2
outputs match the corresponding NT51927 hashes for single
(`fdb8fef0...20ab9`), two-chip (`6f0bbde7...5f58`), and three-chip
(`dc1ee892...fe16`), while process evidence retains the IC-specific
`nt51917_fw.bin` staged name.

| Case | Workbench result | Diff classification | Current conclusion |
| --- | --- | --- | --- |
| NT51926 cascade | Build succeeds; FWConfig reads Common FW `1.4.1` and report trace uses the `1.4.1` `0x32F50` header-copy target | postbuild-category now matches the fixture codebase; later 2026-07-17 intake contains the expected output | Postbuild execution and version selection work. The committed self-replacement VN input is sliced to the archived `1.4.1` length `0x1660`; remaining parity/R3 closure is agent-owned. |
| NT51927 2-chip | Exact full-reference-SHA V2 route; V1/V2 SHA `6f0bbde7...5f58`; 100 changed bytes across 25 ranges | all observed ranges are complete header/integrity words in main/header-copy/final-backup areas; replacement payload drift is zero | Engineering route and process parity are closed using owner-approved base plus repository-derived replay inputs. This remains expected-derived evidence, not independent owner-output parity or support promotion. |
| NT51927 3-chip | Exact full-reference-SHA V2 route; V1/V2 SHA `dc1ee892...fe16`; 138 changed bytes | 116 bytes are 29 complete header/CRC words; 22 bytes are four declared VN replacement ranges caused by the shared VN input differing from the base master VN | Engineering route and one-session/13-command process parity are closed using owner-approved base plus repository-derived replay inputs. This remains expected-derived evidence, not independent owner-output parity or support promotion. |

Report verification gate: committed golden-backed self-replacement tests inspect the generated Replace report, not only the final output bytes. DP self-replacement must leave `OutputDifferences` empty. A CtrlRAM case whose evidence claims zero replacement-payload drift may emit only accepted `PostbuildCrcHeader` rows. An expected-derived CtrlRAM case may additionally emit accepted `DeclaredReplacement` rows only when every row traces to an explicit compiled mapping and an evidence-declared half-open range. Every case forbids `Unexpected` rows and `report.output-difference.unexpected`; a second self-replacement from the postbuild-clean output must return to an empty difference table.

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
| NT51930 | `PostbuildSetup_51930_1.4.0.bat`; `PostbuildSetup_51930_2.0.0.bat` evidence-only | Runtime accepts only 1.x through the 1.4.0 shape (`0x100`). The inspected 2.0.0 shape (`0x200`) is retained for traceability and cannot be selected. |
| NT51931 | `PostbuildSetup_51931_1.3.0.bat` | `0x100`; two supplied BAT versions disagree on mode. Registered Combiner 1.13.0/51931-based is selected after full-byte equality with the hash-only 1.2.0.4/51930-based control. The 1.2.0.4 executable is not packaged or routed. |
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
- NT51930 has documented TP Overview category notes: Common FW `1.4.0/1.x.x` evidence uses `0x7000 -> 0x28FB0`, length `0x100`, single postbuild command, consumes `MP_Ctrlram.bin`, and uses VN length `0x195E`; numeric cascade `2..29` uses the approved `DiffDLM` length `0xFE00`. The inspected Common FW `2.0.0` BAT uses length `0x200`, includes a second header-only command, and does not consume `MP_Ctrlram.bin`, but it is evidence-only and not runtime-selectable. The current Standard Merge golden reads Common FW `1.3.0`.
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
- NT51931 runtime remains fail-closed until exact three-way parity and review.
  The final owner intake fixes the case inputs/expected/BAT/Combiner hash;
  `InsertSID.py` is an out-of-scope pre-step and no longer blocks this parity.

NT51927 flash-header cross-check:

- `IC_FlashMap_20260705.xlsx` records `0x00200` Common Header, `0x00220` Flash Header, `0x1E230` Master header copy, `0x27230` Slave R header copy, `0x30230` Slave L header copy, and `0x32DC0` Header backup.
- `51927_1.4.1_mmap.h` records cascade header CRC offsets.
- Observed changed words align with 16-byte descriptor CRC positions (`descriptor + 0x0C`), including split 3-chip diff ranges where one byte in a 4-byte word happened to match.
- Synthetic sentinel replacement confirmed the real Combiner 1.13.0 path can update additional 3-chip main-header CRC words that self-replacement did not necessarily surface.

### 4. NT51931 corrected command evidence and exact-case candidate

Current repo reference for NT51931 is `1.3.0`, not `2.0.0`:

- `docs/references/ic-flashmap/mmap/51931_1.3.0_mmap.h`
- `docs/references/ic-flashmap/postbuild/PostbuildSetup_51931_1.3.0.bat`

The final 2026-07-18 BAT-shaped command was:

```text
Combiner.exe NT51930BASED_NORMAL_MODE CRC8 ...
```

That mode paired with Combiner 1.13.0 access-violates before mutation:

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

The owner-selected 2026-07-19 experiment retained the final `0x17C00`
DiffDLM input and every other argv token, while comparing these two pairings:

```text
Combiner 1.13.0   + NT51931BASED_NORMAL_MODE CRC8
Combiner 1.2.0.4  + NT51930BASED_NORMAL_MODE CRC8
```

Both runs observed:

```text
ExitCode=0
FW Merge is OK
DiffBytes=108
OutputSha256=f38fdecd95092d9bacabd8ca59c442cbdb601edd280c23efa088693cb256c594
CrossOutputDiffBytes=0
InputArtifactsUnchanged=true
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

- Registered Combiner 1.13.0 with `NT51931BASED_NORMAL_MODE` is the selected
  runtime pairing; 1.2.0.4 remains an uncommitted evidence control.
- The two BATs disagree both on mode and DiffDLM length (`0xC800` versus
  `0x17C00`). The parity experiment deliberately used the final `0x17C00`
  physical input so only tool/mode differed.
- `InsertSID.py` remains an out-of-scope pre-step and is not a parity blocker.
- The shared output differs from the starting owner expected in 108 bytes, all
  in eight reviewed main-header/header-copy CRC ranges; replacement payload
  difference is zero.
- Tool/mode compatibility is closed. Runtime remains fail-closed only until the
  exact V2 profile/route and independent R3 review are complete.

### 5. Header-copy `0xFF` prefill mode assessment

Owner question: should CtrlRAM Replace support a mode that pre-fills header-copy target windows with `0xFF` before running postbuild, so self-replacement can become byte-identical?

Current decision:

```text
Do not implement header-copy prefill as a production mode.
```

Reasoning:

- It does not answer the real NT51926 issue. NT51930 now fails closed outside Common FW 1.x so a 1.x-era golden cannot be tested with the evidence-only 2.0.0 shape.
- Combiner header-copy commands overwrite the target before CRC/header recalculation. When the correct postbuild version is selected, target prefill should usually be irrelevant.
- If any byte in the prefilled target is not overwritten, the mode can erase meaningful prior-generation header state and weaken golden evidence.
- It would add a visible mutation before the external processor, so reports, allowed-write ranges, and golden explanations must all account for it.

Allowed future use:

- Diagnostic-only experiment against a staging copy.
- Output only a report comparing baseline postbuild vs prefilled preimage postbuild.
- Never produce a production Build artifact, never update expected golden hashes from this mode, and never relax allowed-write ranges.

If owner later insists on production behavior, treat it as R3 firmware behavior: add an ADR/contract, declare the prefill as an ordered profile operation over a half-open range, require matching owner golden expected outputs for each postbuild version, and prove wrong-version cases fail closed.

## Current Engineering Gates Before "CtrlRAM Replace OK"

The final owner intake is pinned by the `ctrlram-replace` direct cases in
`testdata/golden/canonical/manifest.json`; its current execution
matrix is `docs/governance/v0.9.9-final-owner-golden-gap-matrix-20260718.md`.
There is no remaining owner-input or owner-decision gate. The rows below are
agent-owned parity, tool, route, and review work.

| Gate | Current evidence | Agent-owned closure |
| --- | --- | --- |
| NT51926 2.0.0 exact cases | Direct single/cascade DP, TP, physical CtrlRAM, DiffDLM where applicable, and full expected outputs are committed. Standard Merge decodes Common FW 2.0.0, PID `0x1309`, and chip counts 1/3. One-session/two-command V1 and V2 outputs are full-byte identical; each differs from owner expected at exactly four approved CRC words (16 bytes), with zero CtrlRAM payload drift. | Closed for the exact route. The D01/D02 naming discrepancy is not a byte-route discriminator; retain it as provenance. Support promotion remains false. |
| NT51930 exact INX case | AUTO_PRJ-302/PID `0x110D`/Common FW 1.3.0/cascade 3 routes only the exact selector/metadata shape to `nt51930-ctrlram-replace-fw130-cascade3`. The pre-retirement V1 control and V2 output are byte-identical at SHA `6725c501...ff48f`; one report session runs the one BAT-ordered `NT51930BASED_NORMAL_MODE CRC8` command through registered Combiner 1.13.0 SHA `ed6b5828...c76bf`. Wrong PID/version/count/numeric-selector shapes fail closed. | Engineering parity is closed for this exact route. The owner supplied final expected is an immutable reference sentinel, not an independent pre-replacement base: owner-to-current differences are 8 CRC bytes, 1 header-copy byte, and 4,388 bytes inside the declared DiffDLM range. The 29 ordered NF_Diff files and direct 577-byte NF composite are hash-pinned, but no DiffNFMerge derivation is claimed. Independent R3 review and support promotion remain separate. |
| NT51931 exact cascade case | AUTO_PRJ-158/PID `0x131B`/cascade 6 has TP, physical inputs, two conflicting BAT modes, owner expected, and both tool hashes. The supplied D8DfT82 FlashCode differs from the D8DT83 expected in 73,645 bytes and is retained as historical non-same-build input. Expected-derived self-replacement has zero payload drift and 108 bytes in eight header/header-copy CRC ranges. | Exact hash-pinned V2 profile `nt51931-ctrlram-replace-fw130-cascade6` matches the pre-retirement V1 control full-byte at SHA `f38fdecd...c594`, with one identical 1.13/51931-based command and immutable inputs. Other builds fail closed; support stays NotAvailable by policy, not by missing evidence. InsertSID remains out of scope and nonblocking. |
| NT51932 exact cascade case | AUTO_PRJ-525/PID `0x5601`/Common FW 2.0.0/cascade 3 is materialized as `nt51932-ctrlram-replace-fw200-cascade3`. Admission additionally requires the exact owner/Standard-Merge reference SHA `3eb556e0...08fd`, so a different base with the same metadata tuple fails closed. The pre-retirement V1 control and V2 output are byte-identical at SHA `0e59a2fb...2566`; their only 16 differences from the owner expected are the four approved CRC words. One session runs the two BAT-ordered registered Combiner 1.13 commands. | Engineering parity is closed. Direct `NF_Ctrlram.bin` is separately hash-pinned and byte-identical to `NF_Diff_0.bin`; DiffNFMerge is neither executed nor claimed as its derivation. Wrong base/PID/version/count/numeric-selector shapes fail closed, and the exact V2 candidate remains support-neutral/no-promotion. |
| NT51951 exact single case | AUTO_PRJ-695/PID `0x5901`/Common FW 2.0.0/single is materialized as `nt51951-ctrlram-replace-fw200-single`. Standard Merge reconstructs the owner expected SHA `c1cd54d9...b6b69`; admission additionally requires that exact reference SHA. The pre-retirement V1 control and V2 output are byte-identical at SHA `64ffa21a...d1ea`, with zero replacement-payload drift. | The owner-authorized 1.11→1.13 hypothesis is resolved as CRC-only, not full-byte equivalence: the four 4-byte ranges are `0xA11C..0xA120`, `0xA130..0xA134`, `0x2D428..0x2D42C`, and `0x2D43C..0x2D440`. Wrong base/PID/version/count/numeric-selector shapes fail closed; the exact candidate remains support-neutral/no-promotion. |
| TP/full-Flash base parity | Hash-pinned config records reviewed/candidate TP prefixes and full-Flash capacities; NT51926 1.4.1 already proves exact TP and full-Flash execution shapes. | Close each release-exposed exact route with its direct expected output and independent R3 byte review before promotion. |
| Cascade scope exclusions | NT51950 has no cascade product case; NT51951 has no cascade project. | Exclude both cascade shapes from v0.9.9 release scope. They are not missing-evidence cases and cannot be inferred as direct support. |

## V1 Retirement State

The release-exposed V1 compiler path is retired: both production callers and the
`CompositionProfileCompiler` implementation are absent. Exact evidence-backed
CtrlRAM and NT51926 DP-only General Replace routes execute through V2; all other
shapes fail closed. `BuiltInTpFlashMapCatalog` remains a config-backed display
and planning projection outside this frozen deletion scope. Stable closure still
requires structure, `verify --all`, required CI, and independent R2/R3 review.
Each route batch must report its remaining caller count and newly deletable
files. Evidence receipt alone must never be reported as retirement completion.

## Update Rules

Update this report whenever any of these change:

- a postbuild command family, argument order, command count, or IC alias;
- a flash-map, mmap, header-copy, CRC/header, or allowed-write range;
- a real-tool crash, timeout, or stdout/stderr diagnostic;
- a new approved CtrlRAM Replace golden fixture or expected output;
- a decision about NT51926, NT51930, NT51931, or another IC's TP/full-Flash shape;
- a support claim moves from investigation to release scope.

Each update should record:

- IC and IC number branch;
- source evidence file or fixture manifest entry;
- exact combiner mode and version/hash;
- input/output sizes and hashes when available;
- changed ranges and classification;
- which independent R2/R3 review gates remain open.

## Current Release Statement

As of this report:

```text
CtrlRAM Replace execution is traceable and the product contract requires both TP BIN and declared full Flash BIN with the same TP-relative semantics. The current Workbench admits both forms but does not yet enforce an explicit TP prefix for every IC; NT51926 Common FW 1.4.1 cascade and 2.0.0 single/cascade without a version edit route through exact V2 prefix/reinsert contracts. NT51926 2.0.0 V1/V2 parity is closed with only owner-approved CRC-word differences to the direct expected output. NT51919's exact perfect-family alias, NT51929 AUTO_PRJ-594/PID 0x4703/Common FW 2.0.0/single, NT51930 AUTO_PRJ-302/PID 0x110D/Common FW 1.3.0/cascade 3, NT51931 AUTO_PRJ-158/PID 0x131B/Common FW 1.3.0/cascade 6, and NT51950 AUTO_PRJ-676/PID 0x4A06/Common FW 2.0.0/single now have exact support-neutral V2 routes with V1/V2 full-byte equality. The 2026-07-18 final intake closes all owner-input gates. InsertSID is outside the retirement parity boundary, NT51950/NT51951 cascade are release-scope exclusions, and every remaining retirement/review gate is agent-owned. No support promotion is claimed.
```
