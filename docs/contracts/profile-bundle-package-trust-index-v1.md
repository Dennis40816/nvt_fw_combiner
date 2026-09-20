# Profile Bundle Package Trust Index v1

Status: normative `0.10.x` runtime and packaging contract.

Normative schema:
[`profile-bundle-package-trust-index-v1.schema.json`](profile-bundle-package-trust-index-v1.schema.json).
The checked-in built-in instance is
[`../../profiles/built-in/package-trust-index.json`](../../profiles/built-in/package-trust-index.json).

## Purpose and authority

The package trust index is the sole admission list for built-in V2 profile
bundle roots, build materialization, and runtime workflow registrations. Each
bundle entry pins one directory, bundle schema/version, exact SHA-256 content
hash, schema materialization inputs, optional canonical-family projection, and
zero or more closed-vocabulary runtime registrations. Optional
`metadataProviderFamilies` bind an exact family id/version to the one bundle
allowed to resolve that family's canonical metadata references; the reference's
family content hash and structure id remain independently exact at runtime.

The same provider admission supplies explicit `fullImageMetadataViews` from
that exact family's normalized catalog in the owning trusted bundle. The
manifest pins its family bytes. A missing or mismatched declared family
identity rejects the candidate; an existing exact family without views supplies
no full-image metadata authority. Consumers do not scan projected family
copies or substitute runtime profile metadata. This authority adds no runtime
registration or family-disclosure permission. A present view with an empty
binding collection remains an explicit empty declaration.

Optional `familyDisclosureFamilies` independently opt an exact family id/version
into canonical family disclosure. Both fields reuse the same closed pair shape
and each requires global uniqueness within its own authority. A family may hold
both authorities. Neither implies the other or any runtime registration.
Disclosure resolves only within its owning trusted bundle's normalized catalog;
the trusted manifest closes the family content hash. Missing or ambiguous exact
identity rejects the candidate. No cross-bundle fallback is permitted.

Disclosure merges these existing typed relationships with compiled runtime
relationships. Duplicate exact family/version/hash/relationship facts may coalesce;
conflicting facts or overlapping contradictory Perfect sources reject publication
and retain the prior catalog. The Standard registry continues to define the IC
universe, and runtime eligibility and profile inventory do not change.

The index does not promote support, waive profile blockers, supply golden
evidence, or grant external processor authority. The selected bundle manifest,
profile/family contracts, capability publication policy, and evidence gates
remain independently binding. Candidate data cannot self-promote merely by
being shipped or listed.

## Runtime registration

Every registration contains an exact `workflowId`, `icId`, `profileId`, and
`profileVersion`. `general-merge` additionally requires `familyId`.
`ctrlram-replace` additionally requires the reviewed postbuild processor and
closed branch token. A CtrlRAM registration whose same-IC Standard profile
declares report-classification metadata must additionally declare the exact
`reportMetadataMapId`; a CtrlRAM registration whose Standard profile declares
no report classification must omit it. This is a cross-workflow counterpart
reference, not a second map definition: the referenced map, metadata
structure, report purpose, capacity, and selection rules remain owned by the
registered Standard profile/family and the canonical profile compiler.
Standard Merge, AB Merge or DP Replace registrations whose profile
declares a selection group additionally declare the reviewed
`mapVariantSetId`; runtime projection rejects a missing or extraneous binding.
Fields that do not belong to the selected workflow are forbidden.

The admitted workflow vocabulary is fixed by the schema. Existing-vocabulary
IC onboarding changes the manifest-pinned bundle plus this index; the
Application capability catalog, UI, CLI, capability inventory, and support
projections only consume typed registrations and must not add IC-specific route
tables. Bootstrap registers the loader/compiler ports but owns no registry or
projection. A new
workflow vocabulary or compiler semantic is a separate contract and
architecture change.

Registration keys are globally unique by workflow, IC, map-variant set, processor,
and branch. Multiple format profiles for one IC require distinct explicit
map-variant sets; IC-only lookup is not sufficient to choose a format.
Runtime loaders validate the index before projecting any route. CtrlRAM
admission resolves the exact same-IC Standard registration, requires the
presence or absence rule above, compiles the named map through the existing
profile compiler, and retains only entries whose declared purpose is report
classification. Unknown, cross-IC, same-capacity substitute, missing, and
extraneous counterparts fail the complete candidate before any route is
published. Runtime then independently validates each deployed bundle version
and content hash. Unknown, duplicate, missing, or mismatched entries fail
closed.

## Materialization and release

`materialization` declares the canonical composition/family schema files used
to make each deployed bundle self-contained. A `canonicalFirmwareFamily`
projection is allowed only as one explicit source/destination pair; both paths
are bounded relative JSON paths and the destination is confined to the target
bundle's `families/` root.

Build and release packaging consume the same checked-in index. The release
contains the exact `profiles/built-in/package-trust-index.json` bytes and only
the listed bundle directories, manifest-pinned bundle entries, and separately
allowlisted runtime catalogs. A published index that differs from the reviewed
source is rejected before packaging.

Before copying materialized bytes, the build validates each source bundle's
manifest identity, canonical entry-array hash, and every actual entry source
(including contract schemas and canonical-family projections) against the
trust-index and manifest hashes. Standalone release smoke pins the exact
reviewed trust-index file SHA-256, so updating a package-local release manifest
cannot self-authorize index or route drift.

The build parses the trust index from one pre-allocation-bounded immutable
snapshot. Its manifest validator is bound to the exact reviewed
`profile-bundle-v1.schema.json` SHA-256 and may be stricter, but cannot admit a
manifest path, identifier, version, or schema identity rejected by that schema.

The build materializer's bounded validator is pinned to the exact SHA-256 of
this normative schema. Every scalar is type-checked before value validation;
wrong JSON types fail closed rather than being coerced. A schema-byte change
therefore requires an explicit validator and digest review before any bundle is
materialized.

## Closed execution boundary

The schema has `additionalProperties: false` at every authority-bearing level.
Scripts, plugins, dynamic assemblies, executable paths, watch paths, mutable UI
state, environment overrides, network locations, and hot reload are outside
this contract. The index is immutable package data loaded from the deployed
application root; it is not a discovery or extension mechanism.
The runtime reads it through a pre-allocation byte bound. An explicit
capability-catalog publication reload does not re-read or hot-swap the package
trust index or bundle bytes; observing a new package requires a new process.

`trustIndexVersion` changes whenever admitted bundle materialization, family
authority bindings, or runtime registrations change. Schema `1.1` adds only the closed CtrlRAM
`reportMetadataMapId` counterpart described above; it does not move map or
metadata semantics into the index. Schema-compatible data changes keep the
current `schemaVersion`; vocabulary or semantic changes require a reviewed
schema revision and the normal R2/R3 gates.

Schema `1.2` additionally admits `mapVariantSetId` for AB Merge selection
groups. The map-set is partitioned by the existing canonical IC-count axis;
each dynamic route binds only its matching map subset. Normal and Dummy are
input selections within that route, not separate maps or support promotions.
Schema `1.3` additionally admits optional `memoryLayoutContextMapId` only for
CtrlRAM. It names one exact map materialized by the existing same-IC Standard
registration, independently of Report metadata. Unknown, cross-IC, unmaterialized
or different-address-space counterparts fail admission; when both counterpart
fields are present they must name the same map. Neither capacity nor input
content selects this map. Its IC, profile/version, bundle hash, map and address
space are retained as immutable Application context and fingerprint bindings.
Omitting the display field preserves the existing report-map overview fallback;
it never changes the mandatory Report presence/absence rules above. The display
context cannot authorize operations, support, Report classification or writes.
Schema `1.4` additionally admits optional `familyDisclosureFamilies` as the
explicit source authority described above. It adds no firmware relationships,
execution support, or runtime registrations. The current built-in candidate uses
schema `1.4`; Unit58 external owner attestation remains required before integration.
