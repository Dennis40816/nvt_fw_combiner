# IC FlashMap Reference

This folder is an owner-provided reference snapshot from the updated workbook plus extracted postbuild/mmap evidence:

`G:/My Drive/Working/Tools/File/word_encrypt/record/IC_FlashMap.xlsx`

The repository keeps the workbook and extracted support files directly so reviewers can diff workbook metadata, postbuild scripts, mmap headers, and guide text.

It contains:

- `IC FlashMap/IC_FlashMap.xlsx` - TP Overview and per-IC flashmap sheets.
- `IC FlashMap/postbuild/` - postbuild command scripts. These are the behavioral source of truth for CtrlRAM Replace postbuild order and Combiner command shape.
- `IC FlashMap/mmap.h/` - mmap headers used to explain offsets, sizes, and symbol intent.
- `IC FlashMap/hsi_combiner_guide/` - Combiner command references.
- `IC FlashMap/combiner_1.13.0/Combiner.exe` - evidence copy only.

Production code must not load anything from `refcode/flashmap`. The runtime package is:

`external-tools/legacy-combiner/1.13.0/Combiner.exe`

## Validation Rule

Trust order for CtrlRAM Replace map migration:

1. postbuild script behavior.
2. mmap symbol/size explanation.
3. TP Overview and detailed Excel sheets as documentation to correct when they disagree.

Run:

```powershell
python scripts\verify_flashmap_reference.py --root "refcode\flashmap\IC FlashMap"
```

Use `--strict` when documentation warnings should fail the command.
