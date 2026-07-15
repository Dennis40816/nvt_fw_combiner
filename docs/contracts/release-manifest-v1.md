# Release Manifest Contract 1.x

`RELEASE-MANIFEST.json` is the machine-readable inventory for the minimal Windows package. It is generated after publishing/signing and validated before upload.

Rules:

- JSON Schema Draft 2020-12.
- UTF-8, deterministic property order, no unknown properties.
- SHA-256 is lowercase 64-character hex.
- Package paths are relative package paths. Base payload files are plain filenames; approved external tool payloads must live under `external-tools/`; human-review evidence and owner-approved golden fixtures must live under `reference/`.
- Manifest lists the five base payload files plus each explicitly approved file path under `external-tools/`, excluding itself and `SHA256SUMS.txt`.
- Manifest also lists every shipped file under `reference/`. Owner-approved golden firmware fixtures use role `goldenFixture`; non-BIN reference evidence uses role `reference`.
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
    },
    {
      "path": "external-tools/legacy-combiner/1.13.0/Combiner.exe",
      "size": 1,
      "sha256": "0000000000000000000000000000000000000000000000000000000000000000",
      "role": "externalTool"
    },
    {
      "path": "reference/testdata/golden/standard-merge-gen-flash/expected/51927/flash.bin",
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

`approvedProcessorIds` may be empty in a pre-transform beta, but a profile requiring processor authority `transform` cannot be included unless its processor id is present and tested. `processorBundleSha256` covers the deterministic registry/parameter-schema bundle, not firmware data. Shipped legacy Combiner binaries are represented as `externalTool` file entries and pinned by their own external tool manifests.
