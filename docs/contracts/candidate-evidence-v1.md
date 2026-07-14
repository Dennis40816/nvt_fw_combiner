# Candidate Evidence Contract 1.0

The executable schema is
[`candidate-evidence-v1.schema.json`](candidate-evidence-v1.schema.json). It
is the active candidate-only, offline contract introduced by proposed ADR
0019. It supersedes `ic-reference-intake-request-v1` and
`firmware-evidence-manifest-v1`; those formats remain only as historical,
non-promotable evidence records and are not accepted by the C# candidate CLI.
It never admits runtime execution.

## Documents and authority

One local-only Draft 2020-12 schema validates four closed document shapes:

- `intake-request`: owner input with normalized source paths beneath the
  caller-selected local source root;
- `candidate-source-bundle`: path-free request projection, exact request
  snapshot hash, owner identity, source-artifact facts, missing-evidence state,
  and materializer provenance;
- `candidate-root-manifest`: the complete allowlist for the materialized
  candidate root; and
- `candidate-validation-report`: a sidecar attestation for a candidate root.

The three generated documents require `runtimeAuthority = "none"`. The intake
request has no runtime-authority field because it is an owner input rather than
a generated candidate document. Facts may record processor, integrity, map, or
range evidence, but they cannot declare an executable binding, invocation,
registration, mutation authority, or composition plan.

`missingEvidenceDisposition` is preserved across the request, source bundle,
and report. `listed` requires one or more declared gaps; `owner-declared-none`
requires an empty list. The latter is an owner assertion of no known gaps, not
an inference, approval, promotion, or support claim.

## Hash and publication model

The source bundle records `requestContentHash` as SHA-256 over the exact strict
UTF-8 request snapshot, without a byte-order mark, using
`sha256-raw-utf8-v1`. It preserves the owner, request identity/version/time,
facts, and missing-evidence state so the root remains bound to one exact owner
request.

`candidate-root-manifest.contentHash` uses
`sha256-rfc8785-candidate-entry-array-v1`: SHA-256 over an RFC 8785 UTF-8
canonical JSON array of every entry object containing exactly `contentHash`,
`entryId`, `kind`, `path`, and `sizeBytes`. Entries sort by ordinal
`entryId`, `kind`, `path`, `contentHash`, then numeric `sizeBytes`. The root
manifest itself is excluded from this array, preventing self-reference.

The required known-answer vector is the hash
`37c0cad56b7fcf7cd033fac12eb2decd9e5a350bb1095988e998ecb0cd1167ef`
for the two unsorted entries `candidate-schema` / `schema` /
`schemas/candidate-evidence-v1.schema.json` / `a` repeated 64 times / `1`, and
`owner-record` / `artifact` / `artifacts/owner-record.txt` / `b` repeated 64
times / `2`. The canonical sorted JSON projection is locked by the
Infrastructure test suite.

The validation report is deliberately outside the closed root. It is a sidecar
in the same host-owned parent staging directory and binds both the root entry
array hash (`rootContentHash`) and the raw root-manifest SHA-256
(`rootManifestSha256`). This prevents the report/manifest hash cycle. Only a
report with `validationOutcome = "passed"` may publish the root and sidecar
together through the no-replace publication operation.

`sourceBundleEntryId` and `contractSchemaEntryId` must each resolve to one
manifest entry during semantic validation. Entry kinds are structurally tied to
`schemas/`, `source/`, `artifacts/`, and `evidence/` paths. Report files are
not root entries. Artifact paths preserve the declared original logical filename,
including safe uppercase letters and spaces; path separators, drive syntax, and
current or parent path segments remain forbidden.

## Limits and privacy

The schema bounds identifiers to 96 characters; source/root paths to 240;
logical names to 255; text to 4 KiB; source artifact and entry bytes to 16 MiB;
candidate capacity to 16 MiB; source artifacts to 128; root entries to 512;
and facts to 512. The C# materializer additionally enforces these immutable
aggregate limits before copying: request/source/root/report JSON each 256 KiB,
JSON depth 64, aggregate source bytes 64 MiB, and candidate-root directories
64. Checked arithmetic is required before allocation or copying.

Generated documents reject backslash, drive-qualified, UNC, and POSIX-absolute
syntax in copied logical names, citations, fact text, gap text, and validation
summaries. The adapter separately rejects Windows alias ambiguity, reparse
points, non-regular files, network roots, case collisions, source/output
containment, and destination overwrite. Local source paths are retained only
in the intake request and never appear in the source bundle, root, or report.

Use the C# command to materialize a candidate evidence set:

```text
nvt_fw_combiner candidate-intake stage --request <request.json> --source-root <owner-drop-folder> --output-dir <new-absent-path>
```

The command validates this schema through the pinned local `Json.Schema`
authority, validates cross-document identifiers/hashes and the complete
inventory, then uses shared closed-root Infrastructure primitives. It does not
interpret BIN bytes, execute workbook code, resolve a map, compile a profile,
register an IC, invoke a processor, or call `ProfileBundleLoader`.
