namespace NvtFwCombiner.Architecture.Tests;

public sealed partial class RepositoryBoundaryTests
{
    /// <summary>General authoring and execution retain one accepted-session path.</summary>
    [Fact]
    public void GeneralWorkflowsUseOnlyCanonicalAcceptedSessionPreparation()
    {
        Assert.False(File.Exists(Path.Combine(
            Root.FullName,
            "src",
            "NvtFwCombiner.Bootstrap",
            "CompositionAuthoringSessionAdapter.GeneralMerge.cs")));
        Assert.False(File.Exists(Path.Combine(
            Root.FullName,
            "src",
            "NvtFwCombiner.Application",
            "Authoring",
            "AuthoringSessionState.GeneralInspectionCache.cs")));

        Assert.False(File.Exists(Path.Combine(
            Root.FullName,
            "src",
            "NvtFwCombiner.Bootstrap",
            "CanonicalAuthoringAdapter.GeneralSession.cs")));
        Assert.False(File.Exists(Path.Combine(
            Root.FullName,
            "src",
            "NvtFwCombiner.Bootstrap",
            "CanonicalAuthoringAdapter.GeneralSelectedFiles.cs")));
        string session = ReadText(
            "src/NvtFwCombiner.Application/Authoring/GeneralAuthoringExperience.cs");
        Assert.Contains("PrepareMergeSessionAsync(", session, StringComparison.Ordinal);
        Assert.Contains("PrepareReplaceSessionAsync(", session, StringComparison.Ordinal);

        string ports = ReadText(
            "src/NvtFwCombiner.Application/Composition/CompositionExperiencePorts.cs");
        Assert.Contains("public interface IGeneralAuthoring", ports, StringComparison.Ordinal);
        Assert.Contains("PrepareMergeSessionAsync(", ports, StringComparison.Ordinal);
        Assert.Contains("PrepareReplaceSessionAsync(", ports, StringComparison.Ordinal);
        Assert.DoesNotContain("ObserveGeneralSelectedFileLengthAsync", ports, StringComparison.Ordinal);
        Assert.DoesNotContain("InspectGeneralSelectedFileAsync", ports, StringComparison.Ordinal);

        string host = ReadText(
            "src/NvtFwCombiner.Bootstrap/CompositionHostServices.cs");
        Assert.Contains("public IGeneralAuthoring GeneralAuthoring", host, StringComparison.Ordinal);
        Assert.DoesNotContain("PrepareGeneralMergeSessionAsync(", ports + host, StringComparison.Ordinal);
        Assert.DoesNotContain("PrepareGeneralReplaceSessionAsync(", ports + host, StringComparison.Ordinal);
        Assert.False(File.Exists(Path.Combine(
            Root.FullName,
            "src",
            "NvtFwCombiner.Bootstrap",
            "CompositionExperienceAdapters.cs")));

        string selectedFilePort = ReadText(
            "src/NvtFwCombiner.Application/Ports/ISelectedFileContentInspector.cs");
        Assert.DoesNotContain("ObserveLengthAsync", selectedFilePort, StringComparison.Ordinal);

        string execution = ReadText(
            "src/NvtFwCombiner.Application/Composition/CompositionExecutionExperience.cs");
        Assert.Contains("ResolvedCapability capability,", execution, StringComparison.Ordinal);
        Assert.Contains("request.AcceptedSession", execution, StringComparison.Ordinal);
        Assert.Contains("capability.GeneralExecutionPlan", execution, StringComparison.Ordinal);
        Assert.DoesNotContain("PrepareGeneralReplaceSession", execution, StringComparison.Ordinal);
        Assert.False(File.Exists(Path.Combine(
            Root.FullName,
            "src",
            "NvtFwCombiner.Bootstrap",
            "CompositionPlanningAdapter.Replace.General.Context.cs")));
    }
}
