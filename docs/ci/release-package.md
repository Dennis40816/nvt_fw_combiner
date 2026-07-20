# Minimal Windows Release Package

## Closed allowlist

The end-user ZIP contains one top-level directory with a closed file allowlist:

```text
NvtFwCombiner-vX.Y.Z-win-x64/
├─ NvtFwCombiner.exe
├─ profiles/
│  └─ built-in/
│     ├─ <Bootstrap-declared bundle>/
│     │  ├─ profile-bundle.json
│     │  └─ <manifest-pinned runtime files>
│     └─ ctrlram-postbuild-v2/
│        ├─ catalog.json
│        └─ flash-map.json
├─ external-tools/
│  ├─ README.md
│  ├─ crc-worker/
│  │  └─ 0.1.0/
│  │     └─ Nfc.CrcWorker.exe
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
│        ├─ canonical/
│           ├─ manifest.json
│           ├─ README.md
│           └─ NT519xx/standard-merge/.../
│        └─ release-standard-merge-v1.json
├─ RELEASE-MANIFEST.json
├─ THIRD-PARTY-NOTICES.txt
├─ LICENSE.txt
├─ README.txt
└─ SHA256SUMS.txt
```

No production source tree, editable source profile tree, Python runtime installation, .NET runtime installation, test projects, non-allowlisted private firmware, unmanifested firmware BINs, generated firmware outputs, PDBs, diagnostics, owner-handoff records, or Codex configuration is shipped. `profiles/built-in/` contains only the bundles explicitly materialized by the Bootstrap project plus the fixed `ctrlram-postbuild-v2/catalog.json` and `flash-map.json` runtime catalogs. Each bundle is limited to `profile-bundle.json` and that manifest's pinned entries; the runtime catalog is a separate closed allowlist and is not a V2 profile bundle. Shipping a candidate bundle does not change its declared stage, blockers, runtime eligibility, or owner-review requirement. The packager rejects extra published bundle, runtime-catalog directories, or files. Shipped external executables are confined to `external-tools/`: the generated CRC Worker 0.1.0 payload and the owner-approved Legacy Combiner package. Packaging uses a fixed allowlist, so repository-only packages such as `diff-nf-merge/1.0.0/`, untracked files, or extra files cannot enter a release package. Release-selected Standard Merge golden fixture BINs and fact-scoped alias manifests may ship under `reference/testdata/golden/canonical/` only when selected by `release-standard-merge-v1.json` for future packaged self-tests. Every shipped file under `profiles/built-in/`, `external-tools/`, and `reference/` is listed in `RELEASE-MANIFEST.json` and `SHA256SUMS.txt`.

## Implemented commands

```powershell
./scripts/package.ps1 -Version 1.0.0 -Commit <40-character-git-sha>
./scripts/package.ps1 -Version 0.0.0 -Commit 0000000000000000000000000000000000000000 -ExternalToolPolicyDryRun
```

The stable release path accepts stable SemVer only, publishes a compressed,
self-contained single-file `win-x64` Avalonia app with trimming disabled, copies
the Bootstrap-declared materialized built-in profile bundles through their
manifests, builds the worker with PyInstaller one-file mode, copies only the
approved external-tool files and paths, copies the approved reference payload
and manifest-declared golden fixture BINs, assembles a new empty directory,
rejects paths outside the allowlist, writes the manifest and hashes, and creates
the ZIP under `artifacts/release/`. Single-file compression changes only the
bundle representation: it does not trim managed code, remove native libraries,
or change the closed package contents.

`-ExternalToolPolicyDryRun` retains its compatibility name but exercises all closed package policies without publishing application or worker binaries. It creates a temporary extra file inside the source `external-tools/` directory, runs the same approved-file copy and external-tool manifest-entry code used by normal packaging, and proves the probe is absent from staging and the persisted manifest. It also builds a temporary materialized-profile fixture from the Bootstrap bundle declarations, includes the two fixed runtime-catalog files, runs the production allowlist/copy/manifest-entry functions, and proves unexpected bundle or runtime-catalog files are rejected. The same dry run resolves `release-standard-merge-v1.json`, requires every selected case/artifact path, size, and SHA-256 to match the canonical inventory, currently locks 34 direct BIN artifacts and 13 direct/alias cases, and rejects diagnostics or other workflows. The deterministic `tests/scripts/test_release_package_policy.py` regression invokes this mode through the canonical `python scripts/verify.py --all` flow and proves that release smoke rejects both an extra external-tool path and a package with no built-in materialized profiles.

`main-package.yml` runs the same packager on every `main` push with `-AllowPrerelease`, using the repository `VERSION` value. Both package workflows run `smoke-release.ps1 -SkipUiLaunch` before upload or publication, which checks that materialized built-in profile paths use the `builtInProfile` role and include bundle manifests, checks the exact approved external-tool paths, verifies manifest/hashes and sidecars, and runs the worker self-test. They do not satisfy the visible startup or clean-machine gate. The main workflow first uploads the ZIP, SBOM, and provenance files as a short-retention CI artifact. If GitHub Actions artifact storage is unavailable, it publishes the same files to a generated `main-package-<sha>` prerelease so the self-contained package remains downloadable. This fallback is not a stable release and does not replace the manually gated `release.yml` flow. A stable `release.yml` run rejects a tag that is not reachable from `main`.

## Local package smoke

After `scripts/package.ps1` produces a ZIP, run the deterministic local smoke before handing it to a reviewer:

```powershell
./scripts/smoke-release.ps1 -PackagePath ./artifacts/release/NvtFwCombiner-vX.Y.Z-win-x64.zip
```

The smoke extracts into a fresh temporary directory, checks the closed package surface and manifest hashes, verifies the adjacent SBOM and provenance sidecars against the package version, source tag/commit, runtime, and declared file hashes, runs the bundled CRC worker `123456789` vector, then briefly starts the self-contained desktop executable. `SHA256SUMS.txt` is UTF-8 without a byte-order mark so every manifest-approved Unicode package path remains exact after extraction; changing or replacing an unrepresentable path must fail verification. Keep the ZIP, SBOM, and provenance files together in the artifact directory. Use `-SkipUiLaunch` only when a visible desktop startup check cannot run; that omission must be recorded in release evidence.

Both `scripts/verify.py` and `scripts/package.ps1` finish by stopping the repository SDK build server and any idle, repo-bound Avalonia BuildServices collector. The cleanup is scoped to that collector command line and never targets the packaged application, CRC worker, or Combiner process.

## Package-size ratchet

The owner-approved `v0.9.7` Windows inner-ZIP baseline is 57,501,699 bytes.
`smoke-release.ps1` rejects any later ZIP above 58,076,715 bytes, the greatest
whole-byte value below a 1% increase. The check runs before extraction and is
therefore also the fail-fast gate in `main-package.yml`, `release.yml`, and the
reviewed workflow template.

The byte ratchet does not authorize trimming, removal of the self-contained
.NET runtime, removal of approved profiles/evidence/external tools, or weaker
smoke coverage. A lower reproducible package result lowers the ratchet only
after release review records the producing commit, environment, and artifact.

## First-sample `v1.0.0` release workflow

The first sample release is allowed only after [`development-tags.md`](../governance/development-tags.md) marks the `v1.0.0` support matrix signed off. The package workflow is the distribution gate, not the firmware-support gate.

Release evidence must include:

- stable tag `v1.0.0` on the reviewed `main` commit and matching `VERSION`, assembly metadata, changelog, release manifest, and ZIP name;
- `python scripts/verify.py --all` plus private golden regression for the signed-off IC/mode matrix;
- clean Windows x64 smoke on a machine without separate .NET or Python installs;
- startup, catalog/settings load, worker `123456789` self-check, representative Preview/Build, report modal/history review, and external Combiner 1.13.0 readiness check;
- `RELEASE-MANIFEST.json`, `SHA256SUMS.txt`, SBOM/provenance, third-party notices, and legal approval.
- human release/security approval of the exact package, external-tool allowlist, and redistribution decision; this repository does not require package signing.

## Release gates still requiring organizational setup

- SBOM/provenance retention policy; generation is implemented by the packager;
- private golden regression runner and firmware-owner approval;
- clean Windows smoke without development runtimes;
- final third-party license/legal review.

The manually dispatched release workflow only accepts an existing approved stable `vX.Y.Z` tag reachable from reviewed `main`. Development tags never publish assets.
