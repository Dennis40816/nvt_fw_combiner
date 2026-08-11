namespace NvtFwCombiner.Architecture.Tests;

public sealed partial class RepositoryBoundaryTests
{
    /// <summary>All accepted workflows enter one typed Application execution operation.</summary>
    [Fact]
    public void AcceptedWorkflowsExposeOneExecutionOperation()
    {
        string ports = ReadText(
            "src/NvtFwCombiner.Application/Composition/CompositionExperiencePorts.cs");
        string clientModels = ReadText(
            "src/NvtFwCombiner.Application/Composition/CompositionClientModels.cs");
        string applicationExecution = ReadText(
            "src/NvtFwCombiner.Application/Composition/CompositionExecutionExperience.cs");

        Assert.Equal(1, CountOccurrences(ports, "ValueTask<CompositionRunResult>"));
        Assert.Contains(
            "ExecuteAsync(\n        AcceptedCompositionExecutionRequest request,",
            ports.ReplaceLineEndings("\n"),
            StringComparison.Ordinal);
        Assert.DoesNotContain("RunStandardMergeAcceptedSession", ports, StringComparison.Ordinal);
        Assert.DoesNotContain("RunReplaceAcceptedSession", ports, StringComparison.Ordinal);
        Assert.Equal(
            1,
            CountOccurrences(
                applicationExecution,
                "public ValueTask<CompositionRunResult>"));
        Assert.DoesNotContain(
            "RunStandardMergeAcceptedSessionWithProgressAsync",
            applicationExecution,
            StringComparison.Ordinal);
        Assert.DoesNotContain(
            "RunGeneralMergeAcceptedSessionWithProgressAsync",
            applicationExecution,
            StringComparison.Ordinal);
        Assert.DoesNotContain(
            "RunAbMergeAcceptedSessionWithProgressAsync",
            applicationExecution,
            StringComparison.Ordinal);
        Assert.DoesNotContain(
            "RunReplaceAcceptedSessionWithProgressAsync",
            applicationExecution,
            StringComparison.Ordinal);
        Assert.DoesNotContain(
            "RunGeneralReplaceAcceptedSessionWithOverridesAsync",
            applicationExecution,
            StringComparison.Ordinal);
        Assert.DoesNotContain(
            "GeneralReplaceAcceptedSessionRunner",
            clientModels,
            StringComparison.Ordinal);
    }
}
