# Repository Verification Report

Status: historical seed-preparation report for the 2026-06-25 bootstrap baseline, updated for the 0.9.11 reconstructed stabilization release candidate. Current verification evidence is produced by the canonical `python scripts/verify.py --structure-only` and `python scripts/verify.py --all` commands.

Specification package version: `0.9.11`

## 0.9.11 reconstructed stabilization release candidate

The candidate is reconstructed from the exact final `v0.9.10` predecessor
`b0266f312a67d644475731153b1af82f7eadcc95`; the premature integration lineage
is not release authority. Reconciliation retained the nine effective
`v0.9.10` behavior groups recorded in
`docs/governance/v0.9.11-baseline-reconstruction.md` and reapplied only the
reviewed DP/LDC authoring, startup/package, IC Number, topology-group, spacing,
and Build-rail intent.

The exact M3 package application commit is
`b73f8876ad3af41ec792141a929b866c59df4462`. Its self-contained compressed
composite-ReadyToRun EXE is 69,990,762 bytes with SHA-256
`bf5f7e3e5d035e9a1cd2dbeff85cb0d137aa20ca1a8270304c4f147d71fd707e`;
the 75,358,293-byte ZIP SHA-256 is
`36d18b6e03a6569b744187ab924f48860ce424103c12e649c618136dd520587a`.
Visible release smoke and the closed package/external-tool policy gates passed.

One warm-up plus five exact-package Home launches measured a 908.146 ms
process-to-window median (901.461 ms minimum, 932.563 ms maximum). The recorded
first-frame synchronous UI median is 412.455 ms; background UI materialization
totals 53.388 ms with a 25.528 ms longest interval. Median working set is
220,708,864 bytes when the Home window appears and 286,752,768 bytes after
warm-up. The largest synchronous Home interval is root DataContext assignment,
which causes the visible template to instantiate and bind: 276.016 ms and
33,824,392 cumulative allocated bytes. No firmware/profile/user-file/processor
I/O occurs in that interval. These are same-machine observations, not universal
wall-clock or live-managed-heap claims.

UI Smoke `271/271`, Architecture `99/99`, release package policy `14/14`,
external-tool policy dry run, package construction, and visible package smoke
passed during M3. A clean short-path detached worktree at `c58711ce` passed
`python scripts/verify.py --structure-only`, including Polytail fast checks.
The final exact candidate still requires `python scripts/verify.py --all`,
independent R2/R3 and owner review, protected CI, reviewed-main packaging, clean
Windows x64 execution without separately installed .NET/Python,
accessibility/visual review, signing, and immutable release publication.

## 0.9.10 performance-remediation stable release candidate

Stable Node B is protected-main `v0.9.9` commit
`32c37e254271de507be49d0f5ef38faaa122dba6`; optimized production Node C is
`6f3698ddeb0ec50a9ca46057af21e93bfbebd55f`. The 130-commit performance stack
was replayed onto Node B with 130 equal range-diff patches and zero conflicts.
Byte-identical external-tool/golden trees, DP and NT51926 filters, physical
real-tool/headless tests, and command catalog/argv tests pass at both nodes.

One authoritative Build replaces the prior duplicated execution. Legacy
Combiner commands remain exact and sequential, while complete staging reads
change from `2C+1` to one final read plus only the evidenced selective-tail
short-output exception. The NT51926 two-command expected output SHA, the
10,000-difference output/JSON hashes, mutations, validation, issue, command,
and report facts remain unchanged. The release makes no support, profile,
range, CRC/header, processor-command, or product-golden promotion.

The frozen reviewed branch head
`87a84ecd1f03e7257a40ddaf0b5531a3e66aaf30` passed
`python scripts/verify.py --all` in a clean detached worktree in 186.8 seconds.
Release build completed with zero warnings and errors: Domain 357, Application
186, ProfileContract 350, Architecture 95, GoldenRegression 9, Infrastructure
245 with 2 platform skips, Bootstrap 608, and UI Smoke 237 passed; Python CRC
worker tests passed 28/28 at 98.88% coverage. Polytail and repository structure
validation passed.

The exact metadata-aligned package, clean-machine Narrator/NVDA and effective
Windows contrast checks, explicit firmware-owner R3 approval, required PR/main
CI, protected-main tag, and immutable GitHub Release verification remain
release gates. Development-package observations do not substitute for them.

## 0.9.9 legacy-convergence stable release candidate

The reviewed `v0.9.9` code milestone retires the production V1 composition
compiler after exact V2-route and fail-closed coverage. The constrained Legacy
Combiner executable and runner remain the sole legacy execution exception.
Production C#/AXAML is below the owner-approved 54,000-nonblank-line ceiling,
and runtime availability, golden verification, and product-support promotion
remain separate states.

Independent clean-tag verification of code milestone commit
`270e803e1f043ffd56d8568c7e80c7f771a35d7e` passed
`python scripts/verify.py --all` in 165.2 seconds with zero build warnings and
errors: Domain 351, Application 156, ProfileContract 350, Architecture 84,
GoldenRegression 7, Infrastructure 221 with 2 platform skips, Bootstrap 575,
and UI Smoke 131 all passed.

The subsequent canonical golden and external-tool consolidation was reviewed
through PR #147 and merged as
`0589ba3a644bba149d7f42b222bcde68efc52bb2`. Required CI run 29678730641
passed the .NET build/test, repository policy/Polytail, and Python verification
jobs. Independent R3 review reported no P0-P3 findings. The consolidation does
not change firmware bytes, expected outputs, runtime routes, profiles, package
allowlists, or support status.

The earlier `v0.9.9` tag incorrectly pointed to an internal milestone tree that
still reported package version `0.9.8`. The owner explicitly approved replacing
that tag only after this metadata-aligned tree passes review and required CI and
is merged to `main`. The `v0.9.9.5` tag remains an internal predecessor node and
is not stable package authority. The replacement `v0.9.9` tag must identify the
exact reviewed `main` commit and pass the canonical release workflow.

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

- Distribution owner identity is `MSP/FW3`; the private source is identified as `urn:msp-fw3:nvt-fw-combiner:source`, under MIT.
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
