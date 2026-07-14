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
| `BuiltInStandardMergeProfiles` | Standard Merge plan/contract parity tests | Direct V2 Standard Merge golden coverage independent of the C# profile oracle for every retained family | `v0.9.5` | retain as evidence |
| `BuiltInReplaceProfiles.DpPerspective` | None after V2 selector, summary, CLI, workbench, display, and golden routes converge on deployed NT51950/NT51951 V2 profiles | Direct V2 plan assertions for all capacities, public synthetic output hashes, owner self-replacement controls, and CLI/workbench regression | `v0.9.5` | retire in `refactor(replace): retire DP Perspective legacy profiles` |
| `CompositionProfileCompiler` legacy authority | Remaining Replace and General Merge/Replace runtime paths | Each applicable profile compiles and executes through trusted V2 authority with equivalent report/output behavior | after per-workflow R2/R3 evidence | retain as runtime dependency |
| `TpFlashMapCatalog` and `IcMetadataFacade` compatibility projection | Replace planning, UI metadata, number selection, and overview projection | Resolved V2 map/profile facts supply every current caller without copied firmware semantics | after per-workflow evidence | retain as compatibility projection |
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
