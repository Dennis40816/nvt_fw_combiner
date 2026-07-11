# ADR 0017: Bind firmware fact aliases to explicit maps

- Status: Proposed
- Date: 2026-07-11
- Owners: Product owner + architecture owner + firmware reviewers
- Amends: ADR 0015 and the firmware-family-v1 contract

## Context

ADR 0015 requires fact-scoped aliases and complete provenance, but the initial
`firmware-family-v1` shape did not identify the source and target maps of an alias. A region or
metadata set may be referenced by several map shapes with different capacity, topology, locators,
or predicates, so Profiles cannot safely infer which map establishes source availability.

The initial capability row also used a globally unique `capabilityId` with only `memberIds`, while a
capability alias carried map-shaped applicability. That cannot represent different member/map
states or prove that an alias is narrower than its source. First-match map selection, implicit
capability scope, or provenance chosen by declaration order would be nondeterministic firmware
authority.

## Decision

If accepted, the executable `firmware-family-v1` contract advances from draft schema version `1.0`
to `1.1`; the major contract and filename stay v1. The schema, DTOs, semantic normalizer, examples,
and tests change in one contract phase. Until that phase lands, this ADR is not normative and ADR
0015 plus the current v1.0 schema remain authoritative.

Aliases use three closed contract shapes: region set, metadata set, and capability. Every alias
declares exact source and target member/map ids, applicability, reason, and evidence. Region and
metadata aliases declare kind-specific set ids. Capability aliases declare kind-specific capability
fact ids. The normalized key is:

```text
(memberId, mapId, factKind, factId)
```

An edge points from one target key to one source key of the same kind. A chain terminates at exactly
one direct fact. Alias ids are family-global unique; target keys are unique; direct/alias conflicts,
cycles, unresolved references, overlapping providers, and declaration-order tie breaking are
forbidden.

### Direct facts

A direct region or metadata fact exists for a key only when the named map includes the member and
references the declared set id. Source and target maps are therefore evidence-bearing parts of
alias identity, not inferred lookup results. Its availability is exactly that map's complete
non-member applicability.

A direct capability row declares `capabilityFactId`, technical `capabilityId`, one `memberId`, one
`mapId`, applicability, state, reason, and evidence. `capabilityFactId` is the aliasable row identity;
`capabilityId` identifies the technical property. This supports different evidence states across
members and maps without making capability state a map-selection or execution input. Its map must
exist, include the declared member, select every structure used by its predicates, and contain the
capability applicability. The same rules apply after capability alias expansion.

### Map fact bindings

Profiles resolves aliases into immutable Domain bindings rather than cloning a source set or
replacing the effective id with the source id:

```text
FirmwareMapFactBinding<TFact>
  EffectiveKey
  DirectSourceKey
  CanonicalFactId
  Value
  Applicability
  Provenance
```

`FirmwareImageMap` retains member-specific region-set and metadata-set bindings. Every binding for
one physical map reference must terminate at the same global declared set id and immutable value;
member/map-scoped `DirectSourceKey` values may differ. `CanonicalFactId` expresses this physical
sameness independently from provenance identity. The effective target identity is preserved for
reports and fingerprints; the direct value remains immutable. A direct binding has equal
effective/source keys and an empty alias chain.

Region-set and metadata-set aliases must equal the complete non-member applicability of the target
map. Their source map or source alias may be broader but must contain that applicability. Conditional
physical bindings inside one map are rejected; the family must declare separate maps.

Every resolved source region set is revalidated against the target map's address space, capacity,
region graph, partitions, and ids. Every resolved source metadata set is revalidated against target
regions, locator geometry, fields, predicates, and artifact bindings. An alias never copies a map,
capacity, processor, profile, promotion state, workflow, or execution permission.

### Capability facts

Capability facts and aliases are normalized as a sibling collection to the Domain map-resolution
definition. They retain map-bound applicability and provenance but never affect map eligibility or
grant Build support. Only a promoted composition profile is execution authority.

Capability declarations for the same `(memberId, mapId, capabilityId)` may not have overlapping
applicability. Equal-state overlap is still rejected because it creates ambiguous evidence
provenance. `confirmed-present`, `confirmed-absent`, and `unknown` remain distinct states.

### Applicability containment

For scopes `A` and `B`, `A` is contained by `B` only when every selection accepted by `A` is
accepted by `B`:

- capacity is exactly equal;
- `A.modeIds` is a non-empty ordinal subset of `B.modeIds`;
- topology is mathematical set containment over positive chip counts: `none` is unbounded,
  `single` is `{1}`, `exact-count(n)` is `{n}`, and `cascade` is its inclusive bounded or unbounded
  interval;
- omitted Common FW categories mean category-independent/unbounded, otherwise ordinal set
  containment applies; and
- metadata predicates are conjunctions grouped by exact `(metadataStructureId, fieldId)`.

For one field, `equals(x)` is `{x}`, `one-of(S)` is finite `S`, and `not-equals(x)` excludes `x`.
Positive sets are intersected and exclusions applied. An empty result is invalid. A narrower finite
set implies a broader predicate only when all remaining values satisfy it. Without a finite positive
set, an exclusion can prove only exclusions it explicitly contains. Values use ADR 0016 exact typed
equality with no coercion. Unknown implication, an unselected structure, and a metadata-set alias
predicate dependency cycle all fail closed.

Satisfiability and overlap use the referenced field's complete finite typed domain, not an assumed
infinite universe: signed and unsigned integer domains come from carrier or effective bit-slice
width, bytes have `256^widthBytes` values, and printable ASCII has `95^widthBytes` values. An
exclusion-only conjunction is empty when its unique representable exclusions cover that domain.
Two constraints overlap only when their intersection is non-empty under the same domain. Checked
cardinality arithmetic may use arbitrary-precision integers; validation never enumerates the domain.

Profiles first resolves the structural fact-alias graph without evaluating applicability. It then
materializes and target-revalidates terminal metadata bindings, maps each applicability predicate to
the exact target-map binding that supplies its structure, and builds a dependency graph from each
predicate-bearing map, capability, or alias to those metadata bindings. Self-dependencies and every
multi-node cycle reject the family. Predicate JSON is typed only against the resolved,
target-revalidated structure. Source containment additionally requires the source map to select that
same canonical structure; Profiles never types a value against an inferred or unrelated source.

Every alias hop's requested applicability must be contained by the source hop and final direct
availability. Region/metadata target-map coverage is equality, defined as containment in both
directions.

### Provenance

Bindings and normalized capability facts own their one immutable provenance value. It retains
effective and direct keys, ordered target-to-source alias hops, each hop's applicability, reason and
evidence, and direct evidence. The normalized family exposes a derived enumeration of these values;
it does not keep a second mutable or independently constructed catalog. Sorting produces
deterministic serialization only and never resolves a conflict. A resolved map and later
`CompiledComposition` carry only the chains they actually used, anchored by the trusted family
content hash.

## Alternatives

- Infer maps from member and fact ids: rejected because several maps may satisfy the same strings.
- Rewrite target ids to source ids: rejected because it erases effective identity and inheritance
  provenance.
- Clone source facts under target ids: rejected because it duplicates physical facts and evidence.
- Allow conditional region/metadata bindings inside a map: deferred; current maps are immutable
  physical shapes, so conditional facts require explicit map splitting.
- Remove capability aliases: viable but unnecessarily prevents evidence-backed family inheritance;
  the owner specifically requires alias-family AB capability evidence to remain traceable. Explicit
  capability fact scope closes the ambiguity without making that evidence execution policy.

## Consequences

- Family JSON, DTOs, Domain map bindings, Profiles normalization, fingerprints, and reports gain
  explicit alias/provenance types.
- Alias-free region/metadata behavior remains valid and direct bindings have empty chains. Draft
  v1.0 grouped capability rows lack map and applicability evidence, so they require explicit reviewed
  v1.1 authoring. Profiles must not infer or mechanically expand missing scope, and v1.0 rows are not
  silently accepted as v1.1.
- No firmware range, operation, processor, support status, or output changes in this decision.
- This R2 contract change requires product/architecture approval before the ADR becomes Accepted.
- AB executable behavior remains a separate R3 evidence and firmware-owner gate.

## Verification

- Strict schema/DTO round trips for all three alias shapes and capability fact scope.
- Direct, one-hop, and multi-hop binding tests with effective identity retained.
- Unknown map/member/fact, wrong-kind, orphan, duplicate, overlap, and cycle rejection.
- Exhaustive topology-pair containment plus mode, capacity, category, and typed predicate tests.
- Target-map revalidation for region partitions and metadata locator geometry.
- Capability state isolation from map resolution and composition support.
- Stable ordered provenance and fingerprint coverage.
- Architecture tests keep alias resolution in Profiles and one compiler/executor path.
