using NvtFwCombiner.Domain.Composition;

namespace NvtFwCombiner.Bootstrap.Tests;

public sealed partial class AbMergeRuntimeAdmissionTests
{
    /// <summary>Delivery admission retains case-distinct selected source paths for the platform guard.</summary>
    [Fact]
    public async Task AFlashCodeDeliveryPlanRetainsCaseDistinctInputPathsAsync()
    {
        Assert.True(CanonicalCapabilityResolution.TryCompileAbMerge(
            "NT51929",
            out CompiledComposition? composition,
            out IReadOnlyList<CompositionIssue> issues),
            string.Join(',', issues.Select(static issue => issue.Code)));
        CompiledComposition compiledComposition = Assert.IsType<CompiledComposition>(composition);
        string firstPath = Path.Combine(Path.GetTempPath(), "ab-a-source.bin");
        string secondPath = Path.Combine(Path.GetTempPath(), "AB-A-SOURCE.bin");
        OutputNamingSummary outputNaming = CreateCompletedAbResult("NT51929", DpLength).OutputNaming!;

        WorkbenchAbAFlashCodeDeliveryPlan plan = Assert.IsType<WorkbenchAbAFlashCodeDeliveryPlan>(
            await AbMergeAFlashCodeExportService.TryCreatePlanAsync(
                compiledComposition,
                new Dictionary<string, string>(StringComparer.Ordinal)
                {
                    ["source-a"] = firstPath,
                    ["source-b"] = secondPath,
                },
                new CompositionOutputNamePreview(outputNaming.ActualFileName, outputNaming, []),
                TestContext.Current.CancellationToken));

        Assert.Equal(
            [Path.GetFullPath(firstPath), Path.GetFullPath(secondPath)],
            plan.InputPaths);
    }
}
