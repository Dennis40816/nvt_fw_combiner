# BUG-20260928-launcher-admission-deadline-test-flake: the late-admission launcher test can see the admission deadline before the process is created

Status: open
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
