namespace NvtFwCombiner.Architecture.Tests;

public sealed partial class RepositoryBoundaryTests
{
    /// <summary>Accepted immutable sessions enter the Application run service through one owner.</summary>
    [Fact]
    public void AcceptedSessionExecutionIsOwnedByApplication()
    {
        string application = ReadText(
            "src/NvtFwCombiner.Application/Composition/AcceptedSessionCompositionExecution.cs");
        string execution = ReadText(
            "src/NvtFwCombiner.Application/Composition/CompositionExecutionExperience.cs");
        string request = ReadText(
            "src/NvtFwCombiner.Application/Composition/AcceptedCompositionExecutionRequest.cs");
        string destination = ReadText(
            "src/NvtFwCombiner.Infrastructure/Files/ProtectedCompositionDestinationProvider.cs");

        AssertContainsAll(application, "CompositionRunService", "AcceptedSessionExecutionInputs.Create",
            ") CreateBindings(", ") CreateGeneralBindings(");
        AssertContainsAll(execution,
            "internal sealed class CompositionExecutionExperience : ICompositionExecution",
            "AcceptedSessionCompositionExecution.ExecuteAsync", "GetAcceptedAbMergeTopologySelection",
            "request.Route(this, request, progress, cancellationToken)",
            "ICompositionExecutionDestinationProvider");
        AssertDoesNotContainAny(execution, "CanonicalCapabilityCompilerAdapter",
            "ResolveAbMergeTopologySelection", "session.WorkflowId switch", ".WorkflowId switch");
        AssertContainsAll(request,
            "internal delegate ValueTask<CompositionRunResult> AcceptedCompositionExecutionRoute",
            "internal static class AcceptedCompositionExecutionRoutes",
            "ExperienceIds.StandardMerge =>", "ExperienceIds.AbMerge =>", "ExperienceIds.GeneralMerge =>",
            "ExperienceIds.DpReplace =>", "ExperienceIds.CtrlRamReplace =>", "ExperienceIds.GeneralReplace =>");
        AssertDoesNotContainAny(request, "AbMergeTopologyToken");
        AssertContainsAll(destination, "ICompositionExecutionDestinationProvider");
        Assert.Empty(Directory.EnumerateFiles(
            Path.Combine(Root.FullName, "src", "NvtFwCombiner.Bootstrap"),
            "CompositionExecutionAdapter*.cs",
            SearchOption.TopDirectoryOnly));
        Assert.False(File.Exists(Path.Combine(
            Root.FullName,
            "src",
            "NvtFwCombiner.Bootstrap",
            "ProtectedCompositionOutputWriter.cs")));
        Assert.False(File.Exists(Path.Combine(
            Root.FullName,
            "src",
            "NvtFwCombiner.Bootstrap",
            "ProtectedCompositionDeliveryWriter.cs")));
        Assert.False(File.Exists(Path.Combine(
            Root.FullName,
            "src",
            "NvtFwCombiner.Bootstrap",
            "AcceptedAuthoringSessionBinding.cs")));
        Assert.False(File.Exists(Path.Combine(
            Root.FullName,
            "src",
            "NvtFwCombiner.Bootstrap",
            "CompiledCompositionInputBindingFactory.cs")));
    }
}
