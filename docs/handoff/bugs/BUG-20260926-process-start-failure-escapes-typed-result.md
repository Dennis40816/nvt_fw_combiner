# BUG-20260926-process-start-failure-escapes-typed-result: an OS start failure of an approved tool escapes every typed failure path

Status: suspected
Severity: P3
Found: 2026-09-26, Claude Code (Opus 5.5), while designing 1.1.13 wave 5 (process cancellation and window lifetime), at `feature/1.1.13/process-lifetime`@`557a9ee6a`
Where: `src/NvtFwCombiner.Infrastructure/ExternalTools/SystemExternalProcessRunner.cs:17` (`ProcessLaunchGate.Start`); catch filters at `ExternalCombinerProcessor.cs:182-191`, `LegacyCombinerPostbuildProcessor.cs:226-236`, `src/NvtFwCombiner.Presentation.Avalonia/ViewModels/CompositionRunPresentationViewModel.cs:197-241`, `src/NvtFwCombiner.Cli/CliApplication.cs:97-106`
Observed: `Process.Start` reports an executable that cannot be started (for example removed or blocked between the manifest SHA-256 check and the launch) as `Win32Exception`. The staged processors catch only `OperationCanceledException`, `IOException` and `UnauthorizedAccessException`; `CompositionRunService` adds no catch; the UI run session catches `InvalidOperationException`, `IOException`, `UnauthorizedAccessException` and `ArgumentException`; the CLI catches `IOException`, `UnauthorizedAccessException` and `ArgumentException`. A `Win32Exception` therefore reaches the UI command or the CLI `Main` unhandled.
Expected: a fail-closed typed issue (for example an `external-tool.process.*` start failure) and a failed Build/Preview or CLI exit 1, per `docs/architecture/external-combiner-tool-runner.md` ("All external combiner errors fail closed") and ADR 0006 item 13.
Evidence: code reading only; not reproduced. The window between hash verification and launch is short, so ordinary use is unlikely to hit it. Found next to F03, whose reproduced `AggregateException` escaped through the same filters.
Owner: unassigned (candidate: a separate R1 on the same runner owner after `PROCESS-CLEANUP-1113-01`; see `docs/handoff/1.1.13/DESIGN-process-lifetime.md`)
Resolution:
