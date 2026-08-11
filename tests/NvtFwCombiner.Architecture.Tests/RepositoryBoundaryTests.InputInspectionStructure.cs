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
                .Where(path => !HasPathSegment(path, "bin") && !HasPathSegment(path, "obj"))
                .Where(path => File.ReadAllText(path).Contains(
                    "public static class CompiledInputArtifactInspectionService",
                    StringComparison.Ordinal)),
        ];
        string headless = ReadText(
            "src/NvtFwCombiner.Application/Authoring/AuthoringInputSlotInspection.cs");
        string abOutputNaming = ReadText(
            "src/NvtFwCombiner.Application/Composition/AbCodeOutputNameResolver.cs");
        string abProjection = ReadText(
            "src/NvtFwCombiner.Application/Authoring/CompiledAuthoringWorkflow.Selection.cs");
        string abInspection = ReadText(
            "src/NvtFwCombiner.Application/Authoring/AbMergeAuthoringExperience.InputInspection.cs");
        string workbenchModels = ReadText(
            "src/NvtFwCombiner.Application/Composition/CompositionClientModels.cs");
        string workbenchIssueCodes = ReadText(
            "src/NvtFwCombiner.Application/Composition/CompositionPlanningIssueCodes.cs");
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

    /// <summary>Application audit-B compatibility aliases and private inspection mirrors stay retired.</summary>
    [Fact]
    public void ApplicationAuditBCompatibilitySurfacesStayRetired()
    {
        string inputInspectionRoot = Path.Combine(
            Root.FullName,
            "src",
            "NvtFwCombiner.Application",
            "InputInspection");
        string inspectionService = ReadText(
                "src/NvtFwCombiner.Application/InputInspection/CompiledInputArtifactInspectionService.cs")
            .ReplaceLineEndings("\n");
        string readiness = ReadText(
            "src/NvtFwCombiner.Application/Capabilities/InputSelectionReadiness.cs");
        string numberPolicy = ReadText(
            "src/NvtFwCombiner.Application/FlashMaps/IcNumberChoicePolicy.cs");
        string runtimeReadiness = ReadText(
            "src/NvtFwCombiner.Application/ExternalTools/RuntimeDependencyReadiness.cs");
        string fileStamp = ReadText(
            "src/NvtFwCombiner.Application/Authoring/FileStamp.cs");
        string workflow = ReadText(
            "src/NvtFwCombiner.Application/Authoring/CompiledAuthoringWorkflow.Selection.cs");
        string memoryLayout = ReadText(
            "src/NvtFwCombiner.Application/MemoryLayout/MemoryLayoutProjector.cs");
        string versionPlan = ReadText(
            "src/NvtFwCombiner.Application/FlashMaps/FirmwareConfigVersionWritePlan.cs");
        string flashMapTypes = ReadText(
            "src/NvtFwCombiner.Application/FlashMaps/TpFlashMapTypes.cs");

        Assert.False(File.Exists(Path.Combine(inputInspectionRoot, "InputArtifactInspection.cs")));
        Assert.False(File.Exists(Path.Combine(inputInspectionRoot, "DeclaredPrefixInputInspector.cs")));
        Assert.False(File.Exists(Path.Combine(inputInspectionRoot, "DeclaredPrefixInputInspectionPolicy.cs")));
        Assert.False(File.Exists(Path.Combine(
            Root.FullName,
            "src",
            "NvtFwCombiner.Application",
            "FlashMaps",
            "IcNumberChoice.cs")));
        Assert.Equal(
            1,
            CountOccurrences(
                inspectionService,
                "public static CompiledInputArtifactInspectionResult Inspect("));
        Assert.DoesNotContain("ProjectMetadataDependency", readiness, StringComparison.Ordinal);
        Assert.Contains("CapabilityNumberChoice", numberPolicy, StringComparison.Ordinal);
        Assert.DoesNotContain("record IcNumberChoice", numberPolicy, StringComparison.Ordinal);
        Assert.DoesNotContain("public bool IsReady", runtimeReadiness, StringComparison.Ordinal);
        Assert.DoesNotContain("public long Length", fileStamp, StringComparison.Ordinal);
        Assert.Contains("acceptedFileStamps[prerequisiteSlot].AcceptedLength", workflow, StringComparison.Ordinal);
        Assert.Contains("state.FileStamp?.AcceptedLength", memoryLayout, StringComparison.Ordinal);
        Assert.DoesNotContain("SourceFirmwareVersionAndBarBytes", versionPlan, StringComparison.Ordinal);
        Assert.DoesNotContain("SourceFirmwareSubVersionBytes", versionPlan, StringComparison.Ordinal);
        Assert.DoesNotContain("IsHiddenInSingle", flashMapTypes, StringComparison.Ordinal);
    }
}
