# Golden pin and assembly

The pin in `../ci/test-baseline.json` identifies every regular file beneath
`testdata/golden` and `docs/references`. Nothing is ignored. Symlinks,
junctions, other reparse points and non-regular entries are errors.

The tree hash is SHA-256 of the concatenation of lines
`<content-sha256> <byte-size> <repository-relative-path>\n`, sorted by the path's
ordinal UTF-8 bytes. Paths use forward slashes. File count and tree identity
describe the assets; they do not authorize firmware support or byte parity.

`golden_pin.py compute` computes this identity. `verify` compares it to the pin.
`write-pin` replaces only the numeric `fileCount` and the `treeSha256` token,
preserving every other byte and the baseline layout. It never binds
`assetsCommit`. That field remains all zeros until the owner supplies an
actual private commit.

`assemble-golden.ps1` defaults to `InTree`, which verifies the old public-tree
layout. `FromDirectory` verifies the source under `nfc/golden-assembly`, copies
the pinned paths, and verifies both source and destination again. `Clone`
uses a partial sparse Git checkout at the exact bound private commit, under
`RUNNER_TEMP`, then applies the same verified copy. Private modes refuse an
unbound commit with `pin is not bound to a private commit`; forks and
Dependabot refuse with `unverified, cannot merge`.

Clone requires `NFC_ASSETS_KEY_FILE` to name the deploy-key file. The runner
must already trust GitHub's SSH host key; interactive SSH and Git prompts and
Git trace output are disabled. Clone output is discarded. Its temporary clone
(including `.git`) and the key are deleted in cleanup. CI must also have an
always-cleanup step for failures before the clone is created.

`export-assets-layout.ps1` verifies the working tree, copies it to
`<Destination>/nfc/golden-assembly/...`, and verifies the export. It does not
delete or move source files. The owner can commit this layout to the private
assets repository and then bind the public pin.

`run-full.ps1` snapshots `git ls-files -z` from the working tree, including
uncommitted edits of tracked files. Newly added helpers must be tracked
(intent-to-add is sufficient); untracked files are not silently included.
Only manifest-hashed Core package prerequisites may be copied separately as
a build feed. Existing SDK/package caches are reused through the child process
environment. The pinned Coverlet adapter is copied into the snapshot's
`.packages` location, with its complete regular-tree hashes checked, because
the canonical resolver requires that boundary. No working-tree file is modified.

It creates the checkout below the existing `NFC_TEST_AREA_ROOT/temp`, sets
`TEMP`, `TMP`, and `TMPDIR` to that child, assembles, and runs exactly
`python scripts/verify.py --ci-dotnet-test-shard <shard>` followed by the shard
checker. Each shard has the requested 45-minute operational limit. This limit
is not a timing-test assertion. Raw child output is captured privately and
deleted. The snapshot is deleted unless `-Keep` is supplied.

Local evidence and summaries remain beneath
`NFC_TEST_AREA_ROOT/outputs/golden-full`. `last-run.json` points to the latest
run using paths relative to the test area, and records each shard's elapsed
time. Raw evidence remains private local evidence; it is not an upload list.

The existing verifier forbids overlap between the test area and repository.
Until the owner approves a narrowly validated snapshot exception, this rule
blocks test execution from these snapshots. Do not bypass it or declare a
second test-area root. A real `FromDirectory` comparison is also blocked while
the public pin is unbound; synthetic policy tests use a bound fixture pin.

## Order and exact commands

```powershell
python scripts/golden/golden_pin.py compute
python scripts/golden/golden_pin.py write-pin
python scripts/golden/golden_pin.py verify
pwsh -NoProfile -File scripts/golden/assemble-golden.ps1 -Mode InTree

# Keep the export outside the public repository.
$export = Join-Path $env:NFC_TEST_AREA_ROOT 'outputs/golden-assets-export'
pwsh -NoProfile -File scripts/golden/export-assets-layout.ps1 -Destination $export

# After the owner binds assetsCommit:
pwsh -NoProfile -File scripts/golden/assemble-golden.ps1 -Mode FromDirectory -Source $export

# CI only, with the key supplied in this step's environment:
pwsh -NoProfile -File scripts/golden/assemble-golden.ps1 -Mode Clone

# Local test area must already exist and be declared.
$declaredTestArea = [Environment]::GetEnvironmentVariable('NFC_TEST_AREA_ROOT', 'User')
if ($declaredTestArea) { $env:NFC_TEST_AREA_ROOT = $declaredTestArea }
$env:TEMP = Join-Path $env:NFC_TEST_AREA_ROOT 'temp'
$env:TMP = $env:TEMP
$env:TMPDIR = $env:TEMP
pwsh -NoProfile -File scripts/golden/run-full.ps1 -Shard all
pwsh -NoProfile -File scripts/golden/run-full.ps1 -Shard all -Source $export
pwsh -NoProfile -File scripts/golden/run-full.ps1 -Shard core -Keep
```

All commands run in the current console. They do not open terminal windows.
Assembly diagnostics contain only fixed sentences, counts, and the first
12 hexadecimal hash characters. They never print asset filenames, case IDs,
content bytes, key contents, clone paths, or private URLs.
Windows extended paths are used for filesystem IO; the hashed relative paths
retain their original forward-slash representation.
