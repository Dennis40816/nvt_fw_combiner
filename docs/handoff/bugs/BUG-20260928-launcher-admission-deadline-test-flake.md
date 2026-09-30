# BUG-20260928-launcher-admission-deadline-test-flake: the late-admission launcher test can see the admission deadline before the process is created

Status: fixed; the Bootstrap host follow-up (`a5b7019f5`) merged into `1.1.x` through #488 (merge `aba286bae`)
Severity: P3
Found: 2026-09-28, CI run 36373462006 (`dotnet / test (core)`) on pull request #474 head `46d73bf91`
Where: `tests/NvtFwCombiner.Application.Tests/VersionManagement/ManagedLauncherEntryCoordinatorTests.cs`,
`BootstrapAdmissionAfterLocalDeadlineFailsClosedBeforeReadyWait`
Observed: `Assert.Equal() Failure: Expected: TerminationUnconfirmed, Actual: HealthUnavailable` at line 365. The test
uses a real 25 ms admission deadline; on a loaded runner the deadline can expire before the deferred handoff reports
that the process was created, so the coordinator correctly reports the pre-creation outcome instead of the
post-creation one. Pull request #474 changes only `scripts/verify.py`, its script tests and two documents, and does not
touch the launcher coordinator or this test.
Expected: the test controls the order of process creation and the deadline (for example with the manual time provider
the neighbouring tests use), so it checks the post-creation outcome without depending on runner speed.
Evidence: the TRX in the run's `dotnet-test-core-evidence-attempt-1` artifact.
Owner: 1.1.14 (test-only, R1) for the Application fix, delivered; the Bootstrap host follow-up is a 1.1.15 test-only R1 correction on `feature/1.1.15/bootstrap-flake`, with a later merge up from 1.1.x to 1.2.x.
Resolution:
The Application test now injects the existing `ManualTimeProvider`, waits for the launch receipt's admission wait
to start, and only then advances the 25 ms cutoff. Both immediate and 100 ms delayed payload admission exercise
the same post-creation outcome. All original product assertions remain, with an added admission-start assertion.
No production behavior, firmware bytes, ranges, integrity, ordering, or support declarations changed.

Red evidence: the delayed theory, before injecting the manual clock, failed with expected
`TerminationUnconfirmed` / actual `HealthUnavailable` (1 failed, 1 passed in each of two diagnostic runs).
Green evidence: `dotnet test tests/NvtFwCombiner.Application.Tests/NvtFwCombiner.Application.Tests.csproj
--no-restore --filter FullyQualifiedName~ManagedLauncherEntryCoordinatorTests` passed 58/58, then passed 58/58
again with `--no-build` (including both delay rows).

Related Bootstrap investigation: `ManagedDistributionLauncherHostServicesTests.ClosedResourcesAreBoundedAndVersionBoundBeforeEntryRouting`
uses the production host's default real 250 ms health cutoff. `ManagedLauncherEntryCoordinator.RunAsync` starts
that timer before `AwaitIsolatedReadOnlyObservationAsync` schedules the resource observation with `Task.Run`.
Expiry before that work starts explains the CI run 36403132737 empty resource list: entry returns
`HealthUnavailable` instead of a payload failure. This is the same class of wall-clock/scheduling race, but a
different cutoff from the Application test. Locally the unchanged Bootstrap class failed the descriptor-only
row once (17 passed, 1 failed), then passed 18/18; the quiet first run did not retain its assertion details,
so that local failure alone does not prove the precise failing stage.

Bootstrap Resolution (2026-09-29): the host's internal `Create` accepts an optional `TimeProvider` and passes it
through the private constructor to the existing Application coordinator owner. The production factory uses
`TimeProvider.System`. The Bootstrap resource-shape test links the existing `ManualTimeProvider`, waits until the
resource callback is observed, then advances its clock by 1 ms. Its original resource-order and complete host-result
assertions remain. The 250 ms health cutoff is unchanged; no private-field mutation or assertion retry was added.

Red evidence: before the host seam, the new test failed compilation with CS1501 because `Create` had no six-argument
overload. Green evidence: `ClosedResourcesAreBoundedAndVersionBoundBeforeEntryRouting` passed all five rows in each
of three narrow runs (5/5 each); the whole `ManagedDistributionLauncherHostServicesTests` class passed 18/18.
These local results resolve the known scheduling race in this test, while CI confirmation, independent fixed-head
review, and the integration full verifier remain commander gates. Local self-check is not independent review.

Both fixes merged into `1.1.x` by the 1.1.14 integration `6703e2517` (#480, reviewed head `fdf68a903`) and reached
`main` in the v1.1.14 release merge `32808e943` (#483, published 2026-09-29): commit `0b20f5bd7` ("Inject clock
into distribution launcher host tests") is the Bootstrap host fix above and is an ancestor of both.

Reopened (2026-09-29): `0b20f5bd7` injected the existing `ManualTimeProvider` into only
`ClosedResourcesAreBoundedAndVersionBoundBeforeEntryRouting`; the other tests in
`ManagedDistributionLauncherHostServicesTests` still construct the host with the real
`TimeProvider.System` (`src/NvtFwCombiner.Bootstrap/ManagedDistributionLauncherHostServices.cs:278`), so the same
class of wall-clock race remains for them. `RecoveryEntryWithoutRootDoesNotExposeRecoverySession` failed in the
`feature/1.1.14/release-back-merge` (#484) CI run `36513730094` (job `109231324480`) and in about 1 of 8 local
runs of the class under load; it passes in isolation, consistent with a timing-sensitive test rather than a
production regression. The Application-side fix (this bug's original scope) is unaffected and stays fixed;
only the Bootstrap host class is reopened. The 1.1.15 test-only correction on
`feature/1.1.15/bootstrap-flake` injects the manual clock into the remaining time-dependent tests in this class
the same way `0b20f5bd7` did for one of them; 1.1.x will later merge up to 1.2.x. No
production behavior, firmware bytes, ranges, integrity, ordering, or support declarations are implicated.

Bootstrap follow-up Resolution (2026-09-29, 1.1.15): all four remaining `RunAsync` tests now inject the
existing `ManualTimeProvider`: `MissingDevelopmentPayloadFailsClosedBeforeEntryRouting`,
`GenuineFirstInstallExposesOnlyTheSharedSetupExperience`,
`RecoveryEntryExposesSessionBoundToTheExactEntryRoot`, and
`RecoveryEntryWithoutRootDoesNotExposeRecoverySession`. Each starts the host run, waits until the embedded-resource
callback is observed, and then advances the clock by 1 ms. Their original product assertions remain. The earlier
`ClosedResourcesAreBoundedAndVersionBoundBeforeEntryRouting` manual-clock coverage remains unchanged. The other
tests do not run the host entry coordinator: they exercise recovery-session delegation/cancellation, factory
configuration, or pure exit-code mapping, so they need no clock injection. No production behavior or cutoff changed.

Red attempt on the unmodified `09d2a6ca2` base: the
`dotnet test tests/NvtFwCombiner.Bootstrap.Tests/NvtFwCombiner.Bootstrap.Tests.csproj --no-restore
--filter FullyQualifiedName~ManagedDistributionLauncherHostServicesTests --verbosity normal` class run passed
18/18 on each of 30 consecutive invocations (`--no-build` added after the first). No local failure was reproduced
within 30 runs; the earlier CI run `36513730094` and about 1/8 local failures under load remain the observed red
evidence, not a new red result from this attempt.

Green on the corrected tests: the same class filter with `--no-restore --no-build --verbosity normal` passed
18/18 on each of 30 consecutive invocations. The full project command
`dotnet test tests/NvtFwCombiner.Bootstrap.Tests/NvtFwCombiner.Bootstrap.Tests.csproj --no-restore
--no-build --verbosity quiet` passed 2142/2142 once. These are local test results; fixed-head review, CI, and merge
remain pending.
