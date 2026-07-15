# Legacy Retirement Matrix

## Purpose

A legacy type is removable only when every current production consumer has one
reviewed V2 replacement and direct regression evidence. A legacy-looking name
is not itself a removal criterion. This matrix is updated in the milestone
that changes a consumer or retires an item.

## Required evidence

Each row marked `retire` must link or name:

1. the exact V2 runtime path that replaces every production consumer;
2. direct contract/application/UI tests for that path;
3. golden or owner evidence when byte behavior, a processor, range, or
   firmware metadata is involved; and
4. the commit and milestone that delete the legacy item.

## Active matrix

| Legacy item | Current consumer | Replacement requirement | Earliest retirement | Status |
| --- | --- | --- | --- | --- |
| `BuiltInStandardMergeProfiles` | None. Standard Merge runtime, CLI/UI projections, direct V2 plan contracts, and golden execution all use V2 registrations | Direct V2 plan contracts for every declared shape; owner golden bytes where supplied; workbench golden regression for all tracked production cases | `v0.9.5` | retired in `2d1ded53` (`refactor(standard-merge): retire legacy C# profiles`); unobserved NT51950/951 capacities remain release-evidence gaps, not runtime gates |
| `BuiltInReplaceProfiles.DpPerspective` and companion `DpPerspectiveCatalog` | None. NT51950/NT51951 DP Replace runtime, Standard Merge policy projection, CLI/UI display, coverage, and golden execution read the trusted V2 registrations, resolved maps, and compiled plans | Direct V2 plan contracts across all declared capacities; static deterministic output hashes; archived owner-approved legacy comparison; owner self-replacement controls | `v0.9.5` | profile definitions retired in `7578de69`; the duplicate C# family-fact catalog retired in `5da4134c`. Neither phase changes candidate policy, firmware facts, Legacy Combiner, or other Replace workflows |
| Dynamic General Merge C# profile construction | None. Default General Merge, CLI, and UI route through the registered logical-output V2 profiles for all built-in ICs; persisted saved rules retain only a profile-id compatibility alias | Direct legacy/V2 byte parity for every built-in IC; default-route byte/report/UI/saved-rule regression; no physical-map, processor, CRC, or firmware-support claim | `v0.9.5` | retired in `a9a65b49` (`refactor(general-merge): route default workflow through V2`) |
| `CompositionProfileCompiler` legacy authority | Remaining Replace and General Replace runtime paths | Each applicable profile compiles and executes through trusted V2 authority with equivalent report/output behavior | after per-workflow R2/R3 evidence | retain as runtime dependency |
| `TpFlashMapCatalog` and `IcMetadataFacade` compatibility projection | CtrlRAM/General Replace planning, UI metadata, number selection, and overview projection | Resolved V2 map/profile facts supply every current caller without copied firmware semantics | after per-workflow evidence | retain as compatibility projection; DP Perspective no longer consumes its duplicate family-fact projection |
| `LegacyCombinerPostbuildCatalog` | CtrlRAM Replace staged `combiner.exe` postbuild | V2 processor declaration with the same declared staging/read/write ranges, exact tool binding, owner command evidence, and golden output | after R3 owner evidence | retain as processor authority |

## Non-targets

`FirmwareConfigMetadataReader` is not a legacy retirement target. It is the
current canonical runtime reader: every FW Config value comes from the unique
NVT Backup at terminal `T - 0xFFF`, with no primary-address fallback.

## Code-size guardrails

- A migration must delete an exactly replaced definition in the same milestone
  or add a matrix row explaining why it remains.
- New IC facts belong in a manifest-pinned profile bundle and shared catalog
  only when genuinely family-wide; do not add per-IC executors or subclasses.
- Do not add a second executor, compiler, loader, schema hierarchy, or
  adapter layer merely to bridge a migration.
- Production C# is measured per milestone. The current baseline is about
  52.4K nonblank lines; 60K is a review threshold, not permission to add
  unnecessary code.
