# Profile Bundle Contract 1.0

The executable schema is [`profile-bundle-v1.schema.json`](profile-bundle-v1.schema.json).
A bundle is the closed production allowlist for schemas, firmware families, composition profiles,
evidence manifests, and saved rules.

## Hash and trust model

`contentHash` is SHA-256 over an RFC 8785 JSON Canonicalization Scheme (JCS) UTF-8 encoding with no
BOM. The encoded value is an array of entry objects containing exactly `entryId`, `kind`, `path`,
`schemaId`, and `contentHash`, sorted with ordinal string comparison by those fields in that order.
The bundle manifest itself is excluded so the hash is not recursive. The loader receives the
expected bundle hash from the release/install authority identified by `trustAnchorBindingId`; a
bundle cannot trust its own declared hash.

Every listed entry is hashed before parsing. The production loader rejects:

- duplicate JSON keys, ids, or paths;
- unknown or missing properties and noncanonical ids or paths;
- unlisted files, missing entries, orphaned schemas/evidence, and case-colliding paths;
- absolute paths, `..`, path escapes, reparse points, or mutable executable paths; and
- content, schema, bundle, release-manifest, or package-signature hash mismatch.

All runtime content is below one loader-selected immutable bundle root. Profile documents cannot
contain host paths, commands, scripts, or arbitrary processor parameters.

Each listed schema declares exactly the Draft 2020-12 `$schema` URI and the `$id` named by its
manifest `schemaId`. Schema and content validation run only over immutable bundle snapshots with
format assertions enabled. `$ref`, `$dynamicRef`, and `$recursiveRef` must be local fragment
references; nested `$id` and `$schema` declarations are rejected. A profile bundle therefore cannot
discover schemas from the network, another bundle, or mutable process-global state.

## Packaging boundary

Repository schemas remain reviewable source contracts under `docs/contracts`. Packaging may copy
the approved schemas into the bundle's `schemas/` directory, then emits a new bundle entry list and
release/install trust binding. Editing a workbook or loose JSON file never changes production
policy until the reviewed bundle and its external trust anchor are replaced together.
