# BUG-20260928-b2-joint-recovery-entry-health-unavailable: Bootstrap recovery entry gate fails

Status: open
Severity: P2
Found: 2026-09-28, Codex (codex/gpt-6-astra), while running ADR 0077 B2 joint gates,
at feature/1.1.13/b2a-prebuilt-catalog@8e74ece67.
Where: tests/NvtFwCombiner.Bootstrap.Tests/ManagedDistributionLauncherHostServicesTests.cs:95
Observed: RecoveryEntryExposesSessionBoundToTheExactEntryRoot returns
HealthUnavailable instead of RecoveryRequired. The complete Bootstrap run had
2,083 passing tests and this one failure; the isolated test also failed.
Expected: The existing test contract requires RecoveryRequired and a recovery
session bound to the entry's exact managed root.
Evidence: Release `dotnet test` with `--no-restore` failed; selecting the exact
test with `--no-build --no-restore --filter` reproduced the same assertion.
Logs and TRX are retained under the test-area evidence directory
`v1113-b2-joint-8e74ece67` (`Bootstrap` and `Bootstrap-isolated-recovery`).
No baseline execution establishes whether this is pre-existing or environmental.
Owner: unassigned; commander to route to the managed launcher/test owner.
Resolution: Pending. This blocks the joint Bootstrap gate. The assembly task
does not authorize changing launcher behavior or weakening the test.
