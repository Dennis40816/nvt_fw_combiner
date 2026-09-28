# BUG-20260928-b2-joint-catalog-lifecycle-cleanup-lock: catalog lifecycle cleanup fails

Status: fixed (change-related; Windows timing-dependent test process lifetime)
Severity: P2
Found: 2026-09-28, Codex (codex/gpt-6-astra), during ADR 0077 B2 joint gates,
at feature/1.1.13/b2a-prebuilt-catalog@8e74ece67.
Where: tests/scripts/test_prebuilt_profile_catalog_build.py:50
Observed: test_clean_incremental_and_failed_generation_never_copy_a_stale_pack
reports an error from TemporaryDirectory cleanup: WinError 32 on the copied
NvtFwCombiner.Presentation.Avalonia directory. The retained traceback reports
cleanup failure, not a lifecycle assertion failure. The original process ID
was not retained; the same directory lock was identified in the diagnosis below.
Expected: The real build/publish lifecycle test must release its scratch tree
and finish successfully; successful assertions do not excuse failed cleanup.
Evidence: The initial six-test run passed five tests but the lifecycle test
could not restore the uncached Crossgen2 package. A targeted retry using the
existing local NuGet fallback and BuildInParallel=false reached cleanup and
failed there after 248.564 seconds. The test-area evidence directory
`v1113-b2-joint-8e74ece67` retains `build-lifecycle-local-fallback.log`.
Owner: Codex, B2 implementer, feature/1.1.13/b2a-prebuilt-catalog.
Resolution: Fixed by the commit carrying this update. The B2 lifecycle test's
build helper allowed Avalonia build-time telemetry to outlive `dotnet build`.
The lock holder is `Avalonia.BuildServices.Collector`, not an MSBuild node or
VBCSCompiler. The existing node-reuse/MSBuild-server environment settings do
not disable this collector. Set the package's documented
`AVALONIA_TELEMETRY_OPTOUT=1` only in this helper's child environment. Keep all
assertions, build/publish flags, timeouts and normal TemporaryDirectory cleanup;
do not kill unrelated build servers, retry cleanup or suppress exceptions.

Diagnosis evidence (test-area directory `v1113-b2-blocker-fixes`):
- `lifecycle-red-diagnostic.log`: the first full attempt stopped at uncached
  Crossgen2; the diagnostic scanner also had a process-enumeration exit-code
  error. This is environmental/diagnostic evidence, not a product failure.
- After using the already installed local NuGet fallback, the unchanged test
  passed in 191.054 seconds (`lifecycle-red-handles.log`). A fresh isolated
  `SharedCompilationId` also passed in 216.408 seconds
  (`lifecycle-cold-compiler.log`); a retry alone did not establish a fix.
- `lifecycle-cold-owned-handles.jsonl` records collector PID 53804, parent
  Desktop-build PID 40292, holding the copied Presentation directory.
- `lifecycle-immediate-cleanup.log` narrows the timing: an external diagnostic
  wrapper deliberately exits the test scope immediately after Desktop build
  returns zero. Cleanup reproduces WinError 32 on the same Presentation
  directory. Handle 84 belongs to collector PID 47224, parent build PID 28748;
  the process classification excludes compiler/MSBuild nodes. The deliberate
  early exit is diagnostic instrumentation outside the repository, not a
  changed assertion or a claimed full test run. A separate minimal
  `AvaloniaStats` target probe did not reproduce the lock.

Verification: one post-fix full run of
`python -m unittest discover -s tests/scripts -p test_prebuilt_profile_catalog_build.py
-k test_clean_incremental_and_failed_generation_never_copy_a_stale_pack`
passed 1/1 in 160.802 seconds (`lifecycle-green.log`), including the original
publish and cleanup assertions. The shell used the existing local package
cache/fallback and `BuildInParallel=false`; no download or install occurred.
The same immediate-cleanup diagnostic also passed after the fix
(`lifecycle-immediate-cleanup-green.log`): the intentional scope exit survived
unchanged, with no cleanup exception. Counts for this task: four full lifecycle
invocations (one blocked by cache/diagnostic setup, two unchanged passes, one
post-fix pass), two shortened lifecycle diagnostics (red/green), and one
minimal telemetry-target probe. No original assertion was removed.
Scoped self-check found no firmware byte/range/order/integrity/support change.
This is not independent review; commander review and full joint verification
remain outstanding, including the separate pre-existing Bootstrap failure.
