# ADR 0019: Candidate Evidence Intake Trust Boundary

- Status: Proposed
- Date: 2026-07-14
- Owners: Product owner + architecture owner + contract owner
- Amends: ADR 0015 and the 0.9.2 profile-bundle consolidation plan

## Context

0.9.4 needs a repeatable way to receive future IC evidence without making an
unreviewed folder, workbook, BIN, or filename convention a firmware-rule
source. SPEC 5.3 requires a complete declared intake manifest and permits only
a candidate bundle, a materialization/validation report, and a missing-evidence
list as output. The 0.9.2 consolidation plan additionally requires one
materializer and one closed-root validation boundary.

The first candidate implementation correctly kept candidates out of runtime
registration and required artifact hashes, but it did not yet produce a closed
candidate root or use the repository's pinned schema authority. Its retired
legacy command also recursively scanned and classified arbitrary files. That
scanner was removed in commit `66fb62e0`; the remaining trust-boundary work
needs an explicit contract decision before it changes schemas or CLI behavior.

## Decision Drivers

- A candidate must never gain profile, map, support, or runtime authority.
- Every input artifact and every candidate-root output must be declared and
  hash-verifiable offline.
- Candidate intake must reuse the existing closed-root primitives without
  presenting a non-trusted candidate as a `profile-bundle-v1` runtime bundle.
- JSON Schema must have one pinned, offline validation authority.
- Local filesystem authority, bounded resource use, and no-overwrite output
  publication must be explicit and testable on supported platforms.

## Considered Options

1. Keep the Python manifest validator and add more checks around it.
2. Encode candidate output as a trusted `profile-bundle-v1` and invoke the
   production profile loader.
3. Create a versioned candidate-evidence contract, validate and materialize it
   through shared closed-root infrastructure, and keep it structurally incapable
   of runtime admission.

Option 1 retains two schema authorities and a separate filesystem policy.
Option 2 falsely represents incomplete evidence as a trusted runtime bundle.
Option 3 is selected.

## Decision

### Candidate contract and authority

`ic-reference-intake-request-v1` and `firmware-evidence-manifest-v1` remain
frozen candidate-only contracts. A breaking correction, including local-only
schema references, explicit missing-evidence declarations, or an
`intakeProvenance` status restriction, must use a new versioned contract rather
than change a published `1.0` schema in place.

The successor request contract must declare:

- every source artifact, its normalized logical path, size, SHA-256, and source
  kind;
- the requested candidate scope and explicitly cited facts;
- a `missingEvidence` list, including an explicit owner assertion when it is
  empty; and
- bounded counts and byte limits that are validated before artifact staging.

The list is a declaration, not inferred completeness. An empty list means only
that the owner declared no known missing evidence; it never promotes a
candidate or authorizes a runtime profile.

One C# candidate-intake use case owns Draft 2020-12 validation through the
repository's pinned `Json.Schema` dependency. The replacement schemas must use
only local fragment references. Semantic validation is limited to rules JSON
Schema cannot represent, such as cross-document IDs, content hashes, filesystem
identity, and output inventory. `candidate-intake stage` is the only candidate
CLI; the prior Python command, validator, and materializer are retired and must
not retain a second schema authority.

### Candidate source bundle and closed root

The intake use case produces a versioned `candidate-evidence-bundle` source
declaration and materializes a caller-selected, initially absent candidate root.
The root contains a complete candidate manifest, local immutable schema
snapshots, the request-derived source bundle, declared artifact snapshots, and
the generated checklist. The candidate manifest is the complete allowlist for
that root and hashes every listed file. The validation report is a sidecar under
the same host-owned parent staging directory, not a root entry: it records both
the root entry-array hash and the raw root-manifest hash without creating a
report/manifest hash cycle. The root and a passed sidecar report publish through
one no-replace parent operation.

Candidate roots do not use `profile-bundle-v1`, do not receive a release trust
anchor, and are never passed to `ProfileBundleLoader`. Instead, Infrastructure
extracts the existing closed-inventory, bounded snapshot, local-schema, and
hash-verification primitives into one shared closed-content-root boundary. The
production profile loader continues to be the sole profile loader and uses that
same boundary. Candidate-specific validation may prove only candidate structure
and evidence declarations; it cannot normalize a family, resolve a map, compile
a profile, or construct `CompiledComposition`.

The report records exact request-snapshot hash, root entry-array hash, raw
root-manifest hash, validated entry count, ordered declared missing evidence,
every validation result, and candidate-only status. It does not expose local
source paths or `sourceRef` values. A failed report remains in private staging
diagnostics and cannot publish a candidate root.

The materializer never serializes a host-clock value. Its provenance timestamp
is the request's declared `requestedAtUtc`, so identical declared source
snapshots produce byte-identical roots and reports.

### Filesystem and resource policy

The C# intake adapter accepts only local request, source, and output roots. It
rejects UNC/network roots, reparse points, non-regular files, case-colliding
logical paths, path segments with Windows alias ambiguity, source/output
containment, existing destinations, and all undeclared files.

It opens each declared source through no-follow semantics where the platform
supports them, validates the opened handle's identity and containment, copies
from that one handle while enforcing named request, per-artifact, aggregate,
entry-count, directory-count, JSON-byte, and JSON-depth limits, then verifies
the copied hash. It publishes the fully verified temporary root with an
OS-backed no-replace operation; a destination-creation race fails without
modifying a caller-owned source or an existing destination.

Exact numeric limits are named contract constants with tests; they are not
firmware facts and do not alter composition behavior. The contract publishes a
16 MiB per-artifact/entry/capacity limit, 64 MiB aggregate source limit,
256 KiB per JSON document, JSON depth 64, 128 source artifacts, 512 root
entries, and 64 root directories.

## Consequences

### Positive

- Future IC evidence has a deterministic, manifest-first input and a closed,
  inspectable candidate output.
- The production and candidate paths share one filesystem snapshot/inventory
  implementation while retaining separate trust and runtime admission states.
- Candidate output is useful for human review without becoming a hidden
  profile-import or support-promotion mechanism.

### Negative / Trade-offs

- The candidate CLI is a breaking replacement for the Python script and needs
  a documented migration command.
- The first C# implementation adds typed contracts and focused infrastructure
  code before it removes the Python candidate implementation.
- Closed-root and atomic-directory behavior require Windows and Unix-specific
  integration evidence.

### Risks and Mitigations

- Candidate mistaken for runtime bundle -> distinct manifest identity, no trust
  anchor, no `ProfileBundleLoader` path, and architecture tests.
- Schema drift -> versioned schemas and one pinned C# validator.
- Local filesystem race or path alias -> opened-handle verification, local-root
  policy, canonical collision checks, and no-replace publication tests.
- Resource exhaustion -> explicit immutable limits checked before allocation or
  copy.

## Compatibility and Migration

1. Accept this ADR and publish the successor request, evidence, and candidate
   bundle contracts without changing existing `1.0` schemas.
2. Extract the generic closed-content-root primitives from the existing profile
   bundle implementation with parity tests; do not add another profile loader.
3. Implement the C# manifest-only candidate use case and its thin CLI handler.
4. Materialize and validate one synthetic candidate root, then retire the
   Python candidate implementation and its v1 CLI documentation.
5. Retain existing v1 candidate outputs only as non-promotable evidence. A
   future runtime profile still requires a separate reviewed V2 bundle,
   explicit registration, parity, and applicable firmware-owner gates.

## Verification

- Offline validation of every request/output document against the exact local
  schemas, plus semantic cross-reference and status/provenance tests.
- Closed-root tests for extra, missing, changed, case-colliding, reparse-point,
  oversized, and bounded-count files.
- Windows and Unix tests for network-root rejection, opened-handle replacement,
  no-follow behavior, and no-replace publication races where supported.
- Known-answer canonical root-entry hash vectors, deterministic identical-request
  root/report order tests, and explicit report-outside-root cycle tests.
- Architecture tests proving candidate intake cannot add allowlist entries,
  runtime registration, profile promotion, a second profile loader, or another
  composition executor.
- `python scripts/verify.py --all`, Polytail, and an independent R2
  architecture/contract review before the candidate interface is merged.
