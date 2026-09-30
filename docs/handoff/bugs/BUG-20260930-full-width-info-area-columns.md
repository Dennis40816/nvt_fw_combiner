# BUG-20260930-full-width-info-area-columns: the full-width firmware info area keeps three columns on wide windows

Status: open (owner decision 207; 1.2.x)
Severity: P3
Found: 2026-09-30, owner review of v1.1.15 on a wide window.
Where: the firmware facts grid of the slot cards (for example TP Version, PID, Common FW Version, Event Buffer
Version, TP SVN on the Base, TPA and TPB cards).
Observed: the info area spans the full card width but lays facts out in three columns, leaving wide gaps.
Expected: owner decision 207 (owner wording "由於寬度比較寬 我認為滿版 info 區域預設長度可以調整為 4"): four fact
columns per row by default when the info area spans the full width. Confirm with the owner before implementation that
"4" means columns and which width triggers it.
Evidence: owner review; v1.1.15 screenshots of the Merge and Replace slot cards.
Owner: unassigned; the 1.2.x version slot is set in the allocation sync (decision 200).
Resolution: pending.
