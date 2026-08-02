namespace NvtFwCombiner.Architecture.Tests;

public sealed partial class RepositoryBoundaryTests
{
    /// <summary>One Application inspector owns input admission while adapters only project its result.</summary>
    [Fact]
    public void HeadlessSlotHealthKeepsOneApplicationInspectionAuthority()
    {
        string applicationRoot = Path.Combine(
            Root.FullName,
            "src",
            "NvtFwCombiner.Application");
        string[] inspectorOwners =
        [
            .. Directory.EnumerateFiles(applicationRoot, "*.cs", SearchOption.AllDirectories)
                .Where(path => File.ReadAllText(path).Contains(
                    "public static class CompiledInputArtifactInspectionService",
                    StringComparison.Ordinal)),
        ];
        string headless = ReadText(
            "src/NvtFwCombiner.Application/Authoring/AuthoringInputSlotInspection.cs");
        string abProjection = ReadText(
            "src/NvtFwCombiner.Bootstrap/WorkbenchAbMergeInputProjection.cs");
        string presentation = ReadPresentationSources();

        _ = Assert.Single(inspectorOwners);
        Assert.Contains(
            "CompiledInputArtifactInspectionService.Inspect(",
            headless,
            StringComparison.Ordinal);
        Assert.Contains(
            "AuthoringInputSlotInspectionService.Inspect(",
            abProjection,
            StringComparison.Ordinal);
        Assert.DoesNotContain(
            "CompiledInputArtifactInspectionService.Inspect(",
            abProjection,
            StringComparison.Ordinal);
        Assert.Contains(
            "s_nonPublishableCompatibilityRevision",
            abProjection,
            StringComparison.Ordinal);
        Assert.DoesNotContain(
            "new AuthoringRevision(0)",
            abProjection,
            StringComparison.Ordinal);
        Assert.DoesNotContain(
            "CompiledInputArtifactInspectionService",
            presentation,
            StringComparison.Ordinal);
    }

    /// <summary>The shared contract remains workflow-neutral and does not grow per-route services.</summary>
    [Fact]
    public void HeadlessSlotHealthDoesNotCreateWorkflowServiceHierarchy()
    {
        string headless = ReadText(
            "src/NvtFwCombiner.Application/Authoring/AuthoringInputSlotInspection.cs");

        Assert.DoesNotContain("StandardMerge", headless, StringComparison.Ordinal);
        Assert.DoesNotContain("AbMerge", headless, StringComparison.Ordinal);
        Assert.DoesNotContain("DpReplace", headless, StringComparison.Ordinal);
        Assert.DoesNotContain("CtrlRamReplace", headless, StringComparison.Ordinal);
        Assert.DoesNotContain("File.", headless, StringComparison.Ordinal);
    }
}
