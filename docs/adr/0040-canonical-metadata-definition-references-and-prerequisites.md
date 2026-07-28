# ADR 0040: Resolve canonical metadata definitions and artifact prerequisites by exact reference

- Status: Accepted
- Date: 2026-07-26
- Accepted: 2026-07-26 by the product, architecture, and firmware owner through
  the owner-approved `0.10.x` specification and GitHub issue #176
- Extended: 2026-07-28 by GitHub issue #186 for the typed TP Flash Header
  specialization and reference-only behavior-binding vocabulary
- Owners: Product owner + architecture owner + firmware owner
- Risk: R2 cross-layer architecture contract; each firmware locator binding
  remains R3
- Amends: ADR 0016 and ADR 0039

## Context

The `0.10.x` architecture requires each artifact or part to declare a metadata
structure once. Resolved inspection plans may retain references, applicability,
prerequisites, readiness, and per-run values, but they may not copy structure
lengths, fields, assertions, relations, ranges, or formatter definitions.

NT51950 and NT51951 expose the first topology-dependent prerequisite in this
model. Their DPCMI logical structure is unchanged, but the physical CMD1 Page 0
anchor needed to inspect the selected DP depends on `observed-ic-count` decoded
from TP FirmwareConfig. A user may select DP before TP, so absence of TP must
remain a typed pending dependency rather than becoming a guessed offset,
nullable value, or UI-specific IC rule.

This introduces a durable boundary across the family contract, Domain,
Profiles, Bootstrap, and Application. The ownership, trust, and failure
semantics therefore require an ADR in addition to schema and contract prose.

## Decision

### Canonical logical definition and located binding are separate

`FirmwareMetadataStructureDefinition` is the immutable,
locator-independent logical definition. It owns:

- length;
- field definitions;
- byte assertions; and
- typed relations; and
- an optional closed typed specialization whose spans, field semantics,
  explicit repeated series, groups, and stored-address meaning remain part of
  the same immutable definition.

The specialization is physical geometry and value meaning, not workflow
execution policy. A reference to a span, field, series, or group never grants
copy, relocation, processor, integrity, or write authority.

`FirmwareMetadataStructure` is one located binding instance. It owns:

- a binding `structureId`;
- an `artifactBindingId`;
- a locator; and
- a reference to the exact immutable logical definition.

A binding may declare its logical definition inline or use one
`definitionReference`, never both. Reusing a definition retains the same
immutable definition object; it does not clone its field table.

### Cross-family references are exact and allow-listed

A `definitionReference` identifies exactly:

```text
familyId
familyVersion
familyContentHash
logical structureId
```

All four values must match one owner-approved provider. A matching name,
version, or current byte shape alone does not grant trust. No runtime lookup
may infer a provider from an IC relationship, filename, PID, decoded version,
hash similarity, or golden observation.

### Artifact prerequisites are typed dependency edges

`metadata-field-selected` is the closed dependent-locator form. It references:

- one prerequisite structure selected by the same resolved map;
- one unsigned field in that structure;
- non-overlapping inclusive value branches;
- one addressed anchor per branch;
- one checked result offset; and
- one allowed result region.

The complete result range must fit the selected anchor, allowed region, map,
and immutable artifact. Unsupported values have no fallback. The resolved
dependency graph must be acyclic.

A missing prerequisite artifact produces typed pending state with exact
artifact, structure, and field identity. A present but invalid, ambiguous,
unreadable, range-invalid, or rejected prerequisite blocks its dependent.
Application projects `NotApplicable`, `PendingInput`, `Blocked`, or `Ready` and
a typed `LoadArtifactFirst` action containing artifact and slot identities.
Presentation and CLI format the operator text; Application does not own the
localized phrase `Load TP first`.

### Layer ownership and dependency direction

- **Contracts** serialize exact definition references and dependent locators.
  Contract prose, schema, compatibility rules, and tests change together.
- **Domain** owns immutable definitions, located bindings, decoding, checked
  ranges, prerequisite evaluation, cycle rejection, and resolution outcomes.
- **Profiles** normalizes trusted documents through a definition-resolver
  interface. It does not know Bootstrap providers and does not duplicate their
  firmware facts.
- **Bootstrap** owns the built-in exact-provider allow-list and composition
  wiring only. It cannot weaken Domain checks or become a second metadata
  catalog.
- **Application** owns reference-only inspection plans, typed readiness/action
  projection, immutable artifact identities, authoring revisions, and stale
  publication rejection.
- **Presentation and CLI** consume the same typed Application result and share
  localization policy. Neither decodes firmware or chooses an IC-specific
  physical offset.

Unknown providers, non-allow-listed providers, stale or mismatched identities,
inline/reference duplication, ambiguous bindings, dependency cycles, invalid
field types, unsupported values, arithmetic overflow, and escaped ranges all
fail closed.

### Metadata follows the three-form semantic ceiling

Metadata uses the repository-wide representation limit from ADR 0015:

1. the serialized Contracts DTO;
2. the one Domain-owned `FirmwareMetadataStructureDefinition` plus its
   canonical located binding; and
3. a reference-only resolved/inspection result with applicability, prerequisite
   state, and per-run decoded values.

Profiles normalization may use private ephemeral validation state but cannot
retain a second metadata definition. Application plans, Workbench adapters, and
Presentation/CLI projections may carry definition ids, readiness, decoded or
formatted values, and typed issues; they may not copy field tables, assertions,
locators, ranges, relations, or formatter rules. Repository callers migrate
directly, and accidental public implementation DTOs receive no indefinite
compatibility shim.

### #176 migration boundary

GitHub issue #176 applies this contract to NT51950 and NT51951 DPCMI inspection:

- both reuse the approved TP FirmwareConfig logical definition;
- both reuse the one logical DPCMI definition;
- NT51950 retains its evidenced count-1 and count-2-or-greater CMD1 Page 0
  anchors;
- NT51951 retains its independently evidenced invariant CMD1 Page 0 anchor;
  and
- their Initial Code, LDC, capacities, AB layouts, workflow operations,
  processor behavior, integrity authority, evidence, and publication facts are
  not shared by this decision.

This slice does not claim the following later work:

- named perfect-like/shared-part family convergence, owned by #177;
- authoring-session ownership, metadata cache lifetime, and downstream-only
  invalidation, owned by the session migration including #179; or
- remaining headless consumer migration and compatibility-facade reduction,
  owned by #194.

The existing Workbench facade may adapt binding identity to logical definition
identity during migration, but it does not become a second resolver or a new
consumer authority.

### #186 TP Flash Header extension boundary

GitHub issue #186 applies the same exact-reference ceiling to the NT51929 and
NT51932 Type-AB TP Flash Header:

- `firmware-family-v1.1-tp-header.schema.json` declares the closed
  `tp-flash-header` typed payload;
- each physical field is declared once, including all eight DLM CRC fields;
- an explicit IC Count table resolves each series member as Active, Unused, or
  Unknown without changing field existence or authority;
- Standard, TPA, and TPB structures retain distinct artifact instances while
  sharing the exact immutable provider definition;
- TPA and TPB input locators remain at the unshifted TP-BIN source coordinate;
  final B-bank placement is a separate composition concern; and
- `composition-profile-v2.11.schema.json` records reference-only target and
  purpose bindings. It does not lower operations or authorize writes.

#189 owns consumption of the relocation group by NT51919/29/32 AB lowering.
#194 owns the remaining headless runtime/report migration and parity-adapter
deletion. This extension therefore freezes their canonical seam without
duplicating those downstream execution owners.

## Alternatives

- Copy canonical fields into every consumer family: rejected because it creates
  multiple firmware-fact owners and allows silent drift.
- Reference a provider by structure name only: rejected because a changed
  provider could be accepted without explicit review.
- Put the NT51950/NT51951 branch in Application or a ViewModel: rejected because
  IC-specific firmware placement belongs to trusted profiles and Domain
  resolution.
- Let missing TP return a nullable or generic pending value: rejected because
  the prerequisite identity and operator action would be lost.
- Let TP metadata choose the IC family or workflow topology: rejected. It may
  select only the declared physical locator branch inside an already selected
  IC/map.

## Consequences

- One logical metadata definition can be instantiated at several evidenced
  artifact locations without duplicating its fields.
- Bundle/schema changes that affect exact references require coordinated
  version and content-hash updates.
- A consumer cannot load until every referenced provider is present and exactly
  trusted.
- Conservative re-inspection is correct for #176. Session caching remains a
  later optimization and must never retain full BIN payloads in authoring
  state.
- This decision changes inspection authority and readiness reporting only; it
  grants no write, CRC, Header, Header Copy, POSTBUILD, workflow execution,
  evidence, publication, or support authority.

## Verification and evidence

- Contract tests reject inline facts beside a reference and every
  family/version/hash/structure mismatch.
- Identity tests prove consumers retain the same immutable logical definition
  object.
- Domain tests reject invalid branch intervals, missing or invalid
  prerequisites, wrong address spaces, escaped/overflowing ranges, unsupported
  values, and direct/transitive cycles.
- NT51950/NT51951 tests cover missing TP, supported and unsupported IC counts,
  Standard Merge and DP Replace artifact bindings, malformed TP, and stale
  token/revision/artifact identities.
- Existing composition operations, expected golden bytes, processor mutation
  authority, and output naming remain unchanged.
- Firmware-owner review must approve the NT51950/NT51951 TP FirmwareConfig
  evidence and physical CMD1 Page 0 anchors before merge. Synthetic fixtures do
  not replace that R3 gate.
