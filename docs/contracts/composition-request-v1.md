# Composition Request Contract 1.0

## Purpose

A request binds files and approved run-time choices to an immutable `CompositionProfile`. It cannot introduce firmware semantics absent from the profile and canonical IC region catalog.

## Required groups

```text
schemaVersion
runId
profile { profileId, profileVersion, expectedSha256? }
inputBindings[]
explicitMappings[]
outputOptions
strictness
surface
```

Each input binding has a stable `bindingId`, declared `slotId`, and host-local path. Paths are resolved only by the host and are never forwarded to the Python worker or written to portable reports.

## Explicit mappings

`explicitMappings[]` is the only runtime extension surface for General Merge and General Replace:

```text
mappingId / sequence
operationKind: copy-range | replace-range
sourceBindingId / sourceRange
targetSpaceId / targetRegionId? / targetRange
overlapPolicy
alignment
reason
```

The compiler rejects dangling bindings, unequal lengths, out-of-bounds ranges, unapproved overlap, alignment violations, persona/region-policy violations, mappings used by fixed experiences, and an operation kind incompatible with `compositionKind`. Drag coordinates and UI pixels are never contract data.

## Security and determinism

- A request cannot add processors, commands, scripts, write ranges, operation kinds beyond the mapping contract, or validation rules.
- File hashes are calculated before preview/build; optional expected hashes fail closed.
- `strict` is the production default. `diagnostic` may return more issues but cannot commit invalid output.
- Date/name overrides are allowed only by profile policy.

Canonical schema: [`composition-request-v1.schema.json`](composition-request-v1.schema.json).
