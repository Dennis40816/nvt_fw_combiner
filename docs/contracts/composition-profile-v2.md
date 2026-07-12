# Composition Profile Contract 2.0

The executable schema is [`composition-profile-v2.schema.json`](composition-profile-v2.schema.json).
It is the only declarative workflow policy compiled for Normal, AB, General, Merge, Replace, saved
rules, and future Register work.

## Boundary

A profile binds an exact firmware-family content hash and an explicit set of compatible map ids.
The compiler accepts one already resolved map, proves it is in that set, and records the exact map
and resolution fingerprint in `CompiledComposition`. The profile references canonical region and
metadata ids; it does not redeclare physical ranges, locators, capacities, aliases, or capabilities.
`requiredCapabilityIds` are requirements, not profile-owned evidence: each requires exactly one
applicable `confirmed-present` capability binding for the resolved effective member and map.
`confirmed-absent`, `unknown`, missing, ambiguous, or unavailable applicability evidence rejects
admission. Capability evidence does not change map selection, profile promotion, or execution support.

Metadata-value validation expected values use the exact typed field semantics from
[ADR 0016](../adr/0016-typed-firmware-metadata-values.md). Profiles resolve the referenced family
field before converting JSON: signed and unsigned integers remain distinct Domain values, byte fields
use exact-width lowercase hex, and printable text uses exact-width `0x20..0x7E` characters. Boolean,
numeric-string, signed/unsigned, text/bytes, padding, and truncation coercions are forbidden. Domain
family validation rechecks complete value representability after normalization.

Integer normalization applies the same representation-independent 4096-expanded-decimal-digit
resource ceiling as firmware-family normalization. Literal, decimal, and exponent forms with equal
mathematical value receive the same verdict. Field values must still pass their exact carrier or bit
slice, while operation sequence and signed addend values remain arbitrary precision within this
resource ceiling.

`CompositionProfileDefinition` is the normalized typed form of this document. It owns:

- immutable input slots and acceptance/normalization policy;
- input, work-buffer, and final-output address spaces;
- logical views over map regions or profile-owned source/work ranges;
- metadata bindings, region authoring access, validations, and promotion state;
- one ordered operation list and closed processor stages; and
- output naming.

## Compiled Plan Boundary

When a profile is admitted to one resolved map, lowering produces one non-executable V2 plan artifact.
Its `CompiledInputContract` retains each slot's id, role, artifact class, required/cardinality policy,
accepted extensions, typed length rule, typed normalization rule, and every immutable plan-space binding
including instance policy. The artifact does not treat `AddressSpace` geometry as a second source of
input acceptance policy; the plan projection must agree with the compiled contract.

Every admitted `requiredCapabilityIds` binding is retained in compilation provenance as the exact
effective/direct `FirmwareMapFactBinding`, including capability value, applicability, alias chain, and
evidence. The V2 compilation fingerprint format is `nfc.compiled-composition.profile-v2.v3` and binds
these compiled input and capability-admission decisions as well as bundle, map, promotion, validation,
output, and plan facts.

The blank-copy lowering subset compiles every `regionAccessRules` declaration with the complete canonical
physical ancestor chain for every logical view. Region access remains profile-owned authoring policy and
cannot be silently dropped or treated as a UI-only restriction. A target write is deny-by-default: every
applicable profile rule and every governing physical write constraint must allow its half-open range.
`whole` requires exact region equality. `parts` names only direct canonical children of its declared region;
it does not imply arbitrary descendants. `explicit-range` requires containment and the declared physical
alignment. `hidden` and `read-only` never authorize a target write. The compiler retains these resolved
policy and logical view-provenance facts in the V2 compilation fingerprint. The selected map remains the
sole authority for physical region ranges; compiled views retain only their resolved half-open logical range
and exact physical region-chain identity so the artifact can verify that provenance.

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
