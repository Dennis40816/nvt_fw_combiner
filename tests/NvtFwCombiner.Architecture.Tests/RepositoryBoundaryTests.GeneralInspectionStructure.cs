namespace NvtFwCombiner.Architecture.Tests;

public sealed partial class RepositoryBoundaryTests
{
    /// <summary>General slot health cannot regress to UI counters or nullable inspection failures.</summary>
    [Fact]
    public void GeneralPerSlotReadinessUsesApplicationSessionIdentity()
    {
        string presentation = ReadText(
                "src/NvtFwCombiner.Presentation.Avalonia/ViewModels/MergePresentationViewModel.General.cs") +
            ReadText(
                "src/NvtFwCombiner.Presentation.Avalonia/ViewModels/ReplacePresentationViewModel.General.cs") +
            ReadText(
                "src/NvtFwCombiner.Presentation.Avalonia/ViewModels/GeneralMappingRowViewModel.cs");
        string bootstrap = ReadText(
                "src/NvtFwCombiner.Bootstrap/WorkbenchCompositionService.GeneralMerge.V2.cs") +
            ReadText(
                "src/NvtFwCombiner.Bootstrap/WorkbenchCompositionService.Replace.General.Readiness.cs");
        string inspection = ReadText(
            "src/NvtFwCombiner.Application/Authoring/GeneralSelectedFileInspection.cs");

        Assert.DoesNotContain("_authoringRevision", presentation, StringComparison.Ordinal);
        Assert.DoesNotContain("_generalReplaceReadinessRevision", presentation, StringComparison.Ordinal);
        Assert.Contains("AuthoringSessionState session", bootstrap, StringComparison.Ordinal);
        Assert.Contains("CompilationFingerprint", bootstrap, StringComparison.Ordinal);
        Assert.Contains("GeneralSelectedFileInspectionResult(", inspection, StringComparison.Ordinal);
        Assert.Contains("GeneralSelectedFileInspectionIssue? Issue", inspection, StringComparison.Ordinal);
    }
}
