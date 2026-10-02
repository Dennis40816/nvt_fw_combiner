# BUG-20261002-support-matrix-tooltip-hover-flicker: hovering some Support Matrix cells makes the tooltip flicker

Status: fixed in code; waiting for the owner's check in the running application
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
Limits: the flicker needs a real pointer and a native tooltip window; the headless test platform hosts tooltips inside
the window and cannot reproduce it, and the commander's desktop-control tool does not accept a development build. The
owner confirms the fix in the running application (board decision 253).
Not changed, for the owner to decide separately: the tooltip's declared width of 560 is clamped to 320 by the theme,
which is what makes it tall; other tall tooltips in the application still use the stock placement.
Owner: Presentation (1.2.2, R58, board decision 253).
