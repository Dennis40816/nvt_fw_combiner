# CtrlRAM Universal Sentinel Validation Table

Status: active 0.7.0 validation aid.
Last updated: 2026-07-05.

This table defines the owner-side one-file smoke validation for CtrlRAM Replace mapping. It does not replace byte-for-byte golden output approval. It is meant to catch wrong slot, wrong range, wrong branch, and wrong postbuild-category selection before final golden parity.

## Universal BIN

Create one file at least `0x23000` bytes long. `0x23000` covers the largest currently inspected CtrlRAM postbuild input: NT51930 Common FW 1.x extended cascade `DiffDLM.bin`.

Recommended content:

```text
byte[i] = (0x5A + i) & 0xFF
```

Generate the default file with:

```powershell
python scripts\create_ctrlram_universal_sentinel.py --output C:\temp\ctrlram-universal-sentinel.bin
```

Use `--seed 0x5B`, `--seed 0x5C`, and so on when creating per-slot variants for stronger slot-swap detection.

For each row below, feed the same file into every listed replacement slot for that IC/branch. After Replace + postbuild, verify each output target range matches the first `length` bytes of the universal file, except ranges explicitly owned by postbuild header/CRC/header-copy writes in the report.

For stronger slot-swap detection, duplicate this same file per slot and change the seed byte per duplicate. The one-file version is enough to verify target offsets and lengths.

## IC FlashMap Evidence Notes

The current `IC_FlashMap_20260705.xlsx` `TP Overview` sheet splits NT51926 into `51926 (2.0.0)` and `51926 (1.X.X)` sections:

| NT51926 source | VN range | FWConfig backup | Header copy |
| --- | --- | --- | --- |
| Common FW 1.4.1 postbuild | `[0x315D0,0x32C30)` len `0x1660` | `[0x3B000,0x3B800)` len `0x800` | `0x0 -> 0x32F50` len `0x100` |
| Common FW 2.0.0 postbuild | `[0x315D0,0x32A6E)` len `0x149E` | `[0x3B000,0x3B780)` len `0x780` | `0x0 -> 0x32A70` len `0x100` |

The `51926 TP Flashmap` detail sheet still carries the 2.0.0-style single table, so code and reports continue to use the selected postbuild category rather than an IC-only detail-sheet row.

Committed NT51926 `1.4.1` BIN evidence has exactly one little-endian end-flag marker (`00 4E 56 54`) and it starts at `0x3BFFC`. The same check on the 2026-07-05 base, Standard Merge TP input, and Standard Merge expected flash shows `0x34FFC = 00 00 00 00`. Use the selected postbuild category for FWConfig/header-copy lengths, and do not treat the 2.0.0 mmap `FLASHMAP_ENDFLAG 0x34FFC` row as the actual marker location for the current `1.4.1` fixture.

NT51930 has an `IC > 13, Max29` extended DiffDLM row and TP Overview category notes in the 2026-07-05 10:34:14 workbook. Its 1.x and 2.0.0 postbuild categories still consume different CtrlRAM slots:

| NT51930 source | Branch | MP | VN | DiffDLM | Header copy |
| --- | --- | --- | --- | --- | --- |
| Common FW 1.x postbuild | single | consumed | len `0x195E` | none | `0x7000 -> 0x28FB0` len `0x100` |
| Common FW 1.x postbuild | cascade `2..13` | consumed | len `0x195E` | len `0xFE00` | `0x7000 -> 0x28FB0` len `0x100` |
| Common FW 1.x postbuild | cascade `14..29` | consumed | len `0x195E` | len `0x23000` | `0x7000 -> 0x28FB0` len `0x100` |
| Common FW 2.0.0 postbuild | cascade `2..29` | not consumed | len `0x1960` | len `0xFE00` | `0x7000 -> 0x28FB0` len `0x200`, plus second header command |

## Full IC Table

Ranges are TP work-image half-open ranges.

| IC | Category / branch | Slots and target ranges |
| --- | --- | --- |
| NT51917 | NT51927 alias, single | NF `[0x16800,0x177D0)` `0xFD0`; Normal `[0x177D0,0x1A7D0)` `0x3000`; MP `[0x1A7D0,0x1CBD0)` `0x2400`; VN `[0x1CBD0,0x1E230)` `0x1660` |
| NT51917 | NT51927 alias, 2-chip | master slots above; right NF `[0x1F800,0x207D0)` `0xFD0`; right Normal `[0x207D0,0x237D0)` `0x3000`; right MP `[0x237D0,0x25BD0)` `0x2400`; right VN `[0x25BD0,0x27230)` `0x1660` |
| NT51917 | NT51927 alias, 3-chip | 2-chip slots plus left NF `[0x28800,0x297D0)` `0xFD0`; left Normal `[0x297D0,0x2C7D0)` `0x3000`; left MP `[0x2C7D0,0x2EBD0)` `0x2400`; left VN `[0x2EBD0,0x30230)` `0x1660` |
| NT51919 | NT51929/NT51932 alias, single | NF `[0x1FC00,0x21B90)` `0x1F90`; Normal `[0x21B90,0x26590)` `0x4A00`; VN `[0x26590,0x27EF0)` `0x1960` |
| NT51919 | NT51929/NT51932 alias, cascade | single slots plus DiffDLM `[0x2D100,0x35D00)` `0x8C00` |
| NT51920 | single | Normal master `[0x22780,0x24F80)` `0x2800`; MP master `[0x24F80,0x26680)` `0x1700`; NF `[0x2A780,0x2C710)` `0x1F90`; VN `[0x2C710,0x2D728)` `0x1018` |
| NT51920 | cascade | single slots plus Normal slave `[0x26780,0x28F80)` `0x2800`; MP slave `[0x28F80,0x2A680)` `0x1700`; Vector `[0x2D728,0x2D980)` `0x258` |
| NT51923 | single | Normal `[0x22800,0x26000)` `0x3800`; MP `[0x26000,0x28800)` `0x2800`; NF `[0x2A000,0x2E4B0)` `0x44B0`; VN `[0x2E800,0x2FE60)` `0x1660` |
| NT51923 | cascade | single slots plus DiffDLM `[0x28800,0x2A000)` `0x1800` |
| NT51926 | Common FW 1.4.1 single | Normal `[0x22800,0x25400)` `0x2C00`; MP `[0x25400,0x27800)` `0x2400`; NF `[0x2C800,0x2F5D0)` `0x2DD0`; VN `[0x315D0,0x32C30)` `0x1660` |
| NT51926 | Common FW 1.4.1 cascade | single slots plus DiffDLM `[0x27800,0x2A000)` `0x2800` |
| NT51926 | Common FW 2.0.0 single | Normal `[0x22800,0x25400)` `0x2C00`; MP `[0x25400,0x27800)` `0x2400`; NF `[0x2C800,0x2F5D0)` `0x2DD0`; VN `[0x315D0,0x32A6E)` `0x149E` |
| NT51926 | Common FW 2.0.0 cascade | single slots plus DiffDLM `[0x27800,0x2A000)` `0x2800` |
| NT51927 | single | NF master `[0x16800,0x177D0)` `0xFD0`; Normal master `[0x177D0,0x1A7D0)` `0x3000`; MP master `[0x1A7D0,0x1CBD0)` `0x2400`; VN master `[0x1CBD0,0x1E230)` `0x1660` |
| NT51927 | 2-chip | single slots plus right NF `[0x1F800,0x207D0)` `0xFD0`; right Normal `[0x207D0,0x237D0)` `0x3000`; right MP `[0x237D0,0x25BD0)` `0x2400`; right VN `[0x25BD0,0x27230)` `0x1660` |
| NT51927 | 3-chip | 2-chip slots plus left NF `[0x28800,0x297D0)` `0xFD0`; left Normal `[0x297D0,0x2C7D0)` `0x3000`; left MP `[0x2C7D0,0x2EBD0)` `0x2400`; left VN `[0x2EBD0,0x30230)` `0x1660` |
| NT51928 | non-NB, NT51927 alias | Same slots as NT51927. NB is not covered. |
| NT51929 | NT51932 alias, single | NF `[0x1FC00,0x21B90)` `0x1F90`; Normal `[0x21B90,0x26590)` `0x4A00`; VN `[0x26590,0x27EF0)` `0x1960` |
| NT51929 | NT51932 alias, cascade | single slots plus DiffDLM `[0x2D100,0x35D00)` `0x8C00` |
| NT51930 | Common FW 1.x single | NF `[0x1FC00,0x21650)` `0x1A50`; Normal `[0x21650,0x24250)` `0x2C00`; MP `[0x24250,0x27650)` `0x3400`; VN `[0x27650,0x28FAE)` `0x195E` |
| NT51930 | Common FW 1.x cascade `2..13` | single slots plus DiffDLM `[0x2F200,0x3F000)` `0xFE00` |
| NT51930 | Common FW 1.x cascade `14..29` | single slots plus DiffDLM `[0x2F200,0x52200)` `0x23000` |
| NT51930 | Common FW 2.0.0 single | NF `[0x1FC00,0x21650)` `0x1A50`; Normal `[0x21650,0x24250)` `0x2C00`; VN `[0x27650,0x28FB0)` `0x1960` |
| NT51930 | Common FW 2.0.0 cascade `2..29` | single slots plus DiffDLM `[0x2F200,0x3F000)` `0xFE00` |
| NT51931 | official single, currently blocked by Combiner 1.13.0 crash | NF `[0x16800,0x177D0)` `0xFD0`; Normal `[0x177D0,0x19FD0)` `0x2800`; MP `[0x19FD0,0x1C3D0)` `0x2400`; VN `[0x1C3D0,0x1DA30)` `0x1660` |
| NT51931 | official cascade, currently blocked by Combiner 1.13.0 crash | single slots plus DLM `[0x22800,0x3A400)` `0x17C00` |
| NT51932 | single | NF `[0x1FC00,0x21B90)` `0x1F90`; Normal `[0x21B90,0x26590)` `0x4A00`; VN `[0x26590,0x27EF0)` `0x1960` |
| NT51932 | cascade | single slots plus DiffDLM `[0x2D100,0x35D00)` `0x8C00` |
| NT51950 | single | NF `[0x22C00,0x25610)` `0x2A10`; Normal `[0x25610,0x2B210)` `0x5C00`; VN `[0x2B210,0x2D30C)` `0x20FC` |
| NT51950 | cascade | single slots plus DiffDLM `[0x33200,0x34600)` `0x1400` |
| NT51951 | NT51950 alias, single | Same slots as NT51950 single. |
| NT51951 | NT51950 alias, cascade | Same slots as NT51950 cascade. |

## Expected Report Checks

Every run should record:

- selected IC, IC number branch, Common FW version, FW/bar, PID, and postbuild category;
- selected processor id and exact Combiner command block;
- replacement slot id and target range for each selected slot;
- allowed write ranges containing postbuild-mapped CtrlRAM pasteback ranges plus declared postbuild header/CRC/header-copy ranges only. Unselected slots may still appear because the workbench stages base-image bytes for Combiner pasteback.

Do not mark a universal sentinel pass as golden parity. Promotion still requires owner-approved final expected output bytes or hashes for the release-scope IC/mode.
