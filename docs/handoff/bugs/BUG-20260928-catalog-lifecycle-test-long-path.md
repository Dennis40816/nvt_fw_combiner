# BUG-20260928-catalog-lifecycle-test-long-path: the catalog lifecycle build test exceeds MAX_PATH inside a local verifier session

Status: open
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
Owner: 1.1.14 (test and verifier, R1-R2). The same run's dotnet coverage lane failure (a 263-character
`catalog-evidence-host` copy) was fixed in 1.1.13 by shortening that folder to `ceh`.
Resolution:
