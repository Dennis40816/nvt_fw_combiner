# Test-count baseline and public shard summaries

These Python entries port FreeformHelper PR 80 at
`6629c03b41f2637b5d6a95509882ac4029d9028a`. The baseline uses schema version 2;
the summary uses schema version 1. Field names and JSON types are retained.
NFC adds the top-level `pin` and admits only `bootstrap`, `ui`, and `core`.
JSON output is UTF-8 without a BOM, two-space indented, with CRLF and a final
newline, matching the upstream writers.

The checked-in floors are the discovered test counts of one local full run at
`measuredAtCommit`: bootstrap 2295, ui 2306, core 4688. The ui run executed all
of its tests. The local bootstrap and core runs did not complete cleanly because
of environment limits in some Infrastructure and catalog tests, so their floors
come from the discovery list, not from a passing total. The 2026-10-11 owner
contract now requires the shard producer and finalizer to enforce these floors.
Raising a floor still requires a complete passing execution; the retained local
measurement is not evidence of a passing CI run.

`read_trx.py` recursively reads every `*.trx`. It rejects DTDs and external
entities. Only `TestMethod.className`, `TestMethod.name`, result outcomes and
counters are used. Display names, theory arguments, `Output`, and `ErrorInfo`
are excluded. Invalid or missing results produce exactly
`TRX results missing or invalid.` A class or method name outside
`[A-Za-z0-9_.]+` (a generic class, a non-ASCII identifier) is left out of the
failed-method and executed-class lists. The counters still count its result, so
the run stays valid.

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

## Public artifact contract

Each shard uploads a schema-5 summary manifest, the schema-1 shard summary,
64-character SHA-256 hashes, and one validated product-code coverage pair per
completed project. The pair uses Coverlet JSON and Cobertura so aggregate
coverage retains its existing inputs. Summaries contain final counters,
parameter-free failed/flaky methods, assembly hashes and a retry receipt with
only a boolean and a recovered-case count. The producer reuses this directory's
TRX reader and shard checker; the finalizer checks their closed summary contract,
hashes and baseline floors for all three shards of the current run. A shard
that did not re-run after "Re-run failed jobs" keeps the evidence of its earlier
attempt, which the finalizer checks against the attempt in its artifact name.

Raw results, discovery lists, execution/retry logs, failure messages, captured
output and hang/crash attachments stay on the producer runner under the existing
local evidence lifecycle. They are never staged for upload. The runner-local
manifest retains schema 4, raw evidence paths and raw hashes. The schema-5
projection is created in a separate temporary subtree inside that evidence root
and removed after publication; rewritten TRX copies stay there too. Coverage
is projected by dropping non-production modules/sources and generated files;
retained test identities, messages or undeclared fields are refused. Raw evidence
still controls producer retry and hang decisions; reproduce failures locally.
The build producer and other CI lanes are outside this shard contract.

## Producer commands

The verifier writes the public summary even when the test shard fails. A failed
producer remains failed; a summary never masks its process result.

```powershell
python scripts/verify.py --ci-dotnet-test-shard bootstrap
python scripts/verify.py --ci-dotnet-test-shard ui
python scripts/verify.py --ci-dotnet-test-shard core
```

The standalone checker remains available for runner-local retained results and
unverified runs. It must not read the public upload tree, which contains no raw
results. Console test-process output is scrubbed whenever `GITHUB_ACTIONS=true`
(a GitHub-hosted runner always sets it) and on a local run that sets
`NFC_CI_SCRUB_TEST_OUTPUT=1`. A local run without the flag keeps its console
output. The public artifact contract is always enforced.

## Local measurement and inspection

Before tests or verifiers, load the existing user-level `NFC_TEST_AREA_ROOT`
and set `TEMP`, `TMP`, and `TMPDIR` to its existing `temp` child. In a managed
console with an inherited declaration, preserve it if the sandbox account has
no user-level declaration. Do not create another test-area root.

Set `$runnerResults` to the current runner-local or reproduced results directory.

```powershell
$declaredTestArea = [Environment]::GetEnvironmentVariable('NFC_TEST_AREA_ROOT', 'User')
if ($declaredTestArea) { $env:NFC_TEST_AREA_ROOT = $declaredTestArea }
$env:TEMP = Join-Path $env:NFC_TEST_AREA_ROOT 'temp'
$env:TMP = $env:TEMP
$env:TMPDIR = $env:TEMP

python scripts/ci/read_trx.py $runnerResults
python scripts/ci/update_test_baseline.py --shard core --results-directory $runnerResults --measured-at-commit $env:GITHUB_SHA
python -m pytest tests/scripts/test_ci_test_baseline.py tests/scripts/test_golden_pin.py
```

`read_trx.py` emits only counters and parameter-free method outcomes. It accepts
an exact result file or a results directory. Inspect raw evidence only on its
producer or during local reproduction; public artifacts carry summaries and
validated product coverage. Existing verifier session cleanup is unchanged.
