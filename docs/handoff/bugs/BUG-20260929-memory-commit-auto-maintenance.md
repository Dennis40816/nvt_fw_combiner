# BUG-20260929-memory-commit-auto-maintenance: Local commit triggers Git automatic packing

Status: resolved for subsequent commands; completed packing cannot be undone
Severity: P2
Found: 2026-09-29, Codex gpt-6-astra, first stage-3 commit invocation.
Where: local Git commit process
Observed: Git printed "Auto packing the repository for optimum performance"
before completing the commit. The task prohibits prune/gc; no explicit such
command was issued, but automatic housekeeping was not disabled on this call.
Expected: local authorized commits must also suppress automatic housekeeping.
Evidence: command output for the initial stage-3 commit, 40966aa32.
Owner: Codex, feature/1.1.15/memory-layout.
Resolution: subsequent Git writes use command-local `gc.auto=0` and
`maintenance.auto=false`; persistent repository configuration is unchanged.
Record this execution deviation in the checkpoint and owner report. No claim
that the automatic packing did not occur.
