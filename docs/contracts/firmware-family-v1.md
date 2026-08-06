# Firmware Family Contract 1.1 through 1.2

The executable schema is [`firmware-family-v1.schema.json`](firmware-family-v1.schema.json).
It is the canonical source for physical firmware facts shared by Normal, AB, Merge, Replace,
General, saved-rule, and future Register workflows.
Bundles that require typed field relations or exact cross-family metadata-definition references use
the build-selected strict extension
[`firmware-family-v1-relations.schema.json`](firmware-family-v1-relations.schema.json); it retains the
same schema id and base ownership rules. The durable ownership, trust, and
prerequisite-resolution decision is recorded in
[ADR 0040](../adr/0040-canonical-metadata-definition-references-and-prerequisites.md).
Bundles that additionally declare typed TP Flash Header metadata use the
build-selected strict successor
[`firmware-family-v1.1-tp-header.schema.json`](firmware-family-v1.1-tp-header.schema.json).
It retains schema version `1.1`, the common firmware-family schema id, every
relations-schema constraint, and is selected by its exact trusted content
hash.
Bundles that need the admitted `data`, `firmware-config`, `ctrlram`, and
`mp-ctrlram` TP Header subjects select the closed-vocabulary successor
[`firmware-family-v1.2-tp-header-subjects.schema.json`](firmware-family-v1.2-tp-header-subjects.schema.json).
It retains schema version `1.1`, the same schema id and every 1.1 invariant;
the new file extends only the typed Header subject vocabulary and is pinned by
its own trusted content hash.
Bundles that additionally require repeated instance-relative bank geometry use
the build-selected strict successor
[`firmware-family-v1.2-bank-instances.schema.json`](firmware-family-v1.2-bank-instances.schema.json).
It retains the common firmware-family schema id and TP Header constraints,
uses schema version `1.2`, and is selected by its exact trusted content hash.

Migration status: the strict relations schema and TP Header successors implement
ADR 0041 with exactly two serialized relationship forms:
`perfect-like-family` and `shared-fact-relationship`. The former dedicated
`initial-code-shared-family` and `tp-shared-family` discriminators are no longer
admitted. ADR 0042/#221 excludes retired ICs from this migration.

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

## Region templates and instances

Schema 1.2 permits one region set to own paired `regionTemplates` and
`regionInstances`. Both properties are present together or absent together.
A template owns one positive capacity and one or more template-local physical
regions whose half-open ranges, parent chains, ownership, kind, write
constraint, and alignment follow the same rules as direct regions.

An instance owns an ordinally unique `instanceId`, one exact template id, a
nonnegative base offset, an optional direct-region parent for template roots,
and exactly one `resolvedRegionId` binding for every template-local region id.
Bindings cannot be omitted, duplicated, or added for an unknown template
region. Expansion adds the instance base to each relative range, rewrites
template-local parent ids through the same binding table, and preserves the
external parent only for template roots.

The normalized region set exposes direct and expanded regions through the same
physical-region collection. Their resolved ids remain globally unique in that
set, ranges remain checked and half-open, and all existing hierarchy,
containment, overlap, map-capacity, and write-constraint rules continue to
apply. A template or instance is declared family geometry only; it neither
infers A/B symmetry nor authorizes composition. Profiles may name exact
instance and template-region ids only through a schema version that explicitly
admits those selectors or addend sources.

## Family relationships

The strict relations schema permits zero or more owner-declared
`familyRelationships`. A relationship is a firmware-semantic declaration, not
an evidence alias, support decision, or runtime selector. Every relationship
has one ordinally unique `relationshipId`, at least two explicit `memberIds`, a
nonblank `reason`, and one or more `evidenceRefs`. Membership is never inferred
from equal bytes, filenames, hashes, versions, PIDs, or golden observations.

There are exactly two serialized relationship forms:

- `perfect-like-family` declares complete semantic equivalence for its members.
  Its exact shape is `relationshipId`, `relationshipKind`, `memberIds`,
  `reason`, and `evidenceRefs`. Every map that contains one relationship member
  contains the exact complete member set; member-scoped fact aliases,
  capability exceptions, partial region lists, and partial metadata lists are
  forbidden.
- `shared-fact-relationship` declares only explicitly referenced facts as
  shared. Its exact shape is `relationshipId`, `relationshipKind`, `memberIds`,
  `role`, `applicability`, `sharedFactReferences`, `reason`, and
  `evidenceRefs`. `applicability.mapIds` is a nonempty exact map-id set; it is
  never inferred from relationship membership or equal map contents.

The closed author-facing roles for `shared-fact-relationship` are
`initial-code-shared`, `tp-shared`, `tp-flash-header-shared`, and
`diffdlm-shared`. A role explains review intent only. Changing it cannot select
runtime behavior, expand applicability, or add an unreferenced fact.

Each `sharedFactReferences` entry has the exact typed shape
`{ "factKind": ..., "factId": ... }`. The current closed fact kinds are
`region` and `metadata-definition`. A region reference selects one canonical
region fact. A metadata-definition reference selects one canonical logical
definition identity, not a structure instance or a copied field/offset table.
The normalizer resolves each reference to the same immutable canonical object.
A fact not referenced is not shared.
For `region`, every applicable map must bind the exact same immutable region
instance through one canonical region set; separately declared value-equal
regions are rejected. Metadata definitions follow the same reference-identity
rule. Profiles resolves the typed identifier once; the Domain relationship
constructor is the sole owner of cross-map coverage, uniqueness, and exact
reference-identity validation.

The old `initial-code-shared-family` and `tp-shared-family` discriminators,
`sharedRegionIds`, `metadataDefinitionIds`, and every other undeclared
relationship, applicability, or reference property are rejected by the strict
schemas. Unknown members, map ids, references, or reference kinds; wrong-kind,
missing, inapplicable, ambiguous, or unequal facts; conflicting relationship
scope; member-specific perfect-like aliases or capabilities; and partial
perfect-like map membership reject normalization.

Unlisted regions, metadata, capacity, topology, LDC, integrity, processor,
workflow, publication, support, evidence classification, and requested
identity remain outside a partial relationship. Composition operations,
processor stages, and product publication remain profile/policy authorities;
their convergence onto the same family-owned facts is a separate migration
gate and cannot be inferred from any relationship declaration.

A metadata definition that is globally canonical across its declared
consumers—`firmware-config-general-parameters` for all ICs, or DPCMI for every
route that explicitly declares it—is not a partial-family fact. Maps bind that
one definition through their own structure instances and locators. A
`shared-fact-relationship` lists such a definition only if the owner explicitly
establishes that the definition itself is restricted to that relationship;
ordinary global reuse must not appear in `sharedFactReferences`.

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

The TP Header successor keeps three closed metadata-structure shapes:

- legacy inline definitions omit both `structureKind` and `tpFlashHeader`;
- typed inline definitions declare `structureKind = "tp-flash-header"` and
  one complete `tpFlashHeader` payload; and
- exact `definitionReference` bindings omit all inline definition facts,
  including the discriminator and typed payload.

Unknown discriminators, a payload without its discriminator, a discriminator
without its payload, or any referenced/inline mixture reject schema
validation. The TP payload requires named structure-relative spans and one
semantic reference for every common physical field. Repeated fields use
explicit `{ index, fieldId }` members and explicit `icCount` applicability
rows; groups reference field or series ids. The payload contains no second
field geometry: offsets, widths, encodings, and byte order remain declared
exactly once in the common `fields` array. Duplicate ids, dangling references,
range containment, applicability membership, and complete field-semantic
coverage remain normalizer/Domain invariants after schema validation.
Current TP Flash Header providers declare `assertions: []`: the typed model
reads field values but imposes no value admission constraint. A CRC, address,
size, option, `same-code`, or `cascade-info` value therefore cannot reject the
Header structure merely because its stored value differs.

Address-valued fields also declare `storedAddress`, which describes the
integer encoded in the field rather than the field's own byte position.
`destination-address` currently requires an `absolute` basis in its named
value address space (for example `sram`). `tp-bin-start-address` requires
address space `tp-bin` with basis `tp-bin-offset`. Non-address roles must omit
`storedAddress`. This keeps a Header stored address distinct from TP BIN byte
position, final Flash image position, and a TPB placement delta; profiles may
reference the fact but do not gain relocation authority from it.

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
