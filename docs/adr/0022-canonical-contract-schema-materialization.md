# ADR 0022: Canonical Contract Schema Materialization

- Status: Accepted
- Date: 2026-07-15
- Owners: Architecture owner
- Amends: 0.9.2 Profile Bundle Consolidation Plan

## Context

The 0.9.2 materializer replaced per-bundle schema copies with nine
content-addressed copies under `profiles/schema-source/sha256`. Every one was
either byte-identical to a reviewable schema already under `docs/contracts` or
unreferenced by any bundle. The intermediate inventory removed per-bundle
duplication but left 9,625 nonblank JSON lines with a second source owner.

Runtime bundles still require local schema entries whose bytes match their
manifest. The closed-root loader must not resolve schemas from repository
contracts, siblings, a network, or mutable process-global state.

## Decision

`docs/contracts` is the only checked-in source owner for contract schemas.
Bootstrap's explicit built-in bundle allowlist selects one canonical profile
schema filename per bundle and copies it, plus the canonical firmware-family
schema, into that bundle's closed materialized root. It does not copy an
author-supplied bundle schema directory.

The bundle manifest remains the byte authority: every materialized schema is
hashed before parsing and must match its manifest entry. Test-only source-bundle
materializers locate a canonical contract schema by that declared SHA-256,
require exactly one match, and then use the same loader. A filename, schema id,
or version never substitutes for the content hash.

The obsolete `profiles/schema-source` inventory is removed. Historical
canonical schemas remain under `docs/contracts` when reviewed bundles or
evidence need their bytes; an unreferenced intermediate copy has no runtime or
historical authority merely because its directory name is a hash.

## Consequences

- Checked-in schema bytes have one owner while every deployed bundle remains
  self-contained and manifest-pinned.
- Runtime loader, schema validation, bundle trust anchors, profile facts,
  compilation fingerprints, promotion stages, and golden outputs do not
  change.
- Adding an IC selects an existing reviewed canonical schema. Changing schema
  bytes still requires a new reviewed contract and matching bundle manifest;
  the materializer never infers or rewrites a schema.
- UI, CLI, and candidate intake cannot select arbitrary host schema paths.

## Verification

- Architecture tests reject a restored `profiles/schema-source` inventory and
  lock the explicit materialization filenames.
- Bootstrap tests reconstruct source candidates by manifest hash and compare
  materialized/deployed schema bytes.
- `python scripts/verify.py --all` remains the final handoff gate.
