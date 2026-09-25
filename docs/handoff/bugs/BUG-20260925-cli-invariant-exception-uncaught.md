# BUG-20260925-cli-invariant-exception-uncaught: three CLI handlers can end with an unhandled exception

Status: suspected
Severity: P3
Found: 2026-09-25, Claude Code (Opus 5.5), while reviewing the Merge/Replace flow (WS-FLOW F10), at `1.1.12`@`d69b6e54a`
Where: `src/NvtFwCombiner.Cli/ReplaceCliCommandHandler.CtrlRam.cs`, `ReplaceCliCommandHandler.General.cs`, `MergeCliCommandHandler.cs`; `src/NvtFwCombiner.Cli/CliApplication.cs:102`
Observed: `CompositionExecutionExperience` throws `InvalidOperationException` for readiness or publication mismatches (`src/NvtFwCombiner.Application/Composition/CompositionExecutionExperience.cs:525,554,575`). The AB handler catches it (`AbMergeCliCommandHandler.cs:287`); these three do not, and `CliApplication.cs:102` catches only I/O, access and argument exceptions.
Expected: a controlled CLI error and exit code, as the AB handler does.
Evidence: code reading only; not reproduced. Each handler checks action readiness first (`ReplaceCliCommandHandler.CtrlRam.cs:124-147`), so the path needs the runtime or publication to change between that check and execution.
Owner: unassigned
Resolution:
