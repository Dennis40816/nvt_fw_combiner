# Repository Verification Report

Status: historical seed-preparation report for the 2026-06-25 bootstrap baseline, updated for the 0.9.8 feature-frozen convergence candidate. Current verification evidence is produced by the canonical `python scripts/verify.py --structure-only` and `python scripts/verify.py --all` commands.

Specification package version: `0.9.8`

## 0.9.10 codebase-audit baseline

On 2026-07-17, local `0.9.10` at `762c777d` was audited before readiness
planning. `origin/0.9.9` and `origin/0.9.10` are sibling lines with five and two
unique commits respectively, so the current branch is not yet a reviewed
successor and cannot be a release baseline.

`python scripts/verify.py --all` passed structure, Polytail, 19 repository
Python tests, Ruff, Pyright, Pylint, 28 CRC-worker tests at 98.88% coverage,
restore, format, Release build with zero warnings/errors, and 1,605 .NET tests.
It failed one Infrastructure process-tree timeout test before the child PID was
published; the isolated test then passed. Two Unix-only special-file tests
skipped on Windows as designed. The candidate timeout/headless-process change
already exists on `origin/0.9.9`, but it is not in the audited `0.9.10`
lineage. The canonical full gate therefore remains red until predecessor
reconciliation, review, and one final rerun.

This baseline is diagnostic evidence only. It does not promote firmware
support, close private golden/signing/legal/clean-machine gates, or authorize a
range, processor, or output-byte change.

## 0.9.8 convergence candidate

The 0.9.8 integration branch is feature-frozen and support-neutral. It retains
the 0.9.7 firmware behavior and evidence gates while lowering the owner-accepted
production ratchet to 56,742 nonblank C#/AXAML lines, exact duplicate JSON to
1,156 lines, `WorkbenchCompositionService` to 4,483 lines, and
`MainWindowViewModel` to 2,847 lines. The portable Windows package remains
bounded by the reviewed 58,076,715-byte maximum. Canonical verification and
package smoke do not replace CtrlRAM owner golden outputs/sign-off, signing and
legal approval, protected remote CI, or clean-machine evidence.

## 0.9.7 integration candidate

The 0.9.7 integration branch combines the reviewed 0.9.6 lineage with the
fact-scoped AB evidence forward-port, compiled final-output validation and the
non-routed NT51926 CtrlRAM V2 candidate, and the audited semantic UI token
consolidation. Phase-local AB, Application, Domain, Bootstrap, Architecture,
and UI smoke tests passed on 2026-07-15.

On 2026-07-15, `python scripts/verify.py --all` passed with zero build warnings
and errors: 12 repository Python tests, 28 CRC-worker tests at 98.88% coverage,
Domain 345, Application 244, ProfileContract 362, Architecture 71,
GoldenRegression 6, Bootstrap 260, Infrastructure 180 with 2 Unix-only skips,
and UI Smoke 119. Polytail and the post-`v0.9.2` Conventional Commit audit also
passed; all 98 integration commits present before this evidence-only update had
clean phase-scoped subjects and no WIP/fixup markers.

This candidate does not promote AB or CtrlRAM runtime support and does not
replace the remaining firmware-owner, product-golden, signing, protected-CI,
or clean-machine release evidence. Stable package smoke is performed only from
the reviewed `main` commit, not from this pre-merge integration branch.

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
