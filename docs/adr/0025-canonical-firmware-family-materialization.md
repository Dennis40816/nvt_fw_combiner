# ADR 0025: Canonical Firmware-Family Materialization

- Status: Accepted for candidate implementation; architecture and R3 review required before integration
- Date: 2026-07-18
- Owners: Architecture owner + firmware owner
- Extends: ADR 0022

## Context

ADR 0022 made reviewed contract schemas single-owner source files while keeping
every deployed profile bundle self-contained and manifest-pinned. Firmware-family
documents are profile data, not schemas, so reusing one reviewed family snapshot
needs a separate decision and explicit evidence boundary.

The NT51930 logical-output General Merge candidate must bind the exact canonical
family bytes already owned by `nt51930-standard-merge`. Checking the same 104-line
JSON document into both bundles creates a second source owner and allows the two
copies to drift even though both manifests claim the same family hash.

## Decision

Bootstrap's existing `MaterializeBuiltInProfileBundles` target accepts an explicit
canonical firmware-family source and bundle-local destination on a declared
`BuiltInProfileBundle`. The first and only admitted mapping is:

```text
profiles/built-in/nt51930-standard-merge/families/nt51930.json
  -> materialized nt51930-general-merge-logical-candidate/families/nt51930.json
```

Both metadata values are required together. The source must resolve beneath the
checked-in built-in profile root, must exist, and the destination must resolve
beneath the selected materialized bundle's `families` directory. A checked-in
file at the destination is a collision and fails the build. No path is inferred
from an IC id, family id, filename, sibling directory, or runtime state.

The candidate manifest remains the byte authority. Its destination path, entry
SHA-256, bundle content hash, trust anchor, profile family hash, promotion stage,
and blockers do not change. Build and publish copy the materialized closed-root
inventory through the existing content items. The runtime loader continues to
open only that self-contained deployed bundle and independently validates every
manifest entry and the pinned bundle hash; it never reads a repository sibling.

## Alternatives Rejected

- Keep two checked-in family copies: rejected because identical facts would have
  two source owners and an avoidable drift surface.
- Teach `ProfileBundleLoader` to resolve a sibling family: rejected because it
  would break closed-root deployment and add runtime filesystem authority.
- Add another manifest field, loader, or family registry: rejected because the
  existing manifest path/hash and materialization boundary already express the
  required deployment result.

## Consequences

- NT51930 Standard Merge remains the only checked-in owner of this exact family
  snapshot; the General Merge candidate remains self-contained after build.
- Domain, Application, manifest/schema contracts, runtime APIs, firmware facts,
  ranges, operations, support status, and golden claims do not change.
- Reuse by another candidate requires its own explicit source/destination entry,
  architecture review, byte-identity evidence, and firmware-owner R3 approval.
- This accepted candidate implementation cannot be integrated while an
  independent architecture/R3 review has a P0/P1 finding or the owner gate is
  open.

## Verification

- Architecture tests lock the single NT51930 mapping, missing/collision/escape
  failure checks, unchanged manifest family path/hash, and absence of runtime
  resolver knowledge.
- General Merge candidate tests reconstruct a closed test bundle from the same
  metadata, validate it through the manifest-pinned loader, and compare the
  materialized family to the canonical source byte-for-byte.
- Bootstrap deployment tests compare canonical source, intermediate materialized
  root, deployed output, manifest entry SHA-256, and loaded bundle hash. Existing
  release package policy includes only the manifest-declared deployed path.
- `python scripts/verify.py --all` remains the final 0.9.9 handoff gate after all
  independent review and firmware-owner evidence gates are recorded.
