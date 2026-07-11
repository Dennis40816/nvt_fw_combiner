# Firmware Family Contract 1.0

The executable schema is [`firmware-family-v1.schema.json`](firmware-family-v1.schema.json).
It is the canonical source for physical firmware facts shared by Normal, AB, Merge, Replace,
General, saved-rule, and future Register workflows.

## Ownership

A family document owns only facts that can be resolved without choosing a workflow:

- IC members and evidence-backed technical capabilities;
- physical address spaces, exact map capacity, region hierarchy, half-open ranges, ownership, and
  kind;
- non-relaxable write constraints and alignment;
- metadata structures, fields, and bounded locators;
- map applicability by member, mode, topology requirement, capacity, Common FW category, and
  decoded metadata predicates; and
- explicit, fact-scoped aliases.

A family document never grants execution support, declares operation order, invokes a processor,
or exposes a UI action. Newly inventoried regions use `writeConstraint = "forbidden"` until owner
evidence approves a narrower constraint. A composition profile may further restrict a map but may
not relax it.

## Resolution

`TopologyRequirement` is static applicability. Runtime `TopologySelection` and other named
`ResolutionInputs` are Domain values and are not persisted as mutable family policy. Resolution
must produce exactly one map or return pending/rejected; it never chooses the first matching map.

Every absolute or search range names its address space. Marker-relative locators require a bounded
search range, exact marker bytes, checked signed result offset, at least one structure assertion,
and an allowed result region. `unique` requires exactly one match. `terminal-match` requires an
exact evidenced match count and explicitly selects the lowest- or highest-address match. Zero
matches, the wrong count, an out-of-range result, or a failed assertion rejects resolution.

Every metadata structure declares one stable `artifactBindingId`; it matches the `artifactId` in
runtime map-resolution inputs and is never a path or filename. Structure ids are ordinally unique
across a family, and field ids are ordinally unique within a structure. Every metadata predicate
declares `metadataStructureId`, so predicate resolution uses the exact
`(artifactBindingId, metadataStructureId, fieldId)` source rather than a global field-name lookup.
A map predicate may reference only a structure selected through that map's metadata sets.

Each image-map shape has one exact `capacityBytes` and declares
`coveragePolicy = "complete-with-explicit-gaps"`. Semantic validation proves that referenced region
sets use the selected address space, stay in bounds, preserve proper parent containment and sibling
overlap rules, and partition the root capacity. Every region that has children is likewise
partitioned by its direct children. Every otherwise unclassified interval is represented by a
`reserved` or `unmapped` region.
`customer-information` and `ctrlram` are physical classifications, not workflow permissions.

## Aliases

An alias copies exactly one `region-set`, `metadata-set`, or `capability` fact under explicit
applicability and evidence. Alias applicability includes the same mode, topology, capacity, Common
FW category, and metadata predicates used by maps. It does not alias a whole IC, map, processor,
capacity, promotion state, or workflow. Alias cycles, unresolved ids, conflicting aliases, and
applicability wider than either the source fact or target map are semantic validation errors.

Technical capabilities such as `ab-code` or pending `register-replace` are evidence facts only.
Only a promoted `composition-profile-v2` can make a workflow executable.
