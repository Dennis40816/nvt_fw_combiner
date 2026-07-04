# CtrlRAM Postbuild Command Matrix

This matrix records the normalized Combiner command sequences used after CtrlRAM Replace. The living experiment and conclusion tracker is [`ctrlram-replace-status-report.md`](ctrlram-replace-status-report.md).

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
| NT51926 | single/cascade | single, cascade | `CRC_Enable` | 2 / 2 | Despite being 926, postbuild uses legacy Normal Mode. Current catalog follows `2.0.0`; `1.4.1` reference is also archived because it uses a different header-copy target. |
| NT51927 | numeric/single/cascade | single, 2-chip, 3-chip | `MERGE_MODE`, `NT51927BASED_GEN_CRC_MODE CRC32` | 7 / 10 / 13 | Special flow. `cascade` maps to the 3-chip sequence; numeric `2` and `3` select explicit branches. |
| NT51928 | numeric/single/cascade | single, 2-chip, 3-chip | `MERGE_MODE`, `NT51927BASED_GEN_CRC_MODE CRC32` | 7 / 10 / 13 | Owner-approved alias of NT51927 for non-NB only. NT51928 NB is a separate IC and is not covered. |
| NT51929 | single/cascade | single, cascade | `NT51932BASED_NORMAL_MODE CRC8` | 2 / 2 | Owner-approved alias of NT51932 reference flow. |
| NT51930 | single/cascade | single, cascade | `NT51930BASED_NORMAL_MODE CRC8` | 2 / 2 | Current catalog follows `2.0.0` with `0x200` header copy plus second header-only command. `1.4.0` reference is archived because the current golden aligns with its `0x100` header-copy shape. |
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
- `LegacyCombinerPostbuildProcessorTests.TransformStagesBinFilesAndAcceptsDeclaredChanges` verifies the staged processor creates an empty `output/map.txt` for normal-mode real-tool runs.
- `LegacyCombinerPostbuildProcessorTests.TransformUsesStagedSourceOverridesWithoutPrePaste` verifies selected replacement bytes are staged for `BIN/*.bin` without pre-writing them into the firmware image before `Combiner.exe` runs.
- `LegacyCombinerPostbuildRealToolSmokeTests.RealToolRunsNt51927GoldenCrcOnlyWithoutUnexpectedChanges` runs the committed Combiner 1.13.0 executable on Windows against the owner-approved NT51927 golden output. This is a smoke test for the real tool binding, not a Replace golden parity claim.
- `LegacyCombinerPostbuildRealToolSmokeTests.DirectRealToolSixteenByteCasesMatchForSingleAndMultipleCtrlRamSelfReplacement` locks the accepted 16-byte self-replacement behavior for NT51920, NT51923, NT51929, NT51932, NT51950, and NT51951 single/cascade cases. This is still direct-combiner smoke evidence, not production CtrlRAM Replace parity.
- `LegacyCombinerPostbuildRealToolSmokeTests.DirectRealToolPureCombinerPastebackMatchesPrePasteFlow` verifies representative NT51920, NT51923, NT51926, NT51927, and NT51950 cases produce identical output when replacement CtrlRAM bytes are pre-pasted into the work image versus supplied only as Combiner staged source bytes. The production adapter uses the staged-source path.

## Open Evidence Gaps

- The adapter now stages an empty `output/map.txt` for normal and NT-based postbuild commands. Empty map is verified only for no-overlay smoke cases; overlay-enabled firmware may require real map content.
- Current workbench CtrlRAM Replace treats the base input as the Combiner TP work image. If the owner requires accepting a larger full-flash container, the flow must add an explicit TP slice and reinsertion step with owner-confirmed TP offset/range.
- NT51926 has two inspected postbuild references: `1.4.1` copies header `0x0 -> 0x32F50`, while `2.0.0` copies header `0x0 -> 0x32A70`. Current 2026-07-05 base aligns with `1.4.1`; production selection still needs owner decision and matching golden outputs.
- NT51930 has two inspected postbuild references: `1.4.0` copies `0x7000 -> 0x28FB0` length `0x100` once, while `2.0.0` copies the same source/target length `0x200` and runs a second header-only command. Current 51930 golden aligns with `1.4.0`; production selection still needs owner decision and matching golden outputs.
- NT51931 official BAT shape uses `NT51930BASED_NORMAL_MODE CRC8`, but committed Combiner 1.13.0 crashes on the current standard golden. This remains a tool-version or compatible-input investigation gap.
- End-to-end production CtrlRAM Replace still needs private golden outputs, declared allowed write ranges, and firmware-owner parity review for each released IC/mode.
