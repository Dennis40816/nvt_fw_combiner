# CtrlRAM Postbuild Command Matrix

This matrix records the normalized Combiner command sequences used after CtrlRAM Replace. The living experiment and conclusion tracker is [`ctrlram-replace-status-report.md`](ctrlram-replace-status-report.md).

`0.10.x` target amendment (2026-07-27): ADR 0042/#221 retire NT51920,
NT51925, NT51930, and NT51931; their rows remain legacy `0.9.x` command
evidence only. For #219/#188, composition scatters only the declared `N - 1`
active DLM prefixes from the selected DiffDLM payload. The AE suffix after the
active prefix does not enter the read set or write set. Every active Diff NF
tail and every inactive target record remains byte-identical to the immutable
reference before the command sequence below runs. NT51923/NT51926 and the
NT51927 TP family retain full-artifact DiffDLM replacement.

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
| NT51930 | single/range | sole `[1.0.0,infinity)` profile: single, cascade `2..13` | `NT51930BASED_NORMAL_MODE CRC8` | 1 / 1 | One runtime profile sourced from 1.4.0 covers every Common FW version from 1.0.0 onward and does not require the informational version to select it. The cascade plan consumes MP and `DiffDLM` length `0xFE00`; count 14 and above are unavailable because no distinct owner plan is implemented. The inspected 2.0.0 BAT and `0x23000` extended branch remain hash-pinned evidence only. |
| NT51931 | single/cascade | single, cascade | `NT51931BASED_NORMAL_MODE CRC8` | 1 / 1 | Registered Combiner 1.13.0 selected after full-byte equality with the owner 1.2.0.4/51930-based control. Exact AUTO_PRJ-158/PID `0x131B`/cascade-6 V1/V2 parity is materialized; support remains neutral. |
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
- `LegacyCombinerPostbuildCatalogVersionTests.Nt51930CountsTwoThroughThirteenUseSameCascadeCommands` locks the complete admitted `2..13` range to one cascade command plan and rejects count 14.
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
- NT51930 has two inspected postbuild references, but the owner confirmed there is no stable Common FW `2.0.0` runtime profile and only one current actual Jira project, INX. The sole runtime profile covers `[1.0.0,infinity)` and missing informational Common FW does not block it; the inspected `2.0.0` row stays evidence-only. Runtime exposes only single and `2..13`; count 14 and above remain unavailable until a distinct owner command plan is implemented. Golden INX metadata remains regression evidence and a promotion gate, not production admission authority.
- NT51931 has conflicting owner BAT provenance: the 2026-07-17 BAT uses `NT51931BASED_NORMAL_MODE`, while the 2026-07-18 BAT uses `NT51930BASED_NORMAL_MODE`. The owner-selected 2026-07-19 experiment fixed the same expected-derived base, physical inputs, lengths, working directory, and argv order: registered 1.13.0/51931-based and owner 1.2.0.4/51930-based both produced SHA `f38fdecd...c594` with zero cross-output differences. Both differ from the starting owner expected only in the same 108 header/header-copy CRC bytes. The 1.2.0.4 executable remains uncommitted; 1.13.0/51931-based is the selected catalog rule.
- End-to-end production CtrlRAM Replace still needs private golden outputs, declared allowed write ranges, and firmware-owner parity review for each released IC/mode.
