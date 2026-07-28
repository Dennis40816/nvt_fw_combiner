# Firmware Family Contract 1.1

The executable schema is [`firmware-family-v1.schema.json`](firmware-family-v1.schema.json).
It is the canonical source for physical firmware facts shared by Normal, AB, Merge, Replace,
General, saved-rule, and future Register workflows.
Bundles that require typed field relations or exact cross-family metadata-definition references use
the build-selected strict extension
[`firmware-family-v1-relations.schema.json`](firmware-family-v1-relations.schema.json); it retains the
same schema id and base ownership rules. The durable ownership, trust, and
prerequisite-resolution decision is recorded in
[ADR 0040](../adr/0040-canonical-metadata-definition-references-and-prerequisites.md).

Migration status: schema 1.1's dedicated partial relationship kinds remain the
current executable compatibility contract. ADR 0041 requires #177 to replace
them in the next reviewed contract revision with one typed
`SharedFactRelationship`; do not add another role-specific relationship kind to
schema 1.1. ADR 0042/#221 excludes retired ICs from that migration.

## Ownership

A family document owns only facts that can be resolved without choosing a workflow:

- IC members and evidence-backed technical capabilities;
- physical address spaces, exact map capacity, region hierarchy, half-open ranges, ownership, and
  kind;
- non-relaxable write constraints and alignment;
- metadata structures, fields, and bounded locators;
- map applicability by member, mode, topology requirement, capacity, and independently evidenced
  physical-variant predicates; and
- explicit, fact-scoped aliases.

A family document never grants execution support, declares operation order, invokes a processor,
or exposes a UI action. Newly inventoried regions use `writeConstraint = "forbidden"` until owner
evidence approves a narrower constraint. A composition profile may further restrict a map but may
not relax it.

## Family relationships

The strict relations schema permits zero or more owner-declared
`familyRelationships`. A relationship is a firmware-semantic declaration, not
an evidence alias, support decision, or runtime selector. Every relationship
has one ordinally unique `relationshipId`, at least two explicit `memberIds`, a
nonblank reason, and one or more evidence references. Membership is never
inferred from equal bytes, filenames, hashes, versions, PIDs, or golden
observations.

The closed relationship kinds are:

- `perfect-like-family`: all family-document semantics are owned once for the
  complete declared member set. Every map that contains one relationship
  member contains the exact complete member set; member-scoped fact aliases,
  capability exceptions, partial region lists, and partial metadata lists are
  forbidden. `sharedRegionIds` and `metadataDefinitionIds` are therefore
  omitted.
- `initial-code-shared-family`: only the listed Initial Code regions and their
  canonical metadata definitions are shared.
- `tp-shared-family`: only the listed TP regions and their canonical metadata
  definitions are shared.

A partial relationship lists one or more `sharedRegionIds`. Every member must
select the same region geometry for each listed id. Its optional
`metadataDefinitionIds` select canonical logical definition identities, not
structure instances or copied field/offset tables. Every member map must select
that definition through its own applicable binding, and all selected
structures must retain the same immutable definition reference. Unlisted
regions, metadata, capacity, topology, LDC, integrity, processor, workflow, and
publication facts remain outside the relationship.

Two relationships of the same kind may not overlap in membership, and every
declared member must exist in the family. Unknown members, missing shared
regions, unequal region geometry, unavailable or ambiguous metadata
definitions, member-specific perfect-like aliases/capabilities, and partial
perfect-like map membership reject normalization.

The family contract enforces only facts it owns. Composition operations,
processor stages, and product publication remain profile/policy authorities;
their convergence onto the same family-owned facts is a separate migration
gate and cannot be inferred from a relationship declaration.

Family identity is resolved before map applicability. The normalized requested IC must name an
explicit member or owner-declared fact-scoped alias; PID, firmware version, chip count, filename,
capacity, hash, and decoded payload metadata never discover, select, or change the family. Map
predicates may validate a physical variant inside the already selected family only when independent
owner-reviewed map or command evidence establishes that distinction. Golden fixture identity is not
such authority. CtrlRAM runtime-profile version and build-plan selection follow
[ADR 0031](../adr/0031-ctrlram-profile-intervals-and-build-plan-authority.md).

## Resolution

`TopologyRequirement` is static applicability. Runtime `TopologySelection` and other named
`ResolutionInputs` are Domain values and are not persisted as mutable family policy. Resolution
must produce exactly one map or return pending/rejected; it never chooses the first matching map.
The member id is supplied from the already resolved requested IC; resolution does not infer family
membership from artifact bytes. Requested topology selects or validates a build plan, not a family.

Every absolute or search range names its address space. Marker-relative locators require a bounded
search range, exact marker bytes, checked signed result offset, at least one structure assertion,
and an allowed result region. `unique` requires exactly one match. `terminal-match` requires an
exact evidenced match count and explicitly selects the lowest- or highest-address match. Zero
matches, the wrong count, an out-of-range result, or a failed assertion rejects resolution.
Matching examines every byte start where the complete marker fits, including overlapping matches.
A terminal expected count cannot exceed `searchLength - markerLength + 1`.

For a concrete FWConfig structure, profile authoring uses the canonical NVT Backup form: marker bytes
`00 4E 56 54`, `unique` selection, and result offset `-0xFFC` from marker start, which is equivalent to
terminal `T - 0xFFF`. The containing region is read-only locator evidence only; it never grants a write
or Replace range. The generic family schema does not infer FWConfig semantics from a structure-id string,
and V2 metadata lowering remains non-executable until field/assertion evidence is complete.

Every metadata structure declares one stable `artifactBindingId`; it matches the `artifactId` in
runtime map-resolution inputs and is never a path or filename. Structure ids are ordinally unique
across a family, and field ids are ordinally unique within a structure. Every metadata predicate
declares `metadataStructureId`, so predicate resolution uses the exact
`(artifactBindingId, metadataStructureId, fieldId)` source rather than a global field-name lookup.
A map predicate may reference only a structure selected through that map's metadata sets.

A located metadata structure is a binding instance: `structureId`, `artifactBindingId`, and
`locator`. Its locator-independent logical definition (`length`, `fields`, `assertions`, and
`relations`) is either declared inline exactly once or supplied by one `definitionReference`; the
two forms are mutually exclusive. A reference pins exact provider `familyId`, `familyVersion`,
`familyContentHash`, and logical `structureId`. Bootstrap resolves only owner-approved provider
bundles, and Profiles retains the provider's same immutable definition object. A missing, stale,
ambiguous, or non-allow-listed reference rejects the family; consumers never copy the provider's
offsets or field table.

`metadata-field-selected` is the closed dependent-locator form. It names one structure selected by
the same map, one unsigned prerequisite field, non-overlapping inclusive value branches, and one
checked signed result offset from each branch's addressed anchor. Every anchor uses the map address
space; the complete result must remain inside both the anchor and `allowedResultRegionId`.
Unsupported prerequisite values reject without a fallback. Missing prerequisite artifacts remain
typed pending requirements, rejected prerequisites block their dependents, and the complete
structure dependency graph must be acyclic.

## Typed metadata

Field and predicate semantics follow [ADR 0016](../adr/0016-typed-firmware-metadata-values.md).
The closed v1 encodings are `bytes`, `printable-ascii`, `unsigned-integer`, and `signed-integer`.
Integer carriers are one to four bytes and always declare byte order. Only unsigned integers may
declare a checked bit slice; signed integers use full-width two's-complement. Byte and printable-text
fields declare neither byte order nor slices.

Predicate JSON is interpreted only in the exact referenced field context. Integer values must fit the
signed carrier or unsigned effective slice. Byte values are exact-width lowercase hex strings, and
printable text is exactly `widthBytes` characters in `0x20..0x7E`. JSON Boolean values and scalar
coercion are not accepted.

Before field-range validation, integer normalization applies a representation-independent resource
ceiling of 4096 expanded decimal digits. Equivalent literal, decimal, and exponent forms receive the
same verdict. This ceiling prevents compact exponent expansion from exhausting resources; it does
not replace the much narrower signed/unsigned field representability checks.

Fields and assertions are structure-relative half-open ranges and must fit the complete structure
with checked arithmetic. Read-only fields may overlap, including multiple unsigned slices of one
carrier. Assertions may overlap fields or other assertions and form a conjunction. Omitted
`maskHex` is the canonical exact-match form; an explicit partial mask has equal length, contains at
least one set bit, is not all `ff`, and has zero expected bits outside the mask. Every assertion passes
before any field is decoded; failure yields no partial facts.

Each image-map shape has one exact `capacityBytes` and declares
`coveragePolicy = "complete-with-explicit-gaps"`. Semantic validation proves that referenced region
sets use the selected address space, stay in bounds, preserve proper parent containment and sibling
overlap rules, and partition the root capacity. Every region that has children is likewise
partitioned by its direct children. Every otherwise unclassified interval is represented by a
`reserved` or `unmapped` region.
`customer-information` and `ctrlram` are physical classifications, not workflow permissions.

## Aliases

Every direct physical fact and alias uses the exact key
`(memberId, mapId, factKind, factId)`. Region-set and metadata-set aliases name source and target
map ids plus kind-specific set ids. Capability aliases name source and target map ids plus
kind-specific capability fact ids. An alias copies immutable terminal values into bindings; it never
clones a set, rewrites an effective id to its source id, aliases a whole IC or map, or grants a
workflow, processor, capacity, promotion state, or execution permission.

Region-set and metadata-set alias applicability must exactly equal the complete non-member
applicability of the target map. Every hop must be contained by its immediate source and terminal
direct availability. A resolved target map revalidates terminal region and metadata values against
its own address space, capacity, region graph, locator geometry, fields, assertions, and artifact
bindings. Conditional physical bindings require separate maps.

Capability facts are one map-bound row per `(capabilityFactId, memberId, mapId)`; `capabilityFactId`
is map-key-local rather than family-global. Each row has a technical `capabilityId`, typed applicability,
state, reason, and evidence. Capability aliases preserve that
evidence provenance but never participate in map eligibility or grant Build support. For one
`(memberId, mapId, capabilityId)`, overlapping capability applicability is rejected even when state
is equal.

When a composition profile requires a technical capability, Profiles evaluates only bindings whose
effective member and map equal the resolved selection. Exactly one applicable `confirmed-present`
binding is required; direct-source identity, declaration order, `confirmed-absent`, `unknown`,
missing evidence, ambiguity, or unavailable independently evidenced physical-variant selection all
fail closed. The
selected binding retains its effective/direct keys and complete alias provenance for later compiler
fingerprinting; it still does not grant promotion or runtime execution authority.

All aliases are closed, map-bound facts. Unknown maps, members, set/fact ids, wrong kinds,
direct/alias conflicts, duplicate target providers, structural cycles, metadata dependency cycles,
ambiguous metadata structure providers, unsatisfied predicates, unknown implication, or widened
applicability fail closed. The normalized result retains effective key, direct source key, ordered
target-to-source hops, direct/alias evidence, and the trusted family content hash.
