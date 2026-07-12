# ADR 0008: Project Canonical IC Facts Through a Read-Only Metadata Facade

- Status: Accepted; catalog ownership superseded after ADR 0015 compatibility migration
- Date: 2026-07-10
- Owners: Product owner + architecture owner
- Superseded by: ADR 0015 after old-catalog removal

## Context

> Compatibility note: ADR 0015 replaces the independent catalog ownership and Bootstrap join
> described below with canonical firmware-family/profile documents and resolved maps. The
> read-only facade and historical report snapshot principles remain valid during and after the
> migration.

The workbench needs onboarding/workflow exposure, TP Overview flash-map facts, FWConfig discovery, TP Header section labels, and postbuild category availability. These facts have distinct canonical owners. Letting each UI, CLI, Settings, and report-adjacent caller join those catalogs independently invites drift when an IC is added.

## Decision

Keep canonical facts in their existing owned categories:

- `IcSupportCatalog` owns selectable IC rows, workflow exposure, aliases, and onboarding notes.
- `TpFlashMapCatalog` owns TP Overview regions and FWConfig start addresses.
- `TpHeaderCatalog` owns the stable header, header-copy, CRC, and postbuild section taxonomy used for run-report labels.
- `LegacyCombinerPostbuildCatalog` owns approved postbuild variants and Common FW category selection.
- `GenFlashVersionCatalog` remains an independent DP-version extractor. Its in-progress work is intentionally not folded into this decision.

`TpFlashMapCatalog` remains the source that declares whether an IC has a primary FWConfig location. `FirmwareConfigMetadataReader` owns the generic backup-copy locator used for display metadata: it finds one unambiguous `00 4E 56 54` NVT end flag and reads the copy at terminal `T - 0xFFF`. Missing or ambiguous markers fail closed. This locator is evidence-backed by the owner-approved Standard Merge output fixtures and does not replace flash-map, TP-header, or postbuild range ownership.

During the ADR 0015 migration, `NvtFwCombiner.Bootstrap.IcMetadataFacade` is an internal compatibility adapter that joins the old catalogs. It delegates selection to the canonical owners and must not copy ranges, header facts, version offsets, processors, or postbuild command data.

`WorkbenchCompositionService` is the public read-only integration surface for UI/CLI/Settings catalog queries. It exposes stable workbench DTOs rather than the compatibility facade or legacy profile types. Executable profile summary facts, including required inputs and IC-number policy, come from `CompiledComposition`; a failed compatibility compile remains visible through stable issue codes.

The report viewer remains snapshot-based: a historical report is interpreted from recorded report JSON, not reclassified using the current metadata facade.

## Consequences

- Adding an IC remains category-driven: onboarding entry, flash map, TP header/postbuild evidence where applicable, profile, and golden evidence.
- UI badges and shell readiness use the same IC support projection instead of a parallel list.
- A missing flash-map row for a selectable IC fails facade construction and is caught by convergence tests.
- Firmware semantics remain outside Bootstrap; the facade is a composition boundary only.
- UI and CLI do not depend on legacy catalog joins or `CompositionProfileDefinition`.
- The workbench may read FW/Common FW/PID/ChipNumber display facts from the validated NVT copy, while flash-map rows remain the primary per-IC validation evidence and the public metadata address remains the flash-map primary start.

## Verification

- Facade tests compare each projected IC row with its canonical onboarding, flash-map, postbuild, number-choice, and DP Perspective sources.
- FWConfig tests verify every current golden output has one NVT end flag and a matching `T - 0xFFF` copy; NT51926 is specifically locked to end flag `0x3BFFF` and copy `0x3B000`.
- Architecture tests ensure `WorkbenchCompositionService` uses the internal `IcMetadataFacade` rather than rebuilding catalog joins, and UI/CLI consume only the workbench projections.
- Existing report tests retain JSON-only rendering behavior.
