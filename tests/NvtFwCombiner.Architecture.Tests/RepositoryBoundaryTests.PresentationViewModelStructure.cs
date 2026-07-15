namespace NvtFwCombiner.Architecture.Tests;

public sealed partial class RepositoryBoundaryTests
{
    /// <summary>Verifies the Presentation runner remains a thin split adapter over Bootstrap workbench contracts.</summary>
    [Fact]
    public void UiCompositionRunnerConcernsStaySplit()
    {
        string root = ReadText("src/NvtFwCombiner.Presentation.Avalonia/UiCompositionRunner.cs");
        string catalog = ReadText("src/NvtFwCombiner.Presentation.Avalonia/UiCompositionRunner.Catalog.cs");
        string common = ReadText("src/NvtFwCombiner.Presentation.Avalonia/UiCompositionRunner.Common.cs");
        string facts = ReadText("src/NvtFwCombiner.Presentation.Avalonia/UiCompositionRunner.FirmwareFacts.cs");
        string merge = ReadText("src/NvtFwCombiner.Presentation.Avalonia/UiCompositionRunner.Merge.cs");
        string replace = ReadText("src/NvtFwCombiner.Presentation.Avalonia/UiCompositionRunner.Replace.cs");

        Assert.Contains("public static partial class UiCompositionRunner", root, StringComparison.Ordinal);
        Assert.DoesNotContain("WorkbenchCompositionService.", root, StringComparison.Ordinal);
        Assert.DoesNotContain("GetFirmwareSlotFacts", root, StringComparison.Ordinal);
        Assert.DoesNotContain("GetStandardMergeMemoryMapRows", root, StringComparison.Ordinal);
        Assert.DoesNotContain("GetReplaceMemoryMapRows", root, StringComparison.Ordinal);
        Assert.Contains("GetSupportedIcIds", catalog, StringComparison.Ordinal);
        Assert.Contains("GetDefaultIcId", catalog, StringComparison.Ordinal);
        Assert.Contains("GetSettingsSnapshot", catalog, StringComparison.Ordinal);
        Assert.Contains("private static MemoryMapRowViewModel ToMemoryMapRow", common, StringComparison.Ordinal);
        Assert.Contains("GetFirmwareSlotFacts", facts, StringComparison.Ordinal);
        Assert.Contains("CreateFlashCodeOutputFileName", facts, StringComparison.Ordinal);
        Assert.Contains("WorkbenchReplaceModes", ReadText("src/NvtFwCombiner.Presentation.Avalonia/ViewModels/MainWindowViewModel.Construction.cs"), StringComparison.Ordinal);
        Assert.Contains("GetStandardMergeMemoryMapRows", merge, StringComparison.Ordinal);
        Assert.Contains("RunGeneralMergeAsync", merge, StringComparison.Ordinal);
        Assert.Contains("GetReplaceMemoryMapRows", replace, StringComparison.Ordinal);
        Assert.Contains("RunReplaceAsync", replace, StringComparison.Ordinal);
    }

    /// <summary>Verifies firmware slot model, icons, and fact badges stay split by UI responsibility.</summary>
    [Fact]
    public void FirmwareSlotViewModelConcernsStaySplit()
    {
        string root = ReadText("src/NvtFwCombiner.Presentation.Avalonia/ViewModels/FirmwareSlotViewModel.cs");
        string icons = ReadText("src/NvtFwCombiner.Presentation.Avalonia/ViewModels/FirmwareSlotViewModel.Icons.cs");
        string resolver = ReadText("src/NvtFwCombiner.Presentation.Avalonia/ViewModels/FirmwareSlotKindResolver.cs");
        string facts = ReadText("src/NvtFwCombiner.Presentation.Avalonia/ViewModels/FirmwareSlotFactViewModel.cs");
        string kind = ReadText("src/NvtFwCombiner.Presentation.Avalonia/ViewModels/FirmwareSlotKind.cs");

        Assert.Contains("public sealed partial class FirmwareSlotViewModel", root, StringComparison.Ordinal);
        Assert.Contains("public partial string? FilePath", root, StringComparison.Ordinal);
        Assert.Contains("public void ApplyDisplayText", root, StringComparison.Ordinal);
        Assert.Contains("public void SetFirmwareFacts", root, StringComparison.Ordinal);
        Assert.Contains("FirmwareSlotKindResolver.Resolve", root, StringComparison.Ordinal);
        Assert.DoesNotContain("SlotIconPathData", root, StringComparison.Ordinal);
        Assert.DoesNotContain("public IBrush SlotBackgroundBrush", root, StringComparison.Ordinal);
        Assert.DoesNotContain("SlotBorderBrush", root, StringComparison.Ordinal);
        Assert.DoesNotContain("RequirementBadgeForegroundBrush", root, StringComparison.Ordinal);
        Assert.DoesNotContain("public sealed record FirmwareSlotFactViewModel", root, StringComparison.Ordinal);
        Assert.DoesNotContain("public enum FirmwareSlotKind", root, StringComparison.Ordinal);
        Assert.Contains("SlotIconPathData", icons, StringComparison.Ordinal);
        Assert.DoesNotContain("InferSlotKind", icons, StringComparison.Ordinal);
        Assert.DoesNotContain("WorkbenchSlotIds", icons, StringComparison.Ordinal);
        Assert.Contains("WorkbenchSlotIds.MergeDp", resolver, StringComparison.Ordinal);
        Assert.Contains("WorkbenchSlotIds.MergeTp", resolver, StringComparison.Ordinal);
        Assert.Contains("WorkbenchSlotIds.MergeLd", resolver, StringComparison.Ordinal);
        Assert.Contains("WorkbenchSlotIds.ReplaceBase", resolver, StringComparison.Ordinal);
        Assert.Contains("WorkbenchSlotIds.ReplaceDp", resolver, StringComparison.Ordinal);
        Assert.Contains("WorkbenchSlotIds.ReplaceCtrlRamPrefix", resolver, StringComparison.Ordinal);
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
