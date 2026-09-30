# BUG-20260930-memory-connector-handle-origin: Card and local-view stems started at the 24 DIP handle, not the real range

Status: fixed (on `feature/1.1.15/memory-layout`, pull request #492, pending merge)
Severity: P2
Found: 2026-09-30, owner visual review of the running build with Golden examples loaded,
at feature/1.1.15/memory-layout@9c946e457.
Where: MemoryCoverageBar.cs (OpenCard, OpenLocal), MemoryCoverageBar.Markers.cs
Observed: The black stem always started at the 24x24 minimum marker (or the position row) under
the rail, so a tiny slice looked as if it sat at the handle instead of at its flash address.
Expected: Decision 197: the stem starts at the slice's true range on the flash bar; the handle stays
the pointer target; a collision list starts at the midpoint of its markers' ranges.
Evidence: MemoryCoveragePopupTests.TrueRange.cs, 8 cases including clamped edge markers; a mutation
reverting the range centre fails the edge-marker case. Narrow Memory|CtrlRam UiSmoke run 504/504
(local, worktree at 9c946e457 plus uncommitted changes).
Owner: Claude Code, feature/1.1.15/memory-layout.
Resolution: A tiny marker stores its true range centre (attached RangeCenter); OpenCard and OpenLocal
start the stem at the flash track bottom edge at that x and cross the overview rows behind text
obstacles. Amended the same day (owner): a CtrlRAM focus lane is the exception and again starts
below its own label (LaneStemStartsBelowItsPositionLabel).
