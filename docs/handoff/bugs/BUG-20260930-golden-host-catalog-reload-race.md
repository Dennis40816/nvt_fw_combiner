# BUG-20260930-golden-host-catalog-reload-race: a parallel catalog reload makes a Golden run stale

Status: fixed (release fix #499 merged into `1.2.0` as `75cdad7ba`; released in v1.2.0, `3a73620c8`, 2026-09-30)
Severity: P2 (it blocked the v1.2.0 release: the push-main CI of release merge `31603c145` failed)
Found: 2026-09-30, Claude Code commander (Claude Opus 5.5), `ci` run `36726559665` (push to `main`, `dotnet / test
(core)`, job `109924822956`) on `31603c145`, the merge commit of release pull request #497. The same tree
(`10f09b2de`) had passed the pull request CI of #497 and #498. GoldenRegression is never retried, so the shard failed
and `release` run `36728045427` was skipped.
Where: `NvtFwCombiner.GoldenRegression.Tests.StandardMergeWorkbenchGoldenTests.WorkbenchBuildStandardMergeMatchesGoldenBytes`
(`caseId: "nt51932-gen-flash"`) failed;
`NvtFwCombiner.GoldenRegression.Tests.PrebuiltProfileCatalogAdmissionTests.GoldenHostUsesPrebuiltAdmission` caused it
(`tests/NvtFwCombiner.GoldenRegression.Tests/PrebuiltProfileCatalogAdmissionTests.cs`, `GoldenTestHost.cs`).
Observed: attempt 1 TRX (artifact `dotnet-test-core-evidence-attempt-1`, downloaded with the owner's consent):
`CompositionPreRunRefusalException: capability.readiness.runtime-snapshot-stale: The capability catalog was reloaded
after this run was accepted, so its compilation is no longer published; the run was not started.` The build never
started, so no output bytes were produced or compared. The admission test calls
`GoldenTestHost.Services.CanonicalCatalogLoader.LoadAsync` on the shared static host while xUnit runs the other
Golden test classes in parallel; a reload that lands between a Golden run's acceptance and its execution republishes
the catalog, and the product refuses the stale run as designed.
Expected: a test that mutates the shared host runs in a serialized collection (ADR 0079, "8. Stability rules": a
parallel test never mutates process-wide state outside a serialized collection); Golden runs never see a concurrent
reload. The race exists since the admission test arrived in v1.1.13 (`acab27dac`).
Evidence: before the fix, three local runs of the GoldenRegression project each showed the admission test running at
the same time as five Golden tests (TRX start and end times); after it, in three runs the admission test started
only after every other Golden test had ended, and all 15 tests passed. The new architecture guard failed before the
fix and passes after it (`RepositoryBoundaryTests.CanonicalCapabilityCatalogIsInjectedWithoutAStaticBootstrapLocator`,
Architecture 277/277). The failure was never reproduced locally, since the window is narrow; the CI TRX is the red
evidence.
Owner: Claude Code, feature/1.2.0/golden-host-race.
Resolution: `PrebuiltProfileCatalogAdmissionTests` joins the new `GoldenHostCatalogReloadSerialGroup` collection
(`DisableParallelization = true`), which xUnit runs after all parallel collections. The architecture guard requires
the admission test to join it and forbids the known catalog-mutation calls (`CanonicalCatalogLoader`,
`.Catalog.Reload(`, `WarmCanonicalCapabilities(`, `CreateSystemInformationService(`) in every other GoldenRegression
source; it is a text check, not a proof. No production change. The v1.2.0
release continues with a new release pull request whose push-main CI passes on its first attempt (decision 193,
ADR 0079), as the owner chose.
Verified: the second release pull request #500 merged as `3a73620c8`; its push `ci` run `36738672808` passed on
attempt 1 and `release` run `36740198408` published v1.2.0.
