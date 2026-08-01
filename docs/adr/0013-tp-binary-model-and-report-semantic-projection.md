# ADR 0013: TP Binary Model and Report Semantic Projection

- Status: Accepted
- Date: 2026-07-10
- Amended: 2026-07-17 for the v0.9.9 code-size convergence gate; 2026-07-24 for owner-confirmed Type A/B and DIFF-CRC field semantics; 2026-08-01 for canonical metadata and write-section convergence
- Risk class: R2

## Context

TP Overview rows currently expose authorization-oriented region kinds: `Dp`, `CtrlRam`, `CustomerInfo`, `ProjectId`, and `Other`. They are sufficient for authoring policy, but are too coarse for a new user reviewing a Replace output. In particular, a permitted postbuild change could only be rendered as `TP flash header / CRC fields`, even when workbook evidence identifies the field as `DLM CRC 0`.

The UI must not infer a firmware field from an output address. Doing so would duplicate firmware semantics in Presentation and create a second, unsafe source of truth.

## Decision

1. Canonical `firmware-family-v1` `tp-flash-header` structures own the
   workbook-derived layouts and fields. Exact `composition-profile-v2`
   metadata/behavior bindings select their permitted report and compilation
   uses. The Application `TpHeaderCatalog`, `TpHeaderLayout`, and
   `TpHeaderField` parity adapter is retired by #194. The previously
   materialized `TpBinaryModelCatalog` root/category tree was already retired
   in v0.9.9 because no UI, CLI, report, or runtime consumer ever read it.

2. Each layout contains named half-open header field ranges. TP Overview regions
   remain owned by hash-pinned `profiles/built-in/ctrlram-postbuild-v2/flash-map.json`;
   report semantics do not duplicate their complete category tree or materialize
   unused address anchors.

3. The semantic catalog is not itself an authorization input. Existing
   `TpFlashMapRegionKind`, region visibility, General Replace access, compiled
   mappings, Combiner command plans, external processor authority, and allowed
   write ranges change only through separately reviewed owner evidence. The
   2026-07-24 amendment supplies that evidence for the exact CRC ranges in
   decision 7; it does not make arbitrary modeled fields writable.

4. `CompositionRunService` projects a nested `OutputDifferences[].Semantic` object after the output byte diff is calculated. It contains a category, a physical parent section, and a field/section subject. Presentation consumes it directly and retains a fallback for historical reports that do not include the object.

5. A field name is emitted only when the changed half-open range is fully inside a modeled field and the difference was classified as an approved postbuild header write. A copied-header target is mapped to a field only when the selected postbuild plan declares an equal-length source-to-target copy range; otherwise it remains a named header-copy section, not a guessed field.

6. The Report Changes view groups by the physical parent section and leads with field name, expected/review status, and explanation. Range, hash, and hex before/after evidence are disclosed in the row details.

7. The 2026-07-24 owner review makes the workbook's 932 Type A/B layout
   authoritative for NT51919/NT51929/NT51932 because the admitted cascade
   count is 2–8 IC. It adds DLM CRC 1 through 7 at `[0x7128,0x7144)`.
   Type C remains excluded. The same review classifies 950/951 workbook
   `Reserved (DIFF CRC)` words `[0xA134,0xA180)` as DLM CRC 1 through 19.

8. A postbuild write carries one Domain
   `ExternalProcessorWriteRangeSection`: stable `SectionId`, destination
   half-open `Range`, and optional equal-length `SourceRange` for copy
   provenance. The planner, compiled processor invocation, output diff, and
   report projection share that one value. `PostbuildWriteSectionIds` supplies
   the closed identifiers and presentation/overlap semantics; it does not own
   geometry. No parallel postbuild write-range record may duplicate this
   contract.

## Consequences

- A normal NT51926 difference in `[0x001C, 0x0020)` is reported as `TP Flash Header` / `DLM CRC 0` rather than a generic CRC bucket.
- Removing the unbound root/category object model does not remove any rendered
  report field, semantic category, historical-report fallback, or authorization
  rule. It removes only a public projection with no production caller.
- The current report file remains backward-compatible because `Semantic` is optional when parsing older JSON.
- Copy provenance is retained as report metadata on the existing processor write-section declaration. It grants no additional processor read/write authority and does not alter the selected Combiner command line.
- Deleting the Application parity adapter and duplicate planner range type does
  not change any declared half-open range, section id, CRC/header byte, or
  processor authority.
- The canonical `composition-report-v1` schema is not expanded by this ADR. `CompositionRunReport` is an Application/workbench report projection and must be documented separately from the versioned wire schema.
- NT51928 NB remains intentionally unmodeled. NT51932 uses the owner-confirmed
  Type A/B layout for the bounded 1–8 IC product scope; Type C (9–16 IC)
  remains intentionally unmodeled and unselectable.
- IC aliases are marked as documented aliases, not direct workbook proof. They do not grant write permissions or promote golden parity.

## Verification

- Canonical metadata contract tests require every supported IC to expose one
  evidenced header definition through its admitted family/profile binding.
- Catalog tests pin NT51926 `[0x0000, 0x0004)` to `ILM start address in BIN`,
  NT51927 `[0x023C, 0x0240)` to `DLM CRC 0`, NT51932 Type A/B DLM CRC
  1 through 7, and NT51950/51 DLM CRC 1 through 19.
- An Application report test pins NT51926 `[0x001C, 0x0020)` postbuild output to semantic subject `DLM CRC 0`.
- UI smoke tests pin semantic-first report parsing and historical-report fallback behavior.

## Human Review Gate

The NT51927 Header #3 continuation remains limited to report projection, based
on worksheet `927`'s continuation marker and the approved three-chip
`final-header-backup` source coverage. The 2026-07-24 owner review approves
only the field identities and bounded postbuild write ranges listed above; it
does not approve a new CRC algorithm, Type C support, or new golden parity.
Any further field/source equivalence still requires firmware-owner review.
