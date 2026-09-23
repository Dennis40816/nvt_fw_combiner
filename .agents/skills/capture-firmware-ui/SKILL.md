---
name: capture-firmware-ui
description: Render NVT FW Combiner release-review screenshots with canonical firmware loaded and Details closed/open in headless Avalonia.
---

# Capture Firmware UI

Use the production `MainWindow` through the `ReleaseExampleScreenshots` headless UI smoke case. Before running `dotnet test`, load the user-level `NFC_TEST_AREA_ROOT` and set `TEMP`, `TMP`, and `TMPDIR` to its existing `temp` child as required by root `AGENTS.md`. Set `NFC_VISUAL_OUTPUT_DIR` to an evidence directory outside Git, then run:

```text
dotnet test tests/NvtFwCombiner.UiSmoke.Tests/NvtFwCombiner.UiSmoke.Tests.csproj --filter FullyQualifiedName~ReleaseExampleScreenshots --no-restore --verbosity quiet
```

The case matrix is in `tests/NvtFwCombiner.UiSmoke.Tests/ReleaseExampleScreenshots.cs`. Each case must produce `<case-id>-details-closed.png` and `<case-id>-details-open.png`. Inspect the images, especially status badges and scroll cutoff. NT51927 3-IC expands every target group so all eight inputs are visible.

These renders use real product XAML and manifest-backed input files in AvaloniaHeadless. For OS window fidelity, launch the corresponding Desktop case with `$open-firmware-example` and inspect it there. The images are presentation evidence only. The NT51929 AB CtrlRAM cross-case image is a candidate; the NT51950 AB CtrlRAM cross-case image currently demonstrates a blocked unsupported reference length. Keep those labels explicit when presenting the set.
