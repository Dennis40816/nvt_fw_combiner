# BUG-20260929-memory-review-pending-blocked-facts: Pending display loses inspection lifecycle failures

Status: fixed
Severity: P1
Found: 2026-09-29, independent review and Codex gpt-6-astra correction,
at feature/1.1.15/memory-layout over 8010d7770.
Where: FirmwareInspectionSession.cs / WorkflowSessionPresentationViewModel.FirmwareInspection.cs
Observed: Stale content, rejected publication, failed inspection and non-terminal blocked readiness are absent from pending inputs; DP attention degrades to Waiting for TP BIN.
Expected: C-06 preserves original typed readiness, availability failures and composition issues.
Evidence: OutputFileNameRefreshDoesNotRetainRejectedProjection. Test artifacts are under
`evidence/1.1.15/test-results/`; see the correction checkpoint in WS-MEMLAYOUT.
Owner: Codex, feature/1.1.15/memory-layout.
Resolution: The original typed status and issues are retained; explicit availability reasons are passed from every inspection failure producer. First correction exposed the additional finally/rejected-publication paths; second correction passes. Included in the correction commit containing this record.
