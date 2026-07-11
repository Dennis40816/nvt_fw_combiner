# TP Binary Model Catalog

## Purpose

This is the searchable reference for the Application-owned TP binary model introduced on 2026-07-10. It explains how a supported IC is arranged for inspection and Report semantics. It does not change firmware bytes, Combiner invocation, write permissions, profile access, or golden parity status.

Related decision: [ADR 0013](../adr/0013-tp-binary-model-and-report-semantic-projection.md).

Evidence sources:

- `docs/references/ic-flashmap/IC_FlashMap_20260705.xlsx` TP Overview rows;
- `docs/references/tddi-flash-header/TDDI_Flash_Header.xlsx` named header worksheets;
- documented owner aliases already recorded in `TpFlashMapCatalog`.

All ranges below use half-open notation. For example, `[0x0000, 0x0004)` means bytes `0x0000` through `0x0003`.

## Stable Root

Every selectable IC exposes exactly this root and category order, even if a category has no documented region for that IC:

```text
TP Flash Image (tp-flash-image)
├─ TP Flash Header (tp-flash-header)
├─ FW Configuration (firmware-configuration)
├─ CtrlRAM (ctrlram)
├─ Display / DP (display)
├─ Project ID (project-identity)
├─ Customer Information (customer-information)
├─ FW Information (firmware-information)
└─ Other documented regions (other-documented-region)
```

The root is inspection/report metadata. It does not replace `TpFlashMapRegionKind`:

| Existing TP Overview kind | Root category | Notes |
| --- | --- | --- |
| `Dp` | Display / DP | Existing DP authoring behavior is unchanged. |
| `CtrlRam` | CtrlRAM | Existing CtrlRAM Replace access and postbuild selection are unchanged. |
| `CustomerInfo` | Customer Information | Preserve/protection behavior is unchanged. |
| `ProjectId` | Project ID | Preserve/protection behavior is unchanged. |
| `Other` with a header tag/id | TP Flash Header | Header backup rows are inspection metadata, not a new write rule. |
| `Other` with FW Config tag/id | FW Configuration | Includes the known primary FW Config start anchor and documented backup rows. |
| `Other` with `fw-information` tag | FW Information | Retains existing protection semantics. |
| Remaining `Other` | Other documented regions | No access-policy change. |

## Header Layout Coverage

| IC | Header layout | Model status | Evidence / scope |
| --- | --- | --- | --- |
| NT51917 | 927 | Documented alias | TP Overview owner confirmation: follows NT51927. |
| NT51919 | 932 common | Documented alias | Alias chain: NT51919 -> NT51929 -> NT51932 postbuild. |
| NT51920 | 920&923 normal | Workbook | Direct `920&923` worksheet model. |
| NT51923 | 920&923 normal | Workbook | Direct `920&923` worksheet model. |
| NT51926 | 925&926 normal | Workbook | Direct `925&926` worksheet model; never uses `926NB`. |
| NT51927 | 927 | Workbook + postbuild continuation | `927` worksheet Header #0 through #2; its continuation marker and the approved three-chip final-header copy confirm Header #3 coverage for reporting. |
| NT51928 | 927 | Documented alias | Non-NB only; NT51928 NB is not modeled. |
| NT51929 | 932 common | Documented alias | TP Overview owner confirmation: follows NT51932 postbuild. |
| NT51930 | 930 | Workbook | Direct `930` worksheet model. |
| NT51931 | 931 | Workbook | Direct `931` worksheet model. |
| NT51932 | 932 common | Workbook common fields | Only fields shared by the worksheet's Type A/B/C diagrams are modeled. |
| NT51950 | 950 | Workbook | Direct `950` worksheet model. |
| NT51951 | 950 | Documented alias | TP Overview owner confirmation: follows NT51950 postbuild. |

### Layout Ranges

| Workbook layout | Modeled header ranges | Notes |
| --- | --- | --- |
| 920&923 | `[0x0000, 0x0100)` | Normal header descriptors. |
| 925&926 | `[0x0000, 0x0100)` | Normal header descriptors used by normal NT51926. |
| 931 | `[0x0000, 0x0100)` | Adds DLM DIFF and DLM CRC 1 through 19 fields. |
| 927 | `[0x0000, 0x0100)`, `[0x0200, 0x02E0)` | Global header, command header, workbook-described per-IC headers 0 through 2, and Header #3 from the workbook continuation plus three-chip final-header copy source coverage. |
| 930 | `[0x7100, 0x7200)` | NT-based descriptor layout. |
| 932 common | `[0x7100, 0x7200)` | Common fields only; no type-specific inference. |
| 950 | `[0xA100, 0xA200)` | NT51950-based descriptor layout. |

## Complete Current Field Inventory

The following is the complete current inspection/report model. `Word` fields are four bytes;
other lengths are written explicitly. An alias IC uses the exact field list of its referenced
layout and is not a second independently inferred model.

### IC-to-Layout Mapping

| IC | Header layout | Model status | Field count |
| --- | --- | --- | ---: |
| NT51917 | NT51927 / 927 | Documented alias | 68 |
| NT51919 | NT51932 / 932 common | Documented alias | 12 |
| NT51920 | 920&923 normal | Workbook | 25 |
| NT51923 | 920&923 normal | Workbook | 25 |
| NT51926 | 925&926 normal | Workbook | 26 |
| NT51927 | 927 | Workbook + postbuild continuation | 68 |
| NT51928 | NT51927 / 927 (non-NB only) | Documented alias | 68 |
| NT51929 | NT51932 / 932 common | Documented alias | 12 |
| NT51930 | 930 | Workbook | 44 |
| NT51931 | 931 | Workbook | 49 |
| NT51932 | 932 common fields | Workbook common fields | 12 |
| NT51950 | 950 | Workbook | 12 |
| NT51951 | NT51950 / 950 | Documented alias | 12 |

### 920&923 Normal (NT51920, NT51923)

| Range | Field |
| --- | --- |
| `[0x0000, 0x0004)` | ILM start address in BIN |
| `[0x0004, 0x0008)` | ILM destination address in SRAM |
| `[0x0008, 0x000C)` | ILM size |
| `[0x000C, 0x0010)` | DATA start address in BIN |
| `[0x0010, 0x0014)` | DATA destination address in SRAM |
| `[0x0014, 0x0018)` | DATA size |
| `[0x0018, 0x001C)` | ILM CRC 0 |
| `[0x001C, 0x0020)` | DLM CRC 0 |
| `[0x0020, 0x0021)` | Same code |
| `[0x0021, 0x0024)` | SPI option |
| `[0x0024, 0x0028)` | SVN auto-build version |
| `[0x0028, 0x002C)` | OV info |
| `[0x0030, 0x0034)` | FW Config destination address in SRAM |
| `[0x0038, 0x003C)` | FW Config start address in BIN |
| `[0x003C, 0x0040)` | FW Config CRC |
| `[0x0040, 0x0044)` | CtrlRAM destination address in SRAM |
| `[0x0048, 0x004C)` | CtrlRAM start address in BIN |
| `[0x004C, 0x0050)` | CtrlRAM CRC |
| `[0x0050, 0x0054)` | MP CtrlRAM destination address in SRAM |
| `[0x0058, 0x005C)` | MP CtrlRAM start address in BIN |
| `[0x005C, 0x0060)` | MP CtrlRAM CRC |
| `[0x00F0, 0x00F4)` | Header destination address in SRAM |
| `[0x00F4, 0x00F8)` | Header size |
| `[0x00F8, 0x00FC)` | Header start address in BIN |
| `[0x00FC, 0x0100)` | Header CRC |

### 925&926 Normal (NT51926)

This is the same as the 920&923 normal model except for the four-byte descriptor at
`[0x0020, 0x0024)`, which is split as follows. Therefore the layout has 26 fields rather than 25.

| Range | Field |
| --- | --- |
| `[0x0020, 0x0021)` | Cascade info |
| `[0x0021, 0x0022)` | SPI option |
| `[0x0022, 0x0024)` | T6/T4 |

### 931 (NT51931)

This is the complete 925&926 normal model plus the following 23 fields:

| Range | Field |
| --- | --- |
| `[0x0060, 0x0064)` | DLM DIFF start address in BIN |
| `[0x0064, 0x0068)` | DLM DIFF destination address in SRAM |
| `[0x0068, 0x006A)` | DLM DIFF size |
| `[0x006B, 0x006C)` | IC number |
| `[0x006C, 0x0070)` | DLM CRC 1 |
| `[0x0070, 0x00B8)` | DLM CRC 2 through DLM CRC 19; one four-byte field every four bytes |

### 927 (NT51917, NT51927, NT51928 non-NB)

Global fields:

| Range | Field |
| --- | --- |
| `[0x0024, 0x0028)` | SVN auto-build version |
| `[0x0028, 0x002C)` | OV info |
| `[0x0030, 0x0034)` | FW Config destination address in SRAM |
| `[0x0038, 0x003C)` | FW Config start address in BIN |
| `[0x003C, 0x0040)` | FW Config CRC |
| `[0x0040, 0x0044)` | CtrlRAM destination address in SRAM |
| `[0x0048, 0x004C)` | CtrlRAM start address in BIN |
| `[0x004C, 0x0050)` | CtrlRAM CRC |
| `[0x0050, 0x0054)` | MP CtrlRAM destination address in SRAM |
| `[0x0058, 0x005C)` | MP CtrlRAM start address in BIN |
| `[0x005C, 0x0060)` | MP CtrlRAM CRC |
| `[0x00F0, 0x00F4)` | Header destination address in SRAM |
| `[0x00F4, 0x00F8)` | Header size |
| `[0x00F8, 0x00FC)` | Header start address in BIN |
| `[0x00FC, 0x0100)` | Header CRC |
| `[0x0200, 0x0202)` | Command header calibration |
| `[0x0202, 0x0203)` | Command header build read command |
| `[0x0203, 0x0204)` | Command header build divider count |
| `[0x0204, 0x0205)` | Command header build T2/T1 |
| `[0x0205, 0x0206)` | Command header build T4/T3 |
| `[0x0206, 0x0207)` | Command header build T6/T5 |
| `[0x0207, 0x0208)` | Command header build T8/T7 |
| `[0x0208, 0x0209)` | Command header build T9 |
| `[0x021C, 0x0220)` | Common Header CRC |

The worksheet explicitly describes the per-IC header template for `n = 0, 1, 2`, base address
`0x0220 + n * 0x30`. Its continuation marker, together with the approved three-chip
`final-header-backup` copy from source `[0x0000, 0x0460)`, establishes the same report-only
template for `n = 3` at `0x02B0`. This supplies 44 fields in total. The continuation does not
grant write authority or assert parity beyond those named fields.

| Relative range | Field |
| --- | --- |
| `[+0x00, +0x04)` | ILM start address in BIN `n` |
| `[+0x04, +0x08)` | ILM destination address in SRAM `n` |
| `[+0x08, +0x0C)` | ILM size `n` |
| `[+0x0C, +0x10)` | ILM CRC `n` |
| `[+0x10, +0x14)` | DATA start address in BIN `n` |
| `[+0x14, +0x18)` | DATA destination address in SRAM `n` |
| `[+0x18, +0x1C)` | DATA size `n` |
| `[+0x1C, +0x20)` | DLM CRC `n` |
| `[+0x20, +0x21)` | IC location `n` |
| `[+0x28, +0x2C)` | Next header address `n` |
| `[+0x2C, +0x30)` | Header CRC `n` |

### 930 (NT51930)

| Range | Field |
| --- | --- |
| `[0x7100, 0x7104)` | Header CRC |
| `[0x7104, 0x7108)` | ILM destination address in SRAM |
| `[0x7108, 0x710C)` | ILM size |
| `[0x710C, 0x7110)` | ILM CRC 0 |
| `[0x7110, 0x7114)` | DLM destination address in SRAM |
| `[0x7114, 0x7118)` | DLM size |
| `[0x7118, 0x711C)` | DLM CRC 0 |
| `[0x711C, 0x7120)` | DLM DIFF destination address in SRAM |
| `[0x7120, 0x7122)` | DLM DIFF size |
| `[0x7123, 0x7124)` | IC number |
| `[0x7124, 0x7125)` | Build read command |
| `[0x7125, 0x7126)` | Build divider count |
| `[0x7126, 0x7127)` | SPI option |
| `[0x7128, 0x7198)` | DLM CRC 1 through DLM CRC 28; one four-byte field every four bytes |
| `[0x7198, 0x719C)` | ILM start address in BIN |
| `[0x719C, 0x71A0)` | DLM start address in BIN |
| `[0x71A0, 0x71A4)` | DLM DIFF start address in BIN |

### 932 Common (NT51919, NT51929, NT51932)

Only the fields common to the worksheet's Type A/B/C diagrams are modeled:

| Range | Field |
| --- | --- |
| `[0x7100, 0x7104)` | Header CRC |
| `[0x7104, 0x7108)` | ILM destination address in SRAM |
| `[0x7108, 0x710C)` | ILM size |
| `[0x710C, 0x7110)` | ILM CRC 0 |
| `[0x7110, 0x7114)` | DLM destination address in SRAM |
| `[0x7114, 0x7118)` | DLM size |
| `[0x7118, 0x711C)` | DLM CRC 0 |
| `[0x711C, 0x7120)` | DLM DIFF destination address in SRAM |
| `[0x7120, 0x7122)` | DLM DIFF size |
| `[0x7124, 0x7125)` | Build read command |
| `[0x7125, 0x7126)` | Build divider count |
| `[0x7126, 0x7127)` | SPI option |

### 950 (NT51950, NT51951)

| Range | Field |
| --- | --- |
| `[0xA100, 0xA104)` | ILM start address in BIN |
| `[0xA104, 0xA108)` | ILM destination address in SRAM |
| `[0xA108, 0xA10C)` | ILM size |
| `[0xA10C, 0xA110)` | ILM CRC 0 |
| `[0xA110, 0xA114)` | DLM start address in BIN |
| `[0xA114, 0xA118)` | DLM destination address in SRAM |
| `[0xA118, 0xA11C)` | DLM size |
| `[0xA11C, 0xA120)` | DLM CRC 0 |
| `[0xA12A, 0xA12B)` | Build read command |
| `[0xA12B, 0xA12C)` | Build divider count |
| `[0xA12C, 0xA12D)` | SPI option |
| `[0xA130, 0xA134)` | Header CRC |

## Field Examples

### NT51926 Normal Header

The `925&926` model includes the following direct workbook fields:

| Field | Range | Report title |
| --- | --- | --- |
| ILM start address in BIN | `[0x0000, 0x0004)` | `ILM start address in BIN` |
| ILM destination address in SRAM | `[0x0004, 0x0008)` | `ILM destination address in SRAM` |
| ILM size | `[0x0008, 0x000C)` | `ILM size` |
| ILM CRC 0 | `[0x0018, 0x001C)` | `ILM CRC 0` |
| DLM CRC 0 | `[0x001C, 0x0020)` | `DLM CRC 0` |
| FW Config CRC | `[0x003C, 0x0040)` | `FW Config CRC` |
| CtrlRAM CRC | `[0x004C, 0x0050)` | `CtrlRAM CRC` |
| MP CtrlRAM CRC | `[0x005C, 0x0060)` | `MP CtrlRAM CRC` |
| Header CRC | `[0x00FC, 0x0100)` | `Header CRC` |

The first row is the requested reference: bytes `0x0000` through `0x0003` are the `ILM start address in BIN` field. The model stores it as `[0x0000, 0x0004)`.

### Postbuild-Relevant Named Fields

| IC family | Range | Title emitted when the exact postbuild change is inside the field |
| --- | --- | --- |
| NT51920/NT51923/NT51926 normal | `[0x001C, 0x0020)` | `DLM CRC 0` |
| NT51920/NT51923/NT51926 normal | `[0x00FC, 0x0100)` | `Header CRC` |
| NT51927-based | `[0x023C, 0x0240)` | `DLM CRC 0` |
| NT51927-based | `[0x024C, 0x0250)` | `Header CRC 0` |
| NT51927-based | `[0x02BC, 0x02C0)` | `ILM CRC 3` |
| NT51927-based | `[0x02DC, 0x02E0)` | `Header CRC 3` |
| NT51930/NT51932-based | `[0x7100, 0x7104)` | `Header CRC` |
| NT51930/NT51932-based | `[0x7118, 0x711C)` | `DLM CRC 0` |
| NT51950/NT51951-based | `[0xA11C, 0xA120)` | `DLM CRC 0` |
| NT51950/NT51951-based | `[0xA130, 0xA134)` | `Header CRC` |

## Report Projection

For a Replace output diff, Application computes byte changes first and then emits this optional object under `OutputDifferences[]`:

```json
{
  "Semantic": {
    "CategoryId": "tp-flash-header",
    "CategoryLabel": "TP Flash Header",
    "ParentId": "tp-header",
    "ParentLabel": "Header",
    "SubjectId": "nt51926-header:dlm-crc-0",
    "SubjectLabel": "DLM CRC 0",
    "Explanation": "Expected: postbuild recalculated DLM CRC 0."
  }
}
```

Presentation must render this data and must not calculate a header field from the address. Old report JSON without `Semantic` retains the legacy section-label fallback.

`Semantic.ParentId` and `Semantic.ParentLabel` identify the physical parent before the field subject. For example, an in-place CRC write is grouped under `Header`, while a copied CRC is grouped under `Header copy / final backup`. The application emits this hierarchy from the approved postbuild section; Presentation does not derive it from an address or a field name.

The report only emits a field title when both conditions hold:

1. the diff was classified as an approved postbuild CRC/header change; and
2. the changed range is wholly inside one documented header field.

This avoids falsely calling a copied header target `DLM CRC 0` before source-to-destination field mapping is itself documented. Such rows stay under `TP Flash Header` with a named copy section such as `Header copy / master`.

## Known Limits and Follow-ups

- `926NB` and NT51928 NB are not aliases of the normal layouts.
- NT51932 variant-specific Type A/B/C field order requires firmware-owner evidence before it can be selected by IC number or Common FW category.
- Header copy destination fields are intentionally generic until a verified mapping relates each copy target back to a source header field.
- This catalog is not golden evidence. It does not change the current per-IC self-replacement conclusions or promote parity.
- Firmware-owner review is required before a new header range, a copy relationship, or an alias is allowed to affect execution rather than report naming.

## Executable Evidence

- `TpHeaderCatalogTests.Nt51926HeaderModelsIlmStartAddressInBin`
- `TpHeaderCatalogTests.Nt51927HeaderModelsDlmCrcZero`
- `TpHeaderCatalogTests.EveryIcExposesOneStableTopLevelBinaryCategoryStructure`
- `CompositionRunServiceTests.ReplaceReportNamesNt51926DlmCrcZero`
- `ShellViewModelTests.ReportReviewShowsAcceptedOutputDifferences`
