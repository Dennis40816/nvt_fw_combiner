# Minimal Windows Release Package

## Closed allowlist

The end-user ZIP contains one top-level directory with a closed file allowlist:

```text
NvtFwCombiner-vX.Y.Z-win-x64/
├─ NvtFwCombiner.exe
├─ Nfc.CrcWorker.exe
├─ external-tools/
│  ├─ README.md
│  └─ legacy-combiner/
│     ├─ README.md
│     └─ 1.13.0/
│        ├─ Combiner.exe
│        └─ manifest.json
├─ RELEASE-MANIFEST.json
├─ THIRD-PARTY-NOTICES.txt
├─ LICENSE.txt
├─ README.txt
└─ SHA256SUMS.txt
```

No source/profile tree, Python runtime installation, .NET runtime installation, tests, firmware BINs, PDBs, Codex configuration, or signing material is shipped. The only shipped external executable is an owner-approved Combiner package under `external-tools/`, and every file in that subtree is listed in `RELEASE-MANIFEST.json` and `SHA256SUMS.txt`.

## Implemented commands

```powershell
./scripts/package.ps1 -Version 1.0.0 -Commit <40-character-git-sha>
```

The stable release path accepts stable SemVer only, publishes a self-contained single-file `win-x64` Avalonia app with trimming disabled, builds the worker with PyInstaller one-file mode, copies the approved external tool subtree, assembles a new empty directory, rejects paths outside the allowlist, writes the manifest and hashes, and creates the ZIP under `artifacts/release/`.

`main-package.yml` runs the same packager on every `main` push with `-AllowPrerelease`, using the repository `VERSION` value. That workflow first uploads the ZIP, SBOM, and provenance files as a short-retention CI artifact. If GitHub Actions artifact storage is unavailable, it publishes the same files to a generated `main-package-<sha>` prerelease so the self-contained package remains downloadable. This fallback is not a stable release and does not replace the manually gated `release.yml` flow.

## Release gates still requiring organizational setup

- approved code-signing provider and certificate identity;
- SBOM/provenance generation and retention policy;
- private golden regression runner and firmware-owner approval;
- clean Windows smoke without development runtimes;
- final third-party license/legal review.

The manually dispatched release workflow only accepts an existing approved stable `vX.Y.Z` tag. Development tags never publish assets.
