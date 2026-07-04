# License Scope

## Repository recommendation

The new repository is licensed under the **MIT License**. The root [`LICENSE`](../../LICENSE) applies to original NFC source code and documentation contributed under that repository unless a file is explicitly marked otherwise.

## Boundaries

- The MIT declaration does not automatically relicense third-party, company-proprietary, or reference material.
- `refcode/` snapshots retain their original ownership and source-specific terms. Their manifests are evidence records, not a license grant.
- A reference snapshot with unclear redistribution rights remains private, is excluded from binary releases, and must not be copied to a public repository until ownership/licensing is approved.
- Firmware BINs, golden payloads, keys, and company data are never covered merely because they are used with the MIT-licensed application. They may be committed or packaged only when the owner explicitly approves the fixture, provenance, hash manifest, and confidentiality boundary.
- Dependencies retain their own licenses and are listed in release `THIRD_PARTY_NOTICES.txt`/SBOM.

## Release rule

Every release includes the root MIT license for NFC, third-party notices, and a manifest identifying bundled components. `refcode/`, private inputs, unmanifested firmware, generated firmware outputs, and source-only implementation trees are excluded from the end-user package. Owner-approved reference evidence and manifest-declared Standard Merge golden fixtures may ship only under the package `reference/` payload for review or packaged self-test purposes.
