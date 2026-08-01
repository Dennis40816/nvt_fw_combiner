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

The index does not promote support, waive profile blockers, supply golden
evidence, or grant external processor authority. The selected bundle manifest,
profile/family contracts, capability publication policy, and evidence gates
remain independently binding. Candidate data cannot self-promote merely by
being shipped or listed.

## Runtime registration

Every registration contains an exact `workflowId`, `icId`, `profileId`, and
`profileVersion`. `general-merge` additionally requires `familyId`.
`ctrlram-replace` additionally requires the reviewed postbuild processor and
closed branch token. Standard Merge or DP Replace registrations whose profile
declares a selection group additionally declare the reviewed
`mapVariantSetId`; runtime projection rejects a missing or extraneous binding.
Fields that do not belong to the selected workflow are forbidden.

The admitted workflow vocabulary is fixed by the schema. Existing-vocabulary
IC onboarding changes the manifest-pinned bundle plus this index; Bootstrap
registries, CLI, Workbench, capability inventory, and support projections only
project typed registrations and must not add IC-specific route tables. A new
workflow vocabulary or compiler semantic is a separate contract and
architecture change.

Registration keys are globally unique by workflow, IC, processor, and branch.
Runtime loaders validate the index before projecting any route and then
independently validate each deployed bundle version and content hash. Unknown,
duplicate, missing, or mismatched entries fail closed.

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

`trustIndexVersion` changes whenever admitted bundle materialization or runtime
registrations change. Schema-compatible data changes keep `schemaVersion`
`1.0`; vocabulary or semantic changes require a reviewed schema revision and
the normal R2/R3 gates.
