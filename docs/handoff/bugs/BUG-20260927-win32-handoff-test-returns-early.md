# BUG-20260927-win32-handoff-test-returns-early: the Win32 start-failure test for the stable launcher handoff never reaches the start

Status: open
Severity: P3
Found: 2026-09-27, Codex (gpt-6-astra) fixed-head review of W6-C (VERSION-MANAGER-STATE-1113-01) in batch 3
Where: `tests/NvtFwCombiner.Infrastructure.Tests/VersionManagement/AnonymousPipeManagedApplicationProcessTests.Bootstrap.cs`,
test `StableLauncherHandoffConvertsWin32StartFailureToFalse`
Observed: the test constructs `StableLauncherHandoff` without `expectedIdentity`, so `TryStartLauncherAsync` returns
`false` at the identity check before any process start. Removing the `Win32Exception` catch in `StableLauncherHandoff`
would not make the test fail. Present before batch 3 (trunk `50c0998e3`); W6-C did not introduce or widen it.
Expected: the test uses a valid probe and its correct identity, throws `Win32Exception` from the existing
`beforeProcessStart` hook, and asserts that the hook ran and the result is `false`.
Evidence: the W6-C review of batch 3 at `634671a90`.
Owner: 1.1.13 follow-up (test-only, R1).
Resolution:
