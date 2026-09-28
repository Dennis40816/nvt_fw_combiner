# BUG-20260927-version-manager-state-test-isolation: test processes can still resolve the real version-manager state

Status: fixed (merged into `1.1.x` by #468, merge `47e01ebab`, 2026-09-28)
Severity: P3
Found: 2026-09-27, Claude Code (Opus 5.5), from the `TEST-LOCAL-STATE-1113-01` design review (`codex/gpt-6-astra`, F-3)
Where: `src/NvtFwCombiner.VersionManagement.Infrastructure/VersionManagement/JsonVersionManagerStateStore.cs` (`GetDefaultPath`,
which prefers the `LOCALAPPDATA` environment value over the platform folder); its default callers
`CompositionHostServices.CreateVersionManagementExperience` and `CreateStableLauncherHandoff` (`statePath: null`),
`ManagedDistributionLauncherHostServices.Create()`, `StableLauncherHandoff`, `AnonymousPipeManagedApplicationProcess`,
`AnonymousPipeManagedLauncherProcess` and the Launcher `Program`; `src/NvtFwCombiner.Cli/CliApplication.cs` (`version-self-test`)
Observed: `TEST-LOCAL-STATE-1113-01` isolates only preferences, report history, toolchain runtime and Event Buffer format.
The version-manager state `%LOCALAPPDATA%\NvtFwCombiner\version-manager.v1.json`, its writer lock and its atomic-write scratch keep
their own resolver, which the `NvtFwCombiner.LocalState.CurrentUserFolderForbidden` switch does not guard. The CLI
`version-self-test` command builds its version-management service with `statePath: null` before any injected local-state
directory is used; `CliVersionSelfTestTests.VersionSelfTestUsesProductionRegistryAdapter` returns early on an unsafe Registry
locator, so no state is read today, but a test with a valid Registry would read the real anti-rollback state.
`LauncherBootstrapLaunchOptionsTests.CanonicalDefaultUsesExactLocalApplicationDataEnvironment` changes the process-wide
`LOCALAPPDATA` value; this is safe today only because the verifier runs Infrastructure serially
(`INFRASTRUCTURE_VSTEST_SETTINGS`), and ADR 0079 item T5 plans to lift that for non-process classes. The real folder holds
`.version-manager.v1.json.9dcba0f0990618c44b4d29b4.writer.lock` dated 2026-09-09; its producer is unknown.
Expected: a test process cannot resolve the real version-manager state by default, the existing production precedence
(`LOCALAPPDATA`, then the platform folder) is unchanged, and tests that exercise the default resolution do so without a
process-wide environment change visible to parallel tests.
Evidence: source reading at `9861d800c`; the `TEST-LOCAL-STATE-1113-01` review (F-3 and its version-manager note); the real
folder's file names and times only.
Owner: 1.1.13 or later, per the roadmap; a separate record (R2 if it adds a production guard branch, as for the four files).
Resolution:
