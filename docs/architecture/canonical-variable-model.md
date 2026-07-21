# Canonical Composition Variable Model

## Purpose

This is the stable vocabulary for Standard Merge, AB Merge, General Merge, DP Replace, CtrlRAM Replace, and General Replace. New features extend these orthogonal variables instead of adding workflow-specific flags or executors.

## Three model layers

| Layer | Contains | Must not contain |
| --- | --- | --- |
| Physical definition | firmware family, exact map capacity/regions, metadata locators, capability facts, aliases | workflow access, operations, processors, promotion |
| Workflow definition | profile map binding, slots, logical views, access, operations, processors, promotion | duplicated physical ranges, selected paths, commands |
| Compiled/run | resolved map, `CompiledComposition`, input bindings, output options, work buffers, mutations | new firmware semantics, mutable global state |

## Identity and intent

```text
bundleId / bundleVersion / bundleContentHash
familyId / familyVersion / familyContentHash / mapId
profileId / profileVersion / promotion.stage
compositionKind: merge | replace
experience
```

`compositionKind` determines the required initializer. `experience` determines catalog/UI authoring constraints. Neither selects a separate executor.

## Image initialization

```text
BlankMutableSpaceInitialization
  capacity: resolved-map | fixed
  fillByte

CloneMutableSpaceInitialization
  capacity: resolved-map | fixed
  sourceSlotId
```

- Merge's single output image starts from blank bytes.
- Replace's single output image clones an immutable reference slot.
- TPA, TPB, and other work buffers use the same engine-owned blank/clone initializers.
- Everything after initialization uses the same planner, operation algebra, processor port, validation engine, mutation trace, and report.

## Experience

```text
ExperienceDescriptor
  experienceId
  audience: system | dp | ctrlram | advanced
  layoutPolicy: fixed | constrained | user-defined
  inputPolicy: fixed | extensible
  topologyAuthoring: hidden | single-or-cascade | exact-count
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
| `work-buffer` | Yes | execution run |
| `output-image` | Yes | execution run |
| `worker-staging-file` | Yes, isolated | infrastructure adapter |

Every range names its address-space id. Bare offsets are invalid.

## Canonical regions

```text
regionId / parentRegionId?
owner: system | dp | tp | ldc | register | customer | shared | reserved | unknown
kind: code | header | data | command | firmware-config | ctrlram | customer-information | ...
range: half-open in one family address space
writeConstraint: forbidden | whole-region | declared-subregions | explicit-range
alignment
```

Experience access is separate from the canonical memory map:

```text
regionId
access: hidden | read-only | whole | parts | explicit-range
  allowedSubregionIds[]
reason
```

Deny by default. DP Replace may edit DP whole/declared parts. CtrlRAM Replace may edit only physical
regions with `owner = tp` and `kind = ctrlram`, or approved groups composed only of those regions.
General Replace may use explicit ranges only where explicitly enabled and never through protected
regions. Its enabled envelope is independent of the DP and CtrlRAM persona categories; a profile
may authorize any reviewed physical range without introducing a workflow-specific executor.

## Inputs

```text
slotId
role
artifactClass: tp-firmware | dp-firmware | reference-image | ctrlram-replacement | auxiliary
required
cardinality: exactly-one | zero-or-one | one-or-more
acceptedExtensions[]
lengthRule: tp-maximum-256k | exact-resolved-map-capacity |
            normal-dp-extract-with-warning | exact-bytes | bounded
normalization: none | pad-shorter | truncate-ctrlram
```

A selected file is a run binding; filenames do not define IC or range truth.

## Explicit mappings

```text
mappingId
sequence
operationKind: copy-range | replace-range
sourceBindingId
sourceRange
targetRegionId
targetOffset
overlapPolicy: reject | allow-declared | replace-existing
reason
```

- General Merge uses `copy-range` over blank initialization.
- General Replace uses `replace-range` over reference initialization.
- One BIN can provide many mappings; many BINs can contribute to one output.
- Canvas drag and exact table/manual entry edit the same mapping object; the normalized numeric model is authoritative.

## Operation algebra

```text
copy-range
replace-range
fill-range
patch-scalar
transform-scalar
run-processor
```

Every operation declares id, sequence, source/target logical views when applicable, overlap policy,
and reason. Views resolve to checked address-space ranges during compilation.

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

Map-resolution input is one atomic Domain value:

```text
FirmwareMapResolutionInputs
  memberId / modeId / exact capacity
  requestedTopology?: count + label + requested sourceId
  artifactPayloads[]
    artifactId (= declared artifactBindingId)
    private immutable byte snapshot
    computed SHA-256 + exact length
```

Domain snapshots each payload and computes its identity; callers cannot supply bytes and hashes as
parallel authorities. Family identity is already fixed by the requested IC and explicit family
membership. Decoded fields, locator outcomes, an independently evidenced runtime-profile interval,
and derived topology are resolver-owned candidate data inside that family; they never rediscover or
change the family. Pre-resolver applicability leaves those discriminators pending. Every
predicate names its metadata structure, and that structure names its artifact binding; comparison is
therefore limited to the exact `(artifactId, metadataStructureId, fieldId)` scope. Only outcomes from
the uniquely selected map enter `ResolvedFirmwareImageMap`, which retains identities but never bytes.

Compile result:

```text
CompiledComposition
  sole CompositionPlan
  bundle/profile/resolved-map hashes and provenance
  selection and locator outcomes
  validations, promotion verdict, output naming
  compilationFingerprint
```

Run request and derived execution data:

```text
compiledComposition
immutableInputBindings[]
outputOptions
previewToken?
resolvedArtifacts
versionTokens
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
