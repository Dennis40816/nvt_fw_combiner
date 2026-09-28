# BUG-20260928-b2-joint-recovery-entry-health-unavailable: Bootstrap recovery entry gate fails

Status: open (pre-existing; reproduced at trunk base)
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
Baseline evidence (2026-09-28): a throwaway detached worktree at `8f5223860`
was restored in locked mode using only installed local packages. The command
`dotnet test tests/NvtFwCombiner.Bootstrap.Tests/NvtFwCombiner.Bootstrap.Tests.csproj
-c Release --no-restore --disable-build-servers -p:BuildInParallel=false
--filter FullyQualifiedName~RecoveryEntryExposesSessionBoundToTheExactEntryRoot`
failed 1/1 with the same expected/actual result (655 ms reported test time).
The test-area evidence directory `v1113-b2-blocker-fixes` retains
`base-restore.log`, `base-recovery.log` and `base-recovery.trx`.
Owner: commander to route the pre-existing failure to the managed launcher/test owner.
Resolution: Classified pre-existing relative to B2; no product or assertion
change, per the owner's base-failure stop rule. One baseline test run; no B2
bisect. The detached baseline worktree was removed after collecting evidence.
The host uses the coordinator's default 250 ms health-observation deadline.
This is plausibly another deadline-sensitive test, but the exact failing
health path is not established. It is not proven identical to
`BUG-20260928-launcher-admission-deadline-test-flake`, whose different test
uses a 25 ms admission deadline and expects `TerminationUnconfirmed` after
process creation. The Bootstrap joint gate remains blocked. Consequently the
optional VERSION=1.1.13 package/smoke run was not run.
