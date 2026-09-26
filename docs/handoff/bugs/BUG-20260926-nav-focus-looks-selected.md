# BUG-20260926-nav-focus-looks-selected: the Home focus underline looks like a second selected tab

Status: open
Severity: P3
Found: 2026-09-26, owner, on a headless release screenshot of the NT51950 AB Merge example
Where: `src/NvtFwCombiner.Presentation.Avalonia/Styles/MainWindowStyles.axaml` (`ToggleButton.nav:focus-visible`);
`src/NvtFwCombiner.Presentation.Avalonia/MainWindow.axaml.cs` (startup focus on `HomeNavigationButton`)
Observed: after the required startup stage succeeds, the shell focuses the Home navigation button with
`NavigationMethod.Tab`, so its `:focus-visible` style draws a 2 px underline in `NfcAccentBorderStrongBrush`
(`#60A5FA` in the light theme). When startup inputs open another page directly (Merge or Replace), that page
shows its selected underline (`NfcAccentBrush`, `#2563EB`) while Home keeps the focus underline, and the two
tabs look selected at the same time until the user clicks elsewhere.
Expected: only the current page looks selected; keyboard focus is visible but distinguishable from selection.
Options: focus the selected page's navigation button instead of always Home; and/or draw the navigation focus
indicator as an outline instead of an underline.
Owner: 1.1.13 (R1 UI), owner decision 32: only the selected tab shows the blue underline; after F08,
which also changes `MainWindow.axaml.cs`.
Resolution: not fixed.
