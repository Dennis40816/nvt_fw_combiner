# Test-count baseline and public shard summaries

These Python entries port FreeformHelper PR 80 at
`6629c03b41f2637b5d6a95509882ac4029d9028a`. The baseline uses schema version 2;
the summary uses schema version 1. Field names and JSON types are retained.
NFC adds the top-level `pin` and admits only `bootstrap`, `ui`, and `core`.
JSON output is UTF-8 without a BOM, two-space indented, with CRLF and a final
newline, matching the upstream writers.

The checked-in floors of 1 are provisional scaffolding, not measured totals.
The canonical .NET build is currently blocked before any shard produces TRX.
Do not enable these floor gates until all three actual totals have been
measured, written by the updater, and checked again against retained TRX.

`read_trx.py` recursively reads every `*.trx`. It rejects DTDs and external
entities. Only `TestMethod.className`, `TestMethod.name`, result outcomes and
counters are used. Display names, theory arguments, `Output`, and `ErrorInfo`
are excluded. Invalid or missing results produce exactly
`TRX results missing or invalid.`

`check_test_shard.py` writes `shard-summary.json` and exits 0 only for a complete
passing run at or above the floor, with zero skipped and unfinished cases and
no update-mode variables. `--test-run-failed` preserves the counters but fails
the overall outcome. `--unverified` always writes outcome `unverified` and
exits 1 with `unverified, cannot merge`.

The checked variables are `NFC_UPDATE_SOURCE_REGISTRY_PATH`,
`NFC_UPDATE_CATALOG_TOOL`, `NFC_UPDATE_SOURCE_REGISTRY_TEMPLATE`, and
`NVT_VERSION_UPDATE_SOURCE_LAB`. Presence includes empty values and `0`.
Current NFC source declares no Golden-regeneration environment variable;
these names are update-source configuration and test-lab controls found in
the scripts/tests search. `exampleMinTests` is zero and `requiredClasses` is
empty until the owner measures the Golden-dependent set by a reverse run.
The upstream `ExampleDataFact`/`ExampleDataTheory` declaration reader is retained
for format compatibility; NFC currently has no such declarations.

`update_test_baseline.py` raises floors only. Missing, failed, skipped,
unfinished, or lower-count evidence is rejected without writing the baseline.
An unchanged floor leaves the file byte-identical. A raised floor records
`measuredAtCommit`; it does not change the pin.

## Commands for the later workflow step

Run the existing producer, then run the checker even when the producer fails.
Use the producer exit status to add `--test-run-failed`. Do not use a successful
TRX row count to mask a failed process or failed producer.

```powershell
python scripts/verify.py --ci-dotnet-test-shard bootstrap
python scripts/ci/check_test_shard.py --shard bootstrap --results-directory artifacts/ci-dotnet-upload/shards/bootstrap/results --summary-path artifacts/public-summaries/bootstrap/shard-summary.json --example-data-present --commit $env:GITHUB_SHA

python scripts/verify.py --ci-dotnet-test-shard ui
python scripts/ci/check_test_shard.py --shard ui --results-directory artifacts/ci-dotnet-upload/shards/ui/results --summary-path artifacts/public-summaries/ui/shard-summary.json --example-data-present --commit $env:GITHUB_SHA

python scripts/verify.py --ci-dotnet-test-shard core
python scripts/ci/check_test_shard.py --shard core --results-directory artifacts/ci-dotnet-upload/shards/core/results --summary-path artifacts/public-summaries/core/shard-summary.json --example-data-present --commit $env:GITHUB_SHA

# Untrusted fork or Dependabot: no key and no private assembly.
python scripts/ci/check_test_shard.py --shard core --results-directory artifacts/unverified-results --summary-path artifacts/public-summaries/core/shard-summary.json --commit $env:GITHUB_SHA --unverified
```

Public summaries are outside the producer evidence directory because the
existing finalizer enforces a closed file inventory and content hashes.
These entries are not wired into `verify.py` or a workflow as floor checks.

## Local measurement and inspection

Before tests or verifiers, load the existing user-level `NFC_TEST_AREA_ROOT`
and set `TEMP`, `TMP`, and `TMPDIR` to its existing `temp` child. In a managed
console with an inherited declaration, preserve it if the sandbox account has
no user-level declaration. Do not create another test-area root.

```powershell
$declaredTestArea = [Environment]::GetEnvironmentVariable('NFC_TEST_AREA_ROOT', 'User')
if ($declaredTestArea) { $env:NFC_TEST_AREA_ROOT = $declaredTestArea }
$env:TEMP = Join-Path $env:NFC_TEST_AREA_ROOT 'temp'
$env:TMP = $env:TEMP
$env:TMPDIR = $env:TEMP

python scripts/ci/read_trx.py artifacts/ci-dotnet-upload/shards/core/results
python scripts/ci/update_test_baseline.py --shard core --results-directory artifacts/ci-dotnet-upload/shards/core/results --measured-at-commit $env:GITHUB_SHA
python -m pytest tests/scripts/test_ci_test_baseline.py tests/scripts/test_golden_pin.py
```

`read_trx.py` emits only counters and parameter-free method outcomes. Raw TRX,
coverage, discovery and attempt artifacts retain their existing formats and
are not made safe for public upload by writing a summary. See the separate
workflow proposal before enabling private asset assembly in public CI.
