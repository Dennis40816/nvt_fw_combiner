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
layout. `FromDirectory` verifies the source under `nfc`, copies
the pinned paths, and verifies both source and destination again. `Clone`
uses a partial sparse Git checkout at the exact bound private commit, under
`RUNNER_TEMP`, then applies the same verified copy. Private modes refuse an
unbound commit with `pin is not bound to a private commit`. Private modes run
only for a local run (no event name) and for the events `push`, `workflow_dispatch`,
`schedule`, `merge_group` and a same-repository `pull_request`. Every other event,
a fork and Dependabot refuse with `unverified, cannot merge`.

Clone requires `NFC_ASSETS_KEY_FILE` to name the deploy-key file. The runner
must already trust GitHub's SSH host key; interactive SSH and Git prompts and
Git trace output are disabled. Clone output is discarded. Its temporary clone
(including `.git`) and the key are deleted in cleanup. CI must also have an
always-cleanup step for failures before the clone is created.

`export-assets-layout.ps1` verifies the working tree, copies it to
`<Destination>/nfc/...`, and verifies the export. It does not
delete or move source files. The owner can commit this layout to the private
test-data repository (Dennis40816/nvt-testdata, folder 
fc) and then bind the
public pin.

`run-full.ps1` snapshots `git ls-files -z` from the working tree, including
uncommitted edits of tracked files. Newly added helpers must be tracked
(intent-to-add is sufficient); untracked files are not silently included.
Submodule pointers are skipped. Only manifest-hashed Core package prerequisites
may be copied separately as a build feed. Existing SDK/package caches are reused
through the child process environment, so a restore can write to the shared
package cache; no other working-tree file is modified. The pinned Coverlet
adapter is copied into the snapshot's `.packages` location, with its complete
regular-tree hashes checked, because the canonical resolver requires that
boundary. When the working tree has no `.dotnet` folder, `NFC_DOTNET_ROOT` names
a folder that holds the exact SDK of `global.json`.

The verifier forbids a repository that overlaps the test area. So the snapshot
lives in the sibling folder `NvtFwCombiner-GoldenRun`, next to the folder that
`NFC_TEST_AREA_ROOT` names, in a run folder `golden-full-<8 hex>`. The run sets
`TEMP`, `TMP`, and `TMPDIR` to the `temp` child of the test area, assembles, and
runs exactly `python scripts/verify.py --ci-dotnet-test-shard <shard>` followed
by the shard checker. Each shard has the requested 45-minute operational limit.
This limit is not a timing-test assertion. The raw child output goes to a log in
the run folder, and the run deletes that log. With `-Keep` and a failed shard,
a copy of the log goes to the retained evidence folder first. The whole run folder, which holds the golden files, is deleted unless `-Keep` is
supplied. A run that ends by an unexpected process kill can leave the folder
behind; delete it by hand.

Local evidence and summaries remain beneath
`NFC_TEST_AREA_ROOT/outputs/golden-full/<run id>`. The evidence copy holds the
raw upload folder of each shard (TRX, discovery list, coverage), so it can
contain test names, theory arguments and `ErrorInfo`. It is private local
evidence and is never an upload list. `last-run.json` points to the latest run
using paths relative to the test area and records each shard's elapsed time and
a `dirty` flag. The flag is true when a tracked file differs from `HEAD`; the
shard summaries still name `HEAD` as the commit.
A real `FromDirectory` run needs a bound `assetsCommit`; synthetic policy tests
use a bound fixture pin.

The pin covers regular files only. An added empty directory or a file mode change
is not part of the tree identity.

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
