# BUG-20260930-inspection-retry-panel-not-hideable: the inspection retry card cannot be hidden

Status: fixed (merged into `1.2.x` by #558, 2026-10-04; owner decision 205, item R45)
Severity: P3
Found: 2026-09-30, owner review of v1.1.15 (the "Inspection unavailable ... can be retried safely" card below the
Merge output layout).
Where: the Merge/Replace inspection status card and the bottom-right round action icons of the main window.
Observed: the inspection-unavailable card with its retry action stays in the page and cannot be hidden.
Expected: owner decision 205: the card can be hidden and folds into one of the round icons at the bottom right of the
window, from where it reopens; the retry action and the diagnostic stay reachable.
Evidence: owner photo of v1.1.15.
Owner: unassigned; `1.2.10` item R45 (decision 212).
Resolution: fixed by item R45 in #558: the Inspection unavailable card of the Merge and Replace pages collapses to a round entry at the lower right and reopens the original diagnostic and Retry; collapsing is presentation state only and the two pages do not share it.
