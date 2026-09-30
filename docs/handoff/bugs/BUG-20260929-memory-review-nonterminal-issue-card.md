# BUG-20260929-memory-review-nonterminal-issue-card: Retained readiness may enter terminal issue-card formatting

Status: fixed
Severity: P1
Found: 2026-09-29, Codex gpt-6-astra author review of the correction over 8010d7770.
Where: FirmwareSlotViewModel.SemanticState.cs, IssueCard
Observed: Newly retained non-terminal blocked status can reach the terminal
inspection formatter, which throws ArgumentException for non-terminal input.
Expected: Preserve the blocked prerequisite's existing error card and detail,
while retaining the original typed status for pending projection.
Evidence: review-nonterminal-red.trx reproduces the exception in English and
Traditional Chinese (0 passed, 2 failed); both pass in review-followup-green.trx.
Owner: Codex, feature/1.1.15/memory-layout.
Resolution: Use terminal formatting only for terminal status. Non-terminal
blocked readiness retains the existing error card and prerequisite detail;
the typed status remains available to the Application pending projector.
