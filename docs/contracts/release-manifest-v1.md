# Release Manifest Contract 1.x

`RELEASE-MANIFEST.json` is the machine-readable inventory for the minimal Windows package. It is generated after publishing/signing and validated before upload.

Rules:

- JSON Schema Draft 2020-12.
- UTF-8, deterministic property order, no unknown properties.
- SHA-256 is lowercase 64-character hex.
- Package paths are relative package paths. Root self-contained application/runtime files use role `application`; approved external tool payloads live under `external-tools/`; built-in materialized profiles live under `profiles/built-in/`; the canonical capability policy has its one exact `docs/contracts/canonical-capability-policy-v1.json` path; human-review evidence and owner-approved golden fixtures live under `reference/`.
- The manifest lists every shipped package file except itself and `SHA256SUMS.txt`. It requires `NvtFwCombiner.exe`, the three root legal/readme files, the CRC Worker at `external-tools/crc-worker/0.1.0/Nfc.CrcWorker.exe`, the canonical capability policy, and at least one built-in profile. No runtime adapter may maintain a smaller handwritten role/path allowlist.
- `SHA256SUMS.txt` is the one canonical auxiliary inventory. It is strict UTF-8 without BOM and contains exactly one lowercase SHA-256 line for every manifest-listed file plus `RELEASE-MANIFEST.json`; it does not list itself. Missing, duplicate, malformed, mismatched, or extra lines fail package admission.
- Owner-approved golden firmware fixtures use role `goldenFixture`; non-BIN reference evidence uses role `reference`.
- Built-in profile/schema/processor digests also pin the aggregated embedded authority, while every separately shipped profile/capability/tool file remains independently listed and hashed.
- The release packager, release smoke, managed installer, and installed-version verifier consume this same schema and closed inventory. Validation counts actual decompressed bytes, not only ZIP metadata.
- `licenseSpdx` is `MIT`.
- Signing fields may be omitted only for explicitly approved unsigned beta/smoke packages.

Example:

```json
{
  "schemaVersion": "1.1",
  "product": "NVT FW Combiner",
  "version": "0.8.0-beta.1",
  "sourceCommit": "0123456789abcdef0123456789abcdef01234567",
  "sourceTag": "v0.8.0-beta.1",
  "runtimeIdentifier": "win-x64",
  "licenseSpdx": "MIT",
  "workerProtocolVersions": ["1.0", "2.0"],
  "approvedProcessorIds": [
    "nfc.crc32-mpeg2.calculate-v1",
    "nfc.nt51917.ctrlram-postbuild-v1",
    "nfc.nt51919.ctrlram-postbuild-v1",
    "nfc.nt51927.ctrlram-postbuild-v1",
    "nfc.nt51928.ctrlram-postbuild-v1",
    "nfc.nt51929.ctrlram-postbuild-v1",
    "nfc.nt51950.ctrlram-postbuild-v1",
    "nfc.nt51951.ctrlram-postbuild-v1"
  ],
  "processorBundleSha256": "0000000000000000000000000000000000000000000000000000000000000000",
  "embeddedProfileCatalogSha256": "0000000000000000000000000000000000000000000000000000000000000000",
  "embeddedSchemaBundleSha256": "0000000000000000000000000000000000000000000000000000000000000000",
  "files": [
    {
      "path": "NvtFwCombiner.exe",
      "size": 1,
      "sha256": "0000000000000000000000000000000000000000000000000000000000000000",
      "role": "application"
    },
    {
      "path": "external-tools/crc-worker/0.1.0/Nfc.CrcWorker.exe",
      "size": 1,
      "sha256": "0000000000000000000000000000000000000000000000000000000000000000",
      "role": "externalTool"
    },
    {
      "path": "THIRD-PARTY-NOTICES.txt",
      "size": 1,
      "sha256": "0000000000000000000000000000000000000000000000000000000000000000",
      "role": "notices"
    },
    {
      "path": "LICENSE.txt",
      "size": 1,
      "sha256": "0000000000000000000000000000000000000000000000000000000000000000",
      "role": "license"
    },
    {
      "path": "README.txt",
      "size": 1,
      "sha256": "0000000000000000000000000000000000000000000000000000000000000000",
      "role": "readme"
    },
    {
      "path": "docs/contracts/canonical-capability-policy-v1.json",
      "size": 1,
      "sha256": "0000000000000000000000000000000000000000000000000000000000000000",
      "role": "capabilityPolicy"
    },
    {
      "path": "profiles/built-in/package-trust-index.json",
      "size": 1,
      "sha256": "0000000000000000000000000000000000000000000000000000000000000000",
      "role": "builtInProfile"
    },
    {
      "path": "external-tools/legacy-combiner/1.13.0/Combiner.exe",
      "size": 1,
      "sha256": "0000000000000000000000000000000000000000000000000000000000000000",
      "role": "externalTool"
    },
    {
      "path": "reference/testdata/golden/canonical/NT51927/standard-merge/gen-flash/topology-unscoped/nt51927-gen-flash/expected/flash.bin",
      "size": 1,
      "sha256": "0000000000000000000000000000000000000000000000000000000000000000",
      "role": "goldenFixture"
    },
    {
      "path": "reference/docs/references/ic-flashmap/IC_FlashMap_20260701.xlsx",
      "size": 1,
      "sha256": "0000000000000000000000000000000000000000000000000000000000000000",
      "role": "reference"
    }
  ],
  "sbomAsset": "NvtFwCombiner-v0.8.0-beta.1.cdx.json",
  "provenanceAsset": "NvtFwCombiner-v0.8.0-beta.1.intoto.jsonl"
}
```

`approvedProcessorIds` may be empty in a pre-transform beta, but a profile requiring processor authority `transform` cannot be included unless its processor id is present and tested. `processorBundleSha256` pins the generated CRC Worker payload, not firmware data. The CRC Worker and shipped Legacy Combiner binaries are represented as `externalTool` file entries; the Combiner remains additionally pinned by its repository external-tool manifest.
