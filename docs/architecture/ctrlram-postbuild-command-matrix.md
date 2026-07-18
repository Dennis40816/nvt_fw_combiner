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
| NT51926 | single/cascade | single, cascade | `CRC_Enable` | 2 / 2 | Despite being 926, postbuild uses legacy Normal Mode. Workbench selects `1.4.1` or `2.0.0` profile from base FWConfig Common FW version; `1.4.1` uses header target `0x32F50`, while `2.0.0` uses `0x32A70`. |
| NT51927 | numeric/single/cascade | single, 2-chip, 3-chip | `MERGE_MODE`, `NT51927BASED_GEN_CRC_MODE CRC32` | 7 / 10 / 13 | Special flow. `cascade` maps to the 3-chip sequence; numeric `2` and `3` select explicit branches. |
| NT51928 | numeric/single/cascade | single, 2-chip, 3-chip | `MERGE_MODE`, `NT51927BASED_GEN_CRC_MODE CRC32` | 7 / 10 / 13 | Owner-approved alias of NT51927 for non-NB only. NT51928 NB is a separate IC and is not covered. |
| NT51929 | single/cascade | single, cascade | `NT51932BASED_NORMAL_MODE CRC8` | 2 / 2 | Owner-approved alias of NT51932 reference flow. |
| NT51930 | single/cascade/numeric | 1.x: single, cascade `2..29` | `NT51930BASED_NORMAL_MODE CRC8` | 1 / 1 | Runtime accepts only the stable Common FW `1.x.x` profile and requires the version decoded from base FWConfig. It follows the archived `1.4.0` shape with MP input and one `0x100` header-copy command; numeric `2..29` uses the approved `DiffDLM` length `0xFE00`. The inspected `2.0.0` BAT and `0x23000` extended branch remain hash-pinned evidence only and are not runtime-selectable. |
| NT51931 | single/cascade | single, cascade | `NT51930BASED_NORMAL_MODE CRC8` | 1 / 1 | Evidence-only command shape; all NT51931 Replace workflows are Not Supported by runtime policy. |
| NT51932 | single/cascade | single, cascade | `NT51932BASED_NORMAL_MODE CRC8` | 2 / 2 | Direct postbuild reference. |
| NT51950 | single/cascade | single, cascade | `NT51950BASED_NORMAL_MODE CRC8` | 2 / 2 | Direct postbuild reference. |
| NT51951 | single/cascade | single, cascade | `NT51950BASED_NORMAL_MODE CRC8` | 2 / 2 | Owner-approved alias of NT51950 reference flow. |

## Tester Coverage

- `LegacyCombinerPostbuildCatalogTests.CommandLineBuilderMatchesHsiCombinerArgumentShapes` verifies every command against the hsi guide argv contract.
- `LegacyCombinerPostbuildCatalogTests.Nt51927ResolvesExplicitNumericIcCountBranches` verifies 927 single/2IC/3IC branch selection.
- `LegacyCombinerPostbuildCatalogTests.Nt51927PostbuildKeepsDifferentRightNfOffsetsByIcCount` locks the 927 2IC vs 3IC `NF_Ctrlram.bin` split offset difference.
- `LegacyCombinerPostbuildCatalogTests.Nt51917AliasesNt51927PostbuildFlow`, `Nt51919AliasesNt51929PostbuildFlow`, `Nt51928AliasesNt51927PostbuildFlow`, `Nt51929AliasesNt51932PostbuildFlow`, and `Nt51951AliasesNt51950PostbuildFlow` lock owner-approved alias behavior.
- `LegacyCombinerPostbuildCatalogTests.Nt51926CommonFw141SelectsLegacyHeaderCopyTarget` locks Common FW `1.4.1` selection to the `0x32F50` header-copy target and archived `1.4.1` command sizes.
- `LegacyCombinerPostbuildCatalogTests.Nt51930CommonFw1xSelectsSingleLegacyHeaderCommand` locks Common FW `1.x.x` selection to the `1.4.0` command shape with one header-copy command and MP input.
- `LegacyCombinerPostbuildCatalogVersionTests.Nt51930CommonFw1xKeepsLargeCountsOnApprovedCascadeDiffLength` locks NT51930 Common FW `1.x.x` numeric `14` to the approved cascade `DiffDLM` length `0xFE00`.
- `LegacyCombinerPostbuildCatalogTests.Nt51930CascadeUsesLessOrEqual13IcDiffDlmLength` locks current NT51930 Common FW `1.x.x` cascade behavior to the approved `0xFE00` branch.
- `LegacyCombinerPostbuildProcessorTests.TransformRunsNt51927TwoChipMergeAndCrcSequence` verifies the staged processor actually invokes the 927 2IC `MERGE_MODE` and CRC-only sequence.
- `LegacyCombinerPostbuildProcessorTests.TransformStagesBinFilesAndAcceptsDeclaredChanges` verifies the staged processor creates an empty `output/map.txt` for normal-mode real-tool runs.
- `LegacyCombinerPostbuildProcessorTests.TransformUsesStagedSourceOverridesWithoutPrePaste` verifies selected replacement bytes are staged for `BIN/*.bin` without pre-writing them into the firmware image before `Combiner.exe` runs.
- `LegacyCombinerPostbuildRealToolSmokeTests.RealToolRunsNt51927GoldenCrcOnlyWithoutUnexpectedChanges` runs the committed Combiner 1.13.0 executable on Windows against the owner-approved NT51927 golden output. This is a smoke test for the real tool binding, not a Replace golden parity claim.
- `LegacyCombinerPostbuildRealToolSmokeTests.DirectRealToolSixteenByteCasesMatchForSingleAndMultipleCtrlRamSelfReplacement` locks the accepted 16-byte self-replacement behavior for NT51920, NT51923, NT51929, NT51932, NT51950, and NT51951 single/cascade cases. This is still direct-combiner smoke evidence, not production CtrlRAM Replace parity.
- `LegacyCombinerPostbuildRealToolSmokeTests.DirectRealToolPureCombinerPastebackMatchesPrePasteFlow` verifies representative NT51920, NT51923, NT51926, NT51927, and NT51950 cases produce identical output when replacement CtrlRAM bytes are pre-pasted into the work image versus supplied only as Combiner staged source bytes. The production adapter uses the staged-source path.
- `ShellViewModelTests.CtrlRamReplacePreviewSelfReplacementRunsPostbuild` and `CtrlRamReplaceBuildCommitsGoldenBackedSelfReplacementOutput` now inspect the generated Replace report from golden-backed self-replacement: postbuild drift must be accepted `PostbuildCrcHeader` only, and a postbuild-clean self-replacement must produce no output-difference rows.

## Open Evidence Gaps

- The adapter now stages an empty `output/map.txt` for normal and NT-based postbuild commands. Empty map is verified only for no-overlay smoke cases; overlay-enabled firmware may require real map content.
- CtrlRAM Replace accepts a TP work BIN or a declared full-Flash container. The V2 contract clones the complete base, stages only the owner-confirmed zero-based TP prefix for Combiner, and imports that prefix while preserving the container tail. Each IC/map still requires direct expected-output evidence and owner review.
- NT51926 has two inspected postbuild references and workbench now selects between them by FWConfig Common FW version. Production promotion still needs matching expected golden outputs; TP Overview now records the `1.4.1` VN/FWConfig/header-copy differences.
- NT51930 has two inspected postbuild references, but the owner confirmed there is no stable Common FW `2.0.0` project and only one current actual Jira project, INX. Workbench therefore requires and accepts only a `1.x.x` FWConfig Common FW version; the `2.0.0` row is marked `evidence-only` in the hash-pinned catalog and cannot be selected. Cataloged no-Jira variants/counts are not separate missing golden cases and do not imply support. Production promotion is limited to the exact INX project/variant/topology after direct golden manifest/SHA validation, Legacy/V2 full-byte parity, and R3 owner review. TP Overview records that `1.x.x` consumes `MP_Ctrlram.bin`; numeric `2..29` retain the current fail-closed command shape but are not promoted without matching INX evidence.
- NT51931 official BAT shape uses `NT51930BASED_NORMAL_MODE CRC8`, but committed Combiner 1.13.0 crashes on the current standard golden. Diagnostic `NT51931BASED_NORMAL_MODE` has unexplained 108-byte drift, so DP/CtrlRAM/General Replace remain Not Supported while the command catalog is retained as evidence.
- End-to-end production CtrlRAM Replace still needs private golden outputs, declared allowed write ranges, and firmware-owner parity review for each released IC/mode.
