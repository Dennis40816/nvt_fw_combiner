namespace NvtFwCombiner.Architecture.Tests;

/// <summary>Application composition and execution boundary checks.</summary>
public sealed partial class ApplicationBoundaryTests : RepositoryBoundaryTestBase
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

        Assert.Contains("CompositionRunService", application, StringComparison.Ordinal);
        Assert.Contains("AcceptedSessionExecutionInputs.Create", application, StringComparison.Ordinal);
        Assert.Contains(") CreateBindings(", application, StringComparison.Ordinal);
        Assert.Contains(") CreateGeneralBindings(", application, StringComparison.Ordinal);
        Assert.Contains(
            "internal sealed class CompositionExecutionExperience : ICompositionExecution",
            execution,
            StringComparison.Ordinal);
        Assert.Contains("AcceptedSessionCompositionExecution.ExecuteAsync", execution, StringComparison.Ordinal);
        Assert.Contains("GetAcceptedAbMergeTopologySelection", execution, StringComparison.Ordinal);
        Assert.DoesNotContain("CanonicalCapabilityCompilerAdapter", execution, StringComparison.Ordinal);
        Assert.DoesNotContain("ResolveAbMergeTopologySelection", execution, StringComparison.Ordinal);
        Assert.DoesNotContain("AbMergeTopologyToken", request, StringComparison.Ordinal);
        Assert.Contains("ICompositionExecutionDestinationProvider", execution, StringComparison.Ordinal);
        Assert.Contains("ICompositionExecutionDestinationProvider", destination, StringComparison.Ordinal);
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
