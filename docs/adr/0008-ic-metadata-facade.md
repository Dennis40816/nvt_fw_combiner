# ADR 0008: Project Canonical IC Facts Through a Read-Only Metadata Facade

- Status: Accepted
- Date: 2026-07-10
- Owners: Product owner + architecture owner

## Context

The workbench needs onboarding/workflow exposure, TP Overview flash-map facts, FWConfig discovery, TP Header section labels, and postbuild category availability. These facts have distinct canonical owners. Letting each UI, CLI, Settings, and report-adjacent caller join those catalogs independently invites drift when an IC is added.

## Decision

Keep canonical facts in their existing owned categories:

- `IcSupportCatalog` owns selectable IC rows, workflow exposure, aliases, and onboarding notes.
- `TpFlashMapCatalog` owns TP Overview regions and FWConfig start addresses.
- `TpHeaderCatalog` owns the stable header, header-copy, CRC, and postbuild section taxonomy used for run-report labels.
- `LegacyCombinerPostbuildCatalog` owns approved postbuild variants and Common FW category selection.
- `GenFlashVersionCatalog` remains an independent DP-version extractor. Its in-progress work is intentionally not folded into this decision.

`NvtFwCombiner.Bootstrap.IcMetadataFacade` is the sole read-only integration surface for UI/CLI/Settings catalog queries. It projects metadata and delegates selection to the canonical owners; it must not copy ranges, header facts, version offsets, processors, or postbuild command data.

The report viewer remains snapshot-based: a historical report is interpreted from recorded report JSON, not reclassified using the current metadata facade.

## Consequences

- Adding an IC remains category-driven: onboarding entry, flash map, TP header/postbuild evidence where applicable, profile, and golden evidence.
- UI badges and shell readiness use the same IC support projection instead of a parallel list.
- A missing flash-map row for a selectable IC fails facade construction and is caught by convergence tests.
- Firmware semantics remain outside Bootstrap; the facade is a composition boundary only.

## Verification

- Facade tests compare each projected IC row with its canonical onboarding, flash-map, postbuild, number-choice, and DP Perspective sources.
- Architecture tests ensure the workbench catalog facade uses `IcMetadataFacade` rather than rebuilding catalog joins.
- Existing report tests retain JSON-only rendering behavior.
