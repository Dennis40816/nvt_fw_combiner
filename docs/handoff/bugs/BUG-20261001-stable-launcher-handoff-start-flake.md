# BUG-20261001-stable-launcher-handoff-start-flake: the stable launcher handoff test returned HandoffFailed once on CI

Status: open; most likely cause identified with medium confidence (below), not proven
Severity: P3 (test flake; no product failure is known)
Found: 2026-10-01, Claude Code commander (Claude Opus 5.5), from pull request #506 run `36830608092` at
`43eb52843` (documents only). Job `dotnet / test (core)`: the test failed on attempt 1 and passed on the in-job
retry, so the decision 193 gate marked it flaky and `dotnet / build-test` failed with "flaky test has no bug record".
Where: `NvtFwCombiner.Infrastructure.Tests.VersionManagement.AnonymousPipeManagedApplicationProcessTests.StableLauncherHandoffRejectsMissingAndStartsExactLauncher`,
`tests/NvtFwCombiner.Infrastructure.Tests/VersionManagement/AnonymousPipeManagedApplicationProcessTests.Bootstrap.cs`
lines 12-37 (assertion at line 36).
Observed: the attempt-1 TRX (artifact `dotnet-test-core-evidence-attempt-1`, downloaded with the owner's approval;
`evidence/1.2.1/flake-36830608092/`, test-area relative) shows `Assert.Equal() Failure` at line 36: expected
`StableLauncherStartResult { Outcome = Started }`, actual `{ Outcome = HandoffFailed }`. The whole test took 18.6 ms
on attempt 1; the retry passed in 3.2 s, of which the probe's deliberate three-second wait is most. 1577 of 1578
Infrastructure tests passed on attempt 1; the other 18 `StableLauncherHandoff*` tests passed.
Expected: after the launcher file is copied into the managed root, the handoff starts the exact launcher.
Investigation (read-only, Codex `gpt-6.1-sol`, `evidence/1.2.1/launcher-flake-sol.md`, at `43eb52843`, whose `src/`
and `tests/` equal `6f2e2cfa2`):
- Most likely, medium confidence: a transient failure to take custody of the freshly copied launcher. The custody
  opens the executable with `ShareRead` only, so any incompatible handle (for example a scanner) is a sharing
  violation; it maps to `Contended`, then `Unavailable`, then `HandoffFailed`, with no retry
  (`src/NvtFwCombiner.VersionManagement.Infrastructure/VersionManagement/WindowsStablePathCustody.Native.cs:50`,
  `:189`; `WindowsStablePathCustody.cs:640`; `StableManagedExecutableLaunchLease.cs:330`; `StableLauncherHandoff.cs:94`).
- Lower confidence: a failure after process creation, which also maps to `HandoffFailed`
  (`StableLauncherHandoff.cs:150`; the test's observer opens the process handle, `...Bootstrap.cs:193`); 18.6 ms does
  not prove that no process was created.
- Ruled out: state left by the first, missing-launcher call (no state file, lease or mutex is used; the missing
  branch closes its ancestor handles, `WindowsStablePathCustody.Native.cs:78`), and concurrent tests: the class is in
  the serial `ReadyProbeProcessSerialGroup` collection (`AnonymousPipeManagedApplicationProcessTests.cs:10`,
  `ReadyProbeProcessCollection.cs:4`), CI runs collections serially, and no other attempt-1 result of the six core
  projects (4,783 results) overlapped the failing test's window. The live-probe helper's environment variables steer
  the child process only.
- Next step: keep the failure stage, the custody issue and the native error in the handoff result instead of folding
  them into `HandoffFailed`, so the next occurrence names its cause. A test-only bounded retry limited to a
  confirmed sharing violation, or a product retry limited to `Contended` in the existing custody owner (each attempt
  fully re-verified, cancellation honored, sharing mode and hash/path checks unchanged), waits for that evidence.
Related: `BUG-20260927-win32-handoff-test-returns-early` (fixed, a different test of the same handoff).
Owner: Claude Code commander until the investigation names a fix owner.
