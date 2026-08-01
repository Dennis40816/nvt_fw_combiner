# ADR 0030: Production firmware admission does not use golden hashes

- Status: Accepted for v0.9.12
- Date: 2026-07-21
- Risk: R3; firmware-owner review remains required for firmware facts and release evidence
- Amended by: [ADR 0031](0031-ctrlram-profile-intervals-and-build-plan-authority.md)
  and ADR 0042 for the `0.10.x` retirement of NT51920, NT51925, NT51930,
  and NT51931

## Context

CtrlRAM Replace selected several V2 routes by an evidence-backed firmware tuple and then compared the
complete reference image SHA-256 with one golden artifact hash. That made one private regression
fixture a production allowlist: a valid customer or engineering build with the same reviewed IC,
processor, Common FW, topology, project id, capacity, and structural markers was sent to the legacy
fallback merely because unrelated preserved bytes differed.

ADR 0042 removes NT51920, NT51925, NT51930, and NT51931 from the `0.10.x`
production capability set. Their cases below remain legacy admission
characterization only and cannot be used to retain or re-create a target route.

SHA-256 still has important roles, but those roles have different trust boundaries:

- golden manifests pin exact inputs and expected outputs for reproducible byte-level regression;
- reports, snapshots, preview tokens, and fingerprints identify the bytes actually processed;
- profile bundles, runtime catalogs, and external tools use pinned hashes for supply-chain integrity.

None of those roles makes a golden firmware payload the production definition of one supported
firmware shape.

## Decision

1. Production firmware-input admission must not compare a complete input/reference SHA-256 with a
   golden or owner-evidence artifact hash.
2. Production route selection uses only typed authority required by that workflow. For CtrlRAM
   Replace, ADR 0031 separates requested IC family identity, effective runtime-profile interval, and
   owner-provided build plan. PID, exact golden Common FW, filename, hash, and a fixture's observed
   chip count are not route keys merely because they are modeled fields.
3. A changed whole-file SHA with the same required firmware facts remains eligible for the same
   route. Normal profile validation, range authority, processor authority, immutable-source, final
   structure validation, and atomic output promotion still apply.
4. Golden regressions continue to verify their manifest input hashes, exact outputs, mutation ranges,
   and tool identity. Golden evidence limits support and parity claims; it does not become a runtime
   firmware allowlist.
5. Hash pins for executable tools, trusted profile bundles, flash-map catalogs, postbuild catalogs,
   release manifests, and other shipped configuration remain mandatory. They validate code or
   configuration identity, not a user's firmware input.
6. Runtime reports may continue to record input and output hashes. A recorded hash is evidence and
   traceability data, never an admission decision by itself.

## Invariants

- This decision does not add a new IC, Common FW profile, topology plan, processor, capacity, range,
  command, or support claim.
- Unknown or mismatched byte-authoritative profile, topology, structural, or processor facts still
  fail closed. Informational metadata does not become authority by being present in FWConfig.
- Input/reference artifacts remain immutable and external tools operate only on host staging copies.
- Golden expected bytes cannot be weakened to accommodate a production input variation.
- Tool, catalog, and profile-bundle integrity checks remain fail-closed.

## Regression evidence

`Nt51930CtrlRamFw130EvidenceTests.ExactRouteAcceptsDifferentReferenceHashWhenFirmwareFactsStillMatchAsync`
changes one preserved byte
outside the route-selection metadata, proves the complete reference hash differs from the golden,
then verifies that the same NT51930 V2 profile runs, emits an output, preserves that byte, and leaves
the source reference unchanged.

Architecture tests reject the removed `requiredBaseSha256` and `referencePayload.Sha256` production
gate in the CtrlRAM route selector. Repository searches retain only golden/report/fingerprint hashes
and tool/catalog/profile-bundle integrity pins in their intended boundaries.

## Release impact

The change is support-neutral. It corrects production admission for already modeled firmware tuples;
full-byte golden parity and firmware-owner approval remain the release evidence gate for each claimed
IC/mode. v0.9.12 release notes must state that golden hashes are regression evidence, not runtime
firmware admission criteria.
