# Installer release manifest v1

Every stable release that includes the distribution Launcher publishes one
separate closed installer evidence set:

```text
NvtFwCombiner-Launcher-vX.Y.Z-win-x64.exe
NvtFwCombiner-Launcher-vX.Y.Z-win-x64.manifest.json
NvtFwCombiner-Launcher-vX.Y.Z-win-x64.spdx.json
NvtFwCombiner-Launcher-vX.Y.Z-win-x64.intoto.jsonl
NvtFwCombiner-Launcher-vX.Y.Z-win-x64.sha256
```

These five assets are additive to, and independent of, the existing version
release assets. They are never placed in `update-catalog.v1.json`. The version
ZIP, version manifest, version SBOM/provenance, candidate manifest, and version
checksum remain Bootstrap-free and unchanged.

The installer manifest is deterministic UTF-8 JSON conforming to
`installer-release-manifest-v1.schema.json`. It is generated only after the
final distribution Launcher exists. Protected tooling independently measures
that executable and extracts the embedded descriptor plus Bootstrap before
writing the manifest.

The manifest binds:

- stable installer version, exact source commit, `win-x64`, protocol `1`, and
  `latest-compatible-verified-registry-candidate` selection policy;
- final `distributionEntry` asset name, byte length, and SHA-256;
- embedded payload-admission descriptor resource name, byte length, and
  SHA-256;
- embedded Bootstrap installed filename, byte length, SHA-256, and protocol;
- exact installer SBOM, provenance, and checksum asset names; and
- the real signed or release-owner-approved unsigned disposition.

The checksum document lists the Launcher executable, manifest, SPDX document, and
provenance document in exactly that order using lowercase SHA-256. It does not
list itself. No sixth adjacent payload is required.

The external installer manifest is release evidence only. Launcher Setup must operate
when only its executable is present. An adjacent manifest, checksum, SBOM, or
provenance file cannot change destination, candidate, Registry, Catalog,
Bootstrap, retry, or cleanup behavior.

The non-circular identity order is:

1. publish and hash Root Bootstrap;
2. generate and embed the canonical payload-admission descriptor plus exact
   Bootstrap in the distribution Launcher;
3. publish and measure the final distribution Launcher;
4. extract and verify descriptor/Bootstrap from that final Launcher; and
5. generate manifest, SBOM, provenance, and checksum.

The runtime transaction marker binds measured distribution Launcher identity and embedded
descriptor identity. It never binds this external manifest's digest.

The manifest distinguishes three executable roles: `distributionEntry` is the
only user-facing Launcher; `embeddedBootstrap` becomes the immutable internal
Root Bootstrap; and the version-scoped Launcher remains exclusively inside the
ordinary version ZIP and its release manifest.
