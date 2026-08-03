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
        string abOutputNaming = ReadText(
            "src/NvtFwCombiner.Application/Composition/AbCodeOutputNameResolver.cs");
        string abProjection = ReadText(
            "src/NvtFwCombiner.Bootstrap/WorkbenchAbMergeInputProjection.cs");
        string abInspection = ReadText(
            "src/NvtFwCombiner.Bootstrap/WorkbenchCompositionService.AbMerge.InputInspection.cs");
        string workbenchModels = ReadText(
            "src/NvtFwCombiner.Bootstrap/WorkbenchCompositionModels.cs");
        string workbenchIssueCodes = ReadText(
            "src/NvtFwCombiner.Bootstrap/WorkbenchIssueCodes.cs");
        string presentation = ReadPresentationSources();

        _ = Assert.Single(inspectorOwners);
        Assert.Contains(
            "CompiledInputArtifactInspectionService.Inspect(",
            headless,
            StringComparison.Ordinal);
        Assert.Contains(
            "CompiledInputArtifactObservationService.Observe(",
            headless,
            StringComparison.Ordinal);
        Assert.Contains(
            "CompiledAuthoringWorkflowService(resolver)",
            abInspection,
            StringComparison.Ordinal);
        Assert.DoesNotContain(
            "CompiledInputArtifactInspectionService.Inspect(",
            abProjection,
            StringComparison.Ordinal);
        Assert.DoesNotContain(
            "s_nonPublishableCompatibilityRevision",
            abProjection,
            StringComparison.Ordinal);
        Assert.DoesNotContain(
            "new AuthoringRevision(0)",
            abProjection,
            StringComparison.Ordinal);
        Assert.DoesNotContain("CompiledInputVersionObservation", abProjection, StringComparison.Ordinal);
        Assert.Contains("CompiledInputVersionObservation", workbenchModels, StringComparison.Ordinal);
        Assert.Contains("status.Observation", abInspection, StringComparison.Ordinal);
        Assert.DoesNotContain("WithNonBlockingAdvisories", headless, StringComparison.Ordinal);
        Assert.DoesNotContain("WithNonBlockingAdvisories", abInspection, StringComparison.Ordinal);
        Assert.DoesNotContain("IsUnknown", abInspection, StringComparison.Ordinal);
        Assert.DoesNotContain("AbVersionMetadataUnknown", abInspection, StringComparison.Ordinal);
        Assert.DoesNotContain("AbInputVersionUnknown", workbenchIssueCodes, StringComparison.Ordinal);
        Assert.DoesNotContain("WorkbenchAbMergeInputInspection", workbenchModels, StringComparison.Ordinal);
        Assert.DoesNotContain("WorkbenchInputInspectionNextAction", workbenchModels, StringComparison.Ordinal);
        Assert.DoesNotContain("WorkbenchInputInspectionIssue", workbenchModels, StringComparison.Ordinal);
        Assert.DoesNotContain(
            "CompiledInputArtifactInspectionService",
            presentation,
            StringComparison.Ordinal);
        Assert.Contains(
            "CompiledInputArtifactObservationService.DecodeDpRegion(",
            abOutputNaming,
            StringComparison.Ordinal);
        Assert.Contains(
            "CompiledInputArtifactObservationService.DecodeTp(",
            abOutputNaming,
            StringComparison.Ordinal);
        Assert.DoesNotContain(
            "FirmwareConfigMetadataReader.TryReadBackup",
            abOutputNaming,
            StringComparison.Ordinal);
        Assert.DoesNotContain("ImageMap.Regions", abOutputNaming, StringComparison.Ordinal);
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
