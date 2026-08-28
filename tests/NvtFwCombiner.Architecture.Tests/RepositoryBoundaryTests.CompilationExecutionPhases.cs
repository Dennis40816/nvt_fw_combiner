namespace NvtFwCombiner.Architecture.Tests;

public sealed partial class RepositoryBoundaryTests
{
    /// <summary>The deep compiler and run owner expose focused phases and immutable published evidence.</summary>
    [Fact]
    public void CompilationAndRunPublicationUseFocusedImmutablePhases()
    {
        string compilerRoot = Path.Combine(
            Root.FullName, "src", "NvtFwCombiner.Profiles", "V2");
        string compiler = string.Join(Environment.NewLine,
            Directory.EnumerateFiles(compilerRoot, "V2CompositionPlanCompiler*.cs")
                .Order(StringComparer.Ordinal)
                .Select(File.ReadAllText));
        string result = ReadText(
            "src/NvtFwCombiner.Application/Composition/CompositionRunResult.cs");
        string execution = ReadText(
                "src/NvtFwCombiner.Application/Composition/CompositionExecutionExperience.cs") +
            ReadText(
                "src/NvtFwCombiner.Application/Composition/AcceptedSessionCompositionExecution.cs");

        Assert.Contains("private static class ResolvedMapCompilationPhase", compiler, StringComparison.Ordinal);
        Assert.DoesNotContain("internal set;", result, StringComparison.Ordinal);
        Assert.DoesNotContain("HasRunReport", result, StringComparison.Ordinal);
        Assert.DoesNotContain("ActionReadiness", result, StringComparison.Ordinal);
        Assert.DoesNotContain("SuppressOutputInExternalReport", result, StringComparison.Ordinal);
        Assert.DoesNotContain("result.ResolvedCapability =", execution, StringComparison.Ordinal);
        Assert.DoesNotContain("result.AcceptedGeneralMappingDraft =", execution, StringComparison.Ordinal);
    }
}
