# License Scope

## Repository recommendation

The new repository is licensed under the **MIT License**. The root [`LICENSE`](../../LICENSE) applies to original NFC source code and documentation contributed under that repository unless a file is explicitly marked otherwise.

## Boundaries

- The MIT declaration does not automatically relicense third-party, company-proprietary, or reference material.
- `refcode/` snapshots retain their original ownership and source-specific terms. Their manifests are evidence records, not a license grant.
- A reference snapshot with unclear redistribution rights remains private, is excluded from binary releases, and must not be copied to a public repository until ownership/licensing is approved.
- Firmware BINs, golden payloads, keys, and company data are never covered merely because they are used with the MIT-licensed application; they are not committed to the public source tree.
- Dependencies retain their own licenses and are listed in release `THIRD_PARTY_NOTICES.txt`/SBOM.

## Release rule

Every release includes the root MIT license for NFC, third-party notices, and a manifest identifying bundled components. `refcode/`, test firmware, and source-only evidence are excluded from the end-user package.
