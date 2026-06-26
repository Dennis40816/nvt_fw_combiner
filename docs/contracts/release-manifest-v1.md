# Release Manifest Contract 1.x

`RELEASE-MANIFEST.json` is the machine-readable inventory for the minimal Windows package. It is generated after publishing/signing and validated before upload.

Rules:

- JSON Schema Draft 2020-12.
- UTF-8, deterministic property order, no unknown properties.
- SHA-256 is lowercase 64-character hex.
- Package paths are plain relative filenames.
- Manifest lists five payload files other than itself and `SHA256SUMS.txt`.
- Built-in profile/schema/processor digests describe resources embedded in the executables.
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
  "approvedProcessorIds": ["nfc.nt51950.tpb-header-crc-v1"],
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
      "path": "Nfc.CrcWorker.exe",
      "size": 1,
      "sha256": "0000000000000000000000000000000000000000000000000000000000000000",
      "role": "crcWorker"
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
    }
  ],
  "sbomAsset": "NvtFwCombiner-v0.8.0-beta.1.cdx.json",
  "provenanceAsset": "NvtFwCombiner-v0.8.0-beta.1.intoto.jsonl"
}
```

`approvedProcessorIds` may be empty in a pre-transform beta, but a profile requiring processor authority `transform` cannot be included unless its processor id is present and tested. `processorBundleSha256` covers the deterministic registry/parameter-schema bundle, not firmware data.
