# BUG-20261002-support-matrix-tooltip-hover-flicker: hovering some Support Matrix cells makes the tooltip flicker

Status: fixed; confirmed by the commander in the running application (2026-10-02)
Severity: P2 (the detail of the affected cells cannot be read with the pointer; no firmware effect)
Found: 2026-10-02, the owner, in the released application: "在 support matrix 上 hover 會不固定狂閃".
Where: `src/NvtFwCombiner.Presentation.Avalonia/Resources/MainWindowPageTemplates.axaml`, `SupportMatrixCellTemplate`
(the cell tooltip in Settings > Support Matrix).
Observed: on some cells the tooltip opens and closes in a rapid loop while the pointer rests on the cell; other cells
are fine.
Cause (from the Avalonia 12.0.5 sources and the catalog data; not reproduced by a tool, see Limits):
- the cell tooltip lists every route of the cell. 12 of the 36 cells have 3 to 8 routes (the CtrlRAM Replace column
  and two Standard Merge cells), and the tooltip is 320 wide in effect (the theme's maximum width clamps the declared
  560), so those tooltips are taller than the room below or above most cells;
- the stock placement (`BottomEdgeAlignedLeft` with every constraint adjustment) then slides the tooltip along the
  vertical axis until it fits the screen, which puts it over the hovered cell and the pointer;
- the tooltip is a separate window. With the pointer over it, the main window receives a leave event, and
  `ToolTipService` closes the tooltip unless the tooltip's own pointer event carries the same timestamp, which is not
  guaranteed;
- once it closes, the pointer is over the cell again, and because a tooltip closed less than `BetweenShowDelay`
  (100 ms) ago the next one opens with no delay, over the pointer again. The loop is the flicker. Cells with one
  route have short tooltips that fit below, which is why it looked irregular.
Fix: the cell tooltip uses `Placement="Custom"` with `ClearOfTargetToolTipPlacement.Callback`
(`Behaviors/ClearOfTargetToolTipPlacement.cs`): below the cell if the tooltip fits there, else above, else to the
right, else to the left, and only slides along the edge it is attached to; a tooltip that fits on no side shrinks on
the roomier vertical side. It can no longer cover its cell. The look is unchanged where the tooltip already fitted
below.
Evidence: `ClearOfTargetToolTipPlacementTests` (the chosen side for each case; for every cell position and six tooltip
heights the placed tooltip never intersects the cell) and the Support Matrix interaction test (the cell uses the
placement). UiSmoke Support Matrix, placement and XAML style contract tests: 290 passed.
Check in the running application (development build of this branch, maximized, pointer resting on the cell,
screenshots 0.15 to 0.5 s apart):
- with the stock placement (the fix reverted in the same worktree): on NT51926 CtrlRAM Replace (8 routes) the cell's
  hover highlight was present in 2 of 6 screenshots taken within about 3 s and the tooltip in none: the loop above;
- with the fix: on the same cell the tooltip stays open to the right of the cell in 4 of 4 screenshots; NT51932
  CtrlRAM Replace (4 routes) and NT51950 Standard Merge (3 routes) open to the right, NT51951 AB Merge (1 route)
  above its cell, each stable in consecutive screenshots. The tooltip of a cell with 8 routes is taller than the
  screen and is cut off at the bottom; the readable presentation is board decision 255 (`1.2.7`, R60).
Limits: the flicker needs a real pointer and a native tooltip window; the headless test platform hosts tooltips inside
the window and cannot reproduce it, so no automated test covers the loop itself, only the placement that prevents it.
Not changed here: the tooltip's declared width of 560 is clamped to 320 by the theme, which is part of what makes it
tall (decided with R60, board decision 255); other tall tooltips in the application still use the stock placement.
Owner: Presentation (1.2.2, R58, board decision 253).
