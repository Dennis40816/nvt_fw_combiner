# CtrlRAM Postbuild Command Matrix

This matrix records the normalized Combiner command sequences used after CtrlRAM Replace.

Evidence order:

1. owner postbuild script command order;
2. `hsi_combiner_guide` argument contract;
3. mmap symbol explanation;
4. TP Overview as the documentation baseline to correct when it disagrees.

The application catalog stores command sequences as structured commands, not as shell strings. Tests build argv arrays from that structure and verify they match the Combiner 1.13.0 command shapes.

The imported full postbuild BAT files also call `python output\InsertSID.py output\*_fw.bin` before Combiner. Owner confirmation for CtrlRAM Replace: this legacy Insert PID step writes `headerStart + 0x24`, and that address is not part of Replace mutation authority. The Replace runner therefore does not execute the BAT `InsertSID.py` step and must not allow the PID bytes in its external-processor write ranges.

CtrlRAM Replace always runs the selected IC/IC-number Combiner postbuild branch once, in the command order below, after host-side CtrlRAM byte replacement. It does not shorten the Combiner command sequence to only the user-selected CtrlRAM slot; unselected CtrlRAM payloads are staged from the current work image so the full postbuild can refresh integrity consistently.

NT51917/NT51927/NT51928 use the special 51927 postbuild shape: the BAT copies the refreshed `output\*_fw.bin` into `output\FlashMerge\TP_FW` after Combiner finishes. NFC therefore stages Combiner against a TP work image, then assembles that refreshed TP_FW back into the cloned base flash so DP/final-flash bytes outside the TP work range stay from the base image. Other current postbuild profiles remain in-place firmware-image stages because their BAT evidence does not contain a `FlashMerge\TP_FW` handoff.

For per-IC Merge/Replace flowcharts, see [`ic-workflow-flowcharts.md`](ic-workflow-flowcharts.md).

| IC | IC num mode | Branches | Combiner modes | Command count | Notes |
| --- | --- | --- | --- | ---: | --- |
| NT51917 | numeric/single/cascade | single, 2-chip, 3-chip | `MERGE_MODE`, `NT51927BASED_GEN_CRC_MODE CRC32` | 7 / 10 / 13 | Owner-approved alias of NT51927 reference flow. |
| NT51919 | single/cascade | single, cascade | `NT51932BASED_NORMAL_MODE CRC8` | 2 / 2 | Owner-approved alias of NT51929/NT51932 reference flow. |
| NT51920 | single/cascade | single, cascade | `CRC_Enable` | 2 / 2 | Legacy Normal Mode. Cascade adds slave Normal/MP and Vector blocks. |
| NT51923 | single/cascade | single, cascade | `CRC_Enable` | 2 / 2 | Cascade uses split `DiffDLM.bin` source offsets `0x0` and `0x1400`. |
| NT51926 | single/cascade | single, cascade | `CRC_Enable` | 2 / 2 | Despite being 926, postbuild uses legacy Normal Mode. |
| NT51927 | numeric/single/cascade | single, 2-chip, 3-chip | `MERGE_MODE`, `NT51927BASED_GEN_CRC_MODE CRC32` | 7 / 10 / 13 | Special flow. `cascade` maps to the 3-chip sequence; numeric `2` and `3` select explicit branches. |
| NT51928 | numeric/single/cascade | single, 2-chip, 3-chip | `MERGE_MODE`, `NT51927BASED_GEN_CRC_MODE CRC32` | 7 / 10 / 13 | Owner-approved alias of NT51927 for non-NB only. NT51928 NB is a separate IC and is not covered. |
| NT51929 | single/cascade | single, cascade | `NT51932BASED_NORMAL_MODE CRC8` | 2 / 2 | Owner-approved alias of NT51932 reference flow. |
| NT51930 | single/cascade | single, cascade | `NT51930BASED_NORMAL_MODE CRC8` | 2 / 2 | Current product target has no `>13 IC` case; cascade uses `DiffDLM.bin` at `0x2F200` size `65024`. |
| NT51931 | single/cascade | single, cascade | `NT51930BASED_NORMAL_MODE CRC8` | 1 / 1 | Postbuild uses NT51930-based mode for NT51931. |
| NT51932 | single/cascade | single, cascade | `NT51932BASED_NORMAL_MODE CRC8` | 2 / 2 | Direct postbuild reference. |
| NT51950 | single/cascade | single, cascade | `NT51950BASED_NORMAL_MODE CRC8` | 2 / 2 | Direct postbuild reference. |
| NT51951 | single/cascade | single, cascade | `NT51950BASED_NORMAL_MODE CRC8` | 2 / 2 | Owner-approved alias of NT51950 reference flow. |

## Tester Coverage

- `LegacyCombinerPostbuildCatalogTests.CommandLineBuilderMatchesHsiCombinerArgumentShapes` verifies every command against the hsi guide argv contract.
- `LegacyCombinerPostbuildCatalogTests.Nt51927ResolvesExplicitNumericIcCountBranches` verifies 927 single/2IC/3IC branch selection.
- `LegacyCombinerPostbuildCatalogTests.Nt51927PostbuildKeepsDifferentRightNfOffsetsByIcCount` locks the 927 2IC vs 3IC `NF_Ctrlram.bin` split offset difference.
- `LegacyCombinerPostbuildCatalogTests.Nt51917AliasesNt51927PostbuildFlow`, `Nt51919AliasesNt51929PostbuildFlow`, `Nt51928AliasesNt51927PostbuildFlow`, `Nt51929AliasesNt51932PostbuildFlow`, and `Nt51951AliasesNt51950PostbuildFlow` lock owner-approved alias behavior.
- `LegacyCombinerPostbuildCatalogTests.Nt51930CascadeUsesLessOrEqual13IcDiffDlmLength` locks current NT51930 cascade behavior to the `<=13 IC` branch.
- `LegacyCombinerPostbuildProcessorTests.TransformRunsNt51927TwoChipMergeAndCrcSequence` verifies the staged processor actually invokes the 927 2IC `MERGE_MODE` and CRC-only sequence.
- `LegacyCombinerPostbuildRealToolSmokeTests.RealToolRunsNt51927GoldenCrcOnlyWithoutUnexpectedChanges` runs the committed Combiner 1.13.0 executable on Windows against the owner-approved NT51927 golden output. This is a smoke test for the real tool binding, not a Replace golden parity claim.

## Open Evidence Gaps

- NT51927 public standard-merge self-replacement is not byte-idempotent after the full Combiner postbuild sequence even when the replacement CtrlRAM bytes are sliced from the same base image and the TP work image uses the Standard Merge TP range `0x00000-0x34FFF`. The observed changed ranges are six 4-byte postbuild/header ranges: `0x1E26C-0x1E26F`, `0x1E27C-0x1E27F`, `0x32FDC-0x32FDF`, `0x32FEC-0x32FEF`, `0x32FFC-0x32FFF`, and `0x3300C-0x3300F`. This remains a golden evidence gap, not an approved expected-output update.
- NT51950/NT51951 `NT51950BASED_NORMAL_MODE CRC8` real-tool execution still needs `map.txt` staging evidence before production CtrlRAM Replace can be claimed.
