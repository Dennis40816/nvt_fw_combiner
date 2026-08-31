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

Windows PowerShell:

```powershell
./scripts/install-dotnet.ps1 -Scope Repository
$env:DOTNET_ROOT = "$PWD/.dotnet"
$env:PATH = "$env:DOTNET_ROOT;$env:PATH"
dotnet --version
python -m pip install -e "./tools/crc-worker[dev]"
python ./scripts/verify.py --all
```

Linux/macOS shell:

```bash
./scripts/install-dotnet.sh --scope repository
export DOTNET_ROOT="$PWD/.dotnet"
export PATH="$DOTNET_ROOT:$DOTNET_ROOT/tools:$PATH"
dotnet --version
python -m pip install -e './tools/crc-worker[dev]'
python scripts/verify.py --all
```

The installers read the exact stable .NET 10 SDK from [`global.json`](global.json), install it under `.dotnet/` by default, verify the installation, and do not require administrator rights.

## Publish Registry and Catalog updates

Production uses one aggregate Catalog and two byte-identical replicas of one
logical Registry publication:

| Purpose | Fixed path |
| --- | --- |
| Primary Registry | `G:\AUTO\projects\模組專案開發\NVT_FW_Combiner\update-source-registry.json` |
| Backup Registry | `G:\AUTO\Tool\NVT_FW_Combiner\update-source-registry.json` |
| Catalog | `G:\AUTO\projects\模組專案開發\NVT_FW_Combiner\update-catalog.v1.json` |
| Version packages | `G:\AUTO\projects\模組專案開發\NVT_FW_Combiner\packages\` |

The Registry only routes clients to Catalog files; it does not list individual
packages. The deployed filename remains `update-source-registry.json`, while
its closed v1 document contains exactly `schemaVersion`, `registryId`,
`registryRevision`, `publishedAtUtc`, `catalogPublication`, and `entries`.
`registryId` is `nvt-fw-combiner-production`.
`catalogPublication` contains `latestVersion`, `catalogSchemaVersion`, and
`catalogSha256`; each entry contains only `status` (`latest`, `available`, or
`deprecated`) and `catalogPath`. There must be exactly one `latest` entry.
For the 1.0.8 update canary, use `schemaVersion: 1`,
`latestVersion: "1.0.8"`, `catalogSchemaVersion: 1`, and one `latest` entry
whose `catalogPath` is the fixed Catalog path in the table above.

The closed Catalog contains `schemaVersion`, `product`, `runtimeIdentifier`,
and `versions`. Each version entry contains `version`, `publishedAt`,
`packagePath`, `packageSize`, `packageSha256`, `releaseManifestSha256`, and
`releaseNotes`. Its fixed root values are `schemaVersion: 1`,
`product: "NVT FW Combiner"`, and `runtimeIdentifier: "win-x64"`. The 1.0.7
and 1.0.8 entries use canonical `packagePath` values
`packages/NvtFwCombiner-v1.0.7-win-x64.zip` and
`packages/NvtFwCombiner-v1.0.8-win-x64.zip`. Do not calculate the three package
identity fields manually:

- `packageSize` is the exact ZIP byte length.
- `packageSha256` is lowercase SHA-256 of the complete ZIP bytes.
- `releaseManifestSha256` is lowercase SHA-256 of the exact inner
  `RELEASE-MANIFEST.json` bytes.
- Registry `catalogSha256` is lowercase SHA-256 of the complete generated
  `update-catalog.v1.json` bytes. The Registry itself is deliberately outside
  package and Catalog checksums; increment `registryRevision` whenever any
  Registry byte changes.

For a new 1.0.7/1.0.8 source, place both immutable ZIPs under `packages\`:

```text
packages\NvtFwCombiner-v1.0.7-win-x64.zip
packages\NvtFwCombiner-v1.0.8-win-x64.zip
```

Then generate one Catalog containing both versions and one staged Registry
whose `latestVersion` is `1.0.8`. Supply the actual locked UTC publication
times, notes paths, and next positive Registry revision:

```powershell
$sourceRoot = 'C:\NvtFwCombiner-Update-Staging'
$stagedRegistry = 'C:\NvtFwCombiner-Registry-Staging\update-source-registry.json'
$nextRegistryRevision = 1 # replace with the current live revision + 1

python .\scripts\create_update_catalog.py `
  --source-root $sourceRoot `
  --published-at '1.0.7=2026-08-31T00:00:00Z' `
  --published-at '1.0.8=2026-08-31T00:01:00Z' `
  --release-notes-file '1.0.7=C:\NvtFwCombiner-Publish\1.0.7-RELEASE-NOTES.md' `
  --release-notes-file '1.0.8=C:\NvtFwCombiner-Publish\1.0.8-RELEASE-NOTES.md' `
  --registry-template '.\docs\ci\update-source-registry.json.in' `
  --registry-output $stagedRegistry `
  --registry-revision $nextRegistryRevision `
  --registry-published-at '2026-08-31T00:01:00Z'
```

Those timestamps and note paths are examples and must be replaced with the
actual release records. Build from a staging copy of the complete source root;
publish immutable ZIPs first and atomically replace the production Catalog
last. If the staged Catalog already contains immutable 1.0.7, add the 1.0.8 ZIP
and supply metadata only for 1.0.8; the generator preserves the existing 1.0.7
metadata and rejects changed bytes under the same version. Never overwrite the
aggregate Catalog with a single-version Actions handoff.

After the production Catalog is in place but before replacing either live
Registry replica, run Version **Self-test** against the staged Registry and
confirm both 1.0.7 and 1.0.8 are visible and 1.0.8 installs. Publish the staged
route through the repository Registry-editor workflow, then confirm the two
fixed Registry replicas have identical SHA-256 values and point to the same
Catalog. Full validation and ACL-preserving publication commands are in
[`docs/contracts/update-catalog-v1.md`](docs/contracts/update-catalog-v1.md)
and
[`docs/contracts/update-source-registry-v1.md`](docs/contracts/update-source-registry-v1.md).

## Repository map

- `src/` — C# Domain, Contracts, Application, Profiles, Infrastructure, Bootstrap, CLI, and Avalonia presentation projects.
- `profiles/` — schema, built-in profiles, and non-firmware samples.
- `tools/crc-worker/` — constrained external Python checksum/header worker.
- `tests/` and `testdata/` — public synthetic tests and private-evidence manifests.
- `refcode/` — exactly two immutable Python references; never compiled or packaged.
- `.agents/skills/`, `.codex/`, and layered `AGENTS.md` — Codex development policy.
- `.github/workflows/` — pull-request verification and stable-tag release packaging.

## Codex start point

Codex must read root `AGENTS.md`, the nearest nested instructions, the relevant ADR/contract, and the matching skill before editing. Every non-trivial change ends with the `polytail` skill and the final canonical gate selected by `AGENTS.md`.

The current `0.10.x` ordering is in
[`docs/governance/0.10.x-ticket-dependency-plan.md`](docs/governance/0.10.x-ticket-dependency-plan.md).
The risk-adaptive development cadence, review checkpoints, retry policy, and
test matrix are in
[`docs/governance/development-execution-workflow.md`](docs/governance/development-execution-workflow.md).
Development tag nodes are in
[`docs/governance/development-tags.md`](docs/governance/development-tags.md).

## Reference and license boundary

New repository code is MIT licensed. `refcode/` remains reference evidence subject to its source ownership and is excluded from production projects and release packages. See [`docs/governance/license-scope.md`](docs/governance/license-scope.md).
