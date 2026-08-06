# Composition Profile Contract 2.0 through 2.15

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
[`composition-profile-v2.10.schema.json`](composition-profile-v2.10.schema.json), and
[`composition-profile-v2.11.schema.json`](composition-profile-v2.11.schema.json), and
[`composition-profile-v2.12.schema.json`](composition-profile-v2.12.schema.json),
[`composition-profile-v2.13.schema.json`](composition-profile-v2.13.schema.json), and
[`composition-profile-v2.14.schema.json`](composition-profile-v2.14.schema.json), and
[`composition-profile-v2.15.schema.json`](composition-profile-v2.15.schema.json). A trusted bundle
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

`CompositionProfileDefinition` is the Domain-owned normalized typed form of this document. It owns:

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
singleton space, and output naming is one of: a token-free legacy-schema
template, the exact closed AB Code v1 compatibility template, or a complete
schema-2.15 typed normal naming rule, all with the `reject` invalid-character
policy. The AB exception is admitted only for Merge composition with the exact
closed `AbCodeV1` renderer and is rendered by Application from accepted execution snapshots. A normal
FlashCode or TP-firmware template without its schema-2.15 rule id, artifact
type, typed token sources, and missing-value policies remains non-executable.
Arbitrary token templates remain non-executable.
An `executable-candidate` never creates generic runtime authority, production routing, or support;
ADR 0019 logical-output and ADR 0020 runtime-reference-replace are the only explicit Application
admission shapes.
`supported` is profile-level V2 runtime admission, not a global IC or product-support claim; the
support matrix and its firmware-owner release gate remain separate authority.
Its `CompiledInputContract` retains each slot's id, role, artifact class, required/cardinality policy,
accepted extensions, typed length rule, typed normalization rule, and every immutable plan-space binding
including instance policy. The artifact does not treat `AddressSpace` geometry as a second source of
input acceptance policy; the plan projection must agree with the compiled contract.

The schema successor implemented by #239 adds profile-owned input selection
groups. A group references existing `zero-or-one` slot ids and declares checked
`minimumSelected`/`maximumSelected` counts. Compilation evaluates the group
only across members applicable to the resolved map and retains the group
definition reference plus resolved selection/readiness state in the compiled
contract and fingerprint. It does not clone slot definitions, create another
route, or make unrelated multi-input profiles optional.

That successor also admits multiple declared maps for one NT51928 capability.
For Standard Merge, LDC absence selects the `0x40000` candidate; supplied LDC
selects the `0x80000` candidate and must then pass structural validation.
Failure blocks and never falls back to absence. DP Replace resolves the same
closed variants from accepted Reference length. Length never infers IC
identity.

Every admitted `requiredCapabilityIds` binding is retained in compilation provenance as the exact
effective/direct `FirmwareMapFactBinding`, including capability value, applicability, alias chain, and
evidence. The capability-bound V2 compilation fingerprint format is
`nfc.compiled-composition.profile-v2.v7`; it references the reviewed
`CapabilityFingerprint` and binds only exact compiled selection, input,
admission, map, validation, output, and plan state introduced by this
compilation. It does not repeat definition provenance already bound by the
capability. The unbound migration format remains v5. Version 5 and its bound
successor also frame the complete staged-artifact binding count and each
artifact id, source space, and source range for external-processor operations. The paired legacy
compilation formats are unbound v3 and capability-bound v4. Logical-output
formats are unbound v1 and capability-bound v3. These format revisions prevent artifacts
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

For this subset, an `exact-resolved-map-capacity` input binds an immutable
complete-container source space at the resolved map capacity. A canonical
`source-view-coverage` section input binds its immutable execution space to the
maximum end-exclusive selected read; it never pads, truncates, or grants access
to bytes outside that snapshot. The compatibility `tp-maximum-256k` token
currently lowers only a restricted subset of that behavior and is removed by
the ADR 0045 migration.

Every mutable space has exactly one engine-owned `blank` or immutable-slot `clone` initializer.
`clone.sourceSlotId` cannot reference a mutable space, which removes mutable initializer cycles by
construction. Exactly one space has kind `output-image`; it is the final output, and the `output`
naming object cannot select another space. The compiler rejects a missing slot, incompatible clone
capacity, duplicate initializer, or unresolved graph reference before plan creation. Callers bind
immutable artifacts only and cannot seed TPA, TPB, or other mutable work buffers.

A `work-buffer` is a virtual engine-owned address space, not a physical firmware map region. Its
views normally use `space-range`; schema 2.14 also permits a source-only
`region-template-range` selector to reuse one family-owned instance-relative
range without converting that work buffer into a physical map space.
Operations may use it as an intermediate source or target without creating a
region-access rule. Only map-backed views are retained in physical access
provenance and are subject to map write constraints. A work buffer can never
be selected as final output.

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

Schema 2.9 keeps every earlier resolved-map and processor contract and adds two closed semantic shapes
to `runtime-reference-replace`. A user-defined/extensible profile remains processor-free, or declares exactly
one final `run-processor` operation and one `legacy-combiner-v1` stage for TP header/integrity refresh.
The operation sequence is `2147483647`, its overlap policy is `replace-existing`, and the stage cannot
stage source or auxiliary artifacts. Its views are profile-owned output/map views only; the typed request
still supplies only immutable binding lengths and explicit mappings, never commands, processor paths,
arguments, or mutable buffers.

The fixed processor shape uses the same Replace reference-clone and typed mapping
algebra but has fixed/fixed authoring, a per-binding `ctrlram-replacement` source with
the evidenced `truncate-ctrlram` normalization, and exactly one final processor stage. Every
mapping target must resolve to a canonical TP-owned CtrlRAM region; no other
region class can borrow profile access. The reference length selects capacity,
and an explicit IC-number topology may disambiguate same-capacity single and
cascade maps when the canonical map applicability declares typed topology requirements. Workflow
identity remains trusted selection/provenance data and does not select this behavior. Only supplied source bytes are
mapped, so a short input preserves the cloned target tail and an oversized input
cannot expand section authority. This is candidate compilation, not built-in
runtime registration or support promotion.

For this typed CtrlRAM source shape, the compiler intersects each profile-declared TP CtrlRAM
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

Schema 2.11 keeps every 2.10 execution, input, and processor constraint and
adds a strict successor shape for metadata bindings. A binding uses exactly
one of:

- legacy `fieldIds`, retained only as a compatibility shape and optionally
  carrying `evidenceRefs`; or
- nonempty typed `targetReferences` plus nonempty `evidenceRefs`.

The two forms cannot be mixed. Each typed target is a reference-only
`{ targetKind, targetId }` pair whose closed kinds are `span`, `field`,
`series`, and `group`. It carries no range, offset, operation, processor,
stage, or write authority. Target existence and kind are validated against
the already resolved canonical metadata definition.

The 2.11 closed metadata-purpose vocabulary is `map-resolution`,
`validation`, `output-naming`, `display`, `version`, `inspection`,
`formatting`, `copy`, `relocation`, `integrity`, `processor`,
`memory-projection`, and `report-classification`. A purpose states why a
read-only metadata reference is consumed; it does not authorize execution or
derive firmware facts.

Schema 2.12 keeps every 2.11 execution, input, processor, and metadata
constraint and adds canonical input-selection groups plus warning-only
plausibility validation:

- `inputSelectionGroups` declare a closed set of input slot ids with
  `minimumSelected` and `maximumSelected` cardinality. A group references
  canonical inputs; it does not redefine their ranges, operations, or
  validation.
- `optionalRegionIds` on a map binding declare which canonical regions may be
  absent for that resolved map. A selected group member whose bound region is
  absent is `NotApplicable`, with its optional profile-owned
  `notApplicableReason`; it is never silently ignored.
- optional input operations and views are lowered only when their input is
  selected and applicable. Profiles without a selection group preserve their
  prior required-input behavior.
- `non-uniform-region` reads one declared source view and emits its typed
  warning when every byte has the same value. It is advisory only and cannot
  block execution or alter output bytes.

Application may compose these retained validations with the resolved map
capacity and DP/TP input-space bindings to classify a candidate. A `FlashCode`
result requires exact declared capacity plus complete and plausible
DP/Initial-Code and TP projections. Missing DP/TP validation authority,
incomplete coverage, or a repeated-byte TP view cannot produce `FlashCode`;
the result is `Unknown` unless the declarations positively establish a TP-only
artifact. Classification never changes slot admission or selects a route.

Application resolves group readiness from the compiled group references and
the selected inputs. UI and CLI consume that same typed result; adapters must
not recreate cardinality or map-applicability rules.

Schema 2.13 keeps every 2.12 execution, selection, and plausibility constraint
and adds the canonical `source-view-coverage` section-admission policy:

- the compiler derives the minimum required source end from the profile's
  canonical views, metadata reads, validations, processor reads, and cloned
  work buffers for that input space;
- the runtime rejects an artifact that does not cover that derived end;
- bytes outside the compiled source projection do not become execution,
  processor, validation, or write authority;
- optional `expectedOuterLengths` and
  `unexpectedOuterLengthIssueCode` are paired advisory diagnostics only; and
- only unnormalized `tp-firmware`, `dp-firmware`, and `auxiliary` section
  sources may use this policy. Reference images, CtrlRAM payloads, and complete
  DP AB containers retain their explicit closed policies.

Schema 2.14 keeps every 2.13 admission and execution constraint and adds two
family-derived composition primitives:

- source views in immutable input spaces or cloned work buffers may use
  `region-template-range` with exact `regionInstanceId` and
  `templateRegionId`; the compiler resolves one instance in the selected map,
  verifies its address space, and exposes the template-relative range, so
  symmetric A/B inputs read identical native coordinates;
- a `transform-scalar` addend may remain a fixed integer or use
  `region-instance-delta` with exact source and target instance ids; the
  compiler requires one instance for each id, the same canonical template,
  compatible address spaces, and a checked target-base-minus-source-base
  result; and
- for an instance-derived addend, the compiled fingerprint additionally
  retains the addend source kind and both instance identities. Existing fixed
  numeric-addend fingerprints remain byte-compatible, while a fixed value and
  a geometry-derived value cannot collide merely because their current
  numeric result is equal.

Neither primitive grants a write range. Output views remain map-backed, and
all writes still pass the existing region-access, overlap, containment, and
write-constraint checks.

Schema 2.15 keeps every 2.14 firmware range and execution constraint and adds
typed normal output naming authority:

- `output.ruleId` is one of `normal-flashcode-v1` or `tp-firmware-v1`;
- `output.outputArtifactType` is the closed `flash-code` or `tp-firmware`
  artifact identity and must match the selected rule;
- `output.invalidCharacterPolicy` is exactly `reject`; schema 2.15 does not
  admit a typed renderer that can lower only as deferred;
- `output.tokenRequirements` exactly matches `requiredTokenIds` and declares
  one closed source plus one `block` or `use-placeholder` policy per token;
- the closed sources are `compiled-ic`, `run-date-utc`, `dpcmi-version`, and
  `firmware-config-tp-version`;
- metadata-backed sources require one exact `metadataBindingId`. That binding
  must declare purpose `output-naming` and resolve the canonical `dpcmi` or
  `firmware-config-general-parameters` structure owned by the selected source;
- the normal FlashCode contract requires IC, UTC date, DPCMI version, and
  FirmwareConfig TP version. The TP-firmware contract omits DPCMI by rule;
- both metadata version tokens use the exact compiled `xxxx` placeholder when
  their accepted value is unavailable, while IC and date block when missing;
  and
- the compiled fingerprint retains rule id, artifact type, token source,
  metadata binding and exact input space, missing policy, and placeholder. A
  template string alone cannot collide with or infer typed runtime authority.

Schema 2.15 does not migrate existing profile files implicitly. A profile that
selects 2.15 must declare the complete typed output block; older schema
snapshots retain their exact static and AB compatibility contracts.

## Input size policy

Every input declares an `artifactClass` and a closed length policy. The
canonical section policy is `source-view-coverage`. The compiler derives its
required end from every selected source view, metadata binding, input
validation, and processor read for that input space. The selected artifact must
cover that end. Optional expected outer-container lengths have one to eight
positive, strictly ascending entries; a nonmatching accepted length emits the
declared warning while execution exposes only the bounded declared snapshot.

Initial Code, DP, TP, LDC, TPA, and TPB are address-bearing section sources. A
standalone section artifact and a compatible same-IC FlashCode are admitted by
the same source views; outer length does not change source coordinates or grant
write/processor authority. Application technical file ceilings remain
resource policy rather than firmware length rules.

The current `tp-maximum-256k` and `normal-dp-extract-with-warning` wire tokens
are migration aliases for subsets of source-view coverage and accept no new
profile authority. They are deleted after ADR 0045 profiles, compiled
requirements, CLI/report consumers, and tests converge. `exact-bytes` and
`bounded` remain available only where owner policy explicitly requires a
payload/container bound.

A complete-container flow uses `exact-resolved-map-capacity` or another closed
declared-capacity variant and fails on mismatch. This includes Replace
Reference and a complete DP AB seed. A section input must not use whole-map
capacity merely because its range is inside that map.

`declared-prefix-with-warning` is a compatibility form available only to unnormalized immutable Merge sources with artifact
class `dp-firmware`, `tp-firmware`, or `auxiliary`. A source shorter than `requiredEndExclusive` is
blocking and receives no accepted execution snapshot. An accepted source exposes exactly the half-open
prefix `[0, requiredEndExclusive)`; bytes after that end remain immutable, are ignored by execution, and
must be retained as actual-source identity plus an ignored trailing range by the Application/report
integration. A supplied length absent from `expectedOuterLengths` emits the declared warning without
granting padding or changing any operation, metadata, or processor range. ADR
0045 migrates TPA/TPB to generic address-bearing section coverage and the
complete DP AB seed to an exact declared container variant; built-in profile
wiring and firmware-owner/golden approval remain separate R3 gates.

`pad-shorter` and `truncate-ctrlram` require evidence and mutate only a transient input buffer.
Padding is limited to a typed `dp-firmware` Replace source and is forbidden when any
processor/integrity stage exists. Truncation requires a typed `ctrlram-replacement` Replace source
and is valid only when every affected operation target resolves to a physical TP `ctrlram` region;
it always emits the declared warning. These target-kind and capacity
checks are mandatory compiler semantic validation because they cross-reference the resolved family
map. Reference images, mutable work buffers, and processor-owned non-CtrlRAM flows remain exact.
Compact CtrlRAM replacement is the only current built-in payload-relative
source: byte `0` maps to the declared CtrlRAM target. Dynamic DiffDLM masked
scatter remains inside that CtrlRAM authority. TPB instead reads a TP-native
source window and applies a resolved bank placement delta.
Original input file names are an unconditional v2 provenance/UI invariant
rather than a configurable profile flag; a V2 runtime binding supplies its
original plain filename and caller-declared typed slot assertion, which
Application matches to the compiled slot and accepted extension before reading
bytes. The original filename remains in reports and preview-token identity.

Legacy runtime templates are normally token-free. The exact AB Code v1
compatibility template
`NT{ic}_FlashCode_A_{dp-a}{tp-a}_B_{dp-b}{tp-b}_{date}.bin` remains executable
only for Merge composition with the exact closed `AbCodeV1` renderer under ADR
0034 and the identity-independent compiler rule in ADR 0015. Schema 2.15 additionally admits the exact
typed normal FlashCode and TP-firmware rules described above; matching template
text in an older schema does not select those renderers.

Application resolves a normal name only from the compiled token-source
declarations and one already accepted metadata inspection. It selects metadata
by compiled binding id, never by presentation text or a new read. The run
boundary requires the accepted inspection and a current admission identity to
match exact route id, capability fingerprint, metadata-plan resolution token,
and authoring revision. A changed publication or revision is stale even when
compiled bytes and the compilation fingerprint are otherwise identical. That
identity is retained in the report and Preview-to-Build token, and Build
requires a freshly captured matching admission.

ADR 0046 separates that reviewed definition identity from per-compilation
identity. `CapabilityFingerprint` binds the complete allowed capability
definition and its policy. `CompiledComposition.CompilationFingerprint` binds
the exact selected map/topology, selected slots, authored mappings/initializer,
selected processor plan, and lowered plan for one compilation. Preview, Build,
Memory Layout when compiled, naming, and reports consume the same immutable
compiled instance; none may recompile it or treat the capability fingerprint as
the compiled-plan fingerprint.

A static template supplies the default output filename; dynamic renderers
supply their automatic candidate. `allowOverride: false` requires that
automatic result, while `allowOverride: true` accepts another Windows-safe
caller filename that is bound to the Preview-to-Build token. Runtime admission
requires the `reject` invalid-character policy. Older schema snapshots may
retain `replace-underscore` only as non-executable legacy policy; schema 2.15
rejects it. Output names still follow `output.fileNameTemplate`.

## Metadata and validation

Metadata bindings attach canonical family structures to named input/output spaces. They support
CMD, CMD-BK, FirmwareConfig, PID, and version facts without copying locator offsets into profiles.
`metadata-equality` verifies a new extractor against an independently modeled legacy extractor.
`pid-sanity` always rejects both `all-zero` and `all-ff`; it does not prove an input is TP by itself,
but it rejects the known invalid identities before compile. `reject-metadata-byte-pattern` remains a
generic validation for non-PID metadata.

The #239 schema successor adds warning-only `non-uniform-region` plausibility
validation over one canonical source view. The view must contain more than one
distinct byte to avoid the warning. A uniform view emits the declared typed
warning through Application, CLI, UI, Preview, and Build Report but never
changes map resolution, group selection, execution admission, or output bytes.
Profiles opt in explicitly; artifact class, filename, and hash do not attach
the rule.

Topology-independent shapes bind multiple map ids to one profile. This is how equivalent cases such
as NT51951 Standard avoid duplicate single/cascade UI choices. A shape such as NT51950 Standard may
still resolve distinct map ids from FirmwareConfig `ChipNumber`; only genuinely different compiled
behavior is exposed as a selectable possibility.

## Operations and processors

All workflows compile to the same operation algebra: `copy-range`, `replace-range`, `fill-range`,
`patch-scalar`, checked `transform-scalar`, and `run-processor`. `transform-scalar` is the bounded AB
relocation primitive: fixed width and byte order, unsigned source value, a
checked signed addend declared either as an integer or the schema-2.14
`region-instance-delta`, optional expected-before value, and
reject-on-overflow. It is not an expression language.

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
