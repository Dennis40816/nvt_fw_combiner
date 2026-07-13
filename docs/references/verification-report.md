# Repository Verification Report

Status: historical seed-preparation report for the 2026-06-25 bootstrap baseline, updated for the 0.9.2 profile-bundle consolidation milestone. Current verification evidence is produced by the canonical `python scripts/verify.py --structure-only` and `python scripts/verify.py --all` commands.

Specification package version: `0.9.2`

## 0.9.2 consolidation evidence

The 0.9.2 integration branch materializes each built-in V2 profile bundle from the content-addressed schema source inventory and removes the Standard Merge legacy runtime fallback. It retains the trusted loader boundary and all existing firmware behavior. On 2026-07-13, `python scripts/verify.py --all` passed with zero build warnings and errors: Python worker 28, Domain 335, Application 219, ProfileContract 347, Architecture 69, GoldenRegression 9, Bootstrap 192, Infrastructure 138 passed with 2 Unix-only skips, and UI Smoke 106. This local-verification milestone does not publish a package, promote IC support, or authorize AB Code behavior.

## 0.9.1 migration evidence

The 0.9.1 release branch retains the documented legacy comparison and golden evidence while routing the covered Normal/Standard Merge and NT51950/NT51951 DP Replace paths through the V2 family/map/profile compiler boundary. On 2026-07-13, `python scripts/verify.py --all` passed with zero build warnings and errors: Python worker 28, Domain 335, Application 219, ProfileContract 347, Architecture 68, GoldenRegression 9, Bootstrap 188, Infrastructure 138 passed with 2 Unix-only skips, and UI Smoke 105. This source-branch evidence does not establish packaged-install trust, IC product support, or AB Code behavior.

## Bootstrap assertions

- Repository identity is `Dennis40816/nvt_fw_combiner`, private, MIT.
- `global.json` pins .NET SDK `10.0.301` and installers consume that value.
- Avalonia packages are centrally pinned to `12.0.5`.
- Root `SPEC.md`, layered AGENTS, Codex configuration and nine skills are present.
- Replace experiences are DP Replace, CtrlRAM Replace and General Replace; Merge experiences are Standard, AB and General.
- `refcode/` contains exactly the two approved Python snapshots and their hashes remain validated.
- No production project references `refcode/`.
- Init node is `v0.1.0-dev.0` and does not claim firmware parity.

## Commands

```text
python scripts/verify.py --structure-only
python scripts/verify.py --all
```

The full command requires the pinned .NET SDK and Python worker development dependencies. Private golden and clean-machine release evidence are intentionally absent at init and remain milestone gates.

## Seed preparation evidence

Executed successfully on 2026-06-25 before the init commit:

- `python scripts/verify.py --structure-only` — repository structure, schemas, policy, source manifests, layered agent files and Polytail fast gate passed.
- `bash -n scripts/install-dotnet.sh scripts/bootstrap.sh scripts/verify.sh scripts/publish-github.sh` — shell syntax passed.
- `python -m pytest --cov=nfc_crc_worker --cov-branch --cov-report=term-missing` — 28 tests passed with 100% line and branch coverage.

Not executed in the seed-preparation container:

- `.NET restore / format / build / test`, because the pinned .NET SDK was not installed and the container could not download it. The repository installers and the `dotnet / build-test` CI job own this gate.
- Ruff, Pyright and Pylint, because those development modules were not present locally. The `python-worker / verify` CI job installs and runs them.
- Windows release packaging, signing and clean-machine smoke; these remain release milestone gates.
