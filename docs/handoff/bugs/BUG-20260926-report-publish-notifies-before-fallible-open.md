# BUG-20260926-report-publish-notifies-before-fallible-open: report publication notifies observers before a fallible step

Status: open
Severity: P2
Found: 2026-09-26, automated review of pull request #457 (thread on
`src/NvtFwCombiner.Presentation.Avalonia/ViewModels/ReportPresentationViewModel.cs`)
Where: generated-report publication (`F07-RESIDUALS-1113-01`)
Observed: when `show` is true and `ShowReport()`'s `_beforeOpen()` throws, the new report has already
been inserted into history, a toast raised and property and collection notifications sent; the catch
restores fields and collections, but observers have already seen the transient new report, so the
publication is not all-or-nothing. The new test checks only the last notification.
Expected: complete every fallible step (such as `_beforeOpen()`) before committing state and notifying
once, or use a modal-open path that cannot fail after commit; a test excludes the transient notification.
Owner: 1.1.13 wave 2 (Presentation, after F08).
Resolution: not fixed.
