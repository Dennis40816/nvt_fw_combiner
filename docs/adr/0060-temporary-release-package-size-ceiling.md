# ADR 0060: Use one temporary complete-release-package ceiling

- Status: Accepted by product and release owner on 2026-08-28; exact 1.0.2
  package evidence remains an R3 release gate
- Date: 2026-08-28
- Owners: Product owner, architecture owner, release owner
- Risk: R3 release package admission and publication
- Amends: ADR 0058 package-size decision only

## Context

Stable packages contain a self-contained application and a release-coupled,
self-contained managed Launcher. The measured 1.0.1 ZIP is about 112 MiB, so
adding an exact package-size exception for every patch would duplicate policy
without changing the package's integrity boundary.

Immutable 1.0.0 and 1.0.1 binaries accept the larger ceiling only for Catalog
rows 1.0.0 and 1.0.1. They therefore reject a greater-than-80,000,000-byte
1.0.2 row before downloading the new runtime. A no-ReadyToRun evaluation still
estimated the complete package at about 91.9 MB, and trimming the Launcher did
not reduce its compressed entry. There is no low-risk 80,000,000-byte bridge
inside this hotfix.

## Decision

Until measured package-size reduction is implemented and accepted, every
complete release ZIP uses one inclusive 134,217,728-byte admission ceiling.
Application runtime validation remains the policy owner; the JSON Schema,
Catalog builder, and release smoke are projections of the same constant and do
not branch on version.

Version 1.0.2 is a one-time fresh/manual installation boundary. Release notes
and operator handoff must require manual replacement or installation and must
not claim that 1.0.0 or 1.0.1 can update to 1.0.2 through Version management.
After 1.0.2 is running, later packages can use the ordinary verified Catalog
update path under this temporary ceiling.

The independent 80,000,000-byte ceilings for `NvtFwCombiner.exe`, the managed
Launcher, and Bootstrap remain unchanged. Closed inventory, package and inner
manifest hashes, SBOM, provenance, archive bounds, stable-handle rechecks, and
all firmware/profile/composition semantics remain unchanged.

The temporary ceiling has an explicit deletion gate. `PKG-SIZE-108-01` must
produce a reproducible size/startup matrix, and `ZIP-SIZE-108-02` must implement
one accepted reduction with full regression and exact artifact evidence. Only
after both are reviewed and the release owner accepts the measured result may
the temporary ceiling be replaced by that approved measured limit.

## Consequences

- Release tooling no longer needs a per-version package-size allowlist.
- 1.0.0 and 1.0.1 remain valid releases, but automatic managed upgrade from
  either version to 1.0.2 is intentionally not supported.
- The larger ZIP envelope does not authorize new files, weaker hashing, weaker
  archive validation, or a larger executable.
- Package-size reduction remains required work rather than becoming an
  unbounded permanent allowance.

## Verification

- Application runtime, embedded JSON Schema, Catalog builder, and release
  smoke admit representative stable versions through exactly 134,217,728 bytes
  and reject zero, negative, and 134,217,729-byte declarations.
- Release smoke continues to reject an application executable above
  80,000,000 bytes and retains Launcher/Bootstrap executable limits.
- Closed inventory, archive safety, package/manifest hashes, SBOM, provenance,
  stable-handle/tamper tests, package smoke, and the complete repository verifier
  remain mandatory.
- Formal 1.0.2 evidence records exact package size and SHA-256, manual-install
  release notes, clean-package smoke, and release-owner acceptance.
