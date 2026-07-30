namespace NvtFwCombiner.Architecture.Tests;

public sealed partial class RepositoryBoundaryTests
{
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
            "src/NvtFwCombiner.Presentation.Avalonia/ViewModels/MainWindowViewModel.ReplaceRefresh.cs");
        string outputNamingViewModel = ReadText(
            "src/NvtFwCombiner.Presentation.Avalonia/ViewModels/MainWindowViewModel.OutputNaming.cs");
        string firmwareInspectionSession = ReadText(
            "src/NvtFwCombiner.Presentation.Avalonia/ViewModels/FirmwareInspectionSession.cs");

        Assert.Contains("SetSlotFileAsync", viewModels, StringComparison.Ordinal);
        Assert.Contains("Task.Run", viewModels, StringComparison.Ordinal);
        Assert.Contains("InspectFirmwareBatch", viewModels, StringComparison.Ordinal);
        Assert.DoesNotContain("public void SetSlotFile(", viewModels, StringComparison.Ordinal);
        Assert.DoesNotContain("RefreshAllSelectedSlotFirmwareFacts", viewModels, StringComparison.Ordinal);
        Assert.DoesNotContain("GetSelectedCtrlRamBasePath", viewModels, StringComparison.Ordinal);
        Assert.DoesNotContain("TryReadFirmwareContextSuggestion", viewModels, StringComparison.Ordinal);
        Assert.DoesNotContain("WorkbenchCompositionService.InspectFirmware", firmwareFacts, StringComparison.Ordinal);
        Assert.DoesNotContain("string? ctrlRamBasePath", replaceRunner, StringComparison.Ordinal);
        Assert.Contains("ctrlRamBasePath: null", replaceRunner, StringComparison.Ordinal);
        Assert.DoesNotContain("File.Exists", replaceRefresh, StringComparison.Ordinal);
        Assert.DoesNotContain("new FileInfo", replaceRefresh, StringComparison.Ordinal);
        Assert.DoesNotContain(
            "FileIdentity.Equals(FirmwareFileIdentity.Capture",
            viewModels,
            StringComparison.Ordinal);
        Assert.Contains("RefreshMergeMemoryMapState", replaceRefresh, StringComparison.Ordinal);
        Assert.Contains("RefreshReplaceMemoryMapState", replaceRefresh, StringComparison.Ordinal);
        Assert.DoesNotContain("ValidateCachedCtrlRamDisplayAsync", viewModels, StringComparison.Ordinal);
        Assert.Contains(
            "CreateFlashCodeOutputFileNameFromInspections",
            firmwareInspectionSession,
            StringComparison.Ordinal);
        Assert.Contains("FirmwareOutputNamingProjection.CreateFlashCodeOutputFileName", outputNamingViewModel, StringComparison.Ordinal);
        Assert.DoesNotContain("FileInfo", outputNamingViewModel, StringComparison.Ordinal);
        Assert.DoesNotContain("File.Exists", outputNamingViewModel, StringComparison.Ordinal);
        Assert.DoesNotContain(
            "WorkbenchCompositionService.CreateFlashCodeOutputFileName(",
            outputNamingViewModel,
            StringComparison.Ordinal);
    }

    /// <summary>Verifies General Merge workbench orchestration, mapping, profile, and report helpers stay split.</summary>
    [Fact]
    public void GeneralMergeWorkbenchConcernsStaySplit()
    {
        string orchestration = ReadText("src/NvtFwCombiner.Bootstrap/WorkbenchCompositionService.GeneralMerge.cs");
        string mapping = ReadText("src/NvtFwCombiner.Bootstrap/WorkbenchCompositionService.GeneralMerge.Mapping.cs");
        string profile = ReadText("src/NvtFwCombiner.Bootstrap/WorkbenchCompositionService.GeneralMerge.Profile.cs");
        string candidate = ReadText("src/NvtFwCombiner.Bootstrap/WorkbenchCompositionService.GeneralMerge.V2.cs");

        Assert.Contains("RunGeneralMergeAsync", orchestration, StringComparison.Ordinal);
        Assert.Contains("GetGeneralMergeMemoryDisplay", orchestration, StringComparison.Ordinal);
        Assert.DoesNotContain("private static bool TryCreateGeneralMergeMappings", orchestration, StringComparison.Ordinal);
        Assert.DoesNotContain("private static CompositionProfileDefinition CreateGeneralMergeProfile", orchestration, StringComparison.Ordinal);
        Assert.DoesNotContain("private static WorkbenchRunResult CreateGeneralMergeReportRunResult", orchestration, StringComparison.Ordinal);
        Assert.False(File.Exists(Path.Combine(
            Root.FullName,
            "src",
            "NvtFwCombiner.Bootstrap",
            "WorkbenchCompositionService.GeneralMerge.Report.cs")));
        Assert.Contains("private static bool TryCreateGeneralMergeMappings", mapping, StringComparison.Ordinal);
        Assert.Contains("public sealed record WorkbenchGeneralMergeMappingInput", mapping, StringComparison.Ordinal);
        Assert.Contains("TryResolveGeneralMergeInitializer", profile, StringComparison.Ordinal);
        Assert.Contains("GeneralMergeDraftState", orchestration, StringComparison.Ordinal);
        Assert.Contains("draft.OutputInitializer", candidate, StringComparison.Ordinal);
        Assert.DoesNotContain(
            "GeneralMergeFillByte",
            orchestration + mapping + profile + candidate,
            StringComparison.Ordinal);
        Assert.DoesNotContain("CompositionProfileDefinition", profile, StringComparison.Ordinal);
        Assert.DoesNotContain("CompositionProfileCompiler", orchestration, StringComparison.Ordinal);
        Assert.Contains("CreateBlockedReportRunResult(", candidate, StringComparison.Ordinal);
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
            "src/NvtFwCombiner.Bootstrap/WorkbenchCompositionService.GeneralAdmission.cs");
        string mergeMapping = ReadText(
            "src/NvtFwCombiner.Bootstrap/WorkbenchCompositionService.GeneralMerge.Mapping.cs");
        string mergeRun = ReadText(
            "src/NvtFwCombiner.Bootstrap/WorkbenchCompositionService.GeneralMerge.V2.cs");
        string mergeDisplay = ReadText(
            "src/NvtFwCombiner.Bootstrap/WorkbenchCompositionService.GeneralMerge.cs");
        string replaceMapping = ReadText(
            "src/NvtFwCombiner.Bootstrap/WorkbenchCompositionService.Replace.General.Mapping.cs");
        string replaceRun = ReadText(
            "src/NvtFwCombiner.Bootstrap/WorkbenchCompositionService.Replace.General.cs");
        string savedRules = ReadText(
            "src/NvtFwCombiner.Bootstrap/MergeCliCommandHandler.SavedRules.cs");
        string runner = ReadText(
            "src/NvtFwCombiner.Bootstrap/WorkbenchCompositionService.Runner.cs");

        Assert.Contains(
            "public sealed class GeneralAuthoringAdmissionUseCase",
            useCase,
            StringComparison.Ordinal);
        Assert.Contains(
            "IGeneralInputResourceObservationPort",
            useCase,
            StringComparison.Ordinal);
        Assert.DoesNotContain("File.", useCase, StringComparison.Ordinal);
        Assert.DoesNotContain("FileInfo", useCase, StringComparison.Ordinal);
        Assert.Contains(
            "GeneralFileResourceObservationAdapter",
            bootstrapAdmission,
            StringComparison.Ordinal);
        Assert.Contains("new FileInfo", bootstrapAdmission, StringComparison.Ordinal);
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
            "generalAdmission: admission",
            mergeRun,
            StringComparison.Ordinal);
        Assert.Contains(
            "generalAdmission: admission",
            replaceRun,
            StringComparison.Ordinal);
        Assert.Contains(
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
        string ids = ReadText("src/NvtFwCombiner.Bootstrap/WorkbenchGeneralMergeIds.cs");
        string mapping = ReadText("src/NvtFwCombiner.Bootstrap/WorkbenchCompositionService.GeneralMerge.Mapping.cs");
        string authoring = ReadText("src/NvtFwCombiner.Bootstrap/WorkbenchCompositionService.GeneralMappingDraft.cs");
        string profile = ReadText("src/NvtFwCombiner.Bootstrap/WorkbenchCompositionService.GeneralMerge.Profile.cs");
        string savedRuleRows = ReadText("src/NvtFwCombiner.Bootstrap/SavedCompositionRuleLoader.MappingRows.cs");

        Assert.Contains("public const string OutputRegionId = \"general-output\";", ids, StringComparison.Ordinal);
        Assert.Contains("WorkbenchGeneralMergeIds.OutputRegionId", authoring, StringComparison.Ordinal);
        Assert.Contains("WorkbenchGeneralMergeIds.OutputRegionId", savedRuleRows, StringComparison.Ordinal);
        Assert.DoesNotContain("\"general-output\"", mapping, StringComparison.Ordinal);
        Assert.DoesNotContain("\"general-output\"", authoring, StringComparison.Ordinal);
        Assert.DoesNotContain("WorkbenchGeneralMergeIds.OutputRegionId", profile, StringComparison.Ordinal);
        Assert.DoesNotContain("\"general-output\"", savedRuleRows, StringComparison.Ordinal);
    }

    /// <summary>Verifies the Workbench partials stay split into catalog, Standard Merge, and shared adapter helpers.</summary>
    [Fact]
    public void WorkbenchCompositionServiceConcernsStaySplit()
    {
        string catalog = ReadText("src/NvtFwCombiner.Bootstrap/WorkbenchCompositionService.Catalog.cs");
        string common = ReadText("src/NvtFwCombiner.Bootstrap/WorkbenchCompositionService.Common.cs");
        string runner = ReadText("src/NvtFwCombiner.Bootstrap/WorkbenchCompositionService.Runner.cs");
        string standardMerge = ReadText("src/NvtFwCombiner.Bootstrap/WorkbenchCompositionService.StandardMerge.cs");
        string standardMergeDisplay = ReadText("src/NvtFwCombiner.Bootstrap/WorkbenchCompositionService.StandardMerge.Display.cs");
        string standardMergeCompilation = ReadText(
            "src/NvtFwCombiner.Bootstrap/WorkbenchCompositionService.StandardMerge.Compilation.cs");
        string standardMergeBuiltInV2 = ReadText(
            "src/NvtFwCombiner.Bootstrap/WorkbenchCompositionService.StandardMerge.BuiltInV2.cs");
        string builtInV2Bundle = ReadText("src/NvtFwCombiner.Bootstrap/BuiltInV2Bundle.cs");
        string builtInV2Registrations = ReadText(
            "src/NvtFwCombiner.Bootstrap/BuiltInV2RegistrationRegistry.cs");
        string standardMergeRun = ReadText("src/NvtFwCombiner.Bootstrap/WorkbenchCompositionService.StandardMerge.Run.cs");
        string generalMergeProfile = ReadText(
            "src/NvtFwCombiner.Bootstrap/WorkbenchCompositionService.GeneralMerge.Profile.cs");
        string generalMerge = ReadText("src/NvtFwCombiner.Bootstrap/WorkbenchCompositionService.GeneralMerge.cs");
        string generalMergeCandidate = ReadText(
            "src/NvtFwCombiner.Bootstrap/WorkbenchCompositionService.GeneralMerge.V2.cs");
        string mergeCli = ReadText("src/NvtFwCombiner.Bootstrap/MergeCliCommandHandler.cs");
        string mergeUi = ReadText("src/NvtFwCombiner.Presentation.Avalonia/UiCompositionRunner.Merge.cs");
        string firmwareMetadata = ReadText("src/NvtFwCombiner.Bootstrap/WorkbenchCompositionService.FirmwareMetadata.cs");
        string firmwareInspection = ReadText("src/NvtFwCombiner.Bootstrap/WorkbenchCompositionService.FirmwareInspection.cs");
        string abInputProjection = ReadText("src/NvtFwCombiner.Bootstrap/WorkbenchAbMergeInputProjection.cs");
        string workbenchModels = ReadText("src/NvtFwCombiner.Bootstrap/WorkbenchCompositionModels.cs");
        string outputNaming = ReadText("src/NvtFwCombiner.Bootstrap/WorkbenchCompositionService.OutputNaming.cs");
        string ctrlRamDisplay = ReadText("src/NvtFwCombiner.Bootstrap/WorkbenchCompositionService.CtrlRamDisplay.cs");
        string replaceDisplay = ReadText("src/NvtFwCombiner.Bootstrap/WorkbenchCompositionService.Replace.Display.cs");
        string replaceCoverage = ReadText("src/NvtFwCombiner.Bootstrap/WorkbenchCompositionService.Replace.Coverage.cs");
        string replacePostbuild = ReadText("src/NvtFwCombiner.Bootstrap/WorkbenchCompositionService.Replace.Postbuild.cs");

        Assert.Contains("GetSupportedIcIds", catalog, StringComparison.Ordinal);
        Assert.Contains("GetSettingsSnapshot", catalog, StringComparison.Ordinal);
        Assert.Contains("IcSupportCatalog.IcIds", catalog, StringComparison.Ordinal);
        Assert.DoesNotContain("BuiltInTpFlashMapCatalog.IcIds", catalog, StringComparison.Ordinal);
        Assert.DoesNotContain("LegacyCombinerPostbuildCatalog.All", catalog, StringComparison.Ordinal);
        Assert.Contains("WorkbenchIcNumberChoice", catalog, StringComparison.Ordinal);
        Assert.Contains("CreateProfileSummary", catalog, StringComparison.Ordinal);
        Assert.Contains("composition.Plan.RequiredInputAddressSpaceIds", catalog, StringComparison.Ordinal);
        Assert.Contains("IcNumberChoicePolicy.GetNumberSelectionChoices", catalog, StringComparison.Ordinal);
        Assert.DoesNotContain("IcNumberChoicePolicy.GetNumberChoices", catalog, StringComparison.Ordinal);
        Assert.DoesNotContain("GetNumberChoices", catalog, StringComparison.Ordinal);
        Assert.Contains("BuiltInPostbuildProfileCatalog.GetProfiles", catalog, StringComparison.Ordinal);
        Assert.DoesNotContain("BuildMetadata", catalog, StringComparison.Ordinal);
        Assert.DoesNotContain("internal sealed record IcMetadata(", catalog, StringComparison.Ordinal);
        Assert.Contains("WorkbenchDpVersionMetadata? TryReadDpVersionMetadata", firmwareMetadata, StringComparison.Ordinal);
        Assert.Contains("WorkbenchCmiDpCodeMetadata? TryReadCmiDpCodeMetadata", firmwareMetadata, StringComparison.Ordinal);
        Assert.Contains("WorkbenchDpVersionMetadata", workbenchModels, StringComparison.Ordinal);
        Assert.Contains("WorkbenchCmiDpCodeMetadata", workbenchModels, StringComparison.Ordinal);
        Assert.DoesNotContain("ToRunProfile", common, StringComparison.Ordinal);
        Assert.Contains("CompositionRunRequest request = new(", runner, StringComparison.Ordinal);
        Assert.DoesNotContain("CompiledCompositionRunAdapter", runner, StringComparison.Ordinal);
        Assert.Contains("internal static string FormatIssues", common, StringComparison.Ordinal);
        Assert.Contains("WorkbenchCompositionService.FormatIssues", abInputProjection, StringComparison.Ordinal);
        Assert.DoesNotContain("private static string FormatIssues", abInputProjection, StringComparison.Ordinal);
        Assert.DoesNotContain("StandardMergeProfilesByIc", standardMergeCompilation, StringComparison.Ordinal);
        Assert.DoesNotContain("BuiltInStandardMergeProfiles", standardMergeCompilation, StringComparison.Ordinal);
        Assert.DoesNotContain("BuiltInStandardMergeProfiles", catalog, StringComparison.Ordinal);
        Assert.Contains("TryGetBuiltInV2StandardMergeCompilation", standardMergeCompilation, StringComparison.Ordinal);
        Assert.DoesNotContain("Nt51920V2", standardMergeCompilation, StringComparison.Ordinal);
        Assert.DoesNotContain("StandardMergeProfilesByIc", standardMerge, StringComparison.Ordinal);
        Assert.DoesNotContain("StandardMergeProfilesByIc", generalMergeProfile, StringComparison.Ordinal);
        Assert.Contains("TryCompileStandardMerge", generalMergeProfile, StringComparison.Ordinal);
        Assert.Contains("RunGeneralMergeV2Async", generalMergeCandidate, StringComparison.Ordinal);
        Assert.Contains("CompileLogicalOutput", generalMergeCandidate, StringComparison.Ordinal);
        Assert.DoesNotContain("CompositionProfileCompiler", generalMergeCandidate, StringComparison.Ordinal);
        Assert.Contains("RunGeneralMergeV2Async", generalMerge, StringComparison.Ordinal);
        Assert.DoesNotContain("RunGeneralMergeV2Async", mergeCli, StringComparison.Ordinal);
        Assert.DoesNotContain("RunGeneralMergeV2Async", mergeUi, StringComparison.Ordinal);
        Assert.DoesNotContain("RunStandardMergeAsync", standardMerge, StringComparison.Ordinal);
        Assert.DoesNotContain("GetStandardMergeMemoryDisplay", standardMerge, StringComparison.Ordinal);
        Assert.Contains("GetStandardMergeMemoryDisplay", standardMergeDisplay, StringComparison.Ordinal);
        Assert.Equal(1, CountOccurrences(standardMergeDisplay, "public static WorkbenchMemoryDisplay GetStandardMergeMemoryDisplay("));
        Assert.DoesNotContain("private static bool TryResolveStandardMergeProfileForDisplay", standardMergeDisplay, StringComparison.Ordinal);
        Assert.Contains("TryCompileStandardMerge", standardMergeDisplay, StringComparison.Ordinal);
        Assert.Equal(1, CountOccurrences(standardMergeDisplay, "TryCompileStandardMerge("));
        Assert.Contains("RunStandardMergeAsync", standardMergeRun, StringComparison.Ordinal);
        Assert.Contains("TryGetStandardMergeDpInputLength", standardMergeRun, StringComparison.Ordinal);
        Assert.Contains("TryCompileStandardMerge", standardMergeRun, StringComparison.Ordinal);
        Assert.Contains("BuiltInV2RegistrationRegistry.StandardMerge", standardMergeBuiltInV2, StringComparison.Ordinal);
        Assert.Contains("ReadOnlyCollection<BuiltInV2Registration>", builtInV2Registrations, StringComparison.Ordinal);
        Assert.Contains("ProfileBundleLoader.Load", builtInV2Bundle, StringComparison.Ordinal);
        Assert.Contains("TrustedV2CompositionCompiler.Compile", builtInV2Bundle, StringComparison.Ordinal);
        Assert.DoesNotContain("CompositionProfileCompiler", builtInV2Bundle, StringComparison.Ordinal);
        Assert.Contains("TryReadBaseCommonFwVersion", firmwareMetadata, StringComparison.Ordinal);
        Assert.Contains("FirmwareConfigMetadataReader.TryReadBackup", firmwareMetadata, StringComparison.Ordinal);
        Assert.DoesNotContain("FirmwareConfigMetadataReader.TryRead(", firmwareMetadata, StringComparison.Ordinal);
        Assert.DoesNotContain("FirmwareConfigMetadataReader.TryReadAtAbsoluteAddress", firmwareMetadata, StringComparison.Ordinal);
        Assert.DoesNotContain("TryGetFirmwareConfigPrimaryStart", firmwareMetadata, StringComparison.Ordinal);
        Assert.DoesNotContain("HaveEquivalentFirmwareConfigValues", firmwareMetadata, StringComparison.Ordinal);
        Assert.Contains("ReadDpVersionMetadata(icId, image)", firmwareMetadata, StringComparison.Ordinal);
        Assert.Contains("GenFlashVersionCatalog.TryReadDpVersion", firmwareInspection, StringComparison.Ordinal);
        Assert.Contains("InspectFirmware", firmwareInspection, StringComparison.Ordinal);
        Assert.Contains("DisplayCategory", firmwareInspection, StringComparison.Ordinal);
        Assert.DoesNotContain("PostbuildSetup_", firmwareMetadata, StringComparison.Ordinal);
        Assert.DoesNotContain("CreateFlashCodeOutputFileName", firmwareMetadata, StringComparison.Ordinal);
        Assert.DoesNotContain("GetCtrlRamRegions", firmwareMetadata, StringComparison.Ordinal);
        Assert.Contains("CreateFlashCodeOutputFileName", outputNaming, StringComparison.Ordinal);
        Assert.Contains("FindDpVersionToken", outputNaming, StringComparison.Ordinal);
        Assert.Contains("FindTpVersionToken", outputNaming, StringComparison.Ordinal);
        Assert.DoesNotContain("GenFlashVersionCatalog", outputNaming, StringComparison.Ordinal);
        Assert.DoesNotContain("OutputMainAbsoluteAddress", outputNaming, StringComparison.Ordinal);
        Assert.DoesNotContain("InputRelativeOffset", outputNaming, StringComparison.Ordinal);
        Assert.DoesNotContain("FirmwareConfigMetadataReader.TryRead", outputNaming, StringComparison.Ordinal);
        Assert.Contains("GetCtrlRamRegions", ctrlRamDisplay, StringComparison.Ordinal);
        Assert.Contains("BuiltInTpFlashMapCatalog.GetRegions", ctrlRamDisplay, StringComparison.Ordinal);
        Assert.DoesNotContain("CreateFlashCodeOutputFileName", ctrlRamDisplay, StringComparison.Ordinal);
        Assert.Equal(1, CountOccurrences(replaceDisplay, "public static WorkbenchMemoryDisplay GetReplaceMemoryDisplay("));
        Assert.Contains("CreateReplaceCoverageSegments", replaceDisplay, StringComparison.Ordinal);
        Assert.Contains("CreateReplaceCoverageSegments", replaceCoverage, StringComparison.Ordinal);
        Assert.DoesNotContain("FirmwareConfigMetadataReader.TryRead", replacePostbuild, StringComparison.Ordinal);
    }

    /// <summary>Verifies every routed V2 workflow shares one immutable directory/hash bundle registry.</summary>
    [Fact]
    public void BuiltInV2BundlePinsHaveOneOwner()
    {
        string bundle = ReadText("src/NvtFwCombiner.Bootstrap/BuiltInV2Bundle.cs");
        string registrations = ReadText("src/NvtFwCombiner.Bootstrap/BuiltInV2RegistrationRegistry.cs");
        string generalMerge = ReadText(
            "src/NvtFwCombiner.Bootstrap/WorkbenchCompositionService.GeneralMerge.V2.cs");
        string dpReplace = ReadText(
            "src/NvtFwCombiner.Bootstrap/WorkbenchCompositionService.Replace.Dp.BuiltInV2.cs");

        static bool IsSha256Literal(string value)
        {
            return value.Length == 64 && value.All(static character => character is (>= '0' and <= '9') or (>= 'a' and <= 'f'));
        }

        Assert.Equal(25, bundle.Split('"').Count(IsSha256Literal));
        Assert.Equal(35, CountOccurrences(registrations, "BuiltInV2BundleRegistry.All[\""));
        Assert.Equal(1, CountOccurrences(bundle, "nt51919-nt51929-nt51932-ab-merge"));
        Assert.Equal(1, CountOccurrences(bundle, "nt51950-ab-merge"));
        Assert.Contains(
            "abdd907710be94470937f4f6ee9c250e9ec1f90c4cbd1d10134584ef15878206",
            bundle,
            StringComparison.Ordinal);
        Assert.Equal(
            1,
            CountOccurrences(
                bundle + registrations + generalMerge + dpReplace,
                "new BuiltInV2Bundle("));
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
        string hostSession = ReadText("src/NvtFwCombiner.Bootstrap/WorkbenchRawBinaryEditorSession.cs");
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
        Assert.Contains("WorkbenchRawBinaryEditorSession _files;", viewModel, StringComparison.Ordinal);
        Assert.Contains("new WorkbenchRawBinaryEditorSession(_editor)", viewModel, StringComparison.Ordinal);
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
