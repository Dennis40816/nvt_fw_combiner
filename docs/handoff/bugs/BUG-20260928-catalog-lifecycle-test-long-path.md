# BUG-20260928-catalog-lifecycle-test-long-path: the catalog lifecycle build test exceeds MAX_PATH inside a local verifier session

Status: fixing
Severity: P3
Found: 2026-09-28, `python scripts/verify.py --all` at integration branch B head `bd8c9e062` (lane `test_prebuilt_profile_catalog_build`)
Where: `tests/scripts/test_prebuilt_profile_catalog_build.py`,
`CatalogOutputLifecycleTests.test_clean_incremental_and_failed_generation_never_copy_a_stale_pack`
Observed: the test copies the repository into a temporary directory under the verifier session
(`<NFC_TEST_AREA_ROOT>\sessions\s-<26 characters>\t\tmp<8>\`) and builds `NvtFwCombiner.Bootstrap`.
The build's materialized profile copy (`obj\Debug\net10.0\materialized-profiles\built-in\<bundle>\profiles\<file>`)
reaches 264 characters, and MSBuild fails with MSB3030 on a machine where `LongPathsEnabled` is 0.
TemporaryDirectory cleanup then fails with WinError 145. The same test passed on the B2 branch when its temporary
directory was the shorter `<NFC_TEST_AREA_ROOT>\temp`. The verifier's 26-character session token is a custody design,
and the materialized path is the product build layout, so neither was shortened in 1.1.13.
Expected: the test builds inside a root short enough for machines without long paths (for example a verifier-owned
short scratch root), or the verifier documents LongPathsEnabled as a prerequisite, without weakening the test.
Evidence: the integration branch B verify log; the B2 joint run at `v1113-b2-joint-8e74ece67` and the blocker fixes
at `v1113-b2-blocker-fixes` (lifecycle test green under the shorter temp root).
Owner: 1.1.14 (test and verifier, R1-R2). The same run's dotnet coverage lane failures (263-character `catalog-evidence-host`
and 260-character `catalog-probe-host` copies) were fixed in 1.1.13 by shortening those folders to `ceh` and `cph`.
Root fix for 1.1.14 (owner discussion, 2026-09-28): shorten the paths we own rather than require LongPathsEnabled:
a short build-internal name for `materialized-profiles\built-in`, a shorter verifier scratch layout that keeps the
26-character random session token, and a package check that bounds the longest relative path so a deep
extraction folder stays under MAX_PATH for users. Bundle IDs and profile file names are product contract and stay.
Resolution: local root fix `e7925014c`; package guard and evidence accompany this
change. Independent review and commander integration remain pending.

## 1.1.14 local implementation admission (2026-09-28)

Base: `8ee916a3b`, branch `feature/1.1.14/long-path-root`; implementer: Codex.
Scope: build-internal materialization root, its test consumers, two temporary
workspace names, and package relative-path validation. Firmware identifiers,
bytes, ranges, ordering, integrity, support and packaged profile layout stay unchanged.
Owner search: repository search for `materialized-profiles` and
`_NfcExpectedMaterializedRoot` identifies Bootstrap's project property as the
root owner (`extend-owner`); both MSBuild targets consume that property (`reuse`).
`package.ps1` consumes published `profiles/built-in`, not the obj root (`reuse`);
its final closed inventory owns package admission (`extend-owner`).
`TempWorkspace` owns C# workspace creation and cleanup (`extend-owner`);
Python uses `TemporaryDirectory` (`reuse`). No parallel semantic owner is added.
The package script/contract are R3 / `release-owner`; script tests have an R2
path floor, and Bootstrap/C# test changes are R1.
Local gates: affected Bootstrap and script tests, session-root reproduction,
package policy boundary tests, structure-only, and scoped Polytail.
Independent exact-head review, release-owner approval and integration/full-suite
verification remain with the commander; self-checks do not satisfy them.
No retired capability-reuse record is created or changed.

### Design and path budget

`_NfcExpectedMaterializedRoot` now ends in `mp` (28 characters saved).
Configuration/TFM/RID isolation and all catalog invalidation checks stay intact;
materializer and catalog targets already consume the property and need no edit.
Python's lifecycle workspace uses `TemporaryDirectory(prefix="")` (8 characters).
C# uses `TempWorkspace.CreateShort()`, which atomically reserves a directory with
`Directory.CreateTempSubdirectory()` (12 characters on Windows), retaining the
existing disposal/retry behavior. `scripts/verify.py` is unchanged: the full
26-character token, scratch layout, custody and isolation checks are retained.

Lengths below use the configured test-area root (25 characters),
`sessions/s-<26 characters>/t`, and the longest selected profile suffix:
`nt51919-nt51929-nt51932-general-merge-logical-candidate/profiles/nt51932-general-merge-logical-candidate.json`.
Lengths exclude the terminating NUL.

| Path | Before | After |
| --- | ---: | ---: |
| Python Bootstrap Debug materialized file (reported failure) | 264 | 233 |
| Python Bootstrap Release/win-x64 materialized file | 274 | 243 |
| Python Bootstrap Release/win-x64 published build output (longest profile copy) | 261 | 258 |
| C# Bootstrap Debug materialized file (reported failure) | 288 | 237 |
| C# CatalogProbe Debug build output (longest profile copy) | 280 | 257 |

The packaged profile layout, bundle IDs and filenames remain unchanged.
The final closed package inventory now rejects relative paths over 216 UTF-16
code units before ZIP creation. The current longest path is 207 (the NT51951
cascade-2 initial-code reference), with nine units of margin. Thus an absolute
package root of at most 42 units yields at most 259-unit full file paths;
the archive's top-level version directory counts toward the 42. Existing
reference names prevent a broader deep-extraction guarantee without a separate
owner decision. The package contract documents this limit.

### Local evidence

Windows `LongPathsEnabled=0`; SDK 10.0.303 (installed, no downloads).
Every test shell loaded user-level `NFC_TEST_AREA_ROOT` and set `TEMP`, `TMP`,
`TMPDIR` to its existing `temp` child. Catalog script and Bootstrap processes
then ran inside the real `verify.verification_test_session(internal_lane=False)`
context, which supplied the full session-shaped TEMP and retained custody through
completion/cleanup. The verifier implementation was not patched or mocked.

- Red: the original lifecycle test on the base source failed with MSB3030 at
  264 characters, followed by TemporaryDirectory WinError 145 (92.632 seconds).
- Green: `python -m unittest discover -s tests/scripts -p
  test_prebuilt_profile_catalog_build.py -v`: 6 passed in 345.551 seconds,
  including lifecycle, invalidation, clean/incremental builds and actual publish.
- Package boundary red: `test_package_path_budget_accepts_boundary_and_rejects_overflow`
  failed against the original package script (missing path-budget gate).
  Green: production function accepted 216, rejected 217, and counted a surrogate
  pair as two UTF-16 units. No application binaries or release ZIP were built
  for this boundary check.
- Offline `dotnet restore --no-dependencies --locked-mode --source <local-cache>
  -p:NuGetAudit=false` passed for the Bootstrap test dependency closure, retaining
  each lock's RID. The first attempt used the repository's empty cache and hit
  NU1100/source mapping; explicitly selecting the existing user package cache
  with `NUGET_PACKAGES` resolved it. No lock-file changes or downloads occurred.
- `python scripts/verify.py --structure-only`: PASS (59.4 seconds).
- `python scripts/polytail_check.py`: PASS. Scoped self-check found no changed
  firmware semantics, duplicate ownership, identifier/layout drift, or weakened
  custody/allowlist checks. This is not an independent review.

Local logs are under `<NFC_TEST_AREA_ROOT>/temp/longpath-{red,green-scripts,bootstrap,package-tests,structure}.log`.
No `verify.py --all`, GitHub operation, push, merge, rebase or installation ran.

- `dotnet test tests/NvtFwCombiner.Bootstrap.Tests/NvtFwCombiner.Bootstrap.Tests.csproj
  --no-restore -m:1 -p:UseSharedCompilation=false --logger "console;verbosity=normal"`:
  2,142 passed (7.5363-minute test run), including
  `AcceptedCatalogNormalizationFailureStaysPrebuiltAndMatchesJsonFailure`
  (2 minutes 45 seconds) inside the custody session. Parent exited 0 after cleanup.
- `python -m unittest discover -s tests/scripts -p test_release_package_policy.py -v`:
  80 passed in 615.571 seconds, including the production package dry-run and
  new path-budget boundary checks. Parent exited 0.

The tested build/test source is committed as `e7925014c`; package source was
frozen during those runs and committed with this evidence. Only evidence prose
changed afterward. Local gates passed; integration is **not** declared ready:
commander still owns the independent exact-head review, required CI/full verifier,
and `release-owner` approval. No firmware-owner claim or Golden promotion is made.
