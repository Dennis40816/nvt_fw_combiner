# Composition Profile Contract 2.0 through 2.10

The executable schemas are [`composition-profile-v2.schema.json`](composition-profile-v2.schema.json)
[`composition-profile-v2.1.schema.json`](composition-profile-v2.1.schema.json), and
[`composition-profile-v2.2.schema.json`](composition-profile-v2.2.schema.json), and
[`composition-profile-v2.3.schema.json`](composition-profile-v2.3.schema.json), and
[`composition-profile-v2.4.schema.json`](composition-profile-v2.4.schema.json), and
[`composition-profile-v2.5.schema.json`](composition-profile-v2.5.schema.json), and
[`composition-profile-v2.6.schema.json`](composition-profile-v2.6.schema.json), and
[`composition-profile-v2.7.schema.json`](composition-profile-v2.7.schema.json), and
[`composition-profile-v2.8.schema.json`](composition-profile-v2.8.schema.json), and
[`composition-profile-v2.9.schema.json`](composition-profile-v2.9.schema.json), and
[`composition-profile-v2.10.schema.json`](composition-profile-v2.10.schema.json). A trusted bundle
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
`V2PlanCompiled` eligibility remains non-executable except for the closed request-scoped candidate
contexts in ADR 0019 and ADR 0020. The separate `V2RuntimeExecutable` eligibility is minted only by
the Profiles compiler for the closed blank-output Merge or reference-clone DP Replace subset when
promotion is exactly `supported`, blockers are empty, each input slot has exactly one immutable
singleton space, and the output template is either token-free or the exact closed AB Code v1 template
with the `reject` invalid-character policy. The AB exception is admitted only for the AB Merge
execution context and is rendered by Application from accepted execution snapshots; arbitrary token
templates remain non-executable.
An `executable-candidate` never creates generic runtime authority, production routing, or support;
ADR 0019 logical-output and ADR 0020 runtime-reference-replace are the only explicit Application
admission shapes.
`supported` is profile-level V2 runtime admission, not a global IC or product-support claim; the
support matrix and its firmware-owner release gate remain separate authority.
Its `CompiledInputContract` retains each slot's id, role, artifact class, required/cardinality policy,
accepted extensions, typed length rule, typed normalization rule, and every immutable plan-space binding
including instance policy. The artifact does not treat `AddressSpace` geometry as a second source of
input acceptance policy; the plan projection must agree with the compiled contract.

Every admitted `requiredCapabilityIds` binding is retained in compilation provenance as the exact
effective/direct `FirmwareMapFactBinding`, including capability value, applicability, alias chain, and
evidence. The V2 compilation fingerprint format is `nfc.compiled-composition.profile-v2.v5` and binds
these compiled input and capability-admission decisions as well as bundle, map, promotion, validation,
output, and plan facts. Version 5 also frames the complete staged-artifact binding count and each
artifact id, source space, and source range for external-processor operations. The paired legacy
compilation format is `nfc.compiled-composition.legacy.v3`; these format revisions prevent artifacts
serialized under the earlier incomplete fingerprint grammar from retaining the same identity.

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
`replace-range` operations from declared `dp-firmware` inputs to canonical DP-owned regions or
from profile-declared `auxiliary` inputs to canonical LDC-owned regions. Every other source/owner
pair fails closed. DP Replace also permits only fully-covered `replace-existing`
`copy-range` operations sourced from the exact cloned reference image at the identical resolved half-open
range. `replace-range` is not a Merge
operation; a Replace `copy-range` from DP or a rejected replacement copy fails closed. Clone initialization
is permitted only for this exact, unnormalized `reference-image` DP Replace base. Metadata validation,
CRC-worker stages, and every other unrecognized runtime authority remain outside this subset and fail closed.
The reserved future-schema `legacy-combiner-v1` stage lowers through the existing external-processor port
only with profile-declared read/write ranges, staged sources, and, when required, named artifact bindings; it never grants
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
field when normalized directly. The 2.3 schema preserves the 2.2 Combiner binding grammar and adds
`runtime-request` capacity only to an `output-image`; `work-buffer` capacity remains `resolved-map` or
`fixed`. This token is reserved for the General Merge logical-output route in
[ADR 0019](../adr/0019-general-merge-logical-output-v2.md). Current map-bound lowering rejects it with a
stable unsupported-declaration issue, so 2.0 through 2.2 and all resolved-map runtime semantics remain
unchanged until that separate route is implemented and promoted.

Schema 2.4 adds an explicit `compilationContext`. Existing `resolved-map` declarations retain their
exact `mapBinding`; `logical-output` declarations bind an exact family and member allowlist without
claiming a physical image map. Logical output is restricted to declarative General Merge: one
per-binding auxiliary input template, one zero-filled runtime-request output, and no physical views,
regions, metadata, validations, processors, or profile-owned byte operations.

Schema 2.5 keeps the 2.4 logical-output shape, but its `logicalOutputBinding.memberIds` use canonical
firmware IC IDs such as `NT51920`. Each declared logical member must be an exact member of the
bound family snapshot; generic lowercase IDs cannot represent firmware-family membership. Existing
2.4 schema snapshots remain immutable and retain their original identifier grammar.

Schema 2.6 adds the map-bound `runtime-reference-replace` context from
[ADR 0020](../adr/0020-v2-runtime-reference-replace.md). Its original shape is restricted to declarative General
Replace: one exact singleton reference image, one unnormalized per-binding auxiliary source, a
runtime-capacity output cloned from the reference, declared physical region access, and no static
views, metadata, operations, validations, or processors. The typed runtime request supplies only
concrete immutable binding ids, their declared profile slot ids, exact binding lengths, and explicit
mappings. Only the unique singleton `reference-image` binding selects map capacity; auxiliary bindings
never select a map, and zero or multiple exact-capacity map candidates reject the request. The request
has no caller-selected capacity override, and every compiled binding id must match the Application
binding identity retained in reports and preview approval. This context remains candidate-only until
its runtime routing and firmware evidence gates are closed.

Schema 2.7 retains every 2.6 declaration and permits a `legacy-combiner-v1` stage to declare zero
`stagedArtifactBindings` when it uses only `stagedSourceBindings`. This does not grant an implicit
artifact: the processor adapter still rejects every undeclared or unused staged artifact. The change
allows source-only legacy postbuild plans to remain declarative. Schema 2.7 also permits a
`legacy-combiner-v1.invocationProfileId` to use either the canonical id grammar or the existing
published `nfc.` legacy Combiner catalog grammar, including its dot-version suffixes; arbitrary
processor identifiers remain invalid.

Schema 2.8 gives every `legacy-combiner-v1` stage a zero-based `targetViewId`. The target view may
cover the entire output or a prefix. Prefix
processing clones the complete reference container, stages only that prefix, imports only the audited
processor result, and preserves all bytes after the prefix. Nonzero subranges remain forbidden. This
is the TP-BIN/full-Flash convergence contract in
[ADR 0024](../adr/0024-ctrlram-tp-and-flash-base-convergence.md); it does not promote a candidate or
replace firmware-owner golden review. Exact ordered Combiner command facts remain selected by the
closed `invocationProfileId` and are loaded from the separately hash-pinned built-in Postbuild data
catalog; the composition profile does not duplicate that command table.

Schema 2.9 keeps every earlier resolved-map and processor contract and adds two closed alternatives
to `runtime-reference-replace`. A General Replace profile remains processor-free, or declares exactly
one final `run-processor` operation and one `legacy-combiner-v1` stage for TP header/integrity refresh.
The operation sequence is `2147483647`, its overlap policy is `replace-existing`, and the stage cannot
stage source or auxiliary artifacts. Its views are profile-owned output/map views only; the typed request
still supplies only immutable binding lengths and explicit mappings, never commands, processor paths,
arguments, or mutable buffers.

The CtrlRAM Replace alternative uses the same reference-clone and typed mapping
algebra but has a fixed/fixed experience, an unnormalized per-binding
`ctrlram-replacement` source, and exactly one final processor stage. Every
mapping target must resolve to a canonical TP-owned CtrlRAM region; no other
region class can borrow profile access. The reference length selects capacity,
and an explicit IC-number topology may disambiguate same-capacity single and
cascade maps through the canonical resolver. Only supplied source bytes are
mapped, so a short input preserves the cloned target tail and an oversized input
cannot expand section authority. This is candidate compilation, not built-in
runtime registration or support promotion.

For CtrlRAM Replace, the compiler intersects each profile-declared TP CtrlRAM
processor write view with the concrete mapping targets. Only those intersections
enter the external invocation's allowed-write set. Non-CtrlRAM processor-only
views remain exact profile authority. The compiled Domain artifact rejects a
broader CtrlRAM write range even when it is contained by a declared view.

The compiler intersects every accepted mapping target with canonical `owner = tp` regions. A DP-only
request omits the declared stage. One or many TP-touching mappings append the stage exactly once after
all mappings; TP authoring without that closed stage fails. Processor allowed-write views have separate
processor authority: they must be declared in the profile and permitted by the canonical physical map,
but a `hidden` or `read-only` authoring rule remains denied to the user. This permits reviewed Header/CRC
writes without exposing those ranges as General Replace mapping targets. Schema 2.9 adds compilation
authority only; it does not register a UI/CLI route, promote support, or replace Legacy Combiner golden
and firmware-owner review.

Schema 2.10 keeps every 2.9 execution and processor contract and adds the generic
`declared-prefix-with-warning` immutable Merge-source length rule. The declaration carries a positive
`requiredEndExclusive`, one to eight strictly ascending `expectedOuterLengths` that each cover that end,
a blocking `shortInputIssueCode`, and a non-blocking `unexpectedOuterLengthIssueCode`. The compiler binds
the accepted execution snapshot to `[0, requiredEndExclusive)`, rejects padding and non-Merge, reference,
or CtrlRAM use, and retains the complete policy in `CompiledInputContract` and its fingerprint. Every
source view and processor read must remain within the declared prefix. This contract does not admit an AB
route, change a profile promotion stage, or connect Application Build/report behavior by itself.

## Input size policy

Every input declares an `artifactClass` and a closed length policy. `tp-firmware` uses
`tp-maximum-256k`, `exact-bytes` no greater than 262144 bytes, or `declared-prefix-with-warning` whose
`requiredEndExclusive` is no greater than 262144, always without normalization.
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

`declared-prefix-with-warning` is available only to unnormalized immutable Merge sources with artifact
class `dp-firmware`, `tp-firmware`, or `auxiliary`. A source shorter than `requiredEndExclusive` is
blocking and receives no accepted execution snapshot. An accepted source exposes exactly the half-open
prefix `[0, requiredEndExclusive)`; bytes after that end remain immutable, are ignored by execution, and
must be retained as actual-source identity plus an ignored trailing range by the Application/report
integration. A supplied length absent from `expectedOuterLengths` emits the declared warning without
granting padding or changing any operation, metadata, or processor range. The first v0.9.14 pilot is
expected to declare `0x80000` for DP_AB and `0x40000` independently for TPA and TPB, each also as its sole
expected outer length; built-in profile wiring and firmware-owner/golden approval remain separate R3 gates.

`pad-shorter` and `truncate-ctrlram` require evidence and mutate only a transient input buffer.
Padding is DP-only, `dp-replace` only, and forbidden when any processor/integrity stage exists.
Truncation is `ctrlram-replace` only and valid only when every affected operation target resolves to
a physical TP `ctrlram` region; it always emits the declared warning. These target-kind and capacity
checks are mandatory compiler semantic validation because they cross-reference the resolved family
map. Reference images, mutable work buffers, and processor-owned non-CtrlRAM flows remain exact.
Original input file names are an unconditional v2 provenance/UI invariant rather than a configurable
profile flag; a V2 runtime binding supplies its original plain filename and caller-declared typed slot
assertion, which Application matches to the compiled slot and accepted extension before reading bytes.
The original filename remains in reports and preview-token identity. Runtime templates are normally
token-free. The sole exception is the exact AB Code v1 template
`NT{ic}_FlashCode_A_{dp-a}{tp-a}_B_{dp-b}{tp-b}_{date}.bin`, which Application renders from accepted execution
snapshots under ADR 0034; no other token template is executable. A static template supplies the
default output filename; the AB renderer supplies its automatic candidate. `allowOverride: false`
requires that automatic result, while `allowOverride: true` accepts another Windows-safe caller
filename that is bound to the Preview-to-Build token. Runtime admission requires the `reject`
invalid-character policy; `replace-underscore` remains non-executable. Output names still follow
`output.fileNameTemplate`.

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
contain a path, script, arbitrary parameter object, or caller-provided command. Transform execution uses a host-created staging
copy, and the host rejects changed bytes outside the allowed write views. The staged target is either
the complete image or an explicitly declared zero-based prefix; bytes after a prefix are retained by
the engine-owned clone. `unknown` cannot appear in this contract.

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
