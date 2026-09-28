# BUG-20260928-launcher-admission-deadline-test-flake: the late-admission launcher test can see the admission deadline before the process is created

Status: Application test fixed locally (2026-09-28); Bootstrap host follow-up remains open
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
Owner: 1.1.14 (test-only, R1); earlier if it fails an integration or release candidate run again.
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

The host's internal `Create` and private constructor expose no `TimeProvider` seam. Making this host test use a
manual clock requires a production composition change, outside this task's test-only scope; no private-field
mutation, timeout increase, retry of the product assertion, or weakened assertion was introduced. Commander
follow-up: authorize a narrow clock-injection seam and retain the exact resource and host-result assertions.
The Bootstrap flake is not resolved and its passing rerun does not clear that blocker. Independent fixed-head
review and the integration full verifier remain commander gates; local self-check is not independent review.
