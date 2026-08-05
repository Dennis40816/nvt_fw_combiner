# ADR 0008: Project Canonical IC Facts Through a Read-Only Metadata Facade

- Status: Superseded by ADR 0015; projection migration completed by issue #195
- Date: 2026-07-10
- Owners: Product owner + architecture owner
- Superseded by: ADR 0015 after old-catalog removal

## Context

> Compatibility note: ADR 0015 replaces the independent catalog ownership and Bootstrap join
> described below with canonical firmware-family/profile documents and resolved maps. The
> read-only projection and historical report snapshot principles remain valid. The temporary
> `IcMetadataFacade` named below was removed after its only callers converged on the public
> `WorkbenchCompositionService` projection. Issue #195 subsequently retired that
> broad projection; the canonical capability catalog and focused query/projection
> owners now expose the same read-only facts without a replacement facade.

The workbench needs onboarding/workflow exposure, TP Overview flash-map facts, FWConfig discovery, TP Header section labels, and postbuild category availability. These facts have distinct canonical owners. Letting each UI, CLI, Settings, and report-adjacent caller join those catalogs independently invites drift when an IC is added.

## Decision

Keep canonical facts in their existing owned categories:

- The former support catalog owned selectable IC rows, workflow exposure, aliases, and onboarding notes. ADR 0015 and issue #195 retire that independent owner; canonical profiles, exact publication-policy routes, and compiled capability projections now own those facts.
- Hash-pinned `profiles/built-in/ctrlram-postbuild-v2/flash-map.json` owns TP Overview regions, base shapes, and primary FWConfig addresses for inspection and evidence only; `BuiltInTpFlashMapCatalog` is its query adapter.
- `TpHeaderCatalog` owns the stable header, header-copy, CRC, and postbuild section taxonomy used for run-report labels.
- The hash-pinned `profiles/built-in/ctrlram-postbuild-v2/catalog.json` owns approved postbuild variants and Common FW category selection; Infrastructure validates and loads it, and Bootstrap exposes the typed projection.
- `GenFlashVersionCatalog` remains an independent DP-version extractor. Its in-progress work is intentionally not folded into this decision.

`FirmwareConfigMetadataReader` owns the generic Backup locator used for every runtime FWConfig value: it finds one unambiguous `00 4E 56 54` NVT end flag and reads the Backup at terminal `T - 0xFFF`. Missing or ambiguous markers fail closed and there is no primary-address fallback. Hash-pinned `flash-map.json` retains a primary FWConfig address only for TP Overview inspection and golden cross-check evidence; it is not a metadata source, a public FWConfig address, or a runtime prerequisite.

During the ADR 0015 migration, `NvtFwCombiner.Bootstrap.IcMetadataFacade` was an internal compatibility adapter that joined the old catalogs. v0.9.9 moved that join to the then-existing Workbench projection boundary. Issue #195 completes the migration: `CanonicalCapabilityCatalog` and focused capability, inspection, and authoring projections delegate every fact to its canonical owner.

`ICanonicalCapabilityQuery` is the Application read-only catalog boundary for UI/CLI/Settings queries. Focused adapters expose stable DTOs instead of a broad compatibility facade or legacy profile types. Executable profile summary facts, including required inputs and IC-number policy, come from `CompiledComposition`; a failed canonical compile remains visible through stable issue codes.

The report viewer remains snapshot-based: a historical report is interpreted from recorded report JSON, not reclassified using the current metadata facade.

## Consequences

- Adding an IC remains category-driven: onboarding entry, flash map, TP header/postbuild evidence where applicable, profile, and golden evidence.
- UI badges and shell readiness use the same IC support projection instead of a parallel list.
- A missing catalog row for a selectable workflow is caught by convergence tests.
- Firmware semantics remain outside Bootstrap; focused Bootstrap projections are read-only composition boundaries only.
- UI and CLI do not depend on legacy catalog joins or `CompositionProfileDefinition`.
- Focused inspection projections read FW/Common FW/PID/ChipNumber display and selection facts exclusively from the validated NVT Backup and expose its actual Backup start. Primary-vs-Backup equality remains golden evidence only and cannot replace or suppress Backup values at runtime.

## Verification

- Focused capability-projection tests compare each projected IC row and number choice with its canonical support and hash-pinned postbuild sources.
- FWConfig tests verify every current direct golden output has one NVT end flag and a matching `T - 0xFFF` Backup; NT51926 is specifically locked to end flag `0x3BFFF` and Backup `0x3B000`.
- Architecture tests forbid `IcMetadataFacade` and the retired Workbench facade from returning, lock the canonical capability-fact owners, and require UI/CLI to consume focused projections.
- Existing report tests retain JSON-only rendering behavior.
