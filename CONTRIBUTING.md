# Contributing

This repository uses protected `main`, version integration branches named with the exact planned release (for example `0.8.1`), and short-lived feature branches.

## Branch model

1. Start each release on its owner-selected version integration branch, such as `0.8.1`.
2. Commit tightly coupled release work directly to that version branch in independently verifiable phases.
3. Create `feature/<version>/<topic>` from the version branch only for an independently reviewable feature; merge it back to the same version branch after review.
4. Open the only `main` PR after the version branch reaches its defined scope and final review gates.

## Change sequence

Ordinary non-normative, non-classifier-governed prose uses the root `AGENTS.md`
R0 short path: preserve existing edits, update the existing document, review the
diff and affected links, and report the result. Run structure/consumer checks
when layout or parsed inputs change. No new issue, branch, ADR, code-size report
or product test is required for that path. AGENTS/governance and other
classifier-governed documents retain their record/integration obligations;
normative, permission and release changes retain their affected gates.

For R1-R3 implementation:

1. Use the owner-approved task or issue and its acceptance criteria; do not create a duplicate issue solely for ceremony.
2. Reuse the appropriate version branch; create a feature branch only for an independently reviewable change.
3. Read root and applicable nested `AGENTS.md`; update an ADR when an architectural decision changes.
4. Implement a coherent change and run the relevant behavioral tests. Reuse an existing regression when it covers the change.
5. At an authorized, tested checkpoint, review the exact staged files and create a Conventional Commit. Do not stage unrelated work or require a commit per paragraph/test correction.
6. Run `python scripts/verify.py --all` once on the frozen R1-R3 integration/release candidate; root risk rules control interim local checks.
7. Merge feature work to its version branch, then open the final version-to-`main` PR under the existing authority and review rules. Use a Conventional Commit style title.

## Current test-platform scope

Current release validation targets Windows x64; CI and release jobs run on
Windows. Six Unix-runtime-only integration tests were retired in v1.1.3 by
owner decision: two FIFO/socket cases and four process-group cleanup cases.
Their source remains recoverable in Git history. Windows execution now admits
zero skipped .NET cases. Shared tests that run on Windows, including mocked
platform-boundary checks, remain; production Unix compatibility code is unchanged.
This deletion was checked statically without a separate test rerun, as requested.
It does not waive final release verification or complete certified Golden output
execution. A possible future Linux CLI requires a separate support decision and
appropriate real-platform evidence; no version or date is committed here.

## Fixed test area

Create one absolute test root outside the repository once and persist it as the
user-level `NFC_TEST_AREA_ROOT`. On Windows the canonical local root is
`D:\NvtFwCombiner-TestArea` when that drive is available:

```powershell
$testArea = 'D:\NvtFwCombiner-TestArea'
New-Item -ItemType Directory -Force $testArea, (Join-Path $testArea 'temp')
[Environment]::SetEnvironmentVariable('NFC_TEST_AREA_ROOT', $testArea, 'User')
```

Every new PowerShell process explicitly loads the declaration and pins all
ambient temporary paths before a verifier or direct narrow test:

```powershell
$env:NFC_TEST_AREA_ROOT = [Environment]::GetEnvironmentVariable('NFC_TEST_AREA_ROOT', 'User')
$env:TEMP = Join-Path $env:NFC_TEST_AREA_ROOT 'temp'
$env:TMP = $env:TEMP
$env:TMPDIR = $env:TEMP
python scripts/verify.py --structure-only
```

Use the same preamble for `python scripts/verify.py --all`, `dotnet test`,
`python -m unittest`, and `python -m pytest`; a direct narrow test never selects
an arbitrary temporary directory. GitHub Actions supplies only `RUNNER_TEMP`;
the verifier derives its exact test root and rejects a conflicting declaration.

## Derived-file preflight

After an authorized source change, use the common tool to inspect and synchronize
its mechanical projections before formal verification. Keep the fixed test-area
preamble above when running tests or the verifier.

```text
python scripts/sync_derived.py --list
python scripts/sync_derived.py
python scripts/sync_derived.py --write --only v0916-workflow-contract --only ci-template-mirror
python scripts/sync_derived.py
python scripts/verify.py --all
```

The first check reports drift with a nonzero exit and a diff. Select only providers
whose source changes are within the current authorization; the example selects a
workflow/CI edit. Agents should perform that mechanical step autonomously, review
the resulting diff, then require the all-provider check to pass. A second write
must make no changes. Bare `--write` and every CI write are rejected.

| Provider | Source and generated targets |
| --- | --- |
| `v0916-workflow-contract` | Approved release workflow → four derived semantic digests, raw contract identity, existing plan/schema/test projections. |
| `ci-template-mirror` | Executable CI → exact bytes of `docs/ci/workflow-templates/ci.yml`; do not edit the mirror directly. |
| `reviewed-source-pins` | Approved canonical capability policy, package trust index and Golden release allowlist → named loader/package/smoke pins and the active test fixture. |
| `release-version-headers` | Authorized stable `VERSION` → only the numeric version headers in `SPEC.md` and `docs/references/verification-report.md`. |

Use `--write --only reviewed-source-pins` only after all changed source payloads
in that provider have been authorized; selecting it does not approve those
payloads. The Golden allowlist projection changes only its two existing package/smoke
raw-SHA pins, never its approval, version, date or payload declarations.
After an authorized version bump, use
`--write --only release-version-headers`; it preserves status prose, dates and
historical results and does not approve Golden redistribution for a new version.
Golden expectations, historical evidence/attestations, commit authority,
SDK versions and coverage baselines remain untouched. The release summary
template is not the CI mirror and is not overwritten.

Release manifest/SHA256SUMS/SBOM/provenance already come from `package.ps1`;
Catalog and Registry metadata from `create_update_catalog.py`; lab and intake
metadata from their existing generators. They need artifact or owner inputs and
are not duplicated in repository preflight. The canonical structure gate begins
with a read-only all-provider check; it never fixes its own expected values.
The boundary is recorded in [ADR 0068](docs/adr/0068-derived-file-synchronization.md).

## Pull request evidence

Each PR must state:

- what changed and why;
- affected ICs, modes, profiles, ranges, and contracts;
- tests and exact commands run;
- golden hashes affected or explicitly unaffected;
- compatibility and release impact;
- remaining risks.

Firmware-semantic changes require human byte-level review. Generated output screenshots are not a substitute for golden regression.
