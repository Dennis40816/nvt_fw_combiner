# IC FlashMap Reference Evidence

This directory contains owner-approved reference evidence extracted from `IC FlashMap.7z`.

Included:

- `IC_FlashMap_20260701.xlsx` workbook evidence used for human flash-map review.
- `IC_FlashMap_20260725.xlsx` workbook update with NT51926/NT51927 TP Overview backup-region corrections and NT51926 versioned TP Overview sections. The current 2026-07-25 01:38:23 copy is tracked by SHA-256 in `SOURCE_MANIFEST.json`.
- `IC_FlashMap_20260730.xlsx` owner-approved workbook update with the compact `TP Address Tables` sheet, unified hexadecimal ranges, separate Initial Code/LDC rows, and the reviewed IC/row exclusions. Its exact bytes are tracked by SHA-256 in `SOURCE_MANIFEST.json`.
- `IC_FlashMap_20260922.xlsx` consolidates the latest owner-provided NT51950/NT51951 DP Perspective into the July workbook; the other eight sheets are preserved. See the source and limitations below.
- `postbuild/` legacy postbuild BAT files used to verify Combiner command order and arguments.
- `mmap/` legacy memory-map headers used to verify address ranges and header locations.
- `common-fw/ap_fwconfig.c` FWConfig structure reference used to verify Common FW, FW/bar, and PID offsets.
- `SOURCE_MANIFEST.json` source archive provenance plus per-file SHA-256 hashes.

Excluded:

- `combiner_1.13.0/Combiner.exe`, because approved runtime binaries are managed under `external-tools/`.
- Generated firmware outputs and private BIN inputs.

These files are evidence only. Production code must not execute anything from this directory. Release packages may copy this directory under `reference/` for human review.

For future IC evidence drops, first run:

```text
python scripts/intake_ic_reference.py --source <owner-drop-folder> --ic <NTxxxxx> --mode <workflow>
```

Promote only reviewed, non-payload reference documents from the generated handoff manifest into this directory, then update `SOURCE_MANIFEST.json` with source path, size, SHA-256, and approval/provenance notes.

## Public DP Perspective refresh — 2026-09-23

The owner requested consolidation of `51950_51951_DP_Perspective_20260922.xlsx`
received through the existing HackMD encrypted-transfer workflow. Source size:
15,370 bytes; SHA-256:
`81df46490d8b686d70135fa6b922c70919136c3898c42fb6399ea2af318c686b`.
Its single `工作表1` sheet, `A1:I73`, replaces `51950 DP Perspective` in a new
dated copy of the July 30 workbook. Original workbooks remain unchanged.

The new table includes OSD allocation labels and revised NT51950 2 IC / 8 Mbit
with-backup, without-LDC content: DP backup at `[0x80000,0x8A000)` and TP backup
allocation at `[0x8A000,0xB8000)`. These are source observations, not new
production write authority. Existing TP overlay `[0xA000,0x37000)` and protected
customer information `[0x37000,0x38000)` remain governed by their approved
profiles and owner decisions; the source's larger TP allocation label does not
authorize expanding writes.

Preserved source issues: `A59` literally says `B8000-BFFF`; OSD labels in
`E73/G73/I73` have no address. No address was guessed or silently corrected.
This source covers public NT51950/NT51951 layouts, not NT51928BT or the separate
Desay intake below. `TP Address Tables` is still the July TP Overview summary
(H/I headers explicitly say No Backup EN), not a summary of this DP Perspective.

Verification: exact values/types, alignment, number formats, dimensions and
merges match the new source; source and final affected-sheet PNGs have identical
SHA-256. Only `xl/worksheets/sheet8.xml` and appended `xl/styles.xml` entries
change in the original package. All other package parts and all prior style
entries remain unchanged, including the other eight sheets, workbook metadata,
theme and relationships. New sheet theme colors are resolved to source RGB so
the original workbook theme does not recolor them. ZIP integrity, saved-file
readback and an Artifact Tool formula-error scan pass; no macros/external links.
Native Excel interaction was not exercised. Evidence is retained in the external
test area's `outputs/v1110-perspective-20260922/evidence` directory.

The new workbook's manifest size/hash passes. A wider manifest audit also found
ten pre-existing mismatches in the nine `mmap/*.h` entries and
`common-fw/ap_fwconfig.c`; each file is byte-identical to `73b3d6e5d`, whose Git
blob already differs from the listed size/hash. This refresh does not alter
those files or silently replace their original evidence hashes. The full
reference-manifest audit is therefore not reported as passing.

## Desay workbook intake — 2026-09-08

Received through the owner-named `51928BT` HackMD API note and the existing
CJK14 decrypt tool: `NT51928BT_NT51950TT_NT51951TT flash mapping table for desay_0908.xlsx`.
Original size: 31,353 bytes. SHA-256:
`f49368ee605301ae4e310cc990b674ef8cd82021ffa40d9c00f42d7b2108030b`.
The original remains in the external transfer archive. Its only sheet is
`FLASH Setting` (123 rows, 23 columns), with no cell formulas or comments.
Workbook metadata records modified time `2026-09-08T02:58:01` without a
timezone; this is not an asserted firmware revision timestamp. Intake/review
date is 2026-09-08. The archive contains three external-link relationships,
so do not copy its original bytes into distributable references or follow those
links. A reviewed link-free reference copy and consolidated workbook update
remain pending; the July workbooks and manifest are unchanged.

The following are source observations, **not admitted production geometry**.
Excel inclusive end addresses below are normalized to half-open ranges.

| Source cells in `FLASH Setting` | Observation | Open issue / impact |
| --- | --- | --- |
| C8, E26:L26, C26, F41/H41/J41/L41, C41/C58 | Left heading is `NT51928JT`; TP FW is labelled at `[0xD000,0x3F000)` and backup at `[0x4D000,0x7F000)`. | The owner/note/filename say BT and the owner states all TP backups become `0x4A000`. Confirm identity and which geometry is authoritative before registering NT51928BT. Family membership alone cannot resolve this. |
| N8, N23, P23:W23 | Right heading is `NT51950TT/NT51951TT`; TP FW envelope is `[0xA000,0x3F000)`. | Current overlay ends at `0x37000`; the new envelope includes the current `[0x37000,0x38000)` customer-information range. Confirm whether this is allocation only or expanded writable TP authority; preserve customer information until resolved. |
| N38/N58, P38:W58 | Merged `TP FW - BK` cells span `[0x4A000,0x7F000)`. | NT51951 AB currently places TP B at `[0x8A000,0xB7000)`. Confirm AB TP B correspondence and source length before changing relocation, staging or imports. |
| C39/N39 versus C40/N40 | Row 39 says `4B000-5BFFF`, overlapping the following rows beginning `4C000`. | Source interval inconsistency; do not silently rewrite it to `4BFFF` or compile overlapping regions. |
| P10/P11 and P61/P93 | P column declares `4M bit`, `W/o BK & W/o LDC`, yet has backup labels and LDC content above `0x80000`. | Confirm whether rows are a shared allocation catalogue or executable content for every column; labels/colors alone cannot authorize beyond-capacity writes. |

Owner erratum decision — 2026-09-14: “保持之前 excel 需要刊誤”. For the
scoped NT51950/NT51951 Desay AB change, retain existing TP overlay
`[0xA000,0x37000)` (length `0x2D000`), not the workbook's larger executable
extent. Do not overwrite Customer Information `[0x37000,0x38000)` with TP.
The separately accepted Desay B start `0x4A000` therefore gives overlay
`[0x4A000,0x77000)`, not `[0x4A000,0x7F000)`. Preserve the table above as
the original source observation; the original workbook has not been edited.
This resolves TP overlay extent for that AB scope only, not NT51928 identity,
the row-39 typo, Standard/CtrlRAM geometry or all capacity-column meanings.

Next: resolve remaining source discrepancies with the owner, retain old/new
customer scope explicitly, update the existing FlashMap reference and manifest,
then admit affected production profiles and byte tests through their normal
firmware gates. NT51928BT stays unavailable. The roadmap's `1.4.1` row in the
[current release sequence](../../architecture/nfc_roadmap.md#current-release-sequence--2026-09-14)
owns order and release allocation; the original
[2026-09-08 intake](../../architecture/nfc_roadmap-history.md#new-owner-intake-iclayout-updates-and-option-density--2026-09-08)
is kept in the roadmap history.
