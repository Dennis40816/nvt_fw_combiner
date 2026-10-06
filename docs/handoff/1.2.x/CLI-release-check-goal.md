# CLI goal for Release Check: consistent, smarter, renamable output

Date: 2026-10-06. Owner decision: [decision 331](../1.2.x.md). Source read: `origin/1.2.x` at `83f0b4e2c`.
Nothing was built or run for this evaluation. No implementation is scheduled before the current work
(the NVT Core extraction until 2026-10-18) ends.

## Why

The owner is evaluating running NT51950 (950) builds in Release Check through the NFC command line.
The owner said: 「只是評估 我認為一致性與更加智能以及輸出命名這塊都要更加完善 紀錄為 NFC 目標排入吧」.
The NFC goal is:

- the CLI and the GUI give the same output for the same input;
- the CLI checks its inputs as well as the GUI does;
- the CLI lets the caller choose every output name.

The CLI already builds the 950 AB Merge cases on Windows through the same Application services as the GUI.

## Findings

| # | Finding | Evidence |
| --- | --- | --- |
| 1 | No test runs the CLI and the GUI path on the same input and compares the outputs. Each side compares only with stored Golden bytes. | `tests/NvtFwCombiner.Bootstrap.Tests/CliTestHarness.cs`, `CliGoldenByteEvidence.cs`; `tests/NvtFwCombiner.GoldenRegression.Tests/GoldenTestHost.cs:38` |
| 2 | Refusals before a run use different exit codes. Standard Merge returns 70, and 1 for one refusal. AB Merge returns 1 or 70. CtrlRAM Replace returns 1. Decision 257 kept this behaviour. | `CliApplication.StandardMerge.cs:81,168,224,251,338`; `AbMergeCliCommandHandler.cs:113,163,180,198,247,270,313`; `ReplaceCliCommandHandler.CtrlRam.cs`; [R14-01](../1.2.3/R14-01.md) Table A |
| 3 | When `--report` cannot be written after the build is committed, the CLI prints "Partial success" on standard error and still exits 0. | `CliCompositionRunSupport.cs:219-250,314` |
| 4 | The GUI reads an IC hint from the file name or header and warns when it does not match. The CLI does not use this check. The caller gives `--profile` and `--ic-num`. The CLI still runs the Application input inspection. | `BuiltInFirmwareInspection.cs:245-258,328-338`; `WorkflowSessionPresentationViewModel.FirmwareIcMismatch.cs`; `CliApplication.Usage.cs:12-25,55` |
| 5 | A missing `event-buffer-format.v1.json` is not an error: the built-in defaults are used. Only a customised file in the user's local folder changes a result. | `CompositionHostServices.cs:37,133-142`; `EventBufferFormatConfigurationStorage.cs:44-47`; `EventBufferFormatConfigurationSession.cs:107-117` |
| 6 | Every build command (`standard-merge`, `ab-merge`, `general-merge`, `ctrlram-replace`, `general-replace`) accepts `--output <path>`. In bundle mode (`--bundle-parent`, `--bundle-name`) only the folder name can be chosen. The file names inside the bundle come from the profile template. The GUI lets the user edit the primary file name, the additional file name and the bundle folder name. | `CliApplication.Usage.cs`; `CliBundleOptions.cs:9,43`; `OutputDeliveryConfirmationViewModel.NameEditing.cs:9,22,28` |
| 7 | There is no CLI document beyond the usage text. | `CliApplication.Usage.cs` |
| 8 | Builds that run the legacy processor work only on Windows. The CLI has no OS check of its own. Release Check runs on Windows, so no change is needed. | `LegacyCombinerPostbuildProcessor`; ADR 0006 |
| 9 | The 950 AB Merge profile is an executable candidate. Its blockers are Golden certification closure and a firmware-owner review. | `profiles/built-in/nt51950-ab-merge/profiles/nt51950-ab-merge.json:7-19` |

## Planned work (after the current work, in this order)

| # | Work | Risk | Effort |
| --- | --- | --- | --- |
| P1 | Parity test: run the CLI and the GUI's runner on the 950 Golden cases and compare the complete outputs and reports. The OSD case `nt51950-ab-osd-d03t02-20260924` is owner certified. The BOE and Hiway cases already run in the CLI byte test. The 950 cascade case and the 951 mixed TP/DP case have no Golden output, so their comparison is CLI against GUI only and is not Golden evidence. | R1 (tests only) | 1 day |
| P2 | Exit codes, step 1: document the current exit codes of every command in one table. | R0 | 0.5 day |
| P3 | Exit codes, step 2: one pull request unifies the codes and replaces decision 257. | R2 (CLI contract) | 1 day |
| P4 | A report that cannot be written after a committed build gets its own non-zero exit code. | R2 (CLI contract) | 0.5 day |
| P5 | Output names: in bundle mode the caller can set the primary and additional file names, like the GUI. A test checks each of the five build commands. `--help` states every naming option. | R1; R3 if a profile naming template changes | 1 day |
| P6 | IC check: the CLI runs the same Application check as the GUI. It warns by default. An option for Release Check makes a mismatch a refusal with a non-zero exit code. The check only confirms the declared profile and never chooses it, because support and family are declared facts. | R2 (reuses the GUI's existing owner) | 1 to 2 days |
| P7 | The report states which event-buffer setting a run used. | R1 | 0.5 day |
| P8 | One CLI page: commands, options, exit codes and report format. | R0 | 0.5 day |

The total is about 6 to 7 working days. The basis is the size of earlier CLI changes, such as the R14-01 exit-code
table and the AB Merge CLI tests. P6 is the least certain: it depends on how much of the GUI's inspection the
Application layer already shares. Release Check must not report 950 as certified before the firmware owner
approves it (R3).
