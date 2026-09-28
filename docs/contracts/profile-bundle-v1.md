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

Every listed entry is hashed before parsing. The full directory JSON loader rejects:

- duplicate JSON keys, ids, or paths;
- unknown or missing properties and noncanonical ids or paths;
- unlisted files, missing entries, orphaned schemas/evidence, and case-colliding paths;
- absolute paths, `..`, path escapes, reparse points, or mutable executable paths; and
- content, schema, or bundle hash mismatch within the closed snapshot or against its supplied
  verified anchor.

Directory-source runtime content is below one loader-selected immutable bundle root. Profile documents cannot
contain host paths, commands, scripts, or arbitrary processor parameters.

Each listed schema declares exactly the Draft 2020-12 `$schema` URI and the `$id` named by its
manifest `schemaId`. Schema and content validation run only over immutable bundle snapshots with
format assertions enabled. `$ref`, `$dynamicRef`, and `$recursiveRef` must be local fragment
references; nested `$id` and `$schema` declarations are rejected. A profile bundle therefore cannot
discover schemas from the network, another bundle, or mutable process-global state.

The loader validates the manifest against its compiled immutable bootstrap schema before it reads
manifest-declared entries. The caller supplies the expected `contentHash` and
`trustAnchorBindingId` from release/install authority; a bundle cannot select or replace either
value. After closed inventory capture and entry validation, the loader rereads the manifest and
rejects a changed raw manifest hash.

The release/install adapter verifies its release manifest and package signature before it constructs
the anchor supplied to this loader. Those authority-verification mechanisms are not replaceable by
the bundle's self-declared fields.

The compiled bootstrap schema is not an entry in the bundle it validates. Every schema that is
listed by a bundle must instead be referenced by at least one non-schema entry; unused schemas are
rejected as orphans.

## Build-bound built-in admission

[ADR 0077](../adr/0077-prebuilt-profile-catalog.md) permits one bounded exception
for built-in bundles listed by the package trust index. The build runs the full
directory loader and DTO compatibility checks, then carries the exact manifests
and family/profile documents in `profiles/built-in/prebuilt-profile-catalog.pack`.
Schemas, evidence and saved rules are not carried. Reviewed JSON remains source
authority; this is a derived delivery representation, not a semantic model or a
new trust anchor.

Runtime acceptance binds the exact parsed trust-index bytes and the complete raw
manifest set to two identities compiled into the admission assembly. It verifies
the closed bundle/document sets, all range hashes and limits, manifest schema,
canonical entry-array hash, anchor, version and schema references before issuing
an immutable accepted-catalog token. Missing, duplicate or invalid compiled
identity fields reject the pack. A repaired body checksum cannot grant trust.

Only `ProfileBundleLoader` constructs trusted bundles. Its token-bound memory
source omits directory inventory, disk manifest rereading and entry-schema
meta-validation/evaluation: the immutable bytes and build identity preserve those
build verdicts. It retains the admitted full manifest, strict document parsing,
DTO compatibility, lazy normalization, shared metadata resolution and compilation.
No general schema-skip switch exists. Aggregate bytes including absent schema
payloads are checked by generation; runtime enforces carried-entry and manifest
bounds and the full manifest entry count.

The registry selects once, synchronously at the first bundle load. Accepted
packs ignore later or pre-existing damage to bundle JSON/schema copies and retain
the build's manifest identity, including when an edited `bundleId` would have
been admitted by the JSON path. A missing/rejected pack uses full unchanged JSON
admission and emits one `system.catalog.prebuilt-unused` Warning; admissible
manifest edits remain admissible on that path. A valid different index uses JSON;
a missing/invalid index fails closed without recovery from the pack. The complete
disposition matrix is in ADR 0077 decision 5.

Cancellation, retry and reload cannot replace the selected source or mix sources.
After acceptance, strict/DTO/normalization failures remain bundle failures with
existing messages, never per-bundle JSON fallback. This exception does not apply
to external bundles, user data or Saved Rule admission. R3 integration approval
follows current [governance](../adr/0080-governance-reset.md); local test evidence
does not replace independent review or the owner's required role approval.

## Packaging boundary

Repository schemas remain reviewable source contracts under `docs/contracts`. Packaging may copy
the approved schemas into the bundle's `schemas/` directory, then emits a new bundle entry list and
release/install trust binding. Editing a workbook or loose JSON file never changes production
policy until the reviewed bundle and its external trust anchor are replaced together.
