# Composition Profile Contract 2.0, 2.1, and 2.2

The executable schemas are [`composition-profile-v2.schema.json`](composition-profile-v2.schema.json)
[`composition-profile-v2.1.schema.json`](composition-profile-v2.1.schema.json), and
[`composition-profile-v2.2.schema.json`](composition-profile-v2.2.schema.json). A trusted bundle
selects one exact schema snapshot through its manifest content hash. They are the only declarative
workflow policy compiled for Normal, AB, General, Merge, Replace, saved rules, and future Register work.

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
- the Replace-only IC-number selector policy;
- one ordered operation list and closed processor stages; and
- output naming.

## Compiled Plan Boundary

When a profile is admitted to one resolved map, lowering produces one V2 plan artifact. `Merge`
profiles omit `icNumberInputMode`; `Replace` profiles must declare exactly one of
`single-selector`, `cascade-selector`, or `numeric-selector`. This is profile execution authority,
not an experience, UI, or member-id inference. The
`V2PlanCompiled` eligibility remains non-executable. The separate `V2RuntimeExecutable` eligibility
is minted only by the Profiles compiler for the closed blank-output Merge or reference-clone DP
Replace subset when promotion is exactly `supported`, blockers are empty, each input slot has exactly
one immutable singleton space, and the output template is token-free with the `reject`
invalid-character policy.
`executable-candidate` is not runtime authority.
`supported` is profile-level V2 runtime admission, not a global IC or product-support claim; the
support matrix and its firmware-owner release gate remain separate authority.
Its `CompiledInputContract` retains each slot's id, role, artifact class, required/cardinality policy,
accepted extensions, typed length rule, typed normalization rule, and every immutable plan-space binding
including instance policy. The artifact does not treat `AddressSpace` geometry as a second source of
input acceptance policy; the plan projection must agree with the compiled contract.

Every admitted `requiredCapabilityIds` binding is retained in compilation provenance as the exact
effective/direct `FirmwareMapFactBinding`, including capability value, applicability, alias chain, and
evidence. The V2 compilation fingerprint format is `nfc.compiled-composition.profile-v2.v4` and binds
these compiled input and capability-admission decisions as well as bundle, map, promotion, validation,
output, and plan facts.

The current runtime lowering subset compiles every `regionAccessRules` declaration with the complete canonical
physical ancestor chain for every logical view. Region access remains profile-owned authoring policy and
cannot be silently dropped or treated as a UI-only restriction. A target write is deny-by-default: every
applicable profile rule and every governing physical write constraint must allow its half-open range.
`whole` requires exact region equality. `parts` names only direct canonical children of its declared region;
it does not imply arbitrary descendants. `explicit-range` requires containment and the declared physical
alignment. `hidden` and `read-only` never authorize a target write. The compiler retains these resolved
policy and logical view-provenance facts in the V2 compilation fingerprint. The selected map remains the
sole authority for physical region ranges; compiled views retain only their resolved half-open logical range
and exact physical region-chain identity so the artifact can verify that provenance.

The Merge subset lowers `copy-range`, `fill-range`, `patch-scalar`, and checked `transform-scalar`
operations with `reject` overlap policy. The DP Replace subset lowers one or more rejected
`replace-range` operations from declared DP inputs and only fully-covered `replace-existing`
`copy-range` operations sourced from the exact cloned reference image at the identical resolved half-open
range. `replace-range` is not a Merge
operation; a Replace `copy-range` from DP or a rejected replacement copy fails closed. Clone initialization
is permitted only for this exact, unnormalized `reference-image` DP Replace base. Metadata validation,
CRC-worker stages, and every other unrecognized runtime authority remain outside this subset and fail closed.
The reserved future-schema `legacy-combiner-v1` stage lowers through the existing external-processor port
only with profile-declared read/write ranges, staged sources, and named artifact bindings; it never grants
C# checksum or header calculation authority.

For this subset, an `exact-resolved-map-capacity` input binds an immutable source space at the resolved map
capacity. A `tp-maximum-256k` input retains its maximum-policy contract while its immutable source space is
the exact maximum end-exclusive span of its resolved source views; it never pads, truncates, or silently
accepts bytes outside that plan geometry.

Every mutable space has exactly one engine-owned `blank` or immutable-slot `clone` initializer.
`clone.sourceSlotId` cannot reference a mutable space, which removes mutable initializer cycles by
construction. Exactly one space has kind `output-image`; it is the final output, and the `output`
naming object cannot select another space. The compiler rejects a missing slot, incompatible clone
capacity, duplicate initializer, or unresolved graph reference before plan creation. Callers bind
immutable artifacts only and cannot seed TPA, TPB, or other mutable work buffers.

A `work-buffer` is a virtual engine-owned address space, not a physical firmware map region. Its
views must use `space-range`; operations may use it as an intermediate source or target without
creating a region-access rule. Only map-backed views are retained in physical access provenance and
are subject to map write constraints. A work buffer can never be selected as final output.

The pinned 2.0 schema intentionally forbids `stagedArtifactBindings`, so no existing trusted bundle
can opt into it by accident. The 2.1 schema permits the property only on a `legacy-combiner-v1` stage
and requires one or more bindings. Each maps one profile view to one named immutable artifact created
only inside the processor staging directory. The selected Combiner command plan must consume every
declared artifact, cannot refer to an undeclared artifact, and the adapter rejects any artifact mutation.
The 2.2 schema keeps that closed artifact-binding contract and adds the exact external-tool-manifest
binding identifier grammar only for `legacy-combiner-v1.toolBindingId`, permitting a version suffix such
as `legacy-combiner-1.13.0`. Every profile, view, stage, artifact, and invocation-profile identifier remains
a canonical lowercase hyphenated id. Schema 2.0 and 2.1 retain the canonical identifier grammar for this
field when normalized directly.

## Input size policy

Every input declares an `artifactClass` and a closed length policy. `tp-firmware` uses either
`tp-maximum-256k` or `exact-bytes` no greater than 262144 bytes, always without normalization.
`tp-maximum-256k` extracts the exact declared source span from a TP artifact within the fixed owner
limit; `exact-bytes` requires one exact TP artifact length and permits a same-capacity engine-owned
work buffer to clone it. A normal
`dp-firmware` source whose outer file length is not controlled uses
`normal-dp-extract-with-warning`: all referenced views must be in bounds, any difference from the
declared expected outer-container lengths emits the declared warning, and operations copy only those
views. The optional `expectedInputLengths` list has one to eight positive, strictly ascending entries;
when omitted, the compiler materializes the selected map capacity as the sole expectation. Every
declared expectation must cover the greatest end-exclusive source view. A whole DP flow such as the
NT51950/NT51951 full-copy path uses `exact-resolved-map-capacity` and fails on mismatch.
`bounded` remains available only for artifact classes whose owner policy does not require one of
those firmware-specific rules. `exact-bytes` is otherwise available only for artifact classes whose
owner policy permits it.

`pad-shorter` and `truncate-ctrlram` require evidence and mutate only a transient input buffer.
Padding is DP-only, `dp-replace` only, and forbidden when any processor/integrity stage exists.
Truncation is `ctrlram-replace` only and valid only when every affected operation target resolves to
a physical TP `ctrlram` region; it always emits the declared warning. These target-kind and capacity
checks are mandatory compiler semantic validation because they cross-reference the resolved family
map. Reference images, mutable work buffers, and processor-owned non-CtrlRAM flows remain exact.
Original input file names are an unconditional v2 provenance/UI invariant rather than a configurable
profile flag; a V2 runtime binding supplies its original plain filename and caller-declared typed slot
assertion, which Application matches to the compiled slot and accepted extension before reading bytes.
The original filename remains in reports and preview-token identity. Runtime templates are token-free.
Their static template supplies the default output filename; `allowOverride: false` requires that exact
filename, while `allowOverride: true` accepts another Windows-safe caller filename that is bound to
the Preview-to-Build token. Runtime admission requires the `reject` invalid-character policy;
`replace-underscore` remains non-executable until token rendering is implemented. Output names still
follow `output.fileNameTemplate`.

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
the prior stage and blockers. `supported` requires no blockers plus all applicable compiler,
processor, regression, human-review, and release gates; schema migration cannot promote a profile
automatically. For a previously executable legacy workflow, an owner may approve a versioned public
synthetic oracle and direct V2/legacy output comparison as the regression/release evidence for V2
runtime admission. That narrow migration decision must record its deterministic input generator,
static expected hashes, known deviations, and boundary cases. It is not hardware golden evidence and
does not change the product support matrix.
