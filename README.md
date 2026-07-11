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

The bounded handoff sequence is in [`docs/governance/codex-handoff.md`](docs/governance/codex-handoff.md). The development cadence, autonomous phase-commit rule, and test matrix are in [`docs/governance/development-execution-workflow.md`](docs/governance/development-execution-workflow.md). Development tag nodes are in [`docs/governance/development-tags.md`](docs/governance/development-tags.md).

## Reference and license boundary

New repository code is MIT licensed. `refcode/` remains reference evidence subject to its source ownership and is excluded from production projects and release packages. See [`docs/governance/license-scope.md`](docs/governance/license-scope.md).
