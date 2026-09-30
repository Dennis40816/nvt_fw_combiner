# BUG-20260929-memory-review-pending-session-error: Session-only pending errors crash diagnostic rendering

Status: fixed
Severity: P1
Found: 2026-09-29, independent review and Codex gpt-6-astra correction,
at feature/1.1.15/memory-layout over 8010d7770.
Where: UiCompositionRunner.Common.cs / ShellTextResources.MemoryLayout.cs
Observed: The linked AB TP toggle reaches Error lifecycle without localized UI inspection text and throws ArgumentException.
Expected: Application pending facts select the blocking state; localization must safely render session-only errors.
Evidence: Nt51950CanonicalLinkedAbTpCanRoundTripThroughIndependentReplaceContext. Test artifacts are under
`evidence/1.1.15/test-results/`; see the correction checkpoint in WS-MEMLAYOUT.
Owner: Codex, feature/1.1.15/memory-layout.
Resolution: Typed pending diagnostics and a localized review-input fallback replace the empty UI text dependency. Included in the correction commit containing this record.
