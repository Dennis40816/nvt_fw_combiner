using System.Text.Json;

namespace NvtFwCombiner.Architecture.Tests;

public sealed partial class RepositoryBoundaryTests
{
    /// <summary>Fixed-workflow resource admission has one Application owner before allocation.</summary>
    [Fact]
    public void FirmwareContentReadCeilingStaysApplicationOwnedAndPreAllocation()
    {
        string applicationOwner = ReadText(
            "src/NvtFwCombiner.Application/InputInspection/CompiledInputArtifactInspectionService.cs");
        string contentRead = ReadText(
            "src/NvtFwCombiner.Infrastructure/Composition/BuiltInFirmwareInspection.ContentRead.cs");
        string filesystemAdapter = ReadText(
            "src/NvtFwCombiner.Infrastructure/Files/FileContentSnapshotInspector.cs");
        string presentation = ReadText(
            "src/NvtFwCombiner.Presentation.Avalonia/ViewModels/WorkflowSessionPresentationViewModel.FirmwareInspection.cs");
        string generalLimits = ReadText(
            "src/NvtFwCombiner.Application/Authoring/GeneralAuthoringResourceLimits.cs");

        Assert.Contains("MaximumContentReadBytes = 100_000_000", applicationOwner, StringComparison.Ordinal);
        Assert.Contains("CompiledTruncateCtrlRamInputNormalization", applicationOwner, StringComparison.Ordinal);
        Assert.Contains("ResolveMaximumContentReadBytes", contentRead, StringComparison.Ordinal);
        Assert.Contains(
            "CompiledInputArtifactInspectionService.MaximumContentReadBytes",
            contentRead,
            StringComparison.Ordinal);
        Assert.DoesNotContain("int.MaxValue", contentRead, StringComparison.Ordinal);
        Assert.DoesNotContain("CompiledInputLengthRequirement", contentRead, StringComparison.Ordinal);
        Assert.DoesNotContain("CompiledExact", contentRead, StringComparison.Ordinal);
        Assert.DoesNotContain("CompiledBounded", contentRead, StringComparison.Ordinal);
        Assert.DoesNotContain("CompiledSourceView", contentRead, StringComparison.Ordinal);
        Assert.DoesNotContain("CompiledTruncateCtrlRam", contentRead, StringComparison.Ordinal);
        Assert.DoesNotContain("100_000_000", contentRead, StringComparison.Ordinal);
        Assert.DoesNotContain("MaximumContentReadBytes", presentation, StringComparison.Ordinal);
        Assert.DoesNotContain("MaximumContentReadBytes", generalLimits, StringComparison.Ordinal);
        Assert.Contains("observedLength > maximumBytes", filesystemAdapter, StringComparison.Ordinal);
        Assert.Contains("new byte[checked((int)observedLength)]", filesystemAdapter, StringComparison.Ordinal);
        Assert.True(
            filesystemAdapter.IndexOf("observedLength > maximumBytes", StringComparison.Ordinal) <
            filesystemAdapter.IndexOf("new byte[checked((int)observedLength)]", StringComparison.Ordinal));
    }

    /// <summary>Slot and context refreshes cannot synchronously inspect firmware from Presentation.</summary>
    [Fact]
    public void PresentationFirmwareInspectionStaysBatchAsync()
    {
        string viewModels = ReadViewModelPartials();
        string firmwareFacts = ReadText(
            "src/NvtFwCombiner.Presentation.Avalonia/UiCompositionRunner.FirmwareFacts.cs");
        string replaceRunner = ReadText(
            "src/NvtFwCombiner.Presentation.Avalonia/UiCompositionRunner.Replace.cs");
        string replaceRefresh = ReadText(
            "src/NvtFwCombiner.Presentation.Avalonia/ViewModels/ReplacePresentationViewModel.Memory.cs");
        string firmwareInspectionReader = ReadText(
            "src/NvtFwCombiner.Presentation.Avalonia/ViewModels/FirmwareInspectionSession.cs");
        string ctrlRamVersion = ReadText(
            "src/NvtFwCombiner.Presentation.Avalonia/ViewModels/ReplacePresentationViewModel.CtrlRamFirmwareVersion.cs");
        string inspectionPort = ReadText(
            "src/NvtFwCombiner.Application/Composition/CompositionExperiencePorts.cs");
        string workflowInspection = ReadText(
            "src/NvtFwCombiner.Presentation.Avalonia/ViewModels/WorkflowSessionPresentationViewModel.FirmwareInspection.cs");
        string firmwareInspection = ReadText(
            "src/NvtFwCombiner.Infrastructure/Composition/BuiltInFirmwareInspection.cs");
        string firmwareInspectionSources = string.Join(
            '\n',
            Directory.GetFiles(
                    Path.Combine(
                        Root.FullName,
                        "src",
                        "NvtFwCombiner.Infrastructure",
                        "Composition"),
                    "BuiltInFirmwareInspection*.cs")
                .Select(File.ReadAllText));
        string firmwareContentRead = ReadText(
            "src/NvtFwCombiner.Infrastructure/Composition/BuiltInFirmwareInspection.ContentRead.cs");
        string contentInspector = ReadText(
            "src/NvtFwCombiner.Infrastructure/Files/FileContentSnapshotInspector.cs");
        string observationOwner = ReadText(
            "src/NvtFwCombiner.Application/InputInspection/CompiledInputArtifactObservationService.cs");
        string ctrlRamVersionOwner = ReadText(
            "src/NvtFwCombiner.Application/Authoring/CtrlRamAuthoringExperience.cs");

        Assert.DoesNotContain("SetSlotFileAsync", viewModels, StringComparison.Ordinal);
        Assert.Contains("SetSlotFileAsync", workflowInspection, StringComparison.Ordinal);
        Assert.DoesNotContain("Task.Run", workflowInspection, StringComparison.Ordinal);
        Assert.DoesNotContain("InspectionReader", workflowInspection, StringComparison.Ordinal);
        Assert.Contains("InspectFirmwareBatchAsync", workflowInspection, StringComparison.Ordinal);
        Assert.DoesNotContain("public void SetSlotFile(", viewModels, StringComparison.Ordinal);
        Assert.DoesNotContain("RefreshAllSelectedSlotFirmwareFacts", viewModels, StringComparison.Ordinal);
        Assert.DoesNotContain("GetSelectedCtrlRamBasePath", viewModels, StringComparison.Ordinal);
        Assert.DoesNotContain("TryReadFirmwareContextSuggestion", viewModels, StringComparison.Ordinal);
        Assert.DoesNotContain("FirmwareInspectionAdapter.InspectFirmware", firmwareFacts, StringComparison.Ordinal);
        Assert.DoesNotContain("string? ctrlRamBasePath", replaceRunner, StringComparison.Ordinal);
        Assert.DoesNotContain("GetDiscoveryDisplay", replaceRunner, StringComparison.Ordinal);
        Assert.Contains("IReadOnlyList<CtrlRamRegion> regions", replaceRunner, StringComparison.Ordinal);
        Assert.DoesNotContain("File.Exists", replaceRefresh, StringComparison.Ordinal);
        Assert.DoesNotContain("new FileInfo", replaceRefresh, StringComparison.Ordinal);
        Assert.Contains("internal static class FirmwareInspectionProjection", firmwareInspectionReader, StringComparison.Ordinal);
        Assert.DoesNotContain("FirmwareInspectionReader", firmwareInspectionReader, StringComparison.Ordinal);
        Assert.DoesNotContain("class FirmwareInspectionSession", firmwareInspectionReader, StringComparison.Ordinal);
        Assert.DoesNotContain("FileInfo", firmwareInspectionReader, StringComparison.Ordinal);
        Assert.DoesNotContain("FirmwareFileIdentity.Capture", firmwareInspectionReader, StringComparison.Ordinal);
        Assert.DoesNotContain("FirmwareFileIdentity.Capture", ctrlRamVersion, StringComparison.Ordinal);
        Assert.Contains("ValueTask<FirmwareInspectionBatchResult> InspectFirmwareBatchAsync", inspectionPort, StringComparison.Ordinal);
        Assert.DoesNotContain("ReadFirmwareConfigMetadataAsync", inspectionPort, StringComparison.Ordinal);
        Assert.DoesNotContain("IsFirmwareContentCurrentAsync", inspectionPort, StringComparison.Ordinal);
        Assert.DoesNotContain("ReadFirmwareConfigMetadataAsync", firmwareInspectionSources, StringComparison.Ordinal);
        Assert.DoesNotContain("IsFirmwareContentCurrentAsync", firmwareInspectionSources, StringComparison.Ordinal);
        Assert.DoesNotContain("ReadFirmwareConfigMetadataAsync", ctrlRamVersion, StringComparison.Ordinal);
        Assert.DoesNotContain("IsFirmwareContentCurrentAsync", ctrlRamVersion, StringComparison.Ordinal);
        Assert.Contains("TpReferenceFirmwareConfig", observationOwner, StringComparison.Ordinal);
        Assert.Contains("slot.Role == ReferenceBaseRole", observationOwner, StringComparison.Ordinal);
        Assert.Contains("DecodeTp(CompiledInputVersionKind.TpReferenceFirmwareConfig", observationOwner, StringComparison.Ordinal);
        Assert.Contains("ProjectFirmwareVersionConfirmationLease", ctrlRamVersionOwner, StringComparison.Ordinal);
        Assert.Contains("Observation.Versions.SingleOrDefault", ctrlRamVersionOwner, StringComparison.Ordinal);
        Assert.Contains("ReferenceEquals(current, lease)", ctrlRamVersionOwner, StringComparison.Ordinal);
        Assert.Contains("ProjectFirmwareVersionConfirmationLease", ctrlRamVersion, StringComparison.Ordinal);
        Assert.Contains("IsFirmwareVersionConfirmationLeaseCurrent", ctrlRamVersion, StringComparison.Ordinal);
        Assert.DoesNotContain("File.", ctrlRamVersion, StringComparison.Ordinal);
        Assert.Contains("record struct AuthoringInspectionProgress", inspectionPort, StringComparison.Ordinal);
        Assert.Contains("IProgress<AuthoringInspectionProgress>? progress = null", inspectionPort, StringComparison.Ordinal);
        Assert.DoesNotContain("IAuthoringInspectionProgressObserver", inspectionPort, StringComparison.Ordinal);
        Assert.False(File.Exists(Path.Combine(
            Root.FullName,
            "src",
            "NvtFwCombiner.Presentation.Avalonia",
            "ViewModels",
            "FirmwareFileIdentity.cs")));
        Assert.False(File.Exists(Path.Combine(
            Root.FullName,
            "src",
            "NvtFwCombiner.Infrastructure",
            "Composition",
            "BuiltInFirmwareInspection.FileIdentity.cs")));
        Assert.DoesNotContain("_fileProjections", firmwareInspectionReader, StringComparison.Ordinal);
        Assert.DoesNotContain("_baseCache", firmwareInspectionReader, StringComparison.Ordinal);
        Assert.DoesNotContain("ConcurrentDictionary", firmwareInspectionSources, StringComparison.Ordinal);
        Assert.DoesNotContain("BuiltInV2RegistrationRegistry", firmwareInspectionSources, StringComparison.Ordinal);
        Assert.Contains("_contentInspector", firmwareContentRead, StringComparison.Ordinal);
        Assert.Contains("InspectAsync", firmwareContentRead, StringComparison.Ordinal);
        Assert.DoesNotContain("File.ReadAllBytes", firmwareContentRead, StringComparison.Ordinal);
        Assert.Contains("ReadAndHashExactLengthAsync", contentInspector, StringComparison.Ordinal);
        Assert.Contains(
            "input.DpReplaceAddressSpaceId is not null",
            firmwareInspection,
            StringComparison.Ordinal);
        Assert.Contains(
            "input.StandardMergeAddressSpaceId is not null",
            firmwareInspection,
            StringComparison.Ordinal);
        Assert.Contains(
            "input.CtrlRamReplaceAddressSpaceId is not null",
            firmwareInspection,
            StringComparison.Ordinal);
        Assert.Contains(
            "input.AbMergeAddressSpaceId is not null",
            firmwareInspection,
            StringComparison.Ordinal);
        Assert.Contains(
            "FirmwareInspectionDispatch.AllStrategiesBaseline",
            firmwareInspection,
            StringComparison.Ordinal);
        Assert.DoesNotContain("RefreshMergeMemoryMapState", replaceRefresh, StringComparison.Ordinal);
        Assert.Contains("RefreshReplaceMemoryMapState", replaceRefresh, StringComparison.Ordinal);
        Assert.DoesNotContain("ValidateCachedCtrlRamDisplayAsync", viewModels, StringComparison.Ordinal);
        Assert.DoesNotContain("OutputNaming", firmwareInspectionReader, StringComparison.Ordinal);
        Assert.False(File.Exists(Path.Combine(
            Root.FullName,
            "src",
            "NvtFwCombiner.Presentation.Avalonia",
            "ViewModels",
            "WorkflowSessionPresentationViewModel.OutputNaming.cs")));
    }

    /// <summary>Verifies General Merge workbench orchestration, mapping, profile, and report helpers stay split.</summary>
    [Fact]
    public void GeneralMergeWorkbenchConcernsStaySplit()
    {
        string mapping = ReadText("src/NvtFwCombiner.Infrastructure/Composition/BuiltInGeneralAuthoringPlanner.GeneralMerge.Mapping.cs");
        string profile = string.Concat(
            ReadText("src/NvtFwCombiner.Application/Authoring/GeneralMergeAuthoringUseCase.cs"),
            ReadText("src/NvtFwCombiner.Infrastructure/Composition/BuiltInGeneralAuthoringPlanner.cs"));
        string entry = ReadText("src/NvtFwCombiner.Application/Composition/CompositionExecutionExperience.cs");
        string candidate = ReadText("src/NvtFwCombiner.Infrastructure/Composition/BuiltInGeneralAuthoringPlanner.GeneralMerge.V2.cs");

        Assert.DoesNotContain("RunGeneralMergeEphemeralDraftAsync", entry, StringComparison.Ordinal);
        Assert.Contains("private async ValueTask<CompositionRunResult> ExecuteGeneralMergeAsync", entry, StringComparison.Ordinal);
        Assert.False(File.Exists(Path.Combine(
            Root.FullName,
            "src",
            "NvtFwCombiner.Bootstrap",
            "CompositionMemoryProjection.GeneralMerge.cs")));
        Assert.False(File.Exists(Path.Combine(
            Root.FullName,
            "src",
            "NvtFwCombiner.Bootstrap",
            "WorkbenchCompositionService.GeneralMerge.Report.cs")));
        Assert.Contains("internal static bool TryCreateGeneralMergeMappings", mapping, StringComparison.Ordinal);
        Assert.DoesNotContain("WorkbenchGeneralMergeMappingInput", mapping, StringComparison.Ordinal);
        Assert.Contains("TryResolveOutputInitializer", profile, StringComparison.Ordinal);
        Assert.Contains("GeneralMergeDraftState", candidate, StringComparison.Ordinal);
        Assert.Contains("draft.OutputInitializer", candidate, StringComparison.Ordinal);
        Assert.DoesNotContain(
            "GeneralMergeFillByte",
            entry + mapping + profile + candidate,
            StringComparison.Ordinal);
        Assert.DoesNotContain("CompositionProfileDefinition", profile, StringComparison.Ordinal);
        Assert.DoesNotContain("CreateBlocked", entry, StringComparison.Ordinal);
        Assert.False(File.Exists(Path.Combine(
            Root.FullName,
            "src",
            "NvtFwCombiner.Bootstrap",
            "CompositionExecutionAdapter.GeneralMerge.V2.cs")));
    }

    /// <summary>Current General workflows cannot restore raw or inclusive-end workbench adapters.</summary>
    [Fact]
    public void GeneralWorkflowsExposeOnlyCanonicalStartLengthDrafts()
    {
        string bootstrap = ReadBootstrapSources() + ReadBootstrapTestSources();
        string production = ReadProductionSources();

        Assert.DoesNotContain("WorkbenchGeneralMergeMappingInput", bootstrap, StringComparison.Ordinal);
        Assert.DoesNotContain("WorkbenchGeneralReplaceMappingInput", bootstrap, StringComparison.Ordinal);
        Assert.DoesNotContain("WorkbenchGeneralReplacePatchInput", bootstrap, StringComparison.Ordinal);
        Assert.DoesNotContain("TryParseLegacyInclusiveRange", bootstrap, StringComparison.Ordinal);
        Assert.DoesNotContain("TargetEndInclusive", production, StringComparison.Ordinal);
    }

    /// <summary>Saved Rule v1 remains historical contract evidence and cannot regain a production parser or projection.</summary>
    [Fact]
    public void GeneralSavedRulesExposeOnlyTheV2RuntimeContract()
    {
        string bootstrap = ReadBootstrapSources();
        string infrastructureComposition = ReadInfrastructureCompositionSources();

        Assert.DoesNotContain("SavedCompositionRuleLoader", bootstrap, StringComparison.Ordinal);
        Assert.DoesNotContain("SavedRuleGeneralMappingDraftAdapter", bootstrap, StringComparison.Ordinal);
        Assert.DoesNotContain("SavedCompositionRule(", bootstrap, StringComparison.Ordinal);
        Assert.False(File.Exists(Path.Combine(
            Root.FullName,
            "src",
            "NvtFwCombiner.Bootstrap",
            "SavedCompositionRule.cs")));
        Assert.DoesNotContain("GeneralMergeFillByteInvalid", bootstrap, StringComparison.Ordinal);
        Assert.DoesNotContain(
            "Saved rule schemaVersion must be '1.0'",
            bootstrap,
            StringComparison.Ordinal);
        Assert.Contains(
            "Saved Rule v1 is retired; migrate the document to Saved Rule v2",
            infrastructureComposition,
            StringComparison.Ordinal);
        Assert.DoesNotContain("Saved Rule v1 is retired", bootstrap, StringComparison.Ordinal);
        Assert.DoesNotContain(
            "LegacyTimestampFileStampCompatibilityAdapter",
            ReadProductionSources(),
            StringComparison.Ordinal);
    }

    /// <summary>General Replace callers use the unified accepted execution request.</summary>
    [Fact]
    public void GeneralReplacePreviewBuildBoundaryStaysTyped()
    {
        string run = ReadText(
            "src/NvtFwCombiner.Application/Composition/CompositionExecutionExperience.cs");
        string cli = ReadText(
            "src/NvtFwCombiner.Cli/ReplaceCliCommandHandler.General.cs");
        string presentation = ReadText(
            "src/NvtFwCombiner.Presentation.Avalonia/ViewModels/ReplacePresentationViewModel.Execution.cs");

        Assert.Contains("ExecuteGeneralReplaceAsync(", run, StringComparison.Ordinal);
        Assert.Contains("AcceptedSessionExecutionInputs.CreateGeneralReplaceBindings(", run, StringComparison.Ordinal);
        Assert.Contains("plan.VirtualArtifacts", run, StringComparison.Ordinal);
        Assert.Contains("RequireGeneralReplaceActionReadiness", run, StringComparison.Ordinal);
        Assert.Contains("services.GeneralAuthoring.PrepareReplaceSessionAsync", cli, StringComparison.Ordinal);
        Assert.Equal(1, CountOccurrences(cli, "services.Execution.ExecuteAsync"));
        Assert.Contains("ResolveAcceptedOutput", cli, StringComparison.Ordinal);
        Assert.Contains("AcceptedCompositionExecutionRequest", cli, StringComparison.Ordinal);
        Assert.Contains("_compositionServices.Execution.ExecuteAsync", presentation, StringComparison.Ordinal);
        Assert.Contains("AcceptedCompositionExecutionRequest", presentation, StringComparison.Ordinal);
        Assert.DoesNotContain("GeneralReplaceAcceptedSessionRunner", presentation, StringComparison.Ordinal);
        Assert.Contains(
            "ExecuteGeneralReplaceAsync(\n        AcceptedCompositionExecutionRequest request,",
            run.ReplaceLineEndings("\n"),
            StringComparison.Ordinal);
        Assert.False(File.Exists(Path.Combine(
            Root.FullName,
            "src",
            "NvtFwCombiner.Bootstrap",
            "CompositionPlanningAdapter.Replace.General.Context.cs")));
        Assert.DoesNotContain("TryCreateGeneralReplaceRunContext", ReadProductionSources(), StringComparison.Ordinal);
        Assert.DoesNotContain("(build, outputPath, token)", cli, StringComparison.Ordinal);
        Assert.DoesNotContain("replaceMode == GeneralReplaceMode\n                        ? build", presentation, StringComparison.Ordinal);
        Assert.DoesNotContain("CapabilityActionKind", run, StringComparison.Ordinal);
    }

    /// <summary>General authoring has one Application admission snapshot from observation through compilation and report.</summary>
    [Fact]
    public void GeneralAuthoringAdmissionStaysApplicationOwnedAndFailClosed()
    {
        string useCase = ReadText(
            "src/NvtFwCombiner.Application/Authoring/GeneralAuthoringAdmissionUseCase.cs");
        string resolver = ReadText(
            "src/NvtFwCombiner.Application/Authoring/GeneralAuthoringResourceLimits.cs");
        string bootstrapAdmission = ReadText(
            "src/NvtFwCombiner.Infrastructure/Composition/BuiltInGeneralAuthoringPlanner.cs");
        string mergeMapping = ReadText(
            "src/NvtFwCombiner.Infrastructure/Composition/BuiltInGeneralAuthoringPlanner.GeneralMerge.Mapping.cs");
        string mergeRun = ReadText(
            "src/NvtFwCombiner.Application/Composition/CompositionExecutionExperience.cs");
        string mergeDisplay = ReadText(
            "src/NvtFwCombiner.Application/MemoryLayout/MemoryLayoutProjector.cs");
        string replaceMapping = ReadText(
            "src/NvtFwCombiner.Infrastructure/Composition/BuiltInGeneralAuthoringPlanner.GeneralReplace.Mapping.cs");
        string replaceRun = ReadText(
            "src/NvtFwCombiner.Application/Composition/CompositionExecutionExperience.cs");
        string savedRules = ReadText(
            "src/NvtFwCombiner.Cli/MergeCliCommandHandler.SavedRules.cs");
        string runner = ReadText(
            "src/NvtFwCombiner.Application/Composition/CompositionExecutionExperience.cs");

        Assert.Contains(
            "public static class GeneralAuthoringAdmissionUseCase",
            useCase,
            StringComparison.Ordinal);
        Assert.Contains("AcceptedFileStamp", useCase, StringComparison.Ordinal);
        Assert.Contains("AcceptedLength", useCase, StringComparison.Ordinal);
        Assert.DoesNotContain("File.", useCase, StringComparison.Ordinal);
        Assert.DoesNotContain("FileInfo", useCase, StringComparison.Ordinal);
        Assert.DoesNotContain("File.", bootstrapAdmission, StringComparison.Ordinal);
        Assert.DoesNotContain("FileInfo", bootstrapAdmission, StringComparison.Ordinal);
        Assert.DoesNotContain("GetSlotOrGlobal", resolver, StringComparison.Ordinal);
        Assert.Contains(
            "TrustedParentSlotMissing",
            resolver,
            StringComparison.Ordinal);
        Assert.Contains(
            "GeneralSavedRuleResourcePolicy",
            savedRules,
            StringComparison.Ordinal);
        Assert.Contains(
            "admission.RequireAdmittedDraft()",
            mergeMapping,
            StringComparison.Ordinal);
        Assert.Contains(
            "admission.RequireAdmittedDraft()",
            replaceMapping,
            StringComparison.Ordinal);
        Assert.DoesNotContain("File.Exists", mergeMapping, StringComparison.Ordinal);
        Assert.DoesNotContain("FileInfo", mergeMapping, StringComparison.Ordinal);
        Assert.DoesNotContain("File.Exists", replaceMapping, StringComparison.Ordinal);
        Assert.DoesNotContain("FileInfo", replaceMapping, StringComparison.Ordinal);
        Assert.Contains(
            "generalAdmission: plan.Admission",
            mergeRun,
            StringComparison.Ordinal);
        Assert.Contains(
            "generalAdmission: plan.Admission",
            replaceRun,
            StringComparison.Ordinal);
        Assert.Contains(
            "GeneralAuthoringAdmissionUseCase.Resolve(",
            bootstrapAdmission,
            StringComparison.Ordinal);
        Assert.DoesNotContain(
            "AdmitGeneralMappingDraft(",
            mergeDisplay,
            StringComparison.Ordinal);
        Assert.Contains(
            "GeneralAuthoringAdmissionResult? generalAdmission",
            runner,
            StringComparison.Ordinal);
    }

    /// <summary>Verifies General Merge workbench target-region ids stay Bootstrap-owned.</summary>
    [Fact]
    public void GeneralMergeWorkbenchIdsStayBootstrapOwned()
    {
        string ids = ReadText("src/NvtFwCombiner.Application/Composition/GeneralMergeIds.cs");
        string mapping = ReadText("src/NvtFwCombiner.Infrastructure/Composition/BuiltInGeneralAuthoringPlanner.GeneralMerge.Mapping.cs");
        string authoring = ReadText(
            "src/NvtFwCombiner.Application/Authoring/GeneralAuthoringMappingUseCase.cs");
        string savedRuleV2 = ReadText(
            "src/NvtFwCombiner.Infrastructure/Composition/SavedRuleV2GeneralMergeDraftLoader.cs");

        Assert.Contains("public const string OutputRegionId = \"general-output\";", ids, StringComparison.Ordinal);
        Assert.Contains("GeneralMergeIds.OutputRegionId", authoring, StringComparison.Ordinal);
        Assert.Contains("GeneralMergeIds.OutputRegionId", savedRuleV2, StringComparison.Ordinal);
        Assert.DoesNotContain("\"general-output\"", mapping, StringComparison.Ordinal);
        Assert.DoesNotContain("\"general-output\"", authoring, StringComparison.Ordinal);
        Assert.DoesNotContain("\"general-output\"", savedRuleV2, StringComparison.Ordinal);
    }

    /// <summary>Verifies the Workbench partials stay split into catalog, Standard Merge, and shared adapter helpers.</summary>
    [Fact]
    public void WorkbenchCompositionServiceConcernsStaySplit()
    {
        string catalog = string.Concat(
            ReadText("src/NvtFwCombiner.Application/Capabilities/CanonicalCapabilityExperience.cs"),
            ReadText("src/NvtFwCombiner.Application/Capabilities/CapabilityProfileSummary.cs"),
            ReadText("src/NvtFwCombiner.Infrastructure/Composition/CanonicalCapabilityDisclosureInventory.cs"),
            ReadText("src/NvtFwCombiner.Infrastructure/Composition/BuiltInV2RegistrationRegistry.cs"));
        string common = ReadText("src/NvtFwCombiner.Application/Composition/CompositionExecutionExperience.cs");
        string runner = common;
        string applicationExecution = ReadText(
            "src/NvtFwCombiner.Application/Composition/AcceptedSessionCompositionExecution.cs");
        string standardMerge = ReadText(
            "src/NvtFwCombiner.Application/Authoring/StandardMergeAuthoringExperience.cs");
        string standardMergeCompilation = string.Concat(
            ReadText("src/NvtFwCombiner.Application/Capabilities/CanonicalCapabilityCompiler.StandardMerge.cs"),
            ReadText("src/NvtFwCombiner.Application/Capabilities/CanonicalCapabilityCompiler.StandardMerge.Routing.cs"));
        string standardMergeBuiltInV2 = ReadText(
            "src/NvtFwCombiner.Infrastructure/Composition/CanonicalCapabilityResolution.StandardMerge.cs");
        string builtInV2Bundle = ReadText("src/NvtFwCombiner.Infrastructure/Composition/BuiltInV2Bundle.cs");
        string builtInV2Registrations = ReadText(
            "src/NvtFwCombiner.Infrastructure/Composition/BuiltInV2RegistrationRegistry.cs");
        string standardMergeRun = common;
        string generalMergeProfile = ReadText(
            "src/NvtFwCombiner.Infrastructure/Composition/BuiltInGeneralAuthoringPlanner.cs");
        string generalMergeEntry = common;
        string generalMergeCandidate = common;
        string generalMergePlanning = ReadText(
            "src/NvtFwCombiner.Infrastructure/Composition/BuiltInGeneralAuthoringPlanner.GeneralMerge.V2.cs");
        string mergeCli = ReadText("src/NvtFwCombiner.Cli/MergeCliCommandHandler.cs");
        string firmwareMetadata = ReadText("src/NvtFwCombiner.Infrastructure/Composition/BuiltInFirmwareInspection.Metadata.cs");
        string firmwareInspection = ReadText("src/NvtFwCombiner.Infrastructure/Composition/BuiltInFirmwareInspection.cs");
        string workbenchModels = ReadText("src/NvtFwCombiner.Application/Composition/CompositionClientModels.cs");
        string outputNaming = ReadText(
            "src/NvtFwCombiner.Application/Composition/AcceptedSessionOutputNameResolver.cs");
        string outputNamingAdapter = ReadText(
            "src/NvtFwCombiner.Application/Composition/CompositionOutputNamingExperience.cs");
        string ctrlRamDisplay = ReadText("src/NvtFwCombiner.Infrastructure/Composition/BuiltInCtrlRamAuthoringAdapter.cs");
        string replacePostbuild = ReadText("src/NvtFwCombiner.Infrastructure/Composition/BuiltInPostbuildProfileResolver.cs");

        Assert.Contains("GetIcIds", catalog, StringComparison.Ordinal);
        Assert.Contains("GetCatalogSummary", catalog, StringComparison.Ordinal);
        Assert.DoesNotContain("IcSupportCatalog.IcIds", catalog, StringComparison.Ordinal);
        Assert.DoesNotContain("BuiltInTpFlashMapCatalog.IcIds", catalog, StringComparison.Ordinal);
        Assert.DoesNotContain("LegacyCombinerPostbuildCatalog.All", catalog, StringComparison.Ordinal);
        Assert.Contains("CapabilityNumberChoice", catalog, StringComparison.Ordinal);
        Assert.Contains("CreateProfileSummary", catalog, StringComparison.Ordinal);
        Assert.Contains("composition.Plan.RequiredInputAddressSpaceIds", catalog, StringComparison.Ordinal);
        Assert.Contains("IcNumberChoicePolicy.GetNumberSelectionChoices", catalog, StringComparison.Ordinal);
        Assert.DoesNotContain("IcNumberChoicePolicy.GetNumberChoices", catalog, StringComparison.Ordinal);
        Assert.Contains("BuiltInPostbuildProfileCatalog.GetProfiles", catalog, StringComparison.Ordinal);
        Assert.DoesNotContain("BuildMetadata", catalog, StringComparison.Ordinal);
        Assert.DoesNotContain("internal sealed record IcMetadata(", catalog, StringComparison.Ordinal);
        Assert.DoesNotContain("DpVersionMetadata? TryReadDpVersionMetadata", firmwareMetadata, StringComparison.Ordinal);
        Assert.DoesNotContain("CmiDpCodeMetadata? TryReadCmiDpCodeMetadata", firmwareMetadata, StringComparison.Ordinal);
        Assert.Contains("InspectFirmwareBatch", firmwareInspection, StringComparison.Ordinal);
        Assert.Contains("DpVersionMetadata", workbenchModels, StringComparison.Ordinal);
        Assert.Contains("CmiDpCodeMetadata", workbenchModels, StringComparison.Ordinal);
        Assert.DoesNotContain("ToRunProfile", common, StringComparison.Ordinal);
        Assert.Contains("new CompositionRunRequest(", applicationExecution, StringComparison.Ordinal);
        Assert.DoesNotContain("new CompositionRunRequest(", runner, StringComparison.Ordinal);
        Assert.DoesNotContain("CompiledCompositionRunAdapter", runner, StringComparison.Ordinal);
        Assert.DoesNotContain("StandardMergeProfilesByIc", standardMergeCompilation, StringComparison.Ordinal);
        Assert.DoesNotContain("BuiltInStandardMergeProfiles", standardMergeCompilation, StringComparison.Ordinal);
        Assert.DoesNotContain("BuiltInStandardMergeProfiles", catalog, StringComparison.Ordinal);
        Assert.Contains("TryGetBuiltInV2StandardMergeCompilation", standardMergeCompilation, StringComparison.Ordinal);
        Assert.DoesNotContain("Nt51920V2", standardMergeCompilation, StringComparison.Ordinal);
        Assert.DoesNotContain("StandardMergeProfilesByIc", standardMerge, StringComparison.Ordinal);
        Assert.DoesNotContain("StandardMergeProfilesByIc", generalMergeProfile, StringComparison.Ordinal);
        Assert.Contains("TryCompileStandardMerge", generalMergeProfile, StringComparison.Ordinal);
        Assert.Contains("ExecuteGeneralMergeAsync", generalMergeCandidate, StringComparison.Ordinal);
        Assert.Contains("CompileLogicalOutput", generalMergePlanning, StringComparison.Ordinal);
        Assert.DoesNotContain("CompileLogicalOutput", generalMergeCandidate, StringComparison.Ordinal);
        Assert.DoesNotContain("CompositionProfileCompiler", generalMergeCandidate, StringComparison.Ordinal);
        Assert.DoesNotContain("RunGeneralMergeV2Async", generalMergeEntry, StringComparison.Ordinal);
        Assert.DoesNotContain("RunGeneralMergeV2Async", mergeCli, StringComparison.Ordinal);
        Assert.False(File.Exists(Path.Combine(
            Root.FullName,
            "src",
            "NvtFwCombiner.Bootstrap",
            "CompositionExecutionAdapter.GeneralMerge.V2.cs")));
        Assert.DoesNotContain("RunStandardMergeAsync", standardMerge, StringComparison.Ordinal);
        Assert.DoesNotContain("GetStandardMergeMemoryDisplay", standardMerge, StringComparison.Ordinal);
        Assert.False(File.Exists(Path.Combine(
            Root.FullName,
            "src",
            "NvtFwCombiner.Bootstrap",
            "CompositionMemoryProjection.StandardMerge.cs")));
        Assert.False(File.Exists(Path.Combine(
            Root.FullName,
            "src",
            "NvtFwCombiner.Bootstrap",
            "CompositionMemoryProjection.AbMerge.cs")));
        Assert.Contains(
            "ExperienceIds.StandardMerge => ExecuteAcceptedCompositionAsync",
            standardMergeRun,
            StringComparison.Ordinal);
        Assert.Contains("ExecuteAcceptedCompositionAsync", standardMergeRun, StringComparison.Ordinal);
        Assert.Equal(2, CountOccurrences(runner, "AcceptedSessionExecutionInputs.CreateBindings"));
        Assert.Contains("AcceptedSessionCompositionExecution.ExecuteAsync", runner, StringComparison.Ordinal);
        Assert.DoesNotContain("TryGetStandardMergeDpInputLength", standardMergeRun, StringComparison.Ordinal);
        Assert.DoesNotContain("TryCompileStandardMerge", standardMergeRun, StringComparison.Ordinal);
        Assert.Contains("BuiltInV2RegistrationRegistry.StandardMerge", standardMergeBuiltInV2, StringComparison.Ordinal);
        Assert.Contains("ReadOnlyCollection<BuiltInV2Registration>", builtInV2Registrations, StringComparison.Ordinal);
        Assert.Contains("ProfileBundleLoader.Load", builtInV2Bundle, StringComparison.Ordinal);
        Assert.Contains("_catalog.Value.Compile", builtInV2Bundle, StringComparison.Ordinal);
        Assert.DoesNotContain("TrustedV2CompositionCompiler", builtInV2Bundle, StringComparison.Ordinal);
        Assert.DoesNotContain("CompositionProfileCompiler", builtInV2Bundle, StringComparison.Ordinal);
        Assert.Contains("TryReadBaseCommonFwVersion", firmwareMetadata, StringComparison.Ordinal);
        Assert.Contains("FirmwareConfigMetadataReader.TryReadBackup", firmwareMetadata, StringComparison.Ordinal);
        Assert.DoesNotContain("FirmwareConfigMetadataReader.TryRead(", firmwareMetadata, StringComparison.Ordinal);
        Assert.DoesNotContain("FirmwareConfigMetadataReader.TryReadAtAbsoluteAddress", firmwareMetadata, StringComparison.Ordinal);
        Assert.DoesNotContain("TryGetFirmwareConfigPrimaryStart", firmwareMetadata, StringComparison.Ordinal);
        Assert.DoesNotContain("HaveEquivalentFirmwareConfigValues", firmwareMetadata, StringComparison.Ordinal);
        Assert.Contains("ReadDpMetadata(", firmwareInspection, StringComparison.Ordinal);
        Assert.Contains("TryReadCanonicalDpcmi", firmwareInspection, StringComparison.Ordinal);
        Assert.DoesNotContain("GenFlashVersionCatalog", firmwareInspection, StringComparison.Ordinal);
        Assert.Contains("InspectFirmware", firmwareInspection, StringComparison.Ordinal);
        Assert.Contains("DisplayCategory", firmwareInspection, StringComparison.Ordinal);
        Assert.DoesNotContain("PostbuildSetup_", firmwareMetadata, StringComparison.Ordinal);
        Assert.DoesNotContain("CreateFlashCodeOutputFileName", firmwareMetadata, StringComparison.Ordinal);
        Assert.DoesNotContain("GetCtrlRamRegions", firmwareMetadata, StringComparison.Ordinal);
        Assert.Contains("CompiledOutputNameResolver.Resolve", outputNaming, StringComparison.Ordinal);
        Assert.Contains("AcceptedSessionOutputNameResolver.Resolve", outputNamingAdapter, StringComparison.Ordinal);
        Assert.DoesNotContain("File.", outputNaming, StringComparison.Ordinal);
        Assert.DoesNotContain("FirmwareConfigMetadataReader", outputNaming, StringComparison.Ordinal);
        Assert.False(File.Exists(Path.Combine(
            Root.FullName,
            "src",
            "NvtFwCombiner.Bootstrap",
            "CompositionOutputNaming.cs")));
        Assert.Contains("MemoryLayoutProjector.ProjectCtrlRamDiscovery", ctrlRamDisplay, StringComparison.Ordinal);
        Assert.Contains("BuiltInTpFlashMapCatalog.GetRegions", ctrlRamDisplay, StringComparison.Ordinal);
        Assert.DoesNotContain("new CtrlRamRegion", ctrlRamDisplay, StringComparison.Ordinal);
        Assert.DoesNotContain("new ReplaceInputSlot", ctrlRamDisplay, StringComparison.Ordinal);
        Assert.DoesNotContain("CreateFlashCodeOutputFileName", ctrlRamDisplay, StringComparison.Ordinal);
        Assert.False(File.Exists(Path.Combine(
            Root.FullName,
            "src",
            "NvtFwCombiner.Bootstrap",
            "CompositionMemoryProjection.Replace.cs")));
        Assert.False(File.Exists(Path.Combine(
            Root.FullName,
            "src",
            "NvtFwCombiner.Bootstrap",
            "CompositionMemoryProjection.Replace.Coverage.cs")));
        Assert.DoesNotContain("FirmwareConfigMetadataReader.TryRead", replacePostbuild, StringComparison.Ordinal);
    }

    /// <summary>Verifies every routed V2 workflow shares one immutable directory/hash bundle registry.</summary>
    [Fact]
    public void BuiltInV2BundlePinsHaveOneOwner()
    {
        string bundle = ReadText("src/NvtFwCombiner.Infrastructure/Composition/BuiltInV2Bundle.cs");
        string trustIndexText = ReadText("profiles/built-in/package-trust-index.json");
        using var trustIndex = JsonDocument.Parse(trustIndexText);
        string registrations = ReadText("src/NvtFwCombiner.Infrastructure/Composition/BuiltInV2RegistrationRegistry.cs");
        string generalMerge = ReadText(
            "src/NvtFwCombiner.Infrastructure/Composition/BuiltInGeneralAuthoringPlanner.GeneralMerge.V2.cs");
        string dpReplace = ReadText(
            "src/NvtFwCombiner.Application/Authoring/DpReplaceAuthoringExperience.cs");

        static bool IsSha256Literal(string value)
        {
            return value.Length == 64 && value.All(static character => character is (>= '0' and <= '9') or (>= 'a' and <= 'f'));
        }

        JsonElement[] entries =
        [
            .. trustIndex.RootElement.GetProperty("bundles").EnumerateArray(),
        ];
        Assert.Equal(26, entries.Length);
        Assert.All(entries, entry =>
            Assert.True(IsSha256Literal(entry.GetProperty("contentHash").GetString()!)));
        Assert.Equal(
            36,
            entries.Sum(static entry => entry.GetProperty("runtimeRegistrations")
                .EnumerateArray()
                .Count(static registration => registration.GetProperty("workflowId").GetString() != "ctrlram-replace")));
        _ = Assert.Single(entries, static entry => entry.GetProperty("bundleDirectory").GetString() ==
            "nt51919-nt51929-nt51932-ab-merge");
        _ = Assert.Single(entries, static entry => entry.GetProperty("bundleDirectory").GetString() ==
            "nt51950-ab-merge");
        Assert.Contains(
            "775c42fba1fbbf1c4c8869656c83c86ce34d612dda3ceed92a93cb4e82f7cd67",
            trustIndexText,
            StringComparison.Ordinal);
        Assert.Equal(
            1,
            CountOccurrences(
                bundle + registrations + generalMerge + dpReplace,
                "new BuiltInV2Bundle("));
        Assert.DoesNotContain(bundle.Split('"'), IsSha256Literal);
        Assert.DoesNotContain(registrations.Split('"'), IsSha256Literal);
        Assert.DoesNotContain(generalMerge.Split('"'), IsSha256Literal);
        Assert.DoesNotContain(dpReplace.Split('"'), IsSha256Literal);
    }

    /// <summary>Verifies the raw Hex Editor stays independent from firmware composition policy and UI file I/O.</summary>
    [Fact]
    public void HexEditorUsesRawBinaryFacadeWithoutUiFirmwareIo()
    {
        string panel = ReadText("src/NvtFwCombiner.Presentation.Avalonia/Views/HexEditorPanel.axaml");
        string viewModel = ReadText("src/NvtFwCombiner.Presentation.Avalonia/ViewModels/HexEditorWorkspaceViewModel.cs");
        string rangeEditing = ReadText(
            "src/NvtFwCombiner.Presentation.Avalonia/ViewModels/HexEditorWorkspaceViewModel.RangeEditing.cs");
        string viewModelDirectory = Path.Combine(
            Root.FullName,
            "src",
            "NvtFwCombiner.Presentation.Avalonia",
            "ViewModels");
        string presentationPartials = string.Concat(
            Directory
                .EnumerateFiles(viewModelDirectory, "HexEditorWorkspaceViewModel*.cs")
                .Order(StringComparer.Ordinal)
                .Select(File.ReadAllText));
        string panelCodeBehind = ReadText("src/NvtFwCombiner.Presentation.Avalonia/Views/HexEditorPanel.axaml.cs");
        string hostSession = ReadText("src/NvtFwCombiner.Infrastructure/Files/RawBinaryEditorFileSession.cs");
        string filePort = ReadText("src/NvtFwCombiner.Application/HexEditor/IRawBinaryEditorFileSession.cs");
        string session = ReadText("src/NvtFwCombiner.Application/HexEditor/RawBinaryEditorSession.cs");

        Assert.Contains("RequestSaveCommand", panel, StringComparison.Ordinal);
        Assert.Contains("InsertZeroBeforeCommand", panelCodeBehind, StringComparison.Ordinal);
        Assert.Contains("DeleteByteCommand", panelCodeBehind, StringComparison.Ordinal);
        Assert.Contains("SetViewportStartRowCommand", panelCodeBehind, StringComparison.Ordinal);
        Assert.DoesNotContain("RawBinaryEditorOperationResult", hostSession, StringComparison.Ordinal);
        Assert.DoesNotContain("RawBinaryEditorViewport", hostSession, StringComparison.Ordinal);
        Assert.DoesNotContain(" State =>", hostSession, StringComparison.Ordinal);
        Assert.DoesNotContain(" CreatePage(", hostSession, StringComparison.Ordinal);
        Assert.DoesNotContain(" GetChangedRanges(", hostSession, StringComparison.Ordinal);
        Assert.DoesNotContain(" OverwriteByte(", hostSession, StringComparison.Ordinal);
        Assert.DoesNotContain(" OverwriteRange(", hostSession, StringComparison.Ordinal);
        Assert.DoesNotContain(" FillRange(", hostSession, StringComparison.Ordinal);
        Assert.DoesNotContain(" InsertZero", hostSession, StringComparison.Ordinal);
        Assert.DoesNotContain(" DeleteByte(", hostSession, StringComparison.Ordinal);
        Assert.DoesNotContain(" Undo(", hostSession, StringComparison.Ordinal);
        Assert.DoesNotContain(" Redo(", hostSession, StringComparison.Ordinal);
        Assert.Contains("TryCopyWorkingBytes", hostSession, StringComparison.Ordinal);
        Assert.DoesNotContain("ToWorkbench", hostSession, StringComparison.Ordinal);
        Assert.False(File.Exists(Path.Combine(
            Root.FullName,
            "src",
            "NvtFwCombiner.Bootstrap",
            "WorkbenchRawBinaryEditorContracts.cs")));
        Assert.Contains("RawBinaryEditorSession _editor = new();", viewModel, StringComparison.Ordinal);
        Assert.Contains("IRawBinaryEditorFileSession _files;", viewModel, StringComparison.Ordinal);
        Assert.Contains("fileSessions.Create(_editor)", viewModel, StringComparison.Ordinal);
        Assert.Contains("interface IRawBinaryEditorFileSession", filePort, StringComparison.Ordinal);
        Assert.DoesNotContain("NvtFwCombiner.Bootstrap", presentationPartials, StringComparison.Ordinal);
        Assert.DoesNotContain("SelectOverwriteModeCommand", presentationPartials, StringComparison.Ordinal);
        Assert.DoesNotContain("SelectFillModeCommand", presentationPartials, StringComparison.Ordinal);
        Assert.DoesNotContain("IsOverwriteModeSelected", rangeEditing, StringComparison.Ordinal);
        Assert.DoesNotContain("UiCompositionRunner", presentationPartials, StringComparison.Ordinal);
        Assert.Contains("RawBinaryEditorSession", session, StringComparison.Ordinal);
        Assert.DoesNotContain("GeneralReplace", panel, StringComparison.Ordinal);
        Assert.DoesNotContain("GeneralReplace", presentationPartials, StringComparison.Ordinal);
        Assert.DoesNotContain("profile", panel, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("postbuild", panel, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("File.", panel, StringComparison.Ordinal);
        Assert.DoesNotContain("File.", presentationPartials, StringComparison.Ordinal);
        Assert.DoesNotContain("Composition", session, StringComparison.Ordinal);
        Assert.DoesNotContain("FlashMap", session, StringComparison.Ordinal);
        Assert.DoesNotContain("ExternalTool", session, StringComparison.Ordinal);
    }
}
