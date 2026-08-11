namespace NvtFwCombiner.Architecture.Tests;

public sealed partial class RepositoryBoundaryTests
{
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
        Assert.Contains("FirmwareSlotKind.Dp", replaceRunner, StringComparison.Ordinal);
        Assert.Contains("FirmwareSlotKind.CtrlRam", replaceRunner, StringComparison.Ordinal);
        Assert.DoesNotContain("ExperienceIds.DpReplace => FirmwareSlotKind", replaceRunner, StringComparison.Ordinal);
        Assert.Contains("public sealed record FirmwareSlotFactViewModel", facts, StringComparison.Ordinal);
        Assert.Contains("public enum FirmwareSlotKind", kind, StringComparison.Ordinal);
    }
}
