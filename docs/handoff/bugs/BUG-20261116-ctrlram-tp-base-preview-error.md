# BUG-20261116-ctrlram-tp-base-preview-error: CtrlRAM Replace preview shows a display error with a TP FW Base and two inputs

Status: open, not reproduced (owner decision 2026-11-28 12:11: record it, keep it open; the owner sends a screenshot the next time it appears)
Severity: P2 (the memory preview only; Build and output bytes are not reported as affected)
Found: 2026-11-16, owner report on the Desktop app.
Where: CtrlRAM Replace screen, memory layout preview (`ReplacePresentationViewModel.HasMemoryLayoutDisplayError`).
Observed (owner report): with a TP FW Base loaded and two of the three CtrlRAM inputs loaded ("2 / 3 targets selected"), the memory preview showed the orange display error instead of the layout. The report named NT51951 and NT51950 single cases with the Normal and VN inputs.
Expected: the preview shows the accepted memory layout for the TP FW Base and the loaded inputs.
Owner: PC-NFC.

## What was tried (2026-11-28, on 1.3.0 at `c3cc658`, local probes, not committed)

Bootstrap projection probe: `MemoryLayoutProjector` on the compiled capability, with display regions from `GetDiscoveryDisplayFromAcceptedBase`.

| IC | Topology | Canonical case | Base artifact | CtrlRAM inputs | Result |
| --- | --- | --- | --- | --- | --- |
| NT51923 | single | `nt51923-fw141-single-auto-prj-662-20260717` | `tp-input` | none | stops before projection |
| NT51926 | single | `nt51926-fw200-single-auto-prj-597-20260718` | `tp-input` | none | stops before projection |
| NT51926 | single | `nt51926-fw141-single-auto-prj-747-20260717` | `tp-input` | none | stops before projection |
| NT51927 | single | `nt51927-fw141-single-auto-prj-529-20260717` | `tp-input` | none | stops before projection |
| NT51929 | single | `nt51929-fw200-single-auto-prj-594-20260717` | `tp-input` | none | stops before projection |
| NT51950 | single | `nt51950-fw200-single-auto-prj-676-20260717` | `tp-input` | none | stops before projection |
| NT51951 | single | `nt51951-fw200-single-auto-prj-695-20260718` | `tp-input` | none | stops before projection |
| NT51950 | cascade | `nt51951-fw200-cascade2-auto-prj-599-20260731` | `tp-firmware-input` | none | stops before projection |
| NT51951 | cascade | `nt51951-fw200-cascade2-auto-prj-599-20260731` | `tp-firmware-input` | none | stops before projection |
| NT51951 | single | `nt51951-fw200-single-auto-prj-695-20260718` | `tp-input` | Normal, VN | layout projects, no display error |
| NT51950 | single | `nt51950-fw200-single-auto-prj-676-20260717` | `tp-input` | Normal, VN | layout projects, no display error |
| NT51950 | cascade | `nt51951-fw200-cascade2-auto-prj-599-20260731` | `tp-firmware-input` | Normal, VN | layout projects, no display error |
| NT51951 | cascade | `nt51951-fw200-cascade2-auto-prj-599-20260731` | `tp-firmware-input` | Normal, VN | layout projects, no display error |

- The rows with no input stop at session preparation with "Select at least one CtrlRAM replacement BIN." Without a compiled capability the screen takes the pending path, which cannot show the orange error. So the owner's state is the two-input state.
- The two-input rows match the owner's "2 / 3 targets selected" state. None of them reproduces the error.

A headless UiSmoke probe was also written. It drives `MainWindowViewModel` through CtrlRAM Replace for the two NT51951 and NT51950 single cases: select the IC, single topology, set the TP FW Base, load the Normal and VN inputs, then assert that `HasMemoryLayoutDisplayError` is false.

## Remaining suspect (not verified)

The screen may build the display regions from a different source than the projection probe. It uses `FirmwareInspectionSnapshot.CtrlRamDisplay` (`BuiltInFirmwareInspection.cs`), which reads the regions from the inspection's FW config. It falls back to `ProjectCtrlRamInspectionDisplay`, or to the Base-less `GetDiscoveryDisplay` display (`ReplacePresentationViewModel.Memory.cs`). The compiled capability resolves its postbuild profile and chip count from the FW config backup (`BuiltInCtrlRamAuthoringAdapter.Context.cs`). A mismatch between those two sources is the next thing to check.

## Next step

- When the owner sees it again: a screenshot, the IC, the topology, the Common FW version, and which inputs were loaded, in which order.
- Then re-run the projection with the inspection-built display regions for that exact state.
