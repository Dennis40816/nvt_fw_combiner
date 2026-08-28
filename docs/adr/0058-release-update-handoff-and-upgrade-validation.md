# ADR 0058: Deliver an exact update handoff and a genuine 1.0.1 validation package

- Status: Accepted by product owner on 2026-08-26; exact packages and release
  evidence remain an R3 release-owner gate
- Date: 2026-08-26
- Owners: Product owner, architecture owner, release owner
- Risk: R2 update-source and package contract; R3 first public release evidence
- Builds on: ADR 0051, ADR 0053, and ADR 0056
- Amended by: ADR 0060 for the temporary complete-release-ZIP ceiling and the
  one-time manual 1.0.2 installation boundary

## Context

Every release must give the operator enough version authority to populate the
configured network update source without reconstructing JSON by hand. The
product owner's term **version profile** means the package's closed
`RELEASE-MANIFEST.json`; the mutable version list is
`update-catalog.v1.json`; and fixed-source recovery uses
`update-source-registry.json`.

The catalog declares the complete ZIP's size and SHA-256. Embedding that
catalog into the same ZIP would create a circular identity. Renaming a 1.0.0
directory or ZIP to 1.0.1 would also leave the executable, package root,
manifest, catalog, and launcher admission identities inconsistent.

## Decision

The immutable GitHub Release keeps its existing exact five-asset contract. The
same protected release run additionally emits a temporary update-source
handoff with this closed layout:

```text
update-source-handoff-v<version>-<source-sha>/
  RELEASE-MANIFEST.json
  update-catalog.v1.json
  update-source-registry.json
  packages/
    NvtFwCombiner-v<version>-win-x64.zip
```

The ZIP contains its canonical inner `RELEASE-MANIFEST.json`. The root copy is
byte-for-byte identical, its SHA-256 equals the catalog's
`releaseManifestSha256`, and the catalog helper rechecks the complete package
SHA-256 immediately before copying it. The helper refuses an existing root
manifest, a non-canonical name, or a destination outside the source root.

The registry JSON in this handoff is an operator seed/snapshot, not a second
runtime registry owner and not the immutable Microsoft 365 locator itself. The
live source may contain multiple retained packages and one aggregate catalog;
operators publish immutable ZIPs first and replace the validated aggregate
catalog last. A one-version handoff manifest copy never changes multi-version
catalog semantics.

After immutable `v1.0.0` publication, a direct single-parent child changes only
the canonical `VERSION` file from `1.0.0` to `1.0.1` and is itself reviewed and
published as formal stable `v1.0.1`. All version-bearing authority,
including `VERSION`, assembly metadata, executable identity, package root and
name, release manifest, launcher owner identity, catalog row, hashes, SBOM,
and provenance, must say 1.0.1. Renaming 1.0.0 bytes is rejected.

The pair proves discovery, verification, install, READY activation, restart,
switch-back, rollback, damaged-version reporting, and explicit deletion. The
1.0.1 package is a formal stable publication whose only purpose is genuine
managed-upgrade validation; it adds no feature or firmware-semantic change.

Because the coupled self-contained launcher makes the measured package about
112 MiB, only canonical 1.0.0 and validation 1.0.1 ZIPs receive the 128 MiB
(134,217,728-byte) complete-package ceiling. Every other package retains the
80,000,000-byte ceiling. The separate application-executable ceiling remains
80,000,000 bytes. ADR 0060 later replaces only this exact-pair package-size
decision; the remaining handoff, identity, integrity, and 1.0.1 decisions stay
current.

## Rejected options

- **Embed the catalog in the ZIP it hashes.** This has no stable finite digest.
- **Rename the 1.0.0 package to 1.0.1.** Internal and external identities would
  disagree and verification must reject it.
- **Add catalog and registry files as extra immutable GitHub Release assets.**
  The catalog is mutable aggregate network-source state; changing the exact
  five-asset stable-release boundary needs separate release-policy approval.
- **Give all 1.0.x packages the larger size allowance.** The exception is
  measured and bounded to the release/validation pair only.

## Consequences

- Each release run gives operators the exact package, manifest, Catalog, and a
  rendered Registry bound to that Catalog's exact bytes for network-source
  staging. The repository's `.json.in` file is deliberately non-admissible and
  is never itself a deployable Registry.
- The network source remains relocatable because catalog package paths are
  relative; registry recovery remains independently rooted at the fixed
  locator contract.
- A new public release must regenerate every version-bearing artifact and
  cannot reuse an old package under a new name.
- Firmware profiles, ranges, processors, output bytes, naming rules, and
  support truth do not change.

## Verification

- Python tests bind the root manifest copy to the inner manifest and catalog
  hashes and reject tamper, overwrite, and path drift. ADR 0060 replaces the
  historical exact-pair size cases with one temporary complete-ZIP ceiling.
- Application and schema tests retain boundary parity under ADR 0060; this
  ADR's original exact 1.0.0/1.0.1 size pair is historical evidence only.
- Workflow-policy tests require all four handoff entries while preserving the
  exact five immutable GitHub Release assets.
- The final isolated lab must exercise 1.0.0 to 1.0.1 and back on local and UNC
  sources before 1.0.0 release approval.
