---
name: open-firmware-example
description: Open an NVT FW Combiner canonical firmware case on the Desktop with its inputs preloaded from CMD; use for owner UI inspection, not firmware execution.
---

# Open Firmware Example

From the repository root, run `cmd /c scripts\open-golden-example.cmd --list` to find a case ID, then run `cmd /c scripts\open-golden-example.cmd <case-id>`. For NT51926 Standard Merge, use `nt51926-gen-flash`; for NT51927 3-IC CtrlRAM Replace, use `nt51927-3chip-self-20260705`; for NT51929 AB Merge, use `nt51929-ab-t05-d06`.

The command resolves paths from `testdata/golden/canonical/**/provenance/case.json`, checks file hashes, builds the current Desktop source, and starts its existing input-selection workflow. It never runs Preview or Build. Check the Desktop's visible IC, mode, selected filenames, status badges, and any input errors after launch. `--dry-run` prints the exact command without opening a window.

The printed evidence kind belongs to the case manifest. A loaded example is not proof of output byte parity. Use `$locate-golden-evidence` when a Golden claim is needed.

For an NT51929 AB CtrlRAM **UI candidate**, use `nt51929-ab-ctrlram-candidate`. `nt51950-ab-ctrlram-unsupported` deliberately shows the currently blocked 512 KiB AB Base admission; report that error state as such.
