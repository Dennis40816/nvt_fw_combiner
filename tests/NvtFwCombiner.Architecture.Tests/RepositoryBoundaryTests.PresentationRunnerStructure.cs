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
        string replace = ReadText("src/NvtFwCombiner.Presentation.Avalonia/UiCompositionRunner.Replace.cs");
        string[] runnerSources = [.. Directory.GetFiles(
                Path.Combine(Root.FullName, "src", "NvtFwCombiner.Presentation.Avalonia"),
                "UiCompositionRunner*.cs")
            .Select(File.ReadAllText)];
        string launch = ReadText("src/NvtFwCombiner.Presentation.Avalonia/UiLaunchOptions.cs");
        string deviceContext = ReadText(
            "src/NvtFwCombiner.Presentation.Avalonia/ViewModels/WorkflowSessionPresentationViewModel.DeviceContext.cs");
        string mergeViewModel = ReadText(
            "src/NvtFwCombiner.Presentation.Avalonia/ViewModels/MergePresentationViewModel.Execution.cs");
        string replaceViewModel = ReadText(
            "src/NvtFwCombiner.Presentation.Avalonia/ViewModels/ReplacePresentationViewModel.Execution.cs");

        Assert.Contains("GetNumberSelectionChoices", catalog, StringComparison.Ordinal);
        Assert.NotEmpty(runnerSources);
        Assert.All(runnerSources, source =>
            Assert.Contains("internal static partial class UiCompositionRunner", source, StringComparison.Ordinal));
        Assert.Contains("internal sealed class UiLaunchOptions", launch, StringComparison.Ordinal);
        Assert.DoesNotContain("GetSupportedIcIds", catalog, StringComparison.Ordinal);
        Assert.DoesNotContain("GetDefaultIcId", catalog, StringComparison.Ordinal);
        Assert.DoesNotContain("GetSettingsSnapshot", catalog, StringComparison.Ordinal);
        Assert.Contains("private static MemoryMapRowViewModel ToMemoryMapRow", common, StringComparison.Ordinal);
        Assert.Contains("GetFirmwareSlotFacts", facts, StringComparison.Ordinal);
        Assert.DoesNotContain("CreateFlashCodeOutputFileName", facts, StringComparison.Ordinal);
        Assert.Contains("ExperienceIds.DpReplace", ReadText(
            "src/NvtFwCombiner.Presentation.Avalonia/ViewModels/MainWindowViewModel.Construction.cs"),
            StringComparison.Ordinal);
        Assert.Contains("GetMemoryDisplay", common, StringComparison.Ordinal);
        Assert.False(File.Exists(Path.Combine(
            Root.FullName,
            "src",
            "NvtFwCombiner.Presentation.Avalonia",
            "UiCompositionRunner.Merge.cs")));
        Assert.DoesNotContain("GetReplaceMemoryDisplay", replace, StringComparison.Ordinal);
        Assert.Contains("GetSelectedReplaceMemoryDisplay", ReadText(
            "src/NvtFwCombiner.Presentation.Avalonia/ViewModels/ReplacePresentationViewModel.Memory.cs"),
            StringComparison.Ordinal);
        Assert.DoesNotContain("RunReplaceAsync", replace, StringComparison.Ordinal);
        Assert.Contains("_compositionServices.Capabilities.GetSelectorPublication", deviceContext, StringComparison.Ordinal);
        Assert.DoesNotContain("_compositionServices.Capabilities.GetIcIds", deviceContext, StringComparison.Ordinal);
        Assert.DoesNotContain("GetAbMergeProfileSummaries", deviceContext, StringComparison.Ordinal);
        Assert.DoesNotContain("AbMergeAuthoring.GetTopologyChoices", deviceContext, StringComparison.Ordinal);
        Assert.Contains("_compositionServices.Execution.ExecuteAsync", mergeViewModel, StringComparison.Ordinal);
        Assert.Contains("_compositionServices.Execution.ExecuteAsync", replaceViewModel, StringComparison.Ordinal);
        Assert.DoesNotContain("CanonicalCapabilityProjection", deviceContext, StringComparison.Ordinal);
        Assert.DoesNotContain("CompositionExecutionAdapter", mergeViewModel, StringComparison.Ordinal);
        Assert.DoesNotContain("CompositionExecutionAdapter", replaceViewModel, StringComparison.Ordinal);
    }
}
