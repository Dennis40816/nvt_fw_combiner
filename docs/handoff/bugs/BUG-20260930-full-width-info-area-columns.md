# BUG-20260930-full-width-info-area-columns: the full-width firmware info area keeps three columns on wide windows

Status: open (owner decision 207; 1.2.7, decision 212)
Severity: P3
Found: 2026-09-30, owner review of v1.1.15 on a wide window.
Where: `src/NvtFwCombiner.Presentation.Avalonia/Views/FirmwareSlotCard.axaml.cs` `ApplyResponsiveLayout` (1 column
below 480 DIP, 2 below `CompactLayoutBreakpoint`, otherwise at most 3): the firmware facts grid of the slot cards
(for example TP Version, PID, Common FW Version, Event Buffer Version, TP SVN).
Observed: the info area spans the full card width but lays facts out in three columns, leaving wide gaps.
Expected: owner decision 207 (owner wording "由於寬度比較寬 我認為滿版 info 區域預設長度可以調整為 4"): four fact
columns per row by default when the info area spans the full width. Confirm with the owner before implementation that
"4" means columns and which width triggers it.
Evidence: owner review; v1.1.15 screenshots of the Merge and Replace slot cards.
Owner: unassigned; `1.2.7` item R46 (decision 212).
Resolution: pending.
