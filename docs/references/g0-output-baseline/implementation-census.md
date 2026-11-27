# G0-5 implementation census

Commit: `46e2b071f497939e34d50f5f69ae3c0e14337752`.
Date: 2026-11-27.
This is static inspection, not runtime or parity evidence.
This is a partial census.
Counts marked `+` are lower bounds.
Listed files are selected owners and supporting implementation files, not a proven exhaustive list.
A total counts the whole file once within a concept, including unrelated lines.
Files may occur in more than one concept.
Owner candidates are not approved ownership decisions.
Non-blank lines include comments and markup.
The count includes UTF-8 text files under src, and excludes directories named bin or obj.
Binary files count as files but have no line count.

```python
from pathlib import Path
from collections import defaultdict
totals = defaultdict(lambda: [0, 0, 0])
for p in Path("src").rglob("*"):
    if not p.is_file() or {"bin", "obj"} & set(p.parts):
        continue
    key = p.parts[1] if len(p.parts) > 2 else "(src root)"
    totals[key][0] += 1
    try:
        lines = p.read_text(encoding="utf-8-sig").splitlines()
    except UnicodeDecodeError:
        totals[key][2] += 1
        continue
    totals[key][1] += sum(bool(line.strip()) for line in lines)
for key, values in sorted(totals.items()):
    print(key, *values)
```

## 1. DP version

Owner candidates: CompiledInputArtifactObservationService; DpcmiMetadataProjector.
Implementation count: 2.
Two decoding paths use different representations.
Correction: The plan omits naming consumers and the metadata adapter.

Selected files and non-blank lines:

- `src/NvtFwCombiner.Application/Composition/AbCodeOutputNameResolver.cs`: 283 lines; declared types: `AbCodeOutputNameResolver`, `TokenResolution`, `OutputNameResolution`.
- `src/NvtFwCombiner.Application/Composition/CompiledOutputNameResolver.cs`: 359 lines; declared types: `CompiledOutputNameResolver`, `TokenResolution`.
- `src/NvtFwCombiner.Application/InputInspection/CompiledInputArtifactObservationService.cs`: 207 lines; declared types: `CompiledInputVersionKind`, `CompiledInputVersionObservation`, `CompiledInputArtifactInspectionAdvisory`, `CompiledInputArtifactObservationResult`, `CompiledInputArtifactObservationService`.
- `src/NvtFwCombiner.Application/Metadata/DpcmiMetadataProjector.cs`: 128 lines; declared types: `DpcmiMetadataContract`, `DpcmiMetadataFacts`, `DpcmiMetadataProjector`.
- `src/NvtFwCombiner.Infrastructure/Composition/BuiltInFirmwareInspection.Metadata.cs`: 283 lines; declared types: `BuiltInFirmwareInspection`.

Selected file total: 1,260 lines.

## 2. Output naming

Owner candidates: CompiledOutputNameResolver; AbCodeOutputNameResolver.
Implementation count: 2.
The compiled resolver dispatches AB to its separate naming logic. The accepted-session resolver is a wrapper.
Correction: The plan omits AcceptedSessionOutputNameResolver.

Selected files and non-blank lines:

- `src/NvtFwCombiner.Application/Composition/AbCodeOutputNameResolver.cs`: 283 lines; declared types: `AbCodeOutputNameResolver`, `TokenResolution`, `OutputNameResolution`.
- `src/NvtFwCombiner.Application/Composition/AcceptedSessionOutputNameResolver.cs`: 172 lines; declared types: `AcceptedSessionOutputNameResolver`.
- `src/NvtFwCombiner.Application/Composition/CompiledOutputNameResolver.cs`: 359 lines; declared types: `CompiledOutputNameResolver`, `TokenResolution`.
- `src/NvtFwCombiner.Application/Composition/CompositionOutputNamingExperience.cs`: 126 lines; declared types: `CompositionOutputNamingExperience`.

Selected file total: 940 lines.

## 3. Input length

Owner candidates: CompiledInputArtifactInspectionService; CompositionEngine.
Implementation count: 2+.
Inspection validates compiled requirements. The engine independently validates input spaces and applies padding or truncation.
Correction: The four numbers in the plan count declarations, not four code paths.

Selected files and non-blank lines:

- `src/NvtFwCombiner.Application/Composition/CompositionRunService.Inputs.cs`: 366 lines; declared types: `CompositionRunService`, `BoundInputs`, `InputDiagnosticIssue`, `ArtifactReadSnapshot`.
- `src/NvtFwCombiner.Application/InputInspection/CompiledInputArtifactInspectionService.cs`: 461 lines; declared types: `InputArtifactInspectionIssueCodes`, `CompiledInputArtifactInspectionSeverity`, `CompiledInputArtifactInspectionNextAction`, `CompiledInputArtifactInspectionResult`, `CompiledInputArtifactInspectionService`.
- `src/NvtFwCombiner.Domain/Composition/AddressSpace.cs`: 113 lines; declared types: `AddressSpace`.
- `src/NvtFwCombiner.Domain/Composition/CompiledInputSlotRequirement.cs`: 334 lines; declared types: `accepted`, `CompiledInputArtifactClass`, `CompiledInputSlotCardinality`, `CompiledInputInstancePolicy`, `CompiledInputLengthRequirement`, `CompiledExactBytesInputLengthRequirement`, `CompiledExactResolvedMapCapacityInputLengthRequirement`, `CompiledBoundedInputLengthRequirement`, `CompiledSourceViewCoverageInputLengthRequirement`, `CompiledInputNormalization`, `CompiledNoInputNormalization`, `CompiledPadShorterInputNormalization`, `CompiledTruncateCtrlRamInputNormalization`, `CompiledInputSlotRequirement`, `CompiledInputSpaceBinding`, `CompiledInputContract`.
- `src/NvtFwCombiner.Domain/Composition/CompositionEngine.Inputs.cs`: 168 lines; declared types: `CompositionEngine`, `NormalizedExecutionInputs`.
- `src/NvtFwCombiner.Domain/Composition/CompositionInputDefinitions.cs`: 309 lines; declared types: `CanonicalPolicyValueRules`, `InputLengthRequirementDefinition`, `ResolvedMapCapacityInputLengthDefinition`, `SourceViewCoverageInputLengthDefinition`, `CompositionInputSlotDefinition`.
- `src/NvtFwCombiner.Profiles/V2/CompositionProfileNormalizer.Input.cs`: 139 lines; declared types: `CompositionProfileNormalizer`.
- `src/NvtFwCombiner.Profiles/V2/V2CompositionPlanCompiler.ContractLowering.cs`: 267 lines; declared types: `V2CompositionPlanCompiler`.
- `src/NvtFwCombiner.Profiles/V2/V2CompositionPlanCompiler.InputGeometry.cs`: 316 lines; declared types: `V2CompositionPlanCompiler`.
- `src/NvtFwCombiner.Profiles/V2/V2CompositionPlanCompiler.RuntimeReferenceReplace.cs`: 524 lines; declared types: `V2CompositionPlanCompiler`, `RuntimeReferenceReplaceProfileShape`, `RuntimeFirmwareVersionEditLowering`.

Selected file total: 2,997 lines.

## 4. LDC

Owner candidates: V2CompositionPlanCompiler.
Implementation count: 1+.
A generic compiler path handles declared selection and lowering. Further LDC call-chain tracing is needed.
Correction: The NT51928 map variants are data alternatives. The proposed 951 behavior is not evidence of an existing implementation.

Selected files and non-blank lines:

- `src/NvtFwCombiner.Application/Capabilities/CanonicalCapabilityCompiler.StandardMerge.cs`: 122 lines; declared types: `IStandardMergeCompilationPort`, `IStandardMergeMetadataPlanQuery`, `CanonicalCapabilityCompilerAdapter`.
- `src/NvtFwCombiner.Application/Capabilities/CanonicalCapabilityCompiler.StandardMerge.Routing.cs`: 412 lines; declared types: `CanonicalCapabilityCompilerAdapter`, `StandardSourceEnvelopeRoutes`.
- `src/NvtFwCombiner.Application/Capabilities/CanonicalCapabilityCompiler.StandardMerge.VersionSelection.cs`: 57 lines; declared types: `CanonicalCapabilityCompilerAdapter`.
- `src/NvtFwCombiner.Infrastructure/Composition/CanonicalDynamicRouteInventory.cs`: 516 lines; declared types: `CanonicalDynamicRouteInventory`, `CanonicalCtrlRamDefinition`.
- `src/NvtFwCombiner.Infrastructure/FlashMaps/BuiltInTpFlashMapCatalog.Selection.cs`: 134 lines; declared types: `BuiltInTpFlashMapCatalog`, `Catalog`, `Slot`.
- `src/NvtFwCombiner.Profiles/V2/V2CompositionPlanCompiler.cs`: 529 lines; declared types: `V2CompositionPlanCompileResult`, `V2CompositionPlanCompiler`, `ResolvedView`, `ResolvedRegionAccessRule`, `LoweredRegionAccess`.
- `src/NvtFwCombiner.Profiles/V2/V2CompositionPlanCompiler.InputSelection.cs`: 249 lines; declared types: `V2CompositionPlanCompiler`, `ResolvedInputSelection`.

Selected file total: 2,019 lines.

## 5. Merge and Replace

Owner candidates: V2CompositionPlanCompiler; CompositionEngine.
Implementation count: 2.
Blank and reference clone are two initialization alternatives in the shared model.
Correction: The discriminator named in the plan is not an implementation type.

Selected files and non-blank lines:

- `src/NvtFwCombiner.Domain/Composition/CompositionEngine.cs`: 69 lines; declared types: `CompositionEngine`.
- `src/NvtFwCombiner.Domain/Composition/CompositionEngine.Inputs.cs`: 168 lines; declared types: `CompositionEngine`, `NormalizedExecutionInputs`.
- `src/NvtFwCombiner.Domain/Composition/CompositionPlan.Initialization.cs`: 94 lines; declared types: `CompositionPlan`.
- `src/NvtFwCombiner.Profiles/V2/V2CompositionPlanCompiler.Initializations.cs`: 105 lines; declared types: `V2CompositionPlanCompiler`.
- `src/NvtFwCombiner.Profiles/V2/V2CompositionPlanCompiler.RuntimeReferenceReplace.cs`: 524 lines; declared types: `V2CompositionPlanCompiler`, `RuntimeReferenceReplaceProfileShape`, `RuntimeFirmwareVersionEditLowering`.

Selected file total: 960 lines.

## 6. Mode authoring

Owner candidates: StandardMergeAuthoringExperience; AbMergeAuthoringExperience; CtrlRamAuthoringExperience; GeneralAuthoringExperience.
Implementation count: 4.
Four mode frontends are counted. The shared workflow is not a fifth frontend.
Correction: The plan total also includes 409 Saved Rule lines. Those lines are outside the four-mode plus shared-workflow set.

Selected files and non-blank lines:

- `src/NvtFwCombiner.Application/Authoring/AbMergeAuthoringExperience.cs`: 207 lines; declared types: `AbMergeAuthoringExperience`, `AbMergeAuthoringResolver`.
- `src/NvtFwCombiner.Application/Authoring/AbMergeAuthoringExperience.Format.cs`: 299 lines; declared types: `AbMergeAuthoringExperience`, `FormatResolution`.
- `src/NvtFwCombiner.Application/Authoring/AbMergeAuthoringExperience.InputInspection.cs`: 143 lines; declared types: `AbMergeAuthoringExperience`, `AbInspectionContext`.
- `src/NvtFwCombiner.Application/Authoring/AbMergeAuthoringExperience.Readiness.cs`: 131 lines; declared types: `AbMergeAuthoringExperience`.
- `src/NvtFwCombiner.Application/Authoring/CompiledAuthoringWorkflow.Contracts.cs`: 104 lines; declared types: `ICompiledAuthoringWorkflowResolver`, `CompiledAuthoringWorkflowDiscovery`, `CompiledAuthoringWorkflowResolution`, `CompiledAuthoringSelectedInput`, `CompiledAuthoringInputBinding`, `CompiledAuthoringSelectionSnapshot`, `CompiledAuthoringInspectionBatch`, `CompiledAuthoringSessionPreparation`.
- `src/NvtFwCombiner.Application/Authoring/CompiledAuthoringWorkflow.cs`: 711 lines; declared types: `CompiledAuthoringWorkflowService`.
- `src/NvtFwCombiner.Application/Authoring/CompiledAuthoringWorkflow.Selection.cs`: 295 lines; declared types: `CompiledAuthoringWorkflowService`.
- `src/NvtFwCombiner.Application/Authoring/CtrlRamAuthoringDraftState.cs`: 45 lines; declared types: `CtrlRamAuthoringDraftState`, `AbCtrlRamBankSelection`, `AbCtrlRamDraftState`.
- `src/NvtFwCombiner.Application/Authoring/CtrlRamAuthoringExperience.cs`: 254 lines; declared types: `CtrlRamAuthoringExperience`.
- `src/NvtFwCombiner.Application/Authoring/CtrlRamAuthoringExperience.Inspection.cs`: 218 lines; declared types: `CtrlRamAuthoringExperience`.
- `src/NvtFwCombiner.Application/Authoring/CtrlRamAuthoringExperience.Transition.cs`: 167 lines; declared types: `CtrlRamAuthoringExperience`.
- `src/NvtFwCombiner.Application/Authoring/GeneralAuthoringAdmissionEvaluator.cs`: 516 lines; declared types: `GeneralAuthoringAdmissionResult`, `GeneralAuthoringAdmission`, `GeneralAuthoringAdmissionSummary`.
- `src/NvtFwCombiner.Application/Authoring/GeneralAuthoringAdmissionUseCase.cs`: 162 lines; declared types: `GeneralAuthoringAdmissionRequest`, `GeneralAuthoringAdmissionUseCase`.
- `src/NvtFwCombiner.Application/Authoring/GeneralAuthoringExperience.Contracts.cs`: 62 lines; declared types: `IGeneralAuthoringPlanner`, `IRuntimeDependencyReadinessLeaseProvider`, `RuntimeDependencyReadinessLease`, `GeneralReplaceRuntimeAuthority`, `GeneralMergeAuthoringPlan`, `GeneralMergeAuthoringPlanResult`, `GeneralReplaceAuthoringPlan`, `GeneralReplaceAuthoringPlanResult`.
- `src/NvtFwCombiner.Application/Authoring/GeneralAuthoringExperience.cs`: 551 lines; declared types: `GeneralAuthoringExperience`.
- `src/NvtFwCombiner.Application/Authoring/GeneralAuthoringExperience.Results.cs`: 56 lines; declared types: `GeneralAuthoringExperience`.
- `src/NvtFwCombiner.Application/Authoring/GeneralAuthoringMappingUseCase.cs`: 158 lines; declared types: `GeneralAuthoringMappingUseCase`.
- `src/NvtFwCombiner.Application/Authoring/GeneralAuthoringResourceLimits.cs`: 612 lines; declared types: `GeneralAuthoringIssueCodes`, `GeneralAuthoringTechnicalLimits`, `GeneralInputResource`, `GeneralSlotLengthLimits`, `GeneralResourceLimits`, `GeneralTrustedParentResourcePolicy`, `GeneralSavedRuleResourcePolicy`, `GeneralOccupancySegment`, `GeneralAuthoringAdmissionIssue`, `GeneralResourceResolutionResult`, `GeneralResourceLimitResolver`.
- `src/NvtFwCombiner.Application/Authoring/GeneralMappingDraftState.cs`: 476 lines; declared types: `GeneralMappingSourceKind`, `GeneralMappingFileRangePreset`, `GeneralInlineSourceCodec`, `GeneralMappingSource`, `GeneralMappingDraftRow`, `GeneralMappingDraftState`.
- `src/NvtFwCombiner.Application/Authoring/GeneralMergeAuthoringUseCase.cs`: 53 lines; declared types: `GeneralMergeAuthoringUseCase`.
- `src/NvtFwCombiner.Application/Authoring/GeneralMergeDraftState.cs`: 105 lines; declared types: `GeneralMergeInitializerIssueCodes`, `GeneralMergeInitializerInput`, `GeneralMergeDraftState`.
- `src/NvtFwCombiner.Application/Authoring/GeneralSelectedFileInspection.cs`: 150 lines; declared types: `GeneralSelectedFileInspection`, `GeneralSelectedFileInspectionIssue`, `GeneralSelectedFileInspectionResult`, `GeneralSelectedFileInspectionIssueCodes`, `GeneralSelectedFileInspectionService`.
- `src/NvtFwCombiner.Application/Authoring/StandardMergeAuthoringExperience.cs`: 224 lines; declared types: `StandardMergeAuthoringExperience`, `StandardMergeAuthoringResolver`.
- `src/NvtFwCombiner.Application/Authoring/StandardMergeAuthoringExperience.InputInspection.cs`: 47 lines; declared types: `StandardMergeAuthoringExperience`.

Selected file total: 5,746 lines.

## 7. CLI commands

Owner candidates: CliApplication; AbMergeCliCommandHandler; MergeCliCommandHandler; ReplaceCliCommandHandler; SavedRuleCliCommandHandler.
Implementation count: 5.
Five command entry paths are counted. Partial files and support wrappers are not additional handlers.
Correction: Standard Merge lives in CliApplication. SavedRuleCliSupport accounts for 46 of the 183 Saved Rule lines.

Selected files and non-blank lines:

- `src/NvtFwCombiner.Cli/AbMergeCliCommandHandler.cs`: 427 lines; declared types: `AbMergeCliCommandHandler`.
- `src/NvtFwCombiner.Cli/AbMergeCliCommandHandler.Dummy.cs`: 54 lines; declared types: `AbMergeCliCommandHandler`.
- `src/NvtFwCombiner.Cli/AbMergeCliCommandHandler.Usage.cs`: 11 lines; declared types: `AbMergeCliCommandHandler`.
- `src/NvtFwCombiner.Cli/CliApplication.StandardMerge.cs`: 375 lines; declared types: `CliApplication`.
- `src/NvtFwCombiner.Cli/MergeCliCommandHandler.cs`: 176 lines; declared types: `MergeCliCommandHandler`.
- `src/NvtFwCombiner.Cli/MergeCliCommandHandler.ManualMappings.cs`: 144 lines; declared types: `MergeCliCommandHandler`.
- `src/NvtFwCombiner.Cli/MergeCliCommandHandler.Options.cs`: 71 lines; declared types: `MergeCliCommandHandler`, `ParsedOptions`.
- `src/NvtFwCombiner.Cli/MergeCliCommandHandler.Result.cs`: 61 lines; declared types: `MergeCliCommandHandler`.
- `src/NvtFwCombiner.Cli/MergeCliCommandHandler.SavedRules.cs`: 38 lines; declared types: `MergeCliCommandHandler`.
- `src/NvtFwCombiner.Cli/MergeCliCommandHandler.Usage.cs`: 12 lines; declared types: `MergeCliCommandHandler`.
- `src/NvtFwCombiner.Cli/ReplaceCliCommandHandler.cs`: 118 lines; declared types: `ReplaceCliCommandHandler`.
- `src/NvtFwCombiner.Cli/ReplaceCliCommandHandler.CtrlRam.Choices.cs`: 138 lines; declared types: `ReplaceCliCommandHandler`, `CtrlRamCliChoices`.
- `src/NvtFwCombiner.Cli/ReplaceCliCommandHandler.CtrlRam.cs`: 211 lines; declared types: `ReplaceCliCommandHandler`.
- `src/NvtFwCombiner.Cli/ReplaceCliCommandHandler.CtrlRam.Slots.cs`: 113 lines; declared types: `ReplaceCliCommandHandler`, `CtrlRamSlotArgument`.
- `src/NvtFwCombiner.Cli/ReplaceCliCommandHandler.General.cs`: 424 lines; declared types: `ReplaceCliCommandHandler`.
- `src/NvtFwCombiner.Cli/ReplaceCliCommandHandler.Options.cs`: 18 lines; declared types: `ReplaceCliCommandHandler`.
- `src/NvtFwCombiner.Cli/ReplaceCliCommandHandler.Report.cs`: 68 lines; declared types: `ReplaceCliCommandHandler`.
- `src/NvtFwCombiner.Cli/ReplaceCliCommandHandler.Result.cs`: 12 lines; declared types: `ReplaceCliCommandHandler`.
- `src/NvtFwCombiner.Cli/ReplaceCliCommandHandler.RunSupport.cs`: 95 lines; declared types: `ReplaceCliCommandHandler`.
- `src/NvtFwCombiner.Cli/ReplaceCliCommandHandler.Usage.cs`: 26 lines; declared types: `ReplaceCliCommandHandler`.
- `src/NvtFwCombiner.Cli/SavedRuleCliCommandHandler.cs`: 98 lines; declared types: `SavedRuleCliCommandHandler`.
- `src/NvtFwCombiner.Cli/SavedRuleCliCommandHandler.V2.cs`: 39 lines; declared types: `SavedRuleCliCommandHandler`.
- `src/NvtFwCombiner.Cli/SavedRuleCliSupport.cs`: 46 lines; declared types: `SavedRuleCliSupport`.

Selected file total: 2,775 lines.

## 8. GUI launch

Owner candidates: MainWindow; UiCompositionRunner.
Implementation count: 3+.
Three mode-specific MainWindow launch paths are visible. General execution requires further call-chain tracing.
Correction: The plan omits General presentation execution paths.

Selected files and non-blank lines:

- `src/NvtFwCombiner.Presentation.Avalonia/MainWindow.AbMergeLaunch.cs`: 38 lines; declared types: `MainWindow`.
- `src/NvtFwCombiner.Presentation.Avalonia/MainWindow.CtrlRamLaunch.cs`: 111 lines; declared types: `MainWindow`.
- `src/NvtFwCombiner.Presentation.Avalonia/MainWindow.StandardMergeLaunch.cs`: 31 lines; declared types: `MainWindow`.
- `src/NvtFwCombiner.Presentation.Avalonia/UiCompositionRunner.Catalog.cs`: 33 lines; declared types: `UiCompositionRunner`.
- `src/NvtFwCombiner.Presentation.Avalonia/UiCompositionRunner.Common.cs`: 386 lines; declared types: `UiCompositionRunner`.
- `src/NvtFwCombiner.Presentation.Avalonia/UiCompositionRunner.FirmwareFacts.cs`: 233 lines; declared types: `UiCompositionRunner`.
- `src/NvtFwCombiner.Presentation.Avalonia/UiCompositionRunner.Replace.cs`: 40 lines; declared types: `UiCompositionRunner`.
- `src/NvtFwCombiner.Presentation.Avalonia/UiLaunchOptions.AbMerge.cs`: 41 lines; declared types: `AbMergeLaunchRequest`, `UiLaunchOptions`.
- `src/NvtFwCombiner.Presentation.Avalonia/UiLaunchOptions.Capture.cs`: 121 lines; declared types: `UiLaunchOptions`, `CaptureLaunchRequest`, `LaunchParseState`.
- `src/NvtFwCombiner.Presentation.Avalonia/UiLaunchOptions.cs`: 337 lines; declared types: `UiLaunchOptions`, `WorkflowInputDisposition`, `LaunchParseState`.
- `src/NvtFwCombiner.Presentation.Avalonia/UiLaunchOptions.CtrlRam.cs`: 93 lines; declared types: `CtrlRamLaunchInput`, `CtrlRamLaunchRequest`, `UiLaunchOptions`.
- `src/NvtFwCombiner.Presentation.Avalonia/UiLaunchOptions.StandardMerge.cs`: 41 lines; declared types: `StandardMergeLaunchRequest`, `UiLaunchOptions`.
- `src/NvtFwCombiner.Presentation.Avalonia/ViewModels/MergePresentationViewModel.Execution.cs`: 278 lines; declared types: `MergePresentationViewModel`.
- `src/NvtFwCombiner.Presentation.Avalonia/ViewModels/ReplacePresentationViewModel.Execution.cs`: 357 lines; declared types: `ReplacePresentationViewModel`.

Selected file total: 2,140 lines.

## 9. Session preparation

Owner candidates: GeneralAuthoringExperience; AbMergeAuthoringExperience; AuthoringSessionState.
Implementation count: 3+.
General Merge, General Replace and AB have distinct preparation entry paths. AuthoringSessionState has shared revision and stale-publication checks.
Correction: Preparation is in Application services called by CLI. Three entry paths do not prove three copies of the same bug.

Selected files and non-blank lines:

- `src/NvtFwCombiner.Application/Authoring/AbMergeAuthoringExperience.cs`: 207 lines; declared types: `AbMergeAuthoringExperience`, `AbMergeAuthoringResolver`.
- `src/NvtFwCombiner.Application/Authoring/AbMergeAuthoringExperience.Format.cs`: 299 lines; declared types: `AbMergeAuthoringExperience`, `FormatResolution`.
- `src/NvtFwCombiner.Application/Authoring/AbMergeAuthoringExperience.InputInspection.cs`: 143 lines; declared types: `AbMergeAuthoringExperience`, `AbInspectionContext`.
- `src/NvtFwCombiner.Application/Authoring/AbMergeAuthoringExperience.Readiness.cs`: 131 lines; declared types: `AbMergeAuthoringExperience`.
- `src/NvtFwCombiner.Application/Authoring/AuthoringSessionState.cs`: 638 lines; declared types: `AuthoringSessionState`.
- `src/NvtFwCombiner.Application/Authoring/AuthoringSessionState.SelectedFileInspection.Adoption.cs`: 232 lines; declared types: `AuthoringSessionState`.
- `src/NvtFwCombiner.Application/Authoring/AuthoringSessionState.SelectedFileInspection.cs`: 647 lines; declared types: `AuthoringSessionState`.
- `src/NvtFwCombiner.Application/Authoring/CompiledAuthoringWorkflow.Contracts.cs`: 104 lines; declared types: `ICompiledAuthoringWorkflowResolver`, `CompiledAuthoringWorkflowDiscovery`, `CompiledAuthoringWorkflowResolution`, `CompiledAuthoringSelectedInput`, `CompiledAuthoringInputBinding`, `CompiledAuthoringSelectionSnapshot`, `CompiledAuthoringInspectionBatch`, `CompiledAuthoringSessionPreparation`.
- `src/NvtFwCombiner.Application/Authoring/CompiledAuthoringWorkflow.cs`: 711 lines; declared types: `CompiledAuthoringWorkflowService`.
- `src/NvtFwCombiner.Application/Authoring/CompiledAuthoringWorkflow.Selection.cs`: 295 lines; declared types: `CompiledAuthoringWorkflowService`.
- `src/NvtFwCombiner.Application/Authoring/GeneralAuthoringExperience.Contracts.cs`: 62 lines; declared types: `IGeneralAuthoringPlanner`, `IRuntimeDependencyReadinessLeaseProvider`, `RuntimeDependencyReadinessLease`, `GeneralReplaceRuntimeAuthority`, `GeneralMergeAuthoringPlan`, `GeneralMergeAuthoringPlanResult`, `GeneralReplaceAuthoringPlan`, `GeneralReplaceAuthoringPlanResult`.
- `src/NvtFwCombiner.Application/Authoring/GeneralAuthoringExperience.cs`: 551 lines; declared types: `GeneralAuthoringExperience`.
- `src/NvtFwCombiner.Application/Authoring/GeneralAuthoringExperience.Results.cs`: 56 lines; declared types: `GeneralAuthoringExperience`.
- `src/NvtFwCombiner.Cli/AbMergeCliCommandHandler.cs`: 427 lines; declared types: `AbMergeCliCommandHandler`.
- `src/NvtFwCombiner.Cli/MergeCliCommandHandler.cs`: 176 lines; declared types: `MergeCliCommandHandler`.
- `src/NvtFwCombiner.Cli/ReplaceCliCommandHandler.General.cs`: 424 lines; declared types: `ReplaceCliCommandHandler`.

Selected file total: 5,103 lines.

## 10. External processors

Owner candidates: ExternalCombinerProcessor; LegacyCombinerPostbuildProcessor.
Implementation count: 2+.
Two staged adapters are dispatched through ExternalProcessorRouter. A worker protocol is not itself a third staged host.
Correction: The Python worker is outside src. The plan does not establish a third C# staged adapter.

Selected files and non-blank lines:

- `src/NvtFwCombiner.Application/ExternalTools/LegacyCombinerPostbuildPlanCompiler.cs`: 41 lines; declared types: `LegacyCombinerPostbuildPlanCompiler`.
- `src/NvtFwCombiner.Application/ExternalTools/LegacyCombinerPostbuildPlanCompiler.Fingerprint.cs`: 174 lines; declared types: `LegacyCombinerPostbuildPlanCompiler`.
- `src/NvtFwCombiner.Application/ExternalTools/LegacyCombinerPostbuildPlanCompiler.IntegrityRanges.cs`: 87 lines; declared types: `LegacyCombinerPostbuildPlanCompiler`.
- `src/NvtFwCombiner.Application/ExternalTools/LegacyCombinerPostbuildPlanCompiler.Normalize.cs`: 65 lines; declared types: `LegacyCombinerPostbuildPlanCompiler`.
- `src/NvtFwCombiner.Application/ExternalTools/LegacyCombinerPostbuildPlanCompiler.Resolve.cs`: 121 lines; declared types: `LegacyCombinerPostbuildPlanCompiler`.
- `src/NvtFwCombiner.Application/ExternalTools/LegacyCombinerPostbuildPlanCompiler.WriteRanges.cs`: 120 lines; declared types: `LegacyCombinerPostbuildPlanCompiler`.
- `src/NvtFwCombiner.Domain/Composition/CompositionEngine.ExternalProcessors.cs`: 125 lines; declared types: `CompositionEngine`.
- `src/NvtFwCombiner.Infrastructure/ExternalTools/ExternalCombinerProcessor.cs`: 247 lines; declared types: `ExternalCombinerProcessor`.
- `src/NvtFwCombiner.Infrastructure/ExternalTools/ExternalCombinerProcessor.Staging.cs`: 131 lines; declared types: `ExternalCombinerProcessor`.
- `src/NvtFwCombiner.Infrastructure/ExternalTools/ExternalProcessorRouter.cs`: 39 lines; declared types: `ExternalProcessorRouter`.
- `src/NvtFwCombiner.Infrastructure/ExternalTools/LegacyCombinerPostbuildProcessor.cs`: 267 lines; declared types: `LegacyCombinerPostbuildProcessor`.
- `src/NvtFwCombiner.Infrastructure/ExternalTools/LegacyCombinerPostbuildProcessor.Results.cs`: 60 lines; declared types: `LegacyCombinerPostbuildProcessor`.
- `src/NvtFwCombiner.Infrastructure/ExternalTools/LegacyCombinerPostbuildProcessor.Staging.cs`: 447 lines; declared types: `LegacyCombinerPostbuildProcessor`, `StagingTreePolicy`, `ShortOutputTailSnapshot`.
- `src/NvtFwCombiner.Infrastructure/ExternalTools/StagedArtifactFileVerifier.cs`: 53 lines; declared types: `StagedArtifactFileVerifier`.
- `src/NvtFwCombiner.Infrastructure/ExternalTools/SystemExternalProcessRunner.cs`: 250 lines; declared types: `SystemExternalProcessRunner`, `ExternalProcessCleanupTiming`, `CleanupSchedule`, `ExternalProcessCapacity`, `ExternalProcessRunnerPhase`, `ExternalProcessRunnerSeams`.
- `src/NvtFwCombiner.Infrastructure/ExternalTools/SystemExternalProcessRunner.Invocation.cs`: 324 lines; declared types: `SystemExternalProcessRunner`, `TerminalSignal`, `Invocation`.

Selected file total: 2,551 lines.

## 11. Profile loading

Owner candidates: ProfileBundleLoader; CompositionProfileNormalizer; FirmwareFamilyResolutionNormalizer.
Implementation count: 3.
Bundle acquisition, profile normalization and family normalization are three complementary stages. They are not three equivalent loaders.
Correction: The quoted directories include compilers and catalog code as well as loading.

Selected files and non-blank lines:

- `src/NvtFwCombiner.Infrastructure/Bundles/AcceptedPrebuiltProfileCatalog.cs`: 144 lines; declared types: `AcceptedPrebuiltProfileCatalog`, `AcceptedBundle`.
- `src/NvtFwCombiner.Infrastructure/Bundles/BuiltInProfileBuildAdmissionIdentity.cs`: 66 lines; declared types: `BuildAdmissionIdentityField`, `BuildAdmissionIdentityFailureKind`, `BuildAdmissionIdentityFailure`, `BuiltInProfileBuildAdmissionIdentity`.
- `src/NvtFwCombiner.Infrastructure/Bundles/BuiltInProfileBundleAdmissionSettings.cs`: 9 lines; declared types: `BuiltInProfileBundleAdmissionSettings`.
- `src/NvtFwCombiner.Infrastructure/Bundles/PrebuiltProfileCatalogCanonicalJson.cs`: 82 lines; declared types: `PrebuiltProfileCatalogCanonicalJson`.
- `src/NvtFwCombiner.Infrastructure/Bundles/PrebuiltProfileCatalogCodec.cs`: 283 lines; declared types: `PrebuiltProfileCatalogCodec`.
- `src/NvtFwCombiner.Infrastructure/Bundles/PrebuiltProfileCatalogFormat.cs`: 53 lines; declared types: `PrebuiltProfileCatalogDecodeFailure`, `PrebuiltProfileCatalogFormat`, `PrebuiltProfileCatalogBodyInput`, `PrebuiltProfileCatalogBodyRange`, `PrebuiltProfileCatalogBody`, `PrebuiltProfileCatalogTrustIdentity`, `PrebuiltProfileCatalogBundleInput`, `PrebuiltProfileCatalogSnapshot`.
- `src/NvtFwCombiner.Infrastructure/Bundles/ProfileBundleEntryArrayHasher.cs`: 65 lines; declared types: `ProfileBundleEntryArrayHasher`.
- `src/NvtFwCombiner.Infrastructure/Bundles/ProfileBundleEntrySnapshotCollection.cs`: 111 lines; declared types: `ProfileBundleEntrySnapshotLimits`, `ProfileBundleEntrySnapshot`, `ProfileBundleEntrySnapshotCollection`.
- `src/NvtFwCombiner.Infrastructure/Bundles/ProfileBundleFileSnapshot.cs`: 131 lines; declared types: `ProfileBundleCaptureFailure`, `ProfileBundleFileSnapshot`, `StrictJsonRoot`.
- `src/NvtFwCombiner.Infrastructure/Bundles/ProfileBundleInventoryVerifier.cs`: 93 lines; declared types: `ProfileBundleInventoryVerifier`.
- `src/NvtFwCombiner.Infrastructure/Bundles/ProfileBundleJsonContext.cs`: 17 lines; declared types: `ProfileBundleJsonContext`.
- `src/NvtFwCombiner.Infrastructure/Bundles/ProfileBundleLoader.cs`: 162 lines; declared types: `ProfileBundleTrustAnchor`, `ProfileBundleLoadLimits`, `TrustedProfileBundle`, `ProfileBundleLoader`.
- `src/NvtFwCombiner.Infrastructure/Bundles/ProfileBundleManifest.cs`: 73 lines; declared types: `ProfileBundleEntryKind`, `ProfileBundleEntry`, `ProfileBundleManifest`.
- `src/NvtFwCombiner.Infrastructure/Bundles/ProfileBundleManifestNormalizationException.cs`: 15 lines; declared types: `ProfileBundleManifestNormalizationException`.
- `src/NvtFwCombiner.Infrastructure/Bundles/ProfileBundleManifestNormalizer.cs`: 140 lines; declared types: `ProfileBundleManifestNormalizer`.
- `src/NvtFwCombiner.Infrastructure/Bundles/ProfileBundleManifestSchema.cs`: 16 lines; declared types: `ProfileBundleManifestSchema`.
- `src/NvtFwCombiner.Infrastructure/Bundles/ProfileBundlePackageTrustIndex.cs`: 234 lines; declared types: `ProfileBundleCanonicalFamilyMaterialization`, `ProfileBundleMaterialization`, `ProfileBundleMetadataProviderFamily`, `ProfileBundleRuntimeRegistration`, `ProfileBundlePackageTrustEntry`, `ProfileBundlePackageTrustIndex`, `ProfileBundlePackageTrustIndexLoader`.
- `src/NvtFwCombiner.Infrastructure/Bundles/ProfileBundlePackageTrustIndexSchema.cs`: 17 lines; declared types: `ProfileBundlePackageTrustIndexSchema`.
- `src/NvtFwCombiner.Infrastructure/Bundles/ProfileBundleSchemaValidator.cs`: 197 lines; declared types: `ProfileBundleSchemaValidator`, `EntrySchemaKey`.
- `src/NvtFwCombiner.Infrastructure/Bundles/ProfileBundleSnapshotSource.cs`: 48 lines; declared types: `AcceptedProfileBundleSnapshotSource`, `IProfileBundleSnapshotSource`, `DirectoryProfileBundleSnapshotSource`.
- `src/NvtFwCombiner.Infrastructure/Bundles/StrictJsonDocumentReader.cs`: 107 lines; declared types: `StrictJsonDocumentReader`.
- `src/NvtFwCombiner.Infrastructure/Bundles/TrustedProfileBundleDocumentProjection.cs`: 148 lines; declared types: `TrustedProfileBundleDocumentIdentity`, `TrustedFirmwareFamilyDocumentEntry`, `TrustedCompositionProfileDocumentEntry`, `TrustedProfileBundleDocumentProjection`.
- `src/NvtFwCombiner.Profiles/FirmwareFamilies/FirmwareFamilyNormalizationException.cs`: 20 lines; declared types: `FirmwareFamilyNormalizationException`.
- `src/NvtFwCombiner.Profiles/FirmwareFamilies/FirmwareFamilyResolutionNormalizer.AbFormatPolicy.cs`: 58 lines; declared types: `FirmwareFamilyResolutionNormalizer`.
- `src/NvtFwCombiner.Profiles/FirmwareFamilies/FirmwareFamilyResolutionNormalizer.AliasResolution.cs`: 415 lines; declared types: `FirmwareFamilyResolutionNormalizer`.
- `src/NvtFwCombiner.Profiles/FirmwareFamilies/FirmwareFamilyResolutionNormalizer.AliasTypes.cs`: 92 lines; declared types: `FirmwareFamilyResolutionNormalizer`, `MapInput`, `AliasDeclaration`, `ResolvedFact`, `FactAliasResolver`.
- `src/NvtFwCombiner.Profiles/FirmwareFamilies/FirmwareFamilyResolutionNormalizer.AliasValidation.cs`: 318 lines; declared types: `FirmwareFamilyResolutionNormalizer`.
- `src/NvtFwCombiner.Profiles/FirmwareFamilies/FirmwareFamilyResolutionNormalizer.cs`: 179 lines; declared types: `FirmwareFamilyResolutionNormalizer`.
- `src/NvtFwCombiner.Profiles/FirmwareFamilies/FirmwareFamilyResolutionNormalizer.FullImageMetadata.cs`: 39 lines; declared types: `FirmwareFamilyResolutionNormalizer`.
- `src/NvtFwCombiner.Profiles/FirmwareFamilies/FirmwareFamilyResolutionNormalizer.Metadata.cs`: 395 lines; declared types: `FirmwareFamilyResolutionNormalizer`.
- `src/NvtFwCombiner.Profiles/FirmwareFamilies/FirmwareFamilyResolutionNormalizer.RegionSets.cs`: 134 lines; declared types: `FirmwareFamilyResolutionNormalizer`.
- `src/NvtFwCombiner.Profiles/FirmwareFamilies/FirmwareFamilyResolutionNormalizer.Relationships.cs`: 175 lines; declared types: `FirmwareFamilyResolutionNormalizer`.
- `src/NvtFwCombiner.Profiles/FirmwareFamilies/FirmwareFamilyResolutionNormalizer.Scalars.cs`: 111 lines; declared types: `FirmwareFamilyResolutionNormalizer`.
- `src/NvtFwCombiner.Profiles/FirmwareFamilies/FirmwareMetadataStructureDefinitionResolver.cs`: 13 lines; declared types: `IFirmwareMetadataStructureDefinitionResolver`.
- `src/NvtFwCombiner.Profiles/V2/CompositionProfileNormalizationException.cs`: 21 lines; declared types: `CompositionProfileNormalizationException`.
- `src/NvtFwCombiner.Profiles/V2/CompositionProfileNormalizer.Ab.cs`: 14 lines; declared types: `CompositionProfileNormalizer`.
- `src/NvtFwCombiner.Profiles/V2/CompositionProfileNormalizer.CompilationContext.cs`: 108 lines; declared types: `CompositionProfileNormalizer`, `NormalizedCompilationContext`.
- `src/NvtFwCombiner.Profiles/V2/CompositionProfileNormalizer.cs`: 139 lines; declared types: `CompositionProfileNormalizer`.
- `src/NvtFwCombiner.Profiles/V2/CompositionProfileNormalizer.Input.cs`: 139 lines; declared types: `CompositionProfileNormalizer`.
- `src/NvtFwCombiner.Profiles/V2/CompositionProfileNormalizer.Metadata.cs`: 108 lines; declared types: `CompositionProfileNormalizer`.
- `src/NvtFwCombiner.Profiles/V2/CompositionProfileNormalizer.Operations.cs`: 155 lines; declared types: `CompositionProfileNormalizer`.
- `src/NvtFwCombiner.Profiles/V2/CompositionProfileNormalizer.Output.cs`: 137 lines; declared types: `CompositionProfileNormalizer`.
- `src/NvtFwCombiner.Profiles/V2/CompositionProfileNormalizer.Processors.cs`: 87 lines; declared types: `CompositionProfileNormalizer`.
- `src/NvtFwCombiner.Profiles/V2/CompositionProfileNormalizer.Root.cs`: 132 lines; declared types: `CompositionProfileNormalizer`.
- `src/NvtFwCombiner.Profiles/V2/CompositionProfileNormalizer.Scalars.cs`: 71 lines; declared types: `CompositionProfileNormalizer`.
- `src/NvtFwCombiner.Profiles/V2/CompositionProfileNormalizer.Spaces.cs`: 128 lines; declared types: `CompositionProfileNormalizer`.
- `src/NvtFwCombiner.Profiles/V2/CompositionProfileNormalizer.Validations.cs`: 168 lines; declared types: `CompositionProfileNormalizer`.
- `src/NvtFwCombiner.Profiles/V2/ProfileBundleSemanticJsonContext.cs`: 21 lines; declared types: `ProfileBundleSemanticJsonContext`.
- `src/NvtFwCombiner.Profiles/V2/TrustedProfileBundleCatalog.BankDefinitions.cs`: 145 lines; declared types: `BankReferenceReplaceAdmission`, `TrustedProfileBundleCatalog`.
- `src/NvtFwCombiner.Profiles/V2/TrustedProfileBundleCatalog.Compilation.cs`: 413 lines; declared types: `TrustedProfileBundleCatalog`.
- `src/NvtFwCombiner.Profiles/V2/TrustedProfileBundleCatalog.cs`: 111 lines; declared types: `TrustedFirmwareFamilyCatalogEntry`, `TrustedCompositionProfileCatalogEntry`, `TrustedProfileBundleCatalog`.
- `src/NvtFwCombiner.Profiles/V2/TrustedProfileBundleCatalog.Selection.cs`: 68 lines; declared types: `TrustedMapBoundProfileDeclaration`, `TrustedProfileBundleCatalog`.
- `src/NvtFwCombiner.Profiles/V2/TrustedProfileBundleCatalogFactory.cs`: 370 lines; declared types: `TrustedProfileBundleCatalogException`, `TrustedProfileBundleCatalogFactory`.
- `src/NvtFwCombiner.Profiles/V2/TrustedProfileBundleCatalogSource.cs`: 7 lines; declared types: `TrustedProfileBundleCatalogEntryIdentity`.
- `src/NvtFwCombiner.Profiles/V2/V2CompositionPlanCompiler.ContractLowering.cs`: 267 lines; declared types: `V2CompositionPlanCompiler`.
- `src/NvtFwCombiner.Profiles/V2/V2CompositionPlanCompiler.cs`: 529 lines; declared types: `V2CompositionPlanCompileResult`, `V2CompositionPlanCompiler`, `ResolvedView`, `ResolvedRegionAccessRule`, `LoweredRegionAccess`.
- `src/NvtFwCombiner.Profiles/V2/V2CompositionPlanCompiler.Initializations.cs`: 105 lines; declared types: `V2CompositionPlanCompiler`.
- `src/NvtFwCombiner.Profiles/V2/V2CompositionPlanCompiler.InputGeometry.cs`: 316 lines; declared types: `V2CompositionPlanCompiler`.
- `src/NvtFwCombiner.Profiles/V2/V2CompositionPlanCompiler.InputSelection.cs`: 249 lines; declared types: `V2CompositionPlanCompiler`, `ResolvedInputSelection`.
- `src/NvtFwCombiner.Profiles/V2/V2CompositionPlanCompiler.LogicalOutput.cs`: 186 lines; declared types: `V2CompositionPlanCompiler`.
- `src/NvtFwCombiner.Profiles/V2/V2CompositionPlanCompiler.OperationLowering.cs`: 660 lines; declared types: `V2CompositionPlanCompiler`.
- `src/NvtFwCombiner.Profiles/V2/V2CompositionPlanCompiler.ResolvedMapPhase.cs`: 112 lines; declared types: `V2CompositionPlanCompiler`, `ResolvedMapCompilationPhase`.
- `src/NvtFwCombiner.Profiles/V2/V2CompositionPlanCompiler.RuntimeReferenceReplace.BankCompilation.cs`: 34 lines; declared types: `V2CompositionPlanCompiler`.
- `src/NvtFwCombiner.Profiles/V2/V2CompositionPlanCompiler.RuntimeReferenceReplace.BankOperations.cs`: 91 lines; declared types: `V2CompositionPlanCompiler`.
- `src/NvtFwCombiner.Profiles/V2/V2CompositionPlanCompiler.RuntimeReferenceReplace.Banks.cs`: 584 lines; declared types: `V2CompositionPlanCompiler`.
- `src/NvtFwCombiner.Profiles/V2/V2CompositionPlanCompiler.RuntimeReferenceReplace.cs`: 524 lines; declared types: `V2CompositionPlanCompiler`, `RuntimeReferenceReplaceProfileShape`, `RuntimeFirmwareVersionEditLowering`.
- `src/NvtFwCombiner.Profiles/V2/V2CompositionPlanCompiler.RuntimeReferenceReplace.Postbuild.cs`: 134 lines; declared types: `V2CompositionPlanCompiler`.
- `src/NvtFwCombiner.Profiles/V2/V2CompositionPlanCompiler.SourceEnvelope.cs`: 109 lines; declared types: `V2CompositionPlanCompiler`, `SourceEnvelopeSeed`.
- `src/NvtFwCombiner.Profiles/V2/V2CompositionPreparationService.cs`: 173 lines; declared types: `V2CompositionPreparationService`, `PreparedCompilation`.
- `src/NvtFwCombiner.Profiles/V2/V2LogicalOutputCompileRequest.cs`: 36 lines; declared types: `V2ExplicitMappingInputBinding`, `V2ExplicitMappingCompileRequest`, `V2LogicalOutputCompileRequest`.
- `src/NvtFwCombiner.Profiles/V2/V2RuntimeReferenceBankReplacePlan.cs`: 37 lines; declared types: `V2RuntimeReferenceBankReplaceRequest`, `V2RuntimeReferenceBankReplaceBinding`, `V2RuntimeReferenceBankReplacePlan`.
- `src/NvtFwCombiner.Profiles/V2/V2RuntimeReferenceReplaceCompileRequest.cs`: 113 lines; declared types: `V2RuntimeReferenceReplaceFirmwareVersionEdit`, `V2RuntimeReferenceReplaceSourceEnvelope`, `V2RuntimeReferenceReplacePostbuildPolicy`, `V2RuntimeReferenceReplaceCompileRequest`.

Selected file total: 10,961 lines.

## 12. Hard-coded IC data

Owner candidates: ExternalCombinerInvocationCatalog; TrustedProfileBundleCatalog; BuiltInV2BundlePreload.
Implementation count: 3+.
Three listed sources are a lower bound. Registration and firmware semantics are different roles.
Correction: Additional built-in flash-map and postbuild catalogs contain IC-specific facts.

Selected files and non-blank lines:

- `src/NvtFwCombiner.Application/ExternalTools/ExternalCombinerInvocationCatalog.cs`: 34 lines; declared types: `ExternalCombinerInvocationCatalog`.
- `src/NvtFwCombiner.Infrastructure/Composition/BuiltInV2BundlePreload.cs`: 121 lines; declared types: `BuiltInV2BundlePreload`.
- `src/NvtFwCombiner.Infrastructure/ExternalTools/BuiltInPostbuildProfileCatalog.cs`: 395 lines; declared types: `BuiltInPostbuildProfileCatalog`, `CatalogDocument`, `ProfileDocument`, `CommandDocument`, `BlockDocument`, `DiffDlmPolicyDocument`, `DiffDlmMaskDocument`, `RangeDocument`, `PlanSelectorDocument`.
- `src/NvtFwCombiner.Infrastructure/FlashMaps/BuiltInTpFlashMapCatalog.cs`: 103 lines; declared types: `BuiltInTpFlashMapCatalog`.
- `src/NvtFwCombiner.Infrastructure/FlashMaps/BuiltInTpFlashMapCatalog.Loader.cs`: 168 lines; declared types: `BuiltInTpFlashMapCatalog`, `CatalogDocument`, `PendingMapDocument`, `ProfileDocument`, `RegionDocument`, `LoadedCatalog`, `PendingTpFlashMap`.
- `src/NvtFwCombiner.Infrastructure/FlashMaps/BuiltInTpFlashMapCatalog.Postbuild.cs`: 221 lines; declared types: `BuiltInTpFlashMapCatalog`.
- `src/NvtFwCombiner.Infrastructure/FlashMaps/BuiltInTpFlashMapCatalog.Selection.cs`: 134 lines; declared types: `BuiltInTpFlashMapCatalog`, `Catalog`, `Slot`.
- `src/NvtFwCombiner.Profiles/V2/TrustedProfileBundleCatalog.BankDefinitions.cs`: 145 lines; declared types: `BankReferenceReplaceAdmission`, `TrustedProfileBundleCatalog`.

Selected file total: 1,321 lines.

## 13. Region access

Owner candidates: CompiledRegionAccessContract; FirmwareRegion.
Implementation count: 2.
Experience access and physical region write constraints are two representations. Their enforcement may serve different purposes.
Correction: The plan names fields rather than implementing types.

Selected files and non-blank lines:

- `src/NvtFwCombiner.Application/Authoring/GeneralAuthoringAdmissionEvaluator.cs`: 516 lines; declared types: `GeneralAuthoringAdmissionResult`, `GeneralAuthoringAdmission`, `GeneralAuthoringAdmissionSummary`.
- `src/NvtFwCombiner.Domain/Composition/CompiledRegionAccessContract.cs`: 117 lines; declared types: `CompiledResolvedPhysicalView`, `CompiledRegionAccessRequirement`, `CompiledRegionAccessContract`.
- `src/NvtFwCombiner.Domain/Firmware/FirmwareRegion.cs`: 142 lines; declared types: `FirmwareRegionOwner`, `FirmwareRegionKind`, `FirmwareWriteConstraint`, `FirmwareRegion`, `value`.
- `src/NvtFwCombiner.Domain/Firmware/FirmwareRegionTemplate.cs`: 116 lines; declared types: `FirmwareRegionTemplate`, `FirmwareRegionInstance`.
- `src/NvtFwCombiner.Infrastructure/Composition/BuiltInGeneralAuthoringPlanner.GeneralMerge.Mapping.cs`: 86 lines; declared types: `BuiltInGeneralAuthoringPlanner`.
- `src/NvtFwCombiner.Infrastructure/Composition/BuiltInGeneralAuthoringPlanner.GeneralReplace.Mapping.cs`: 372 lines; declared types: `BuiltInGeneralAuthoringPlanner`, `GeneralReplacePatchArtifact`.
- `src/NvtFwCombiner.Profiles/FirmwareFamilies/FirmwareFamilyResolutionNormalizer.RegionSets.cs`: 134 lines; declared types: `FirmwareFamilyResolutionNormalizer`.
- `src/NvtFwCombiner.Profiles/V2/CompositionProfileNormalizer.Root.cs`: 132 lines; declared types: `CompositionProfileNormalizer`.
- `src/NvtFwCombiner.Profiles/V2/V2CompositionPlanCompiler.ContractLowering.cs`: 267 lines; declared types: `V2CompositionPlanCompiler`.

Selected file total: 1,882 lines.

## 14. Address and write range checks

Owner candidates: V2CompositionPlanCompiler; GeneralAuthoringAdmission; CompositionPlan; staged adapters.
Implementation count: 4+.
Compiler lowering, General admission, plan validation and staged output diff are distinct checking stages. The two adapters have separate result checks.
Correction: The type in GeneralAuthoringAdmissionEvaluator.cs is GeneralAuthoringAdmission. CompositionPlan also checks bounds and overlap.

Selected files and non-blank lines:

- `src/NvtFwCombiner.Application/Authoring/GeneralAuthoringAdmissionEvaluator.cs`: 516 lines; declared types: `GeneralAuthoringAdmissionResult`, `GeneralAuthoringAdmission`, `GeneralAuthoringAdmissionSummary`.
- `src/NvtFwCombiner.Application/Composition/CompiledInputLoadValidationEvaluator.cs`: 64 lines; declared types: `CompiledInputLoadValidationEvaluator`, `InputLoadValidationEvaluationResult`.
- `src/NvtFwCombiner.Domain/Composition/ByteRange.cs`: 95 lines; declared types: `ByteRange`.
- `src/NvtFwCombiner.Domain/Composition/CompositionPlan.Operations.cs`: 206 lines; declared types: `CompositionPlan`.
- `src/NvtFwCombiner.Infrastructure/Composition/BuiltInGeneralAuthoringPlanner.GeneralMerge.Mapping.cs`: 86 lines; declared types: `BuiltInGeneralAuthoringPlanner`.
- `src/NvtFwCombiner.Infrastructure/Composition/BuiltInGeneralAuthoringPlanner.GeneralReplace.Mapping.cs`: 372 lines; declared types: `BuiltInGeneralAuthoringPlanner`, `GeneralReplacePatchArtifact`.
- `src/NvtFwCombiner.Infrastructure/ExternalTools/ExternalCombinerProcessor.cs`: 247 lines; declared types: `ExternalCombinerProcessor`.
- `src/NvtFwCombiner.Infrastructure/ExternalTools/ExternalCombinerProcessor.Staging.cs`: 131 lines; declared types: `ExternalCombinerProcessor`.
- `src/NvtFwCombiner.Infrastructure/ExternalTools/LegacyCombinerPostbuildProcessor.Results.cs`: 60 lines; declared types: `LegacyCombinerPostbuildProcessor`.
- `src/NvtFwCombiner.Profiles/V2/V2CompositionPlanCompiler.LogicalOutput.cs`: 186 lines; declared types: `V2CompositionPlanCompiler`.
- `src/NvtFwCombiner.Profiles/V2/V2CompositionPlanCompiler.OperationLowering.cs`: 660 lines; declared types: `V2CompositionPlanCompiler`.
- `src/NvtFwCombiner.Profiles/V2/V2CompositionPlanCompiler.RuntimeReferenceReplace.BankCompilation.cs`: 34 lines; declared types: `V2CompositionPlanCompiler`.
- `src/NvtFwCombiner.Profiles/V2/V2CompositionPlanCompiler.RuntimeReferenceReplace.BankOperations.cs`: 91 lines; declared types: `V2CompositionPlanCompiler`.
- `src/NvtFwCombiner.Profiles/V2/V2CompositionPlanCompiler.RuntimeReferenceReplace.Banks.cs`: 584 lines; declared types: `V2CompositionPlanCompiler`.
- `src/NvtFwCombiner.Profiles/V2/V2CompositionPlanCompiler.RuntimeReferenceReplace.cs`: 524 lines; declared types: `V2CompositionPlanCompiler`, `RuntimeReferenceReplaceProfileShape`, `RuntimeFirmwareVersionEditLowering`.
- `src/NvtFwCombiner.Profiles/V2/V2CompositionPlanCompiler.RuntimeReferenceReplace.Postbuild.cs`: 134 lines; declared types: `V2CompositionPlanCompiler`.

Selected file total: 3,990 lines.

## 15. Saved Rule read and write

Owner candidates: SavedRuleV2Inspector; SavedRuleDocumentLifecycleUseCase.
Implementation count: 2+.
Inspection and lifecycle storage are separate responsibilities. General Merge and Replace have separate draft loaders.
Correction: The lifecycle class is not itself a GUI implementation. Presentation wiring needs further tracing.

Selected files and non-blank lines:

- `src/NvtFwCombiner.Application/Authoring/SavedCompositionRuleV2ContentHasher.cs`: 114 lines; declared types: `SavedCompositionRuleV2ContentHasher`.
- `src/NvtFwCombiner.Application/Authoring/SavedRuleDocumentLifecycleUseCase.cs`: 124 lines; declared types: `SavedRuleDocumentSaveRequest`, `SavedRuleDocumentSaveResult`, `SavedRuleDocumentLifecycleUseCase`.
- `src/NvtFwCombiner.Application/Authoring/SavedRuleDraftLoadResult.cs`: 32 lines; declared types: `SavedRuleValidationIssue`, `SavedRuleV2DraftLoadResult`, `SavedRuleV2InspectionResult`.
- `src/NvtFwCombiner.Application/Authoring/SavedRuleLifecycle.cs`: 253 lines; declared types: `SavedRuleParentIdentity`, `SavedRuleExecutionIdentity`, `SavedRuleStorageKind`, `SavedRuleLifecycleState`, `SavedRuleLifecycleSnapshot`, `SavedRuleLifecycleIssue`, `SavedRuleSaveDecision`, `SavedRuleLifecycle`.
- `src/NvtFwCombiner.Cli/MergeCliCommandHandler.SavedRules.cs`: 38 lines; declared types: `MergeCliCommandHandler`.
- `src/NvtFwCombiner.Cli/SavedRuleCliCommandHandler.cs`: 98 lines; declared types: `SavedRuleCliCommandHandler`.
- `src/NvtFwCombiner.Cli/SavedRuleCliCommandHandler.V2.cs`: 39 lines; declared types: `SavedRuleCliCommandHandler`.
- `src/NvtFwCombiner.Cli/SavedRuleCliSupport.cs`: 46 lines; declared types: `SavedRuleCliSupport`.
- `src/NvtFwCombiner.Infrastructure/Composition/BuiltInSavedRuleAuthoring.cs`: 65 lines; declared types: `BuiltInSavedRuleAuthoring`.
- `src/NvtFwCombiner.Infrastructure/Composition/SavedCompositionRuleV2Admission.cs`: 497 lines; declared types: `SavedRuleV2ParentInputPolicy`, `SavedRuleV2GeneralMergeAdmissionContext`, `SavedCompositionRuleV2AdmissionResult`, `SavedCompositionRuleV2Admission`.
- `src/NvtFwCombiner.Infrastructure/Composition/SavedCompositionRuleV2Admission.GeneralReplace.cs`: 151 lines; declared types: `SavedRuleV2GeneralReplaceAdmissionContext`, `SavedRuleV2GeneralReplaceExactParent`, `SavedRuleV2GeneralReplaceExactParentResolver`, `SavedCompositionRuleV2Admission`.
- `src/NvtFwCombiner.Infrastructure/Composition/SavedRuleV2GeneralMergeDraftLoader.cs`: 465 lines; declared types: `SavedRuleV2GeneralMergeDraftLoader`, `SavedRuleV2MappingProjection`.
- `src/NvtFwCombiner.Infrastructure/Composition/SavedRuleV2GeneralMergeDraftLoader.Values.cs`: 147 lines; declared types: `SavedRuleV2GeneralMergeDraftLoader`.
- `src/NvtFwCombiner.Infrastructure/Composition/SavedRuleV2GeneralReplaceDraftLoader.cs`: 72 lines; declared types: `SavedRuleV2GeneralMergeDraftLoader`.
- `src/NvtFwCombiner.Infrastructure/Composition/SavedRuleV2Inspector.cs`: 209 lines; declared types: `SavedRuleV2Inspector`.
- `src/NvtFwCombiner.Infrastructure/Files/SavedRuleDocumentIdentityReader.cs`: 55 lines; declared types: `SavedRuleDocumentIdentityReader`.
- `src/NvtFwCombiner.Infrastructure/Files/SavedRuleDocumentStorage.cs`: 117 lines; declared types: `SavedRuleDocumentStorage`.

Selected file total: 2,522 lines.

## Summary

| Concept | Implementation count | Selected file lines | Plan lines |
| ---: | ---: | ---: | ---: |
| 1 | 2 | 1,260 | — |
| 2 | 2 | 940 | — |
| 3 | 2+ | 2,997 | — |
| 4 | 1+ | 2,019 | — |
| 5 | 2 | 960 | — |
| 6 | 4 | 5,746 | 6,155 |
| 7 | 5 | 2,775 | 2,775 |
| 8 | 3+ | 2,140 | — |
| 9 | 3+ | 5,103 | — |
| 10 | 2+ | 2,551 | — |
| 11 | 3 | 10,961 | 10,961 |
| 12 | 3+ | 1,321 | — |
| 13 | 2 | 1,882 | — |
| 14 | 4+ | 3,990 | — |
| 15 | 2+ | 2,522 | — |

The row 6 set totals 5,746 lines: Standard 271, AB 780, CtrlRAM 684, General 2,901, and shared workflow 1,110.
Adding the plan section 1 Saved Rule category of 409 gives the quoted 6,155.
The row 7 set reproduces Standard 375, AB 492, General Merge 502, Replace 1,223, and Saved Rule 183.
The row 11 set reproduces V2 6,801, FirmwareFamilies 1,949, and Bundles 2,211.

## Source totals by project

| Project | Files | Non-blank text lines | Binary files |
| --- | ---: | ---: | ---: |
| (src root) | 1 | 12 | 0 |
| NvtFwCombiner.Application | 252 | 38,818 | 0 |
| NvtFwCombiner.Bootstrap | 8 | 970 | 0 |
| NvtFwCombiner.Cli | 37 | 4,052 | 0 |
| NvtFwCombiner.Contracts | 23 | 1,201 | 0 |
| NvtFwCombiner.Desktop | 3 | 545 | 0 |
| NvtFwCombiner.DistributionLauncher | 8 | 1,183 | 0 |
| NvtFwCombiner.Domain | 133 | 15,064 | 0 |
| NvtFwCombiner.Infrastructure | 123 | 19,158 | 0 |
| NvtFwCombiner.Launcher | 3 | 173 | 0 |
| NvtFwCombiner.LauncherBootstrap | 3 | 101 | 0 |
| NvtFwCombiner.Platform | 5 | 584 | 0 |
| NvtFwCombiner.Presentation.Avalonia | 328 | 49,229 | 1 |
| NvtFwCombiner.Profiles | 54 | 8,877 | 0 |
| NvtFwCombiner.VersionManagement.Application | 33 | 9,994 | 0 |
| NvtFwCombiner.VersionManagement.Infrastructure | 47 | 14,959 | 0 |
| Total | 1,061 | 164,920 | 1 |

## Uncertainties

- The lists and lower-bound counts are not an exhaustive behavior census.
- LDC, GUI launch, session, processor, IC-data and range-check call chains need further review.
- Preparation entry paths alone do not establish whether the reported overwrite bug occurs on this commit.
- Data outside src and the Python worker implementation are outside this file census.
- Static inspection does not establish runtime reachability or byte parity.
