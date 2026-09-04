# NVT FW Combiner

NVT FW Combiner is a profile-driven desktop utility for deterministic firmware image composition. The repository begins at the architecture/bootstrap node `v0.1.0-dev.0`; it does **not** claim firmware parity yet.

## Product model

All workflows compile to one checked `CompositionEngine`:

- **Merge** initializes a blank image and synthesizes a new output.
- **Replace** clones one required reference BIN and modifies the clone.
- **General Merge/Replace** use the same typed explicit-mapping model as fixed profiles; they do not execute arbitrary scripts.

Merge experiences are **Standard**, **AB Code**, and **General**. Replace experiences are:

- **Display** — DP whole or declared partitions; TP is one atomic whole when offered.
- **TP HW** — only named TP CtrlRAM regions/groups; DP is whole-only.
- **TP FW** — declared non-CtrlRAM TP regions; DP is whole-only and CtrlRAM is blocked by default.
- **General** — one or more BINs and explicit source-to-target mappings inside a profile-approved safety envelope.

The canonical specification is [`SPEC.md`](SPEC.md). The access matrix is documented in [`docs/architecture/experience-and-access-policy.md`](docs/architecture/experience-and-access-policy.md).

## Bootstrap

Initialize one fixed local test area once. Replace the example path only when
the machine uses another existing absolute directory outside the repository:

```powershell
$testArea = 'D:\NvtFwCombiner-TestArea'
New-Item -ItemType Directory -Force $testArea, (Join-Path $testArea 'temp')
[Environment]::SetEnvironmentVariable('NFC_TEST_AREA_ROOT', $testArea, 'User')
```

Windows PowerShell:

```powershell
$env:NFC_TEST_AREA_ROOT = [Environment]::GetEnvironmentVariable('NFC_TEST_AREA_ROOT', 'User')
$env:TEMP = Join-Path $env:NFC_TEST_AREA_ROOT 'temp'
$env:TMP = $env:TEMP
$env:TMPDIR = $env:TEMP
./scripts/install-dotnet.ps1 -Scope Repository
$env:DOTNET_ROOT = "$PWD/.dotnet"
$env:PATH = "$env:DOTNET_ROOT;$env:PATH"
dotnet --version
python -m pip install -e "./tools/crc-worker[dev]"
python ./scripts/verify.py --all
```

Linux/macOS shell:

```bash
export NFC_TEST_AREA_ROOT=/absolute/fixed/NvtFwCombiner-TestArea
export TEMP="$NFC_TEST_AREA_ROOT/temp"
export TMP="$TEMP"
export TMPDIR="$TEMP"
mkdir -p "$TEMP"
./scripts/install-dotnet.sh --scope repository
export DOTNET_ROOT="$PWD/.dotnet"
export PATH="$DOTNET_ROOT:$DOTNET_ROOT/tools:$PATH"
dotnet --version
python -m pip install -e './tools/crc-worker[dev]'
python scripts/verify.py --all
```

The installers read the exact stable .NET 10 SDK from [`global.json`](global.json), install it under `.dotnet/` by default, verify the installation, and do not require administrator rights.
Every later verifier or direct narrow-test shell repeats the four environment
exports above. GitHub Actions does not use the local declaration; the canonical
verifier derives `RUNNER_TEMP/NvtFwCombiner-TestArea` itself.

## Deploy a published version to an update source

Use one Windows command after the exact stable GitHub Release exists. The
source root and its `packages` child must already exist as ordinary local-drive
or UNC directories. Copy `CatalogPublishedAtUtc` from the protected release
workflow evidence or its single-version handoff; GitHub's `publishedAt` is not
the canonical Catalog value.

First validate the stable Release, download, digest, destination path, and
existing package without changing the source:

```powershell
.\scripts\deploy-update-source.ps1 `
  -Version '1.0.8' `
  -CatalogPublishedAtUtc '2026-09-01T00:00:00Z' `
  -SourceRoot 'G:\AUTO\projects\模組專案開發\NVT_FW_Combiner' `
  -WhatIf
```

Replace the timestamp with the exact release evidence, review the plan, and
run the same command without `-WhatIf`. The script is pinned to
`Dennis40816/nvt_fw_combiner`. It authenticates with `gh`, accepts only the
exact non-draft, non-prerelease `v<Version>` Release and canonical Windows ZIP,
verifies the published size and SHA-256 after downloading, preserves every
existing version package, admits a new ZIP without overwrite, and calls the
existing locked `create_update_catalog.py` publisher. The exact GitHub Release
body becomes that version's Catalog release notes, so the same command remains
valid for 1.0.9 and later changelogs. It does not publish tags, Releases, or the
live Registry.

Catalog v1 is the default and is the required 1.0.7-to-1.0.8 canary path. After
the v1 canary, Catalog v2 can assign `manual-only` or `notify`; provide one
explicit assignment for every retained version because the tool never guesses:

```powershell
.\scripts\deploy-update-source.ps1 `
  -Version '1.0.8' `
  -CatalogPublishedAtUtc '2026-09-01T00:00:00Z' `
  -SourceRoot 'G:\AUTO\projects\模組專案開發\NVT_FW_Combiner' `
  -CatalogSchemaVersion 2 `
  -NotificationPolicy @('1.0.7=notify', '1.0.8=manual-only')
```

If Catalog publication fails after a new immutable ZIP was admitted, retain
and report the unreferenced ZIP. Do not delete it manually until a
repository-owned Catalog-publisher operation proves under its existing lock
that neither Catalog version references its path, version, or bytes.

The Registry is a separate R3 route decision. After Version **Self-test**
confirms all retained versions and the new install, use the ACL-preserving,
complete-route workflow in
[`docs/contracts/update-source-registry-v1.md`](docs/contracts/update-source-registry-v1.md).
Lower-level package, handoff, and validation evidence remains in
[`docs/ci/release-package.md`](docs/ci/release-package.md).

## Documentation start point

Start documentation work here. This is navigation, not a second TODO list;
each linked canonical source owns its scoped facts.

| Need | Canonical route |
| --- | --- |
| Current TODO and version allocation | [NFC roadmap](docs/architecture/nfc_roadmap.md) — linked handoffs provide detail; its owner-unallocated queue is not approved work. |
| Repository document convergence | [v1.1.2 handoff](docs/architecture/v1.1.2-repository-document-convergence-handoff.md) and [manifest](docs/architecture/v1.1.2-repository-document-convergence-manifest.md) — Phase 1 frozen evidence and the current Phase 2 basis; no second inventory or TODO owner. |
| Product and behavior | [SPEC.md](SPEC.md) |
| Architecture decisions | [ADR index](docs/adr/README.md) |
| Development and agent workflow | [AGENTS.md](AGENTS.md), [development execution workflow](docs/governance/development-execution-workflow.md), [branch/version/release governance](docs/governance/branch-version-and-release-governance.md), [agent-skill routing](docs/governance/agent-skill-routing.md), and generated [agent-skill inventory](docs/governance/agent-skill-inventory.md) |
| Release history and evidence | [CHANGELOG.md](CHANGELOG.md), [verification report](docs/references/verification-report.md), and [release package](docs/ci/release-package.md) |

## Reference and license boundary

New repository code is MIT licensed. `refcode/` remains reference evidence subject to its source ownership and is excluded from production projects and release packages. See [`docs/governance/license-scope.md`](docs/governance/license-scope.md).
