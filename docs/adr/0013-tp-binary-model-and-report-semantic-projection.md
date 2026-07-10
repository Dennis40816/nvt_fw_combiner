# ADR 0013: TP Binary Model and Report Semantic Projection

- Status: Accepted
- Date: 2026-07-10
- Risk class: R2

## Context

TP Overview rows currently expose authorization-oriented region kinds: `Dp`, `CtrlRam`, `CustomerInfo`, `ProjectId`, and `Other`. They are sufficient for authoring policy, but are too coarse for a new user reviewing a Replace output. In particular, a permitted postbuild change could only be rendered as `TP flash header / CRC fields`, even when workbook evidence identifies the field as `DLM CRC 0`.

The UI must not infer a firmware field from an output address. Doing so would duplicate firmware semantics in Presentation and create a second, unsafe source of truth.

## Decision

1. `NvtFwCombiner.Application.FlashMaps.TpBinaryModelCatalog` owns an inspection-only root model for every supported IC:

   ```text
   TP Flash Image
   ├─ TP Flash Header
   ├─ FW Configuration
   ├─ CtrlRAM
   ├─ Display / DP
   ├─ Project ID
   ├─ Customer Information
   ├─ FW Information
   └─ Other documented regions
   ```

2. Each model points to the existing TP Overview regions and, for `TP Flash Header`, to a workbook-derived `TpHeaderLayout` containing named half-open field ranges. The model may also contain a documented start-address anchor when the source provides a start but not a safe range length.

3. Existing `TpFlashMapRegionKind`, region visibility, General Replace access, compiled mappings, Combiner command plans, external processor authority, and allowed write ranges remain unchanged. The new binary model is not an authorization input.

4. `CompositionRunService` projects a nested `OutputDifferences[].Semantic` object after the output byte diff is calculated. It contains a category and a field/section subject. Presentation consumes it directly and retains a fallback for historical reports that do not include the object.

5. A field name is emitted only when the changed half-open range is fully inside a modeled field and the difference was classified as an approved postbuild header write. A copied-header target without a verified source-to-target field mapping remains a named header-copy section, not a guessed field.

6. The Report Changes view groups by semantic category and leads with field name, expected/review status, and explanation. Range, hash, and hex before/after evidence are disclosed in the row details.

## Consequences

- A normal NT51926 difference in `[0x001C, 0x0020)` is reported as `TP Flash Header` / `DLM CRC 0` rather than a generic CRC bucket.
- The current report file remains backward-compatible because `Semantic` is optional when parsing older JSON.
- The canonical `composition-report-v1` schema is not expanded by this ADR. `CompositionRunReport` is an Application/workbench report projection and must be documented separately from the versioned wire schema.
- NT51928 NB remains intentionally unmodeled. NT51932 uses only workbook fields common to its Type A/B/C diagrams until an owner-approved variant selector is documented.
- IC aliases are marked as documented aliases, not direct workbook proof. They do not grant write permissions or promote golden parity.

## Verification

- Catalog tests require every supported IC to expose the stable root/category structure.
- Catalog tests pin NT51926 `[0x0000, 0x0004)` to `ILM start address in BIN` and NT51927 `[0x023C, 0x0240)` to `DLM CRC 0`.
- An Application report test pins NT51926 `[0x001C, 0x0020)` postbuild output to semantic subject `DLM CRC 0`.
- UI smoke tests pin semantic-first report parsing and historical-report fallback behavior.

## Human Review Gate

This decision does not approve any firmware range, CRC algorithm, copy-header mapping, or parity claim. Firmware-owner review remains required before promoting new field ranges, variant-specific NT51932 data, or header-copy source/destination field equivalence into production behavior.
