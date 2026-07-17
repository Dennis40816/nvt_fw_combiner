namespace NvtFwCombiner.Architecture.Tests;

public sealed partial class RepositoryBoundaryTests
{
    /// <summary>Verifies the Presentation projection keeps only UI-owned contract adaptation.</summary>
    [Fact]
    public void UiCompositionRunnerConcernsStaySplit()
    {
        string catalog = ReadText("src/NvtFwCombiner.Presentation.Avalonia/UiCompositionRunner.Catalog.cs");
        string common = ReadText("src/NvtFwCombiner.Presentation.Avalonia/UiCompositionRunner.Common.cs");
        string facts = ReadText("src/NvtFwCombiner.Presentation.Avalonia/UiCompositionRunner.FirmwareFacts.cs");
        string merge = ReadText("src/NvtFwCombiner.Presentation.Avalonia/UiCompositionRunner.Merge.cs");
        string replace = ReadText("src/NvtFwCombiner.Presentation.Avalonia/UiCompositionRunner.Replace.cs");
        string bindings = ReadText(
            "src/NvtFwCombiner.Presentation.Avalonia/ViewModels/MainWindowViewModel.Bindings.cs");
        string mergeViewModel = ReadText(
            "src/NvtFwCombiner.Presentation.Avalonia/ViewModels/MainWindowViewModel.Merge.cs");
        string replaceViewModel = ReadText(
            "src/NvtFwCombiner.Presentation.Avalonia/ViewModels/MainWindowViewModel.Replace.cs");

        Assert.Contains("GetNumberSelectionChoices", catalog, StringComparison.Ordinal);
        Assert.DoesNotContain("GetSupportedIcIds", catalog, StringComparison.Ordinal);
        Assert.DoesNotContain("GetDefaultIcId", catalog, StringComparison.Ordinal);
        Assert.DoesNotContain("GetSettingsSnapshot", catalog, StringComparison.Ordinal);
        Assert.Contains("private static MemoryMapRowViewModel ToMemoryMapRow", common, StringComparison.Ordinal);
        Assert.Contains("GetFirmwareSlotFacts", facts, StringComparison.Ordinal);
        Assert.DoesNotContain("CreateFlashCodeOutputFileName", facts, StringComparison.Ordinal);
        Assert.Contains("WorkbenchReplaceModes", ReadText("src/NvtFwCombiner.Presentation.Avalonia/ViewModels/MainWindowViewModel.Construction.cs"), StringComparison.Ordinal);
        Assert.Contains("GetStandardMergeMemoryMapRows", merge, StringComparison.Ordinal);
        Assert.DoesNotContain("RunGeneralMergeAsync", merge, StringComparison.Ordinal);
        Assert.Contains("GetReplaceMemoryMapRows", replace, StringComparison.Ordinal);
        Assert.DoesNotContain("RunReplaceAsync", replace, StringComparison.Ordinal);
        Assert.Contains("WorkbenchCompositionService.GetSupportedIcIds", bindings, StringComparison.Ordinal);
        Assert.Contains("WorkbenchCompositionService.RunStandardMergeAsync", mergeViewModel, StringComparison.Ordinal);
        Assert.Contains("WorkbenchCompositionService.RunReplaceAsync", replaceViewModel, StringComparison.Ordinal);
    }

    /// <summary>Verifies all composition commands share one UI-owned run lifecycle.</summary>
    [Fact]
    public void CompositionCommandsShareRunLifecycle()
    {
        string lifecycle = ReadText(
            "src/NvtFwCombiner.Presentation.Avalonia/ViewModels/MainWindowViewModel.RunLifecycle.cs");
        string merge = ReadText(
            "src/NvtFwCombiner.Presentation.Avalonia/ViewModels/MainWindowViewModel.Merge.cs");
        string replace = ReadText(
            "src/NvtFwCombiner.Presentation.Avalonia/ViewModels/MainWindowViewModel.Replace.cs");

        Assert.Equal(2, CountOccurrences(merge, "return RunCompositionAsync("));
        Assert.Equal(1, CountOccurrences(replace, "return RunCompositionAsync("));
        Assert.Contains("WorkbenchCompositionService.RunStandardMergeAsync", merge, StringComparison.Ordinal);
        Assert.Contains("WorkbenchCompositionService.RunGeneralMergeAsync", merge, StringComparison.Ordinal);
        Assert.Contains("WorkbenchCompositionService.RunReplaceAsync", replace, StringComparison.Ordinal);
        Assert.Contains("ApplyRunResult(result, build);", lifecycle, StringComparison.Ordinal);
        Assert.Contains("loadErrorReport(action, exception.Message);", lifecycle, StringComparison.Ordinal);
        Assert.Contains("CompleteRun(cancellationSource);", lifecycle, StringComparison.Ordinal);
        Assert.Equal(
            1,
            CountOccurrences(
                ReadViewModelPartials(),
                "catch (OperationCanceledException) when (cancellationSource is { IsCancellationRequested: true })"));
        Assert.Equal(
            1,
            CountOccurrences(
                ReadViewModelPartials(),
                "catch (Exception exception) when (exception is InvalidOperationException or IOException or UnauthorizedAccessException or ArgumentException)"));
    }

    /// <summary>Prevents retired, unbound shell inspector projections from returning.</summary>
    [Fact]
    public void ShellViewModelOmitsUnboundInspectorCompatibilityState()
    {
        string viewModel = ReadViewModelPartials();

        foreach (string retiredName in new[]
                 {
                     "ActiveMergeRows",
                     "ActiveReplaceRows",
                     "ShowNormalMergeCommand",
                     "ShowGeneralMergeCommand",
                     "RemoveGeneralMergeMappingCommand",
                     "RemoveGeneralReplaceMappingCommand",
                     "GetCtrlRamRegionSummary",
                     "FirmwareIcMismatchSlotTitle",
                     "FooterStatus",
                     "SettingsPreferenceRows",
                     "CanPreviewStandardMerge",
                     "CanBuildStandardMerge",
                     "CanPreviewMerge",
                     "CanPreviewReplace",
                 })
        {
            Assert.DoesNotContain(retiredName, viewModel, StringComparison.Ordinal);
        }
    }

    /// <summary>Prevents retired, unbound report projections from returning.</summary>
    [Fact]
    public void ReportReviewOmitsUnboundCompatibilityProjections()
    {
        string report = string.Join(
            Environment.NewLine,
            Directory.GetFiles(
                    Path.Combine(Root.FullName, "src/NvtFwCombiner.Presentation.Avalonia/ViewModels"),
                    "Report*ViewModel*.cs")
                .Order(StringComparer.Ordinal)
                .Select(File.ReadAllText));

        foreach (string retiredName in new[]
                 {
                     "HasOutputFileName",
                     "public bool? OutputCommitted",
                      "CommandOperationCount",
                      "CommandOperations",
                      "HasCommandOperations",
                      "HasNoCommandOperations",
                     "HasNoStepOperations",
                     "HasNoMutations",
                     "HasOutputDifferenceGroups",
                     "HasNoOutputDifferences",
                     "IsSuccessful",
                     "HasArtifactPath",
                     "InputLabel",
                     "RoleLabel",
                     "HasSectionLabel",
                     "RuntimeCommandsLabel",
                     "public IReadOnlyList<ReportLineViewModel> SummaryRows",
                     "TriageRows",
                     "EvidenceRows",
                     "CreateSummaryRows",
                     "CreateTriageRows",
                     "CreateEvidenceRows",
                 })
        {
            Assert.DoesNotContain(retiredName, report, StringComparison.Ordinal);
        }

        string templates = ReadText(
            "src/NvtFwCombiner.Presentation.Avalonia/Resources/MainWindowReportTemplates.axaml");
        Assert.DoesNotContain("ReportEvidenceChipTemplate", templates, StringComparison.Ordinal);
        Assert.DoesNotContain("ReportTriageRowTemplate", templates, StringComparison.Ordinal);
    }

    /// <summary>Prevents unbound memory-coverage and hex-cell projections from returning.</summary>
    [Fact]
    public void PresentationOmitsUnboundCoverageAndHexCellState()
    {
        string coverage = ReadText(
            "src/NvtFwCombiner.Presentation.Avalonia/ViewModels/MemoryCoverageSegmentViewModel.cs");
        string hexCell = ReadText(
            "src/NvtFwCombiner.Presentation.Avalonia/ViewModels/HexEditorViewportViewModels.cs");

        Assert.DoesNotContain("TooltipText", coverage, StringComparison.Ordinal);
        Assert.DoesNotContain("InlineValidationMessage", hexCell, StringComparison.Ordinal);
    }

    /// <summary>Verifies non-critical local UI stores share one JSON and atomic-promotion mechanism.</summary>
    [Fact]
    public void LocalUiFileStoresShareBestEffortJsonPersistence()
    {
        string helper = ReadText(
            "src/NvtFwCombiner.Presentation.Avalonia/BestEffortLocalJsonFileStore.cs");
        string stores = ReadText("src/NvtFwCombiner.Presentation.Avalonia/ReportHistoryFileStore.cs") +
            ReadText("src/NvtFwCombiner.Presentation.Avalonia/ShellPreferenceFileStore.cs");

        Assert.Equal(6, CountOccurrences(stores, "BestEffortLocalJsonFileStore."));
        Assert.DoesNotContain("JsonSerializerOptions", stores, StringComparison.Ordinal);
        Assert.DoesNotContain("File.Replace", stores, StringComparison.Ordinal);
        Assert.Contains("JsonSerializerOptions", helper, StringComparison.Ordinal);
        Assert.Contains("File.Replace", helper, StringComparison.Ordinal);
        Assert.Contains("UnauthorizedAccessException", helper, StringComparison.Ordinal);
    }

    /// <summary>Verifies firmware slot model, icons, and fact badges stay split by UI responsibility.</summary>
    [Fact]
    public void FirmwareSlotViewModelConcernsStaySplit()
    {
        string root = ReadText("src/NvtFwCombiner.Presentation.Avalonia/ViewModels/FirmwareSlotViewModel.cs");
        string icons = ReadText("src/NvtFwCombiner.Presentation.Avalonia/ViewModels/FirmwareSlotViewModel.Icons.cs");
        string replaceRunner = ReadText("src/NvtFwCombiner.Presentation.Avalonia/UiCompositionRunner.Replace.cs");
        string facts = ReadText("src/NvtFwCombiner.Presentation.Avalonia/ViewModels/FirmwareSlotFactViewModel.cs");
        string kind = ReadText("src/NvtFwCombiner.Presentation.Avalonia/ViewModels/FirmwareSlotKind.cs");

        Assert.Contains("public sealed partial class FirmwareSlotViewModel", root, StringComparison.Ordinal);
        Assert.Contains("public partial string? FilePath", root, StringComparison.Ordinal);
        Assert.Contains("public void ApplyDisplayText", root, StringComparison.Ordinal);
        Assert.Contains("public void SetFirmwareFacts", root, StringComparison.Ordinal);
        Assert.Contains("FirmwareSlotKind kind", root, StringComparison.Ordinal);
        Assert.DoesNotContain("FirmwareSlotKindResolver", root, StringComparison.Ordinal);
        Assert.DoesNotContain("SlotIconPathData", root, StringComparison.Ordinal);
        Assert.DoesNotContain("public IBrush SlotBackgroundBrush", root, StringComparison.Ordinal);
        Assert.DoesNotContain("SlotBorderBrush", root, StringComparison.Ordinal);
        Assert.DoesNotContain("RequirementBadgeForegroundBrush", root, StringComparison.Ordinal);
        Assert.DoesNotContain("public sealed record FirmwareSlotFactViewModel", root, StringComparison.Ordinal);
        Assert.DoesNotContain("public enum FirmwareSlotKind", root, StringComparison.Ordinal);
        Assert.Contains("SlotIconPathData", icons, StringComparison.Ordinal);
        Assert.Contains("SlotIconTooltip", icons, StringComparison.Ordinal);
        Assert.DoesNotContain("Avalonia.Media", icons, StringComparison.Ordinal);
        Assert.DoesNotContain("SlotIconBackgroundBrush", icons, StringComparison.Ordinal);
        Assert.DoesNotContain("SlotIconBorderBrush", icons, StringComparison.Ordinal);
        Assert.DoesNotContain("SlotIconForegroundBrush", icons, StringComparison.Ordinal);
        Assert.DoesNotContain("InferSlotKind", icons, StringComparison.Ordinal);
        Assert.DoesNotContain("WorkbenchSlotIds", icons, StringComparison.Ordinal);
        Assert.Contains("WorkbenchReplaceModes.Dp => FirmwareSlotKind.Dp", replaceRunner, StringComparison.Ordinal);
        Assert.Contains("WorkbenchReplaceModes.CtrlRam => FirmwareSlotKind.CtrlRam", replaceRunner, StringComparison.Ordinal);
        Assert.Contains("public sealed record FirmwareSlotFactViewModel", facts, StringComparison.Ordinal);
        Assert.Contains("public enum FirmwareSlotKind", kind, StringComparison.Ordinal);
    }

    /// <summary>Verifies report line rows, chips, groups, and flow nodes stay split by UI responsibility.</summary>
    [Fact]
    public void ReportLineViewModelConcernsStaySplit()
    {
        string root = ReadText("src/NvtFwCombiner.Presentation.Avalonia/ViewModels/ReportLineViewModel.cs");
        string badges = ReadText("src/NvtFwCombiner.Presentation.Avalonia/ViewModels/ReportLineBadgeViewModel.cs");
        string facts = ReadText("src/NvtFwCombiner.Presentation.Avalonia/ViewModels/ReportLineFactViewModel.cs");
        string rangeRows = ReadText("src/NvtFwCombiner.Presentation.Avalonia/ViewModels/ReportRangeTableRowViewModel.cs");
        string differenceRows = ReadText("src/NvtFwCombiner.Presentation.Avalonia/ViewModels/ReportDifferenceSummaryRowViewModel.cs");
        string inputGroups = ReadText("src/NvtFwCombiner.Presentation.Avalonia/ViewModels/ReportInputGroupViewModel.cs");
        string flowNodes = ReadText("src/NvtFwCombiner.Presentation.Avalonia/ViewModels/ReportOperationFlowNodeViewModel.cs");

        Assert.Contains("public sealed class ReportLineViewModel", root, StringComparison.Ordinal);
        Assert.Contains("public static ReportLineViewModel Empty", root, StringComparison.Ordinal);
        Assert.DoesNotContain("public sealed class ReportLineBadgeViewModel", root, StringComparison.Ordinal);
        Assert.DoesNotContain("public sealed class ReportLineFactViewModel", root, StringComparison.Ordinal);
        Assert.DoesNotContain("public sealed record ReportRangeTableRowViewModel", root, StringComparison.Ordinal);
        Assert.DoesNotContain("public sealed record ReportDifferenceSummaryRowViewModel", root, StringComparison.Ordinal);
        Assert.DoesNotContain("public sealed class ReportInputGroupViewModel", root, StringComparison.Ordinal);
        Assert.DoesNotContain("public sealed class ReportOperationFlowNodeViewModel", root, StringComparison.Ordinal);
        Assert.Contains("public sealed class ReportLineBadgeViewModel", badges, StringComparison.Ordinal);
        Assert.Contains("public sealed class ReportLineFactViewModel", facts, StringComparison.Ordinal);
        Assert.Contains("public sealed record ReportRangeTableRowViewModel", rangeRows, StringComparison.Ordinal);
        Assert.Contains("public sealed record ReportDifferenceSummaryRowViewModel", differenceRows, StringComparison.Ordinal);
        Assert.Contains("public sealed class ReportInputGroupViewModel", inputGroups, StringComparison.Ordinal);
        Assert.Contains("public sealed class ReportOperationFlowNodeViewModel", flowNodes, StringComparison.Ordinal);
    }
}
