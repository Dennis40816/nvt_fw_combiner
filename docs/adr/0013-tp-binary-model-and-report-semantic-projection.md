# ADR 0013: TP Binary Model and Report Semantic Projection

- Status: Accepted
- Date: 2026-07-10
- Amended: 2026-07-17 for the v0.9.9 code-size convergence gate
- v0.9.9 amendment status: Proposed; architecture review required before merge
- Risk class: R2

## Context

TP Overview rows currently expose authorization-oriented region kinds: `Dp`, `CtrlRam`, `CustomerInfo`, `ProjectId`, and `Other`. They are sufficient for authoring policy, but are too coarse for a new user reviewing a Replace output. In particular, a permitted postbuild change could only be rendered as `TP flash header / CRC fields`, even when workbook evidence identifies the field as `DLM CRC 0`.

The UI must not infer a firmware field from an output address. Doing so would duplicate firmware semantics in Presentation and create a second, unsafe source of truth.

## Decision

1. `NvtFwCombiner.Application.FlashMaps.TpHeaderCatalog` owns one workbook-derived
   `TpHeaderLayout` for every supported IC and the stable semantic category ids
   actually emitted by output-difference reports. The previously materialized
   `TpBinaryModelCatalog` root/category tree is retired in v0.9.9 because no
   UI, CLI, report, or runtime consumer ever read it.

2. Each layout contains named half-open header field ranges. TP Overview regions
   remain owned by hash-pinned `profiles/built-in/ctrlram-postbuild-v2/flash-map.json`;
   report semantics do not duplicate their complete category tree or materialize
   unused address anchors.

3. Existing `TpFlashMapRegionKind`, region visibility, General Replace access, compiled mappings, Combiner command plans, external processor authority, and allowed write ranges remain unchanged. The semantic catalog is not an authorization input.

4. `CompositionRunService` projects a nested `OutputDifferences[].Semantic` object after the output byte diff is calculated. It contains a category, a physical parent section, and a field/section subject. Presentation consumes it directly and retains a fallback for historical reports that do not include the object.

5. A field name is emitted only when the changed half-open range is fully inside a modeled field and the difference was classified as an approved postbuild header write. A copied-header target is mapped to a field only when the selected postbuild plan declares an equal-length source-to-target copy range; otherwise it remains a named header-copy section, not a guessed field.

6. The Report Changes view groups by the physical parent section and leads with field name, expected/review status, and explanation. Range, hash, and hex before/after evidence are disclosed in the row details.

## Consequences

- A normal NT51926 difference in `[0x001C, 0x0020)` is reported as `TP Flash Header` / `DLM CRC 0` rather than a generic CRC bucket.
- Removing the unbound root/category object model does not remove any rendered
  report field, semantic category, historical-report fallback, or authorization
  rule. It removes only a public projection with no production caller.
- The current report file remains backward-compatible because `Semantic` is optional when parsing older JSON.
- Copy provenance is retained as report metadata on the existing processor write-section declaration. It grants no additional processor read/write authority and does not alter the selected Combiner command line.
- The canonical `composition-report-v1` schema is not expanded by this ADR. `CompositionRunReport` is an Application/workbench report projection and must be documented separately from the versioned wire schema.
- NT51928 NB remains intentionally unmodeled. NT51932 uses only workbook fields common to its Type A/B/C diagrams until an owner-approved variant selector is documented.
- IC aliases are marked as documented aliases, not direct workbook proof. They do not grant write permissions or promote golden parity.

## Verification

- Catalog tests require every supported IC to expose one evidenced header layout.
- Catalog tests pin NT51926 `[0x0000, 0x0004)` to `ILM start address in BIN` and NT51927 `[0x023C, 0x0240)` to `DLM CRC 0`.
- An Application report test pins NT51926 `[0x001C, 0x0020)` postbuild output to semantic subject `DLM CRC 0`.
- UI smoke tests pin semantic-first report parsing and historical-report fallback behavior.

## Human Review Gate

This decision does not approve any firmware range, CRC algorithm, or parity claim. The NT51927 Header #3 continuation is limited to report projection, based on worksheet `927`'s continuation marker and the approved three-chip `final-header-backup` source coverage. Firmware-owner review remains required before promoting variant-specific NT51932 data or any new field/source equivalence into production behavior.
