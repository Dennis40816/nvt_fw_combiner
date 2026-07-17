namespace NvtFwCombiner.Architecture.Tests;

public sealed partial class RepositoryBoundaryTests
{
    /// <summary>Verifies General Merge workbench orchestration, mapping, profile, and report helpers stay split.</summary>
    [Fact]
    public void GeneralMergeWorkbenchConcernsStaySplit()
    {
        string orchestration = ReadText("src/NvtFwCombiner.Bootstrap/WorkbenchCompositionService.GeneralMerge.cs");
        string mapping = ReadText("src/NvtFwCombiner.Bootstrap/WorkbenchCompositionService.GeneralMerge.Mapping.cs");
        string profile = ReadText("src/NvtFwCombiner.Bootstrap/WorkbenchCompositionService.GeneralMerge.Profile.cs");
        string report = ReadText("src/NvtFwCombiner.Bootstrap/WorkbenchCompositionService.GeneralMerge.Report.cs");

        Assert.Contains("RunGeneralMergeAsync", orchestration, StringComparison.Ordinal);
        Assert.Contains("GetGeneralMergeMemoryMapRows", orchestration, StringComparison.Ordinal);
        Assert.DoesNotContain("private static bool TryCreateGeneralMergeMappings", orchestration, StringComparison.Ordinal);
        Assert.DoesNotContain("private static CompositionProfileDefinition CreateGeneralMergeProfile", orchestration, StringComparison.Ordinal);
        Assert.DoesNotContain("private static WorkbenchRunResult CreateGeneralMergeReportRunResult", orchestration, StringComparison.Ordinal);
        Assert.Contains("private static bool TryCreateGeneralMergeMappings", mapping, StringComparison.Ordinal);
        Assert.Contains("public sealed record WorkbenchGeneralMergeMappingInput", mapping, StringComparison.Ordinal);
        Assert.DoesNotContain("CompositionProfileDefinition", profile, StringComparison.Ordinal);
        Assert.DoesNotContain("CompositionProfileCompiler", orchestration, StringComparison.Ordinal);
        Assert.Contains("private static WorkbenchRunResult CreateGeneralMergeReportRunResult", report, StringComparison.Ordinal);
    }

    /// <summary>Verifies General Merge workbench target-region ids stay Bootstrap-owned.</summary>
    [Fact]
    public void GeneralMergeWorkbenchIdsStayBootstrapOwned()
    {
        string ids = ReadText("src/NvtFwCombiner.Bootstrap/WorkbenchGeneralMergeIds.cs");
        string mapping = ReadText("src/NvtFwCombiner.Bootstrap/WorkbenchCompositionService.GeneralMerge.Mapping.cs");
        string profile = ReadText("src/NvtFwCombiner.Bootstrap/WorkbenchCompositionService.GeneralMerge.Profile.cs");
        string savedRuleRows = ReadText("src/NvtFwCombiner.Bootstrap/SavedCompositionRuleLoader.MappingRows.cs");

        Assert.Contains("public const string OutputRegionId = \"general-output\";", ids, StringComparison.Ordinal);
        Assert.Contains("WorkbenchGeneralMergeIds.OutputRegionId", mapping, StringComparison.Ordinal);
        Assert.Contains("WorkbenchGeneralMergeIds.OutputRegionId", savedRuleRows, StringComparison.Ordinal);
        Assert.DoesNotContain("\"general-output\"", mapping, StringComparison.Ordinal);
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
        string standardMergeCoverage = ReadText("src/NvtFwCombiner.Bootstrap/WorkbenchCompositionService.StandardMerge.Coverage.cs");
        string standardMergeDisplayProfile = ReadText("src/NvtFwCombiner.Bootstrap/WorkbenchCompositionService.StandardMerge.DisplayProfile.cs");
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
        string workbenchModels = ReadText("src/NvtFwCombiner.Bootstrap/WorkbenchCompositionModels.cs");
        string outputNaming = ReadText("src/NvtFwCombiner.Bootstrap/WorkbenchCompositionService.OutputNaming.cs");
        string ctrlRamDisplay = ReadText("src/NvtFwCombiner.Bootstrap/WorkbenchCompositionService.CtrlRamDisplay.cs");
        string replaceDisplay = ReadText("src/NvtFwCombiner.Bootstrap/WorkbenchCompositionService.Replace.Display.cs");
        string replaceCoverage = ReadText("src/NvtFwCombiner.Bootstrap/WorkbenchCompositionService.Replace.Coverage.cs");
        string replacePostbuild = ReadText("src/NvtFwCombiner.Bootstrap/WorkbenchCompositionService.Replace.Postbuild.cs");
        string icMetadata = ReadText("src/NvtFwCombiner.Bootstrap/IcMetadataFacade.cs");

        Assert.Contains("GetSupportedIcIds", catalog, StringComparison.Ordinal);
        Assert.Contains("GetSettingsSnapshot", catalog, StringComparison.Ordinal);
        Assert.Contains("IcMetadataFacade.IcIds", catalog, StringComparison.Ordinal);
        Assert.DoesNotContain("IcSupportCatalog.IcIds", catalog, StringComparison.Ordinal);
        Assert.DoesNotContain("TpFlashMapCatalog.IcIds", catalog, StringComparison.Ordinal);
        Assert.DoesNotContain("LegacyCombinerPostbuildCatalog.All", catalog, StringComparison.Ordinal);
        Assert.Contains("internal static class IcMetadataFacade", icMetadata, StringComparison.Ordinal);
        Assert.DoesNotContain("public static class IcMetadataFacade", icMetadata, StringComparison.Ordinal);
        Assert.Contains("WorkbenchIcNumberChoice", catalog, StringComparison.Ordinal);
        Assert.Contains("CreateProfileSummary", catalog, StringComparison.Ordinal);
        Assert.Contains("composition.Plan.RequiredInputAddressSpaceIds", catalog, StringComparison.Ordinal);
        Assert.Contains("TpFlashMapCatalog.GetNumberChoices", icMetadata, StringComparison.Ordinal);
        Assert.Contains("BuiltInPostbuildProfileCatalog.GetProfiles", icMetadata, StringComparison.Ordinal);
        Assert.DoesNotContain("BuildMetadata", icMetadata, StringComparison.Ordinal);
        Assert.DoesNotContain("internal sealed record IcMetadata(", icMetadata, StringComparison.Ordinal);
        Assert.Contains("WorkbenchDpVersionMetadata? TryReadDpVersionMetadata", firmwareMetadata, StringComparison.Ordinal);
        Assert.Contains("WorkbenchCmiDpCodeMetadata? TryReadCmiDpCodeMetadata", firmwareMetadata, StringComparison.Ordinal);
        Assert.Contains("WorkbenchDpVersionMetadata", workbenchModels, StringComparison.Ordinal);
        Assert.Contains("WorkbenchCmiDpCodeMetadata", workbenchModels, StringComparison.Ordinal);
        Assert.DoesNotContain("ToRunProfile", common, StringComparison.Ordinal);
        Assert.Contains("CompositionRunRequest request = new(", runner, StringComparison.Ordinal);
        Assert.DoesNotContain("CompiledCompositionRunAdapter", runner, StringComparison.Ordinal);
        Assert.Contains("private static string FormatIssues", common, StringComparison.Ordinal);
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
        Assert.DoesNotContain("GetStandardMergeMemoryMapRows", standardMerge, StringComparison.Ordinal);
        Assert.Contains("GetStandardMergeMemoryMapRows", standardMergeDisplay, StringComparison.Ordinal);
        Assert.Equal(1, CountOccurrences(standardMergeDisplay, "public static IReadOnlyList<WorkbenchMemoryMapRow> GetStandardMergeMemoryMapRows("));
        Assert.DoesNotContain("GetStandardMergeCoverageSegments", standardMergeDisplay, StringComparison.Ordinal);
        Assert.DoesNotContain("private static bool TryResolveStandardMergeProfileForDisplay", standardMergeDisplay, StringComparison.Ordinal);
        Assert.Contains("TryCompileStandardMerge", standardMergeDisplay, StringComparison.Ordinal);
        Assert.Contains("GetStandardMergeCoverageSegments", standardMergeCoverage, StringComparison.Ordinal);
        Assert.Equal(1, CountOccurrences(standardMergeCoverage, "public static IReadOnlyList<WorkbenchMemoryCoverageSegment> GetStandardMergeCoverageSegments("));
        Assert.Contains("TryCompileStandardMerge", standardMergeCoverage, StringComparison.Ordinal);
        Assert.Contains("GetStandardMergeMemoryRangeLabel", standardMergeDisplayProfile, StringComparison.Ordinal);
        Assert.Equal(1, CountOccurrences(standardMergeDisplayProfile, "public static string GetStandardMergeMemoryRangeLabel("));
        Assert.DoesNotContain("TryResolveStandardMergeProfileForDisplay", standardMergeDisplayProfile, StringComparison.Ordinal);
        Assert.Contains("TryCompileStandardMerge", standardMergeDisplayProfile, StringComparison.Ordinal);
        Assert.Contains("RunStandardMergeAsync", standardMergeRun, StringComparison.Ordinal);
        Assert.Contains("TryGetStandardMergeDpInputLength", standardMergeRun, StringComparison.Ordinal);
        Assert.Contains("TryCompileStandardMerge", standardMergeRun, StringComparison.Ordinal);
        Assert.Contains("BuiltInV2RegistrationRegistry.StandardMerge", standardMergeBuiltInV2, StringComparison.Ordinal);
        Assert.Contains("ReadOnlyCollection<BuiltInV2StandardMergeRegistration>", builtInV2Registrations, StringComparison.Ordinal);
        Assert.Contains("ProfileBundleLoader.Load", builtInV2Bundle, StringComparison.Ordinal);
        Assert.Contains("TrustedV2CompositionCompiler.Compile", builtInV2Bundle, StringComparison.Ordinal);
        Assert.DoesNotContain("CompositionProfileCompiler", builtInV2Bundle, StringComparison.Ordinal);
        Assert.Contains("TryReadBaseCommonFwVersion", firmwareMetadata, StringComparison.Ordinal);
        Assert.Contains("FirmwareConfigMetadataReader.TryReadBackup", firmwareMetadata, StringComparison.Ordinal);
        Assert.DoesNotContain("FirmwareConfigMetadataReader.TryRead(", firmwareMetadata, StringComparison.Ordinal);
        Assert.DoesNotContain("FirmwareConfigMetadataReader.TryReadAtAbsoluteAddress", firmwareMetadata, StringComparison.Ordinal);
        Assert.DoesNotContain("TryGetFirmwareConfigPrimaryStart", firmwareMetadata, StringComparison.Ordinal);
        Assert.DoesNotContain("HaveEquivalentFirmwareConfigValues", firmwareMetadata, StringComparison.Ordinal);
        Assert.Contains("GenFlashVersionCatalog.TryReadDpVersion", firmwareMetadata, StringComparison.Ordinal);
        Assert.Contains("DisplayCategory", firmwareMetadata, StringComparison.Ordinal);
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
        Assert.Contains("TpFlashMapCatalog.GetCtrlRamRegions", ctrlRamDisplay, StringComparison.Ordinal);
        Assert.DoesNotContain("CreateFlashCodeOutputFileName", ctrlRamDisplay, StringComparison.Ordinal);
        Assert.Contains("GetReplaceMemoryMapRows", replaceDisplay, StringComparison.Ordinal);
        Assert.Equal(1, CountOccurrences(replaceDisplay, "public static string GetReplaceMemoryRangeLabel("));
        Assert.Contains("GetReplaceMemoryRangeLabel", replaceDisplay, StringComparison.Ordinal);
        Assert.DoesNotContain("GetReplaceCoverageSegments", replaceDisplay, StringComparison.Ordinal);
        Assert.Contains("GetReplaceCoverageSegments", replaceCoverage, StringComparison.Ordinal);
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

        Assert.Equal(17, bundle.Split('"').Count(IsSha256Literal));
        Assert.Equal(28, CountOccurrences(registrations, "BuiltInV2BundleRegistry.All[\""));
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
        string panelCodeBehind = ReadText("src/NvtFwCombiner.Presentation.Avalonia/Views/HexEditorPanel.axaml.cs");
        string hostSession = ReadText("src/NvtFwCombiner.Bootstrap/WorkbenchRawBinaryEditorSession.cs");
        string session = ReadText("src/NvtFwCombiner.Application/HexEditor/RawBinaryEditorSession.cs");

        Assert.Contains("RequestSaveCommand", panel, StringComparison.Ordinal);
        Assert.Contains("InsertZeroBeforeCommand", panelCodeBehind, StringComparison.Ordinal);
        Assert.Contains("DeleteByteCommand", panelCodeBehind, StringComparison.Ordinal);
        Assert.Contains("SetViewportStartRowCommand", panelCodeBehind, StringComparison.Ordinal);
        Assert.Contains("RawBinaryEditorOperationResult", hostSession, StringComparison.Ordinal);
        Assert.Contains("RawBinaryEditorViewport", hostSession, StringComparison.Ordinal);
        Assert.DoesNotContain("ToWorkbench", hostSession, StringComparison.Ordinal);
        Assert.False(File.Exists(Path.Combine(
            Root.FullName,
            "src",
            "NvtFwCombiner.Bootstrap",
            "WorkbenchRawBinaryEditorContracts.cs")));
        Assert.Contains("WorkbenchRawBinaryEditorSession _session = new();", viewModel, StringComparison.Ordinal);
        Assert.DoesNotContain("SelectOverwriteModeCommand", viewModel + rangeEditing, StringComparison.Ordinal);
        Assert.DoesNotContain("SelectFillModeCommand", viewModel + rangeEditing, StringComparison.Ordinal);
        Assert.DoesNotContain("IsOverwriteModeSelected", rangeEditing, StringComparison.Ordinal);
        Assert.DoesNotContain("UiCompositionRunner", viewModel, StringComparison.Ordinal);
        Assert.Contains("RawBinaryEditorSession", session, StringComparison.Ordinal);
        Assert.DoesNotContain("GeneralReplace", panel, StringComparison.Ordinal);
        Assert.DoesNotContain("GeneralReplace", viewModel, StringComparison.Ordinal);
        Assert.DoesNotContain("profile", panel, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("postbuild", panel, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("File.", panel, StringComparison.Ordinal);
        Assert.DoesNotContain("File.", viewModel, StringComparison.Ordinal);
        Assert.DoesNotContain("Composition", session, StringComparison.Ordinal);
        Assert.DoesNotContain("FlashMap", session, StringComparison.Ordinal);
        Assert.DoesNotContain("ExternalTool", session, StringComparison.Ordinal);
    }
}
