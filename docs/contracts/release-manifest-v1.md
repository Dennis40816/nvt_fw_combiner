# Release Manifest Contract 1.x

`RELEASE-MANIFEST.json` is the machine-readable inventory for the minimal Windows package. It is generated after publishing/signing and validated before upload.

Rules:

- JSON Schema Draft 2020-12.
- UTF-8, deterministic property order, no unknown properties.
- SHA-256 is lowercase 64-character hex.
- Package paths are relative package paths. Base payload files are plain filenames; approved external tool payloads must live under `external-tools/`; human-review evidence and owner-approved golden fixtures must live under `reference/`.
- Manifest lists the four root payload files plus each explicitly approved file path under `external-tools/`, excluding itself and `SHA256SUMS.txt`. The CRC Worker is an `externalTool` at `external-tools/crc-worker/0.1.0/Nfc.CrcWorker.exe`, not a root payload.
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
