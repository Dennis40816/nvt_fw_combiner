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
├─ reference/
│  ├─ README.txt
│  ├─ docs/
│  │  ├─ architecture/
│  │  └─ references/
│  └─ testdata/
│     └─ golden/
│        ├─ standard-merge-gen-flash/
│        │  ├─ manifest.json
│        │  ├─ inputs/
│        │  └─ expected/
│        ├─ ctrlram-replace/
│        └─ owner-handoff/
├─ RELEASE-MANIFEST.json
├─ THIRD-PARTY-NOTICES.txt
├─ LICENSE.txt
├─ README.txt
└─ SHA256SUMS.txt
```

No source/profile tree, Python runtime installation, .NET runtime installation, test projects, private golden inputs, unmanifested firmware BINs, generated firmware outputs, PDBs, Codex configuration, or signing material is shipped. The only shipped external executable is an owner-approved Combiner package under `external-tools/`. Owner-approved Standard Merge golden fixture BINs may ship under `reference/testdata/golden/standard-merge-gen-flash/` only when they are declared by that fixture manifest for future packaged self-tests. Every shipped file under `external-tools/` and `reference/` is listed in `RELEASE-MANIFEST.json` and `SHA256SUMS.txt`.

## Implemented commands

```powershell
./scripts/package.ps1 -Version 1.0.0 -Commit <40-character-git-sha>
```

The stable release path accepts stable SemVer only, publishes a self-contained single-file `win-x64` Avalonia app with trimming disabled, builds the worker with PyInstaller one-file mode, copies the approved external tool subtree, copies the approved reference payload and manifest-declared golden fixture BINs, assembles a new empty directory, rejects paths outside the allowlist, writes the manifest and hashes, and creates the ZIP under `artifacts/release/`.

`main-package.yml` runs the same packager on every `main` push with `-AllowPrerelease`, using the repository `VERSION` value. That workflow first uploads the ZIP, SBOM, and provenance files as a short-retention CI artifact. If GitHub Actions artifact storage is unavailable, it publishes the same files to a generated `main-package-<sha>` prerelease so the self-contained package remains downloadable. This fallback is not a stable release and does not replace the manually gated `release.yml` flow. A stable `release.yml` run rejects a tag that is not reachable from `main`.

## Local package smoke

After `scripts/package.ps1` produces a ZIP, run the deterministic local smoke before handing it to a reviewer:

```powershell
./scripts/smoke-release.ps1 -PackagePath ./artifacts/release/NvtFwCombiner-vX.Y.Z-win-x64.zip
```

The smoke extracts into a fresh temporary directory, checks the closed package surface and manifest hashes, runs the bundled CRC worker `123456789` vector, then briefly starts the self-contained desktop executable. Use `-SkipUiLaunch` only when a visible desktop startup check cannot run; that omission must be recorded in release evidence.

Both `scripts/verify.py` and `scripts/package.ps1` finish by stopping the repository SDK build server and any idle, repo-bound Avalonia BuildServices collector. The cleanup is scoped to that collector command line and never targets the packaged application, CRC worker, or Combiner process.

## First-sample `v1.0.0` release workflow

The first sample release is allowed only after [`development-tags.md`](../governance/development-tags.md) marks the `v1.0.0` support matrix signed off. The package workflow is the distribution gate, not the firmware-support gate.

Release evidence must include:

- stable tag `v1.0.0` on the reviewed `main` commit and matching `VERSION`, assembly metadata, changelog, release manifest, and ZIP name;
- `python scripts/verify.py --all` plus private golden regression for the signed-off IC/mode matrix;
- clean Windows x64 smoke on a machine without separate .NET or Python installs;
- startup, catalog/settings load, worker `123456789` self-check, representative Preview/Build, report modal/history review, and external Combiner 1.13.0 readiness check;
- `RELEASE-MANIFEST.json`, `SHA256SUMS.txt`, SBOM/provenance, third-party notices, and signing/legal approval.

## Release gates still requiring organizational setup

- approved code-signing provider and certificate identity;
- SBOM/provenance retention policy; generation is implemented by the packager;
- private golden regression runner and firmware-owner approval;
- clean Windows smoke without development runtimes;
- final third-party license/legal review.

The manually dispatched release workflow only accepts an existing approved stable `vX.Y.Z` tag reachable from reviewed `main`. Development tags never publish assets.
