# IC FlashMap Reference Evidence

This directory contains owner-approved reference evidence extracted from `IC FlashMap.7z`.

Included:

- `IC_FlashMap_20260701.xlsx` workbook evidence used for human flash-map review.
- `IC_FlashMap_20260705.xlsx` workbook update with NT51926/NT51927 TP Overview backup-region corrections and NT51926 versioned TP Overview sections. The current 2026-07-06 22:04:22 copy is tracked by SHA-256 in `SOURCE_MANIFEST.json`.
- `postbuild/` legacy postbuild BAT files used to verify Combiner command order and arguments.
- `mmap/` legacy memory-map headers used to verify address ranges and header locations.
- `common-fw/ap_fwconfig.c` FWConfig structure reference used to verify Common FW, FW/bar, and PID offsets.
- `SOURCE_MANIFEST.json` source archive provenance plus per-file SHA-256 hashes.

Excluded:

- `combiner_1.13.0/Combiner.exe`, because approved runtime binaries are managed under `external-tools/`.
- Generated firmware outputs and private BIN inputs.

These files are evidence only. Production code must not execute anything from this directory. Release packages may copy this directory under `reference/` for human review.

For future IC evidence drops, first run:

```text
python scripts/intake_ic_reference.py --source <owner-drop-folder> --ic <NTxxxxx> --mode <workflow>
```

Promote only reviewed, non-payload reference documents from the generated handoff manifest into this directory, then update `SOURCE_MANIFEST.json` with source path, size, SHA-256, and approval/provenance notes.
