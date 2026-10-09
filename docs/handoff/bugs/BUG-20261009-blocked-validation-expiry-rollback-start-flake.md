# BUG-20261009-blocked-validation-expiry-rollback-start-flake: the start after an expired blocked validation returned StartFailed once on CI

Status: open; cause not proven
Severity: P3 (test flake; no product failure is known)
Found: 2026-10-09, Claude Code NFC session (Claude Sonnet 5.5), from pull request 599 run `37827502134`
at `e4a7be545` (a TP flash-map catalog change that touches no version-management code). Job `dotnet / test (core)`
retried the test and it passed, so the decision 193 gate marked it flaky and `dotnet / build-test` failed with
"flaky test has no bug record".
Where: `NvtFwCombiner.Infrastructure.Tests.VersionManagement.AnonymousPipeManagedApplicationProcessTests.BlockedValidationExpiresBeforeProcessCreationAndCleansUp`,
`tests/NvtFwCombiner.Infrastructure.Tests/VersionManagement/AnonymousPipeManagedApplicationProcessTests.Deadline.cs`
(assertion at line 63).
Observed: on attempt 1 the last assertion failed with `Assert.Equal() Failure`: expected
`ManagedProcessStartOutcome.Ready`, actual `StartFailed`. That assertion checks the second start of the same
probe application, which runs right after the first start expired while its final validation was blocked.
Expected: after the expired start has settled as `ReadyTimeout` and left no late child, the next start of the same
version reaches `Ready`.
Possible cause, not confirmed: the residual windows that `BUG-20260930-managed-start-deadline-residual-windows`
describes (the in-flight admission state in N1 and the lifetime lease in N3). This record does not claim a match.
The attempt-1 result files were not downloaded.
Next step: when the test fails again, keep the attempt-1 result file and compare the failure stage with those windows
before any change to the test or to the product code.
Owner: NFC session until the investigation names a fix owner.
