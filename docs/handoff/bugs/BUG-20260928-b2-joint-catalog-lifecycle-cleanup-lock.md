# BUG-20260928-b2-joint-catalog-lifecycle-cleanup-lock: catalog lifecycle cleanup fails

Status: open
Severity: P2
Found: 2026-09-28, Codex (codex/gpt-6-astra), during ADR 0077 B2 joint gates,
at feature/1.1.13/b2a-prebuilt-catalog@8e74ece67.
Where: tests/scripts/test_prebuilt_profile_catalog_build.py:50
Observed: test_clean_incremental_and_failed_generation_never_copy_a_stale_pack
reports an error from TemporaryDirectory cleanup: WinError 32 on the copied
NvtFwCombiner.Presentation.Avalonia directory. The retained traceback reports
cleanup failure, not a lifecycle assertion failure. The locking process has
not been identified.
Expected: The real build/publish lifecycle test must release its scratch tree
and finish successfully; successful assertions do not excuse failed cleanup.
Evidence: The initial six-test run passed five tests but the lifecycle test
could not restore the uncached Crossgen2 package. A targeted retry using the
existing local NuGet fallback and BuildInParallel=false reached cleanup and
failed there after 248.564 seconds. The test-area evidence directory
`v1113-b2-joint-8e74ece67` retains `build-lifecycle-local-fallback.log`.
Owner: unassigned; commander to route to the build/test lifecycle owner.
Resolution: Pending. No timeout, cleanup exception, or test assertion was
suppressed. This remains a failing joint gate, not a passing publish test.
