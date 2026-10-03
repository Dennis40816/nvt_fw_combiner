# BUG-20261003-application-bootstrap-start-deadline-flake: the Bootstrap start deadline test failed once on a pull-request CI run

Status: suspected
Severity: P3
Found: 2026-10-03, Claude Code commander (Claude Sonnet 5.5), from the pull-request CI run named under Observed; no local
reproduction was attempted.
Where: `NvtFwCombiner.Application.Tests.VersionManagement.ManagedFirstInstallationBootstrapOutcomeTests.InstallBootstrapStartHasInternalDeadline`,
`tests/NvtFwCombiner.Application.Tests/VersionManagement/ManagedFirstInstallationBootstrapOutcomeTests.cs`, the assertion at
line 205 (`Assert.Equal(1, handoff.StartCount)`). The test was moved there unchanged from the class
`ManagedFirstInstallationExperienceTests` by the R33-05 split in the pull request that reported it; it builds the experience
with a 25 millisecond internal Bootstrap start deadline.
Observed: on the pull-request CI run `37122400346` for the R33-05 splits pull request (a tests-only change that moves this
test between files and does not alter its body), job `dotnet / build-test` (`111202515988`) reported the test failed on
attempt 1 with `Assert.Equal() Failure: Values differ`, `Expected: 1`, `Actual: 0` at line 205, and passed on the in-job
retry; the job failed with "flaky test has no bug record" under the decision 193 gate. A passing retry does not establish
a fix.
Expected: when the Bootstrap start does not return within the internal deadline, the experience fails closed after promotion
with the `TimedOut` launch failure and the blocking handoff has been started exactly once, whatever the runner timing.
Evidence: the run, job, attempt outcome, message and failing line come from the job log of
https://github.com/Dennis40816/nvt_fw_combiner/actions/runs/37122400346/job/111202515988, read by the commander on
2026-10-03 (the log text is not stored in Git and GitHub keeps it for a limited time); the root cause is not established
and no hypothesis is claimed. Not investigated: whether the 25 millisecond deadline can expire before the handoff's start is
observed on a loaded runner, or whether the start count is read before the start call has begun.
Owner: unassigned
Resolution: pending investigation and a merged fix; keep this record open until a fix merges. No code, test or CI fix is
included in this record.
