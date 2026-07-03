# Canonical Composition Variable Model

## Purpose

This is the stable vocabulary for Standard Merge, AB Merge, General Merge, DP Replace, CtrlRAM Replace, and General Replace. New features extend these orthogonal variables instead of adding workflow-specific flags or executors.

## Three model layers

| Layer | Contains | Must not contain |
| --- | --- | --- |
| Definition | IC facts, canonical regions, experience policy, profile operations, processor requirements | selected paths, UI pixels, timestamps |
| Run binding | input bindings, output options, approved explicit mappings | new firmware semantics, commands, scripts, processors |
| Execution | resolved artifacts, address spaces, work buffers, plan, validations, mutations, hashes | mutable global state or writes back to definitions |

## Identity and intent

```text
schemaVersion
profileId / profileVersion / supportStatus
icId / modeId
compositionKind: merge | replace
experience
```

`compositionKind` determines the required initializer. `experience` determines catalog/UI authoring constraints. Neither selects a separate executor.

## Image initialization

```text
BlankImageInitialization
  capacity
  fillByte

ReferenceImageInitialization
  baseSlotId
  expectedCapacity
  baseValidationRules[]
```

- Merge starts from blank bytes.
- Replace starts from an immutable reference BIN cloned into the output work image.
- Everything after initialization uses the same planner, operation algebra, processor port, validation engine, mutation trace, and report.

## Experience

```text
ExperienceDescriptor
  experienceId
  audience: system | dp | ctrlram | advanced
  layoutPolicy: fixed | constrained | user-defined
  inputPolicy: fixed | extensible
  icNumInputMode: single | cascade | numeric
  displayNameKey
  regionAccessRules[]
```

Baseline experiences:

```text
standard-merge
ab-merge
general-merge
dp-replace
ctrlram-replace
general-replace
```

## Address spaces

| Kind | Mutable | Owner |
| --- | --- | --- |
| `input-artifact` | No | artifact loader |
| `reference-base` | No | artifact loader |
| `work-buffer` | Yes | execution run |
| `output-image` | Yes | execution run |
| `worker-staging-file` | Yes, isolated | infrastructure adapter |

Every range names its address-space id. Bare offsets are invalid.

## Canonical regions

```text
regionId
parentRegionId?
role
classificationTags[]
range
atomicity: whole | partitioned | explicit-mapping
writePolicy: forbidden | whole-only | declared-parts | general-explicit
alignment
processorDependencies[]
compatibilityTags[]
```

Experience access is separate from the canonical memory map:

```text
regionId
access: hidden | read-only | whole | parts | explicit-range
allowedPartIds[]
reason
```

Deny by default. DP Replace may edit DP whole/declared parts. CtrlRAM Replace may edit only `tp-ctrlram` regions/groups. General Replace may use explicit ranges only where explicitly enabled and never through protected regions.

## Inputs

```text
slotId
role
required
cardinality: exactly-one | zero-or-one | one-or-more
acceptedExtensions[]
sizeRule
fileNameGuards[]
contentGuards[]
compatibilityTags[]
```

A selected file is a run binding; filenames do not define IC or range truth.

## Explicit mappings

```text
mappingId
sequence
operationKind: copy-range | replace-range
sourceBindingId
sourceRange
targetSpaceId
targetRegionId?
targetRange
overlapPolicy: reject | allow-declared | replace-existing
alignment
reason
```

- General Merge uses `copy-range` over blank initialization.
- General Replace uses `replace-range` over reference initialization.
- One BIN can provide many mappings; many BINs can contribute to one output.
- Canvas drag and exact table/manual entry edit the same mapping object; the normalized numeric model is authoritative.

## Operation algebra

```text
initialize-image
create-work-buffer
copy-range
fill-range
patch-scalar
replace-range
run-external-processor
assert-range
validate-checksum
extract-metadata
finalize-output
```

Every operation declares id, sequence, source/target spaces and ranges when applicable, overlap policy, preconditions, postconditions, and reason.

## Integrity and processor authority

```text
integrityDisposition:
  none
  verify-existing
  recalculate-and-write

processorAuthority:
  calculate
  transform
```

Evidence inventories may use `unknown`; supported profiles may not. `transform` modifies only a host-created staging copy within declared write ranges. The host independently verifies the diff.

## Runtime and derived variables

Request:

```text
runId
profileId / profileVersion
inputBindings[]
explicitMappings[]
outputOptions
strictness
```

Derived execution data:

```text
resolvedArtifacts
versionTokens
compositionPlan
occupancySegments
processorRuns
issues
mutations
outputHash
report
```

Derived data is never persisted into canonical profile source.

## Anti-ambiguity rules

- All ranges are half-open `[start, endExclusive)`.
- JSON uses `start` + `length`; UI may additionally display inclusive end.
- `unknown` and `none` are different states.
- Experience controls authoring policy, not execution branching.
- A region label or filename is not a range definition.
- General layouts are validated request overlays or versioned profiles, not arbitrary code.
