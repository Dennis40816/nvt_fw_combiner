# ADR 0029: Explicit FWConfig version write routes

- Status: Accepted for v0.9.12 implementation; firmware-owner release gate remains
- Date: 2026-07-21
- Risk: R3; firmware-owner review is required before release
- Amended by: ADR 0042 for the `0.10.x` retirement of NT51920, NT51930,
  and NT51931

## Context

CtrlRAM TP firmware-version authoring reads the final contract from the canonical NVT FWConfig
Backup, applies the requested values before postbuild, and verifies the same values in Backup before
committing output. The pre-postbuild write location is not identical for every legacy mode:

ADR 0042 makes the NT51920, NT51930, and NT51931 rows below legacy `0.9.x`
characterization only. They do not migrate into `0.10.x` production selectors,
profiles, processors, or authoring routes.

- some command plans explicitly copy a Primary FWConfig source block to Backup;
- some NT-based modes implicitly propagate the TP Overview Primary FWConfig to Backup;
- a mode without either relationship must reject version authoring.

Before the v0.9.12 migration, `LegacyCombinerFirmwareConfigPropagation.None` did not distinguish an explicit
command source from an unreviewed or accidentally omitted route. The v2 family data also models the
Primary structure as `fw-config-source` for only NT51919, NT51926, and NT51929. Other exact routes
could therefore have a correct postbuild source offset while the compiler still saw that address as an
unmapped gap. The v0.9.12 implementation closes that modeling gap for every CtrlRAM profile without
changing the processor's write authority.

## Evidence inventory

All offsets below are existing hash-pinned TP Overview or postbuild facts. No new address is inferred.
`Mode evidence` describes the source-to-Backup relationship only; it does not promote product support
or replace full expected-output golden review.

| IC | Primary start | Write route | Backup target | v0.9.12 source model | Mode evidence |
| --- | ---: | --- | --- | --- | --- |
| NT51917 | `0x16000` | explicit command source | `[0x34000,0x34800)` | modeled | NT51927-family command blocks and approved perfect-family alias |
| NT51919 | `0x1F200` | implicit Primary to canonical Backup | terminal-marker rule | modeled | NT51929 perfect-family alias plus real-tool propagation test |
| NT51920 | `0x22000` | explicit command source | `[0x2F000,0x2F780)` | modeled | direct command blocks and owner golden routes |
| NT51923 | `0x22000` | explicit command source | `[0x3B000,0x3B800)` | modeled | direct command blocks and owner golden routes |
| NT51926 | `0x22000` | explicit command source | `[0x3B000,0x3B780)` or `[0x3B000,0x3B800)` by Common FW | modeled | direct versioned command blocks and existing Build-only edit test |
| NT51927 | `0x16000` | explicit command source | `[0x34000,0x34800)` | modeled | direct single/two-/three-chip command blocks |
| NT51928 non-NB | `0x16000` | explicit command source | `[0x34000,0x34800)` | modeled | owner-approved NT51927 partial-family command alias |
| NT51929 | `0x1F200` | implicit Primary to canonical Backup | terminal-marker rule | modeled | direct real-tool propagation and production Build tests |
| NT51930 | `0x1F200` | implicit Primary to canonical Backup | terminal-marker rule | modeled | direct real-tool Common FW 1.x propagation test |
| NT51931 | `0x16000` | explicit command source | `[0x3B000,0x3B800)` | modeled | direct command blocks and owner golden route |
| NT51932 | `0x1F200` | implicit Primary to canonical Backup | terminal-marker rule | modeled | direct real-tool propagation test |
| NT51950 | `0x22200` | implicit Primary to canonical Backup | terminal-marker rule | modeled | direct real-tool propagation test |
| NT51951 | `0x22200` | implicit Primary to canonical Backup | terminal-marker rule | modeled | direct real-tool propagation test |

The executable six-mode implicit matrix is
`LegacyCombinerPostbuildRealToolSmokeTests.FirmwareVersionRouting.cs`. Explicit command routes remain
grounded in `ctrlram-postbuild-v2/catalog.json`; their `sourceOffset` must equal the IC's
`firmwareConfigPrimaryStart`.

## Decision

1. Replace the optional propagation flag with one required profile-owned write route:
   `command-source-to-canonical-backup`, `primary-to-canonical-backup`, or `unavailable`.
   Missing and unknown serialized values fail catalog construction.
2. `command-source-to-canonical-backup` requires exactly one `fw-config-backup` command block in the
   selected plan. Its firmware-image `sourceOffset` is the authoring source; its destination is the
   declared Backup copy range.
3. `primary-to-canonical-backup` requires no such command block. Its authoring source is the selected
   IC's TP Overview `firmwareConfigPrimaryStart`, and the exact legacy mode/tool relationship must
   have real-tool or firmware-owner evidence.
4. `unavailable` always rejects version authoring before processor execution. It is distinct from a
   forgotten field.
5. Every executable v2 route that permits version authoring must contain one exact
   `fw-config-source` region covering `FirmwareConfigLayout.RequiredLength`, owned by TP, with kind
   `firmware-config`. User access remains forbidden; only the typed Build-only edit adapter may write
   its three reviewed bytes.
6. The canonical destination remains the single terminal `00 4E 56 54` marker rule. Before planning
   any write, Primary/source and canonical Backup metadata must match exactly except for structure
   address. Final Backup validation remains mandatory and output promotion remains atomic.
7. Neutral metadata uses neutral names. `FirmwareConfigMetadata` should expose `StructureStart`;
   version write plans should expose `SourceStructureStart`, source field ranges, and canonical Backup
   field ranges. Region ids use `fw-config-source` and `fw-config-backup`. Existing
   `fw-config-backup-copy` ids may remain as compatibility ids until their profile family receives a
   separately reviewed version migration; the suffix never means authoring source.

## Ownership and dependency direction

- Profiles and the postbuild catalog own the route declaration and physical regions.
- Application owns the typed route, source/Backup validation, and immutable write plan.
- Infrastructure deserializes the closed value and runs only the registered tool in host staging.
- Bootstrap selects the reviewed profile and adapts the Application plan to the shared v2 compiler.
- Presentation only collects a Build-only version request and displays issues; it owns no offsets or
  propagation decisions.

## Invariants

- All ranges remain half-open and in the firmware/output address space.
- No source BIN or owner golden artifact is mutated.
- Version bytes are written before postbuild; CRC/header work observes the staged values.
- The processor may write only its compiled ranges, and final Backup values must match before atomic
  output promotion.
- Default CtrlRAM Build without a version edit remains byte-for-byte unchanged.
- A route declaration, v2 source region, or evidence gap fails closed and creates no support claim.

## Alternatives rejected

- Infer the source from the IC id or mode string: duplicates firmware semantics in Bootstrap and
  silently couples aliases to naming conventions.
- Always patch canonical Backup directly: legacy modes may overwrite it from Primary during
  postbuild, producing a zero-output or final-validation failure.
- Keep the optional Boolean/enum default: safe at runtime but cannot distinguish deliberate
  unavailability from incomplete modeling.
- Rename every legacy `fw-config-backup-copy` region in one migration: unnecessary profile/hash churn
  for destination ids that are already unambiguous in execution.

## Migration and tests

1. Add the required write route to every built-in postbuild profile and reject missing values.
2. Add an all-profile contract test: explicit routes have exactly one selected command source whose
   offset equals TP Overview Primary; implicit routes have none and are covered by the real-tool
   matrix; unavailable routes have neither.
3. Add `fw-config-source` to each affected v2 family/profile with version/hash updates and contract
   tests that locate all three edited bytes inside that region.
4. Rename neutral/source plan fields mechanically with Application and architecture coverage.
5. Run focused Application, Infrastructure real-tool, Bootstrap per-IC, profile-contract, and
   architecture tests, then Polytail and `python scripts/verify.py --all`.
6. Obtain firmware-owner review for the route matrix and changed-version output evidence before
   release. Existing no-edit golden parity remains valid but is not changed-version parity.

## Release impact

The proposed migration is support-neutral. It changes only Build requests that explicitly ask to edit
TP firmware version and were previously blocked by incomplete source modeling. It does not change
normal CtrlRAM output, postbuild order, CRC algorithms, tool versions, offsets, report schema, or
released support status. v0.9.12 release notes must distinguish real-tool propagation evidence from
full changed-version expected-output golden approval.
