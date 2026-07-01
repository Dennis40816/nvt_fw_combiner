# CtrlRAM Postbuild Command Matrix

This matrix records the normalized Combiner command sequences used after CtrlRAM Replace.

Evidence order:

1. owner postbuild script command order;
2. `hsi_combiner_guide` argument contract;
3. mmap symbol explanation;
4. TP Overview as the documentation baseline to correct when it disagrees.

The application catalog stores command sequences as structured commands, not as shell strings. Tests build argv arrays from that structure and verify they match the Combiner 1.13.0 command shapes.

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
