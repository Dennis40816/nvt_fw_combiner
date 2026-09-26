# BUG-20260925-cli-invariant-exception-uncaught: four CLI build routes can end with an unhandled exception

Status: open
Severity: P3
Found: 2026-09-25, Claude Code (Opus 5.5), while reviewing the Merge/Replace flow (WS-FLOW F10), at `1.1.12`@`d69b6e54a`
Where: `src/NvtFwCombiner.Cli/ReplaceCliCommandHandler.CtrlRam.cs`, `ReplaceCliCommandHandler.General.cs`, `MergeCliCommandHandler.cs`; `src/NvtFwCombiner.Cli/CliApplication.cs:102`
Observed: `CompositionExecutionExperience` throws `InvalidOperationException` for readiness or publication mismatches (`src/NvtFwCombiner.Application/Composition/CompositionExecutionExperience.cs:525,554,575`). The AB handler catches it (`AbMergeCliCommandHandler.cs:287`); these three do not, and `CliApplication.cs:102` catches only I/O, access and argument exceptions.
Expected: a controlled CLI error and exit code, as the AB handler does.
Evidence: code reading first. Each handler checks action readiness first (`ReplaceCliCommandHandler.CtrlRam.cs:124-147`), so the path needs the runtime or publication to change between that check and execution.
Reproduced 2026-09-26 at `cf4e42697` by the tests of the proposed fix: a runtime reload after the CtrlRAM readiness check, and a catalog reload after General Replace or General Merge preparation, each end the process with an unhandled exception. `CliApplication.RunStandardMergeAsync` has the same uncaught call and reproduced the same way, so four routes are affected. Lines 554 and 575 cannot be reached from the CLI, because each handler's own check uses the same readiness object.
Owner: 1.1.13 CLI hardening, as `CLI-EXEC-REFUSAL-1113-01` (R2): the Application classifies these conditions as the existing typed pre-run refusal (ADR 0072), and the CLI routes print the typed issue and exit 1. The proposal is under independent design review; it changes the UI rendering of these conditions from a failed Build with an error report to a blocked Build without one.
Resolution:
