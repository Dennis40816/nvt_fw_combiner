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
| 1 | No test runs the CLI and the GUI path on the same input and compares the outputs. The CLI tests compare the 950 OSD, BOE and Hiway outputs with their stored Golden bytes. The rows that mix CtrlRAM TP inputs (950 cascade, 951) compare the CLI's Normal build with its Dummy build and have no Golden anchor. | `tests/NvtFwCombiner.Bootstrap.Tests/CliTestHarness.cs`; `AbMergeGoldenRegressionTests.PublicHost.cs:43`; `AbMergeCliCommandTests.GoldenBytes.cs:12,17-19,23-25,103-110`; `tests/NvtFwCombiner.GoldenRegression.Tests/GoldenTestHost.cs:38` |
| 2 | Refusals before a run use different exit codes. Standard Merge returns 70, and 1 for one refusal. AB Merge returns 1 or 70. CtrlRAM Replace returns 1. Decision 257 kept this behaviour. | `CliApplication.StandardMerge.cs:81,168,224,251,338`; `AbMergeCliCommandHandler.cs:113,163,180,198,247,270,313`; `ReplaceCliCommandHandler.CtrlRam.cs`; [R14-01](../1.2.3/R14-01.md) Table A |
| 3 | When `--report` cannot be written after the build is committed, the CLI prints "Partial success" on standard error and still exits 0. | `CliCompositionRunSupport.cs:219-250,314` |
| 4 | The GUI reads an IC hint from the file name or header and warns when it does not match. The CLI does not use this check. The caller declares the profile with `--profile`. CtrlRAM and General Replace also need `--ic-num`, and AB Merge takes `--ab-topology`. The CLI still runs the Application input inspection. | `BuiltInFirmwareInspection.cs:245-258,328-338`; `WorkflowSessionPresentationViewModel.FirmwareIcMismatch.cs`; `CliApplication.Usage.cs:12-25,55`; `CliApplication.StandardMerge.cs:39`; `AbMergeCliCommandHandler.cs:47` |
| 5 | A missing `event-buffer-format.v1.json` is not an error: the built-in defaults are used. Only a customised file in the user's local folder changes a result. | `CompositionHostServices.cs:37,133-142`; `EventBufferFormatConfigurationStorage.cs:44-47`; `EventBufferFormatConfigurationSession.cs:107-117` |
| 6 | Every build command (`standard-merge`, `ab-merge`, `general-merge`, `ctrlram-replace`, `general-replace`) accepts `--output <path>`. Decision 230 retires General Replace. The retirement (item R54) is not on the trunk yet, so the command still builds, but this goal adds no option to it. In bundle mode (`--bundle-parent`, `--bundle-name`) only the folder name can be chosen. The file names inside the bundle come from the profile template. The GUI lets the user edit the primary file name, the additional file name and the bundle folder name. | `CliApplication.Usage.cs`; `CliBundleOptions.cs:9,43`; `OutputDeliveryConfirmationViewModel.NameEditing.cs:9,22,28` |
| 7 | There is no user guide for the CLI. The usage text lists the commands, and [R14-01](../1.2.3/R14-01.md) Table A documents commands, options and exit codes as a gap inventory. | `CliApplication.Usage.cs`; [R14-01](../1.2.3/R14-01.md) |
| 8 | Builds that run the legacy processor work only on Windows. The CLI has no OS check of its own. Release Check runs on Windows, so no change is needed. | `LegacyCombinerPostbuildProcessor`; ADR 0006 |
| 9 | The 950 AB Merge profile is an executable candidate. Its blockers are Golden certification closure and a firmware-owner review. | `profiles/built-in/nt51950-ab-merge/profiles/nt51950-ab-merge.json:7-19` |

## Planned work (after the current work, in this order)

| # | Work | Risk | Effort |
| --- | --- | --- | --- |
| P1 | Parity test: run the CLI and the GUI's runner on the 950 Golden cases and compare the complete output bytes and every semantic report field. Both runs use the same injected clock, the Bundled runtime (a non-bundled runtime adds a GUID to its deployment path in `ExternalRuntimeDeployment.cs`) and separate empty output folders. `CreateRunId` always adds a new GUID, so the test needs a run-ID seam in Application first. The test names every run-specific field it normalizes: `RunId`, `StartedAtUtc`, `CompletedAtUtc`, `OutputNaming.ResolvedAtUtc`, `BundleDelivery.ResolvedDirectory`, and the paths in `Operations[*].ExecutedCommands[*]`. Any other difference fails the test. The OSD case `nt51950-ab-osd-d03t02-20260924` is owner certified. CLI tests already compare the OSD, BOE and Hiway outputs with their Golden outputs. What is missing is the CLI-against-GUI comparison. The 950 cascade case and the 951 mixed TP/DP case have no Golden output, so their comparison is CLI against GUI only and is not Golden evidence. | R3 for the run-ID seam: `src/NvtFwCombiner.Application/Composition/**` is a firmware-owner path. The test goes in `tests/NvtFwCombiner.UiSmoke.Tests`, which already hosts the GUI path (`MergePresentationViewModel` builds the `AcceptedCompositionExecutionRequest`), so it exercises the real Presentation request assembly. `UiSmoke.Tests` has no reference to `NvtFwCombiner.Cli` today (only `Bootstrap.Tests` does, with `CliTestHarness`), so the test either runs the built CLI as a child process or adds a `NvtFwCombiner.Cli` project reference that `ProjectDependencyTests` must allow. It only reads the existing Golden fixtures (R1). A test in `tests/NvtFwCombiner.GoldenRegression.Tests` or any change under `testdata/` would be R3 | 1 day |
| P2 | Exit codes, step 1: document the current exit codes of every command in one table. | R0 | 0.5 day |
| P3 | Exit codes, step 2: one pull request unifies the codes and replaces decision 257. | R3: `AbMergeCliCommandHandler.cs` and `ReplaceCliCommandHandler.CtrlRam.cs` are firmware-owner paths in the authority policy | 1 day |
| P4 | A report that cannot be written after a committed build gets its own non-zero exit code. Today every build path ignores the report result and returns from `result.Succeeded`: Standard Merge (`CliApplication.StandardMerge.cs`), General Merge (`MergeCliCommandHandler.cs`), AB Merge (`AbMergeCliCommandHandler.cs`) and the shared Replace path (`ReplaceCliCommandHandler.RunSupport.cs`, CtrlRAM and General Replace). `CliCompositionRunSupport.WriteReportJsonAsync` returns `Task` and only prints an issue when the write fails after commit, so it first changes to return that outcome; then all four callers change, each with a failure test. | R3 for `AbMergeCliCommandHandler.cs` (firmware-owner path); R2 (CLI contract) for `CliCompositionRunSupport.cs` and the other three | 0.5 day |
| P5 | Output names: in bundle mode the caller can set the primary file name, like the GUI. The additional file name can be set only where the command produces an additional delivery; today that is AB Merge with `--include-a-flashcode`, because `CompositionOutputBundleIntent` refuses an additional name otherwise. A test checks each supported build command: Standard Merge, AB Merge, General Merge and CtrlRAM Replace. General Replace gets no new option, because decision 230 retires it. `--help` states every naming option. | R3 for the AB Merge and CtrlRAM handlers (firmware-owner paths); R1 for the others | 1 day |
| P6 | IC check: the GUI's mismatch decision lives in Presentation today (`WorkflowSessionPresentationViewModel.ReconcileFirmwareIcMismatch`), and it skips the Perfect-family members named by a file-name hint. First move the complete predicate and a typed result into one Application owner. Then the GUI and the CLI both use it. It warns by default. An option for Release Check makes a mismatch a refusal with a non-zero exit code. The check only confirms the declared profile and never chooses it, because support and family are declared facts. | R3: the pieces it moves or extends live in `Application/Capabilities/CanonicalCapabilityExperience.cs` (`ArePerfectFamilyMembers`) and `Application/Composition/CompositionClientModels.cs` (`FirmwareInspectionSnapshot`, `FirmwareIcHintSource`), both firmware-owner paths | 1 to 2 days |
| P7 | AB Merge reports already carry the event-buffer format summary that was captured at run time (`CompositionRunReport.AbMergeFormat`, ADR 0072). Add a CLI test that the summary is present, and document it. Add no second projection of the setting. | R1 | 0.5 day |
| P8 | One CLI user guide: commands, options, exit codes and report format, built from R14-01 Table A and the final exit-code table. | R0 | 0.5 day |

The total is about 6 to 7 working days. The basis is the size of earlier CLI changes, such as the R14-01 exit-code
table and the AB Merge CLI tests. P6 is the least certain: it depends on how much of the GUI's inspection the
Application layer already shares. Release Check must not report 950 as certified before the firmware owner
approves it (R3).

This work has no version yet. `docs/architecture/nfc_roadmap.md` owns version allocation; its CLI row still
points at `1.2.3`. The commander updates that row and links this page when the work is allocated after the
current work.
