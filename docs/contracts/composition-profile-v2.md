# Composition Profile Contract 2.0

The executable schema is [`composition-profile-v2.schema.json`](composition-profile-v2.schema.json).
It is the only declarative workflow policy compiled for Normal, AB, General, Merge, Replace, saved
rules, and future Register work.

## Boundary

A profile binds an exact firmware-family content hash and an explicit set of compatible map ids.
The compiler accepts one already resolved map, proves it is in that set, and records the exact map
and resolution fingerprint in `CompiledComposition`. The profile references canonical region and
metadata ids; it does not redeclare physical ranges, locators, capacities, aliases, or capabilities.

Metadata-value validation expected values use the exact typed field semantics from
[ADR 0016](../adr/0016-typed-firmware-metadata-values.md). Profiles resolve the referenced family
field before converting JSON: signed and unsigned integers remain distinct Domain values, byte fields
use exact-width lowercase hex, and printable text uses exact-width `0x20..0x7E` characters. Boolean,
numeric-string, signed/unsigned, text/bytes, padding, and truncation coercions are forbidden. Domain
family validation rechecks complete value representability after normalization.

`CompositionProfileDefinition` is the normalized typed form of this document. It owns:

- immutable input slots and acceptance/normalization policy;
- input, work-buffer, and final-output address spaces;
- logical views over map regions or profile-owned source/work ranges;
- metadata bindings, region authoring access, validations, and promotion state;
- one ordered operation list and closed processor stages; and
- output naming.

Every mutable space has exactly one engine-owned `blank` or immutable-slot `clone` initializer.
`clone.sourceSlotId` cannot reference a mutable space, which removes mutable initializer cycles by
construction. Exactly one space has kind `output-image`; it is the final output, and the `output`
naming object cannot select another space. The compiler rejects a missing slot, incompatible clone
capacity, duplicate initializer, or unresolved graph reference before plan creation. Callers bind
immutable artifacts only and cannot seed TPA, TPB, or other mutable work buffers.

## Input size policy

Every input declares an `artifactClass` and a closed length policy. `tp-firmware` must use
`tp-maximum-256k`, whose fixed limit is 262144 bytes and fails when oversized. A normal
`dp-firmware` source whose outer file length is not controlled uses
`normal-dp-extract-with-warning`: all referenced views must be in bounds, any difference from the
selected map capacity emits the declared warning, and operations copy only those views. A whole DP
flow such as the NT51950/NT51951 full-copy path uses `exact-resolved-map-capacity` and fails on
mismatch. `exact-bytes` and `bounded` remain available only for artifact classes whose owner policy
does not require one of those firmware-specific rules.

`pad-shorter` and `truncate-ctrlram` require evidence and mutate only a transient input buffer.
Padding is DP-only, `dp-replace` only, and forbidden when any processor/integrity stage exists.
Truncation is `ctrlram-replace` only and valid only when every affected operation target resolves to
a physical TP `ctrlram` region; it always emits the declared warning. These target-kind and capacity
checks are mandatory compiler semantic validation because they cross-reference the resolved family
map. Reference images, mutable work buffers, and processor-owned non-CtrlRAM flows remain exact.
Original input file names are an unconditional v2 provenance/UI invariant rather than a configurable
profile flag; output names still follow `output.fileNameTemplate`.

## Metadata and validation

Metadata bindings attach canonical family structures to named input/output spaces. They support
CMD, CMD-BK, FirmwareConfig, PID, and version facts without copying locator offsets into profiles.
`metadata-equality` verifies a new extractor against an independently modeled legacy extractor.
`pid-sanity` always rejects both `all-zero` and `all-ff`; it does not prove an input is TP by itself,
but it rejects the known invalid identities before compile. `reject-metadata-byte-pattern` remains a
generic validation for non-PID metadata.

Topology-independent shapes bind multiple map ids to one profile. This is how equivalent cases such
as NT51951 Standard avoid duplicate single/cascade UI choices. A shape such as NT51950 Standard may
still resolve distinct map ids from FirmwareConfig `ChipNumber`; only genuinely different compiled
behavior is exposed as a selectable possibility.

## Operations and processors

All workflows compile to the same operation algebra: `copy-range`, `replace-range`, `fill-range`,
`patch-scalar`, checked `transform-scalar`, and `run-processor`. `transform-scalar` is the bounded AB
relocation primitive: fixed width and byte order, unsigned source value, checked signed addend,
optional expected-before value, and reject-on-overflow. It is not an expression language.

Processor parameters are a closed union. `crc-worker-v1` references a registered calculation set and
is fixed to `calculate`, checksum purpose, and zero write views; it may verify existing integrity but
cannot transform bytes. `legacy-combiner-v1` references an approved tool binding and invocation
profile and is fixed to `transform` with at least one write view. Its integrity disposition is
`recalculate-and-write`, or evidence-backed `none` for a non-integrity transform. Neither variant can
contain a path, command, argument template, script, or arbitrary parameter object. Transform
execution uses a host-created staging copy, and the host rejects changed bytes outside the allowed
write views. `unknown` cannot appear in this contract.

## Promotion

The profile alone owns the monotonic stage:

```text
known -> map-resolvable -> inspectable -> authorable
-> compilable -> executable-candidate -> supported
```

Evidence manifests supply facts and blocker evidence but never set this stage. Migration preserves
the prior stage and blockers. `supported` requires no blockers plus all compiler, processor, golden,
human-review, and release gates; schema migration cannot promote a profile automatically.
