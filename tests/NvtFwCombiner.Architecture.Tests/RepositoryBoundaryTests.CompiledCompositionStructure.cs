namespace NvtFwCombiner.Architecture.Tests;

public sealed partial class RepositoryBoundaryTests
{
    /// <summary>Verifies only the profile compiler can mint executable legacy artifacts in production code.</summary>
    [Fact]
    public void ProductionCompiledCompositionCreationStaysProfileCompilerOwned()
    {
        string project = ReadText("src/NvtFwCombiner.Domain/NvtFwCombiner.Domain.csproj");
        string composition = ReadText(
            "src/NvtFwCombiner.Domain/Composition/CompiledComposition.cs");
        string plan = ReadText(
            "src/NvtFwCombiner.Domain/Composition/CompositionPlan.cs");
        string identity = ReadText(
            "src/NvtFwCombiner.Domain/Composition/LegacyCompiledCompositionIdentity.cs");
        string compiler = ReadText(
            "src/NvtFwCombiner.Profiles/CompositionProfileCompiler.cs");
        string preparation = ReadText(
            "src/NvtFwCombiner.Profiles/V2/V2CompositionPreparationService.cs");
        string v2Compiler = ReadText(
            "src/NvtFwCombiner.Profiles/V2/V2CompositionPlanCompiler.cs");
        string logicalV2Compiler = ReadText(
            "src/NvtFwCombiner.Profiles/V2/V2CompositionPlanCompiler.LogicalOutput.cs");
        string compileResult = ReadText(
            "src/NvtFwCombiner.Profiles/ProfileCompileResult.cs");
        string profileSources = ReadProfileSources();
        string request = ReadText(
            "src/NvtFwCombiner.Application/Composition/CompositionRunRequest.cs");
        string previewTokens = ReadText(
            "src/NvtFwCombiner.Application/Composition/CompositionRunService.PreviewTokens.cs");
        string runner = ReadText(
            "src/NvtFwCombiner.Bootstrap/WorkbenchCompositionService.Runner.cs");
        string bootstrapSources = ReadBootstrapSources();

        Assert.Contains(
            "<InternalsVisibleTo Include=\"NvtFwCombiner.Profiles\" />",
            project,
            StringComparison.Ordinal);
        Assert.Contains(
            "<InternalsVisibleTo Include=\"NvtFwCombiner.Application.Tests\" />",
            project,
            StringComparison.Ordinal);
        Assert.Contains(
            "<InternalsVisibleTo Include=\"NvtFwCombiner.Domain.Tests\" />",
            project,
            StringComparison.Ordinal);
        Assert.DoesNotContain(
            "<InternalsVisibleTo Include=\"NvtFwCombiner.Application\" />",
            project,
            StringComparison.Ordinal);
        Assert.DoesNotContain(
            "<InternalsVisibleTo Include=\"NvtFwCombiner.Bootstrap\" />",
            project,
            StringComparison.Ordinal);
        Assert.Contains(
            "internal static CompiledComposition CreateLegacy",
            composition,
            StringComparison.Ordinal);
        Assert.Contains(
            "internal static CompiledComposition CreateV2",
            composition,
            StringComparison.Ordinal);
        Assert.Contains(
            "internal static CompiledComposition CreateV2RuntimeExecutable",
            composition,
            StringComparison.Ordinal);
        Assert.DoesNotContain(
            "public static CompiledComposition Create",
            composition,
            StringComparison.Ordinal);
        Assert.DoesNotContain("CompositionPlanProvenance", plan, StringComparison.Ordinal);
        Assert.DoesNotContain("Provenance { get; }", plan, StringComparison.Ordinal);
        Assert.Contains("internal sealed class LegacyCompiledCompositionIdentity", identity, StringComparison.Ordinal);
        Assert.DoesNotContain("public sealed class LegacyCompiledCompositionIdentity", identity, StringComparison.Ordinal);
        Assert.DoesNotContain("CompositionPlan? Plan", compileResult, StringComparison.Ordinal);
        Assert.False(File.Exists(Path.Combine(
            Root.FullName,
            "src",
            "NvtFwCombiner.Domain",
            "Composition",
            "CompositionPlanProvenance.cs")));
        Assert.Contains("CompiledComposition.CreateLegacy(", compiler, StringComparison.Ordinal);
        Assert.Equal(1, CountOccurrences(profileSources, "CompiledComposition.CreateLegacy("));
        Assert.Equal(2, CountOccurrences(profileSources, "CompiledComposition.CreateV2("));
        Assert.Equal(1, CountOccurrences(profileSources, "CompiledComposition.CreateV2RuntimeExecutable("));
        Assert.Contains("profile.Family.Family.ResolveMap", preparation, StringComparison.Ordinal);
        Assert.Contains("CompositionProfileMapAdmissionValidator.Validate", preparation, StringComparison.Ordinal);
        Assert.DoesNotContain("CompositionPlan", preparation, StringComparison.Ordinal);
        Assert.DoesNotContain("CompiledComposition.CreateV2", preparation, StringComparison.Ordinal);
        Assert.DoesNotContain("NvtFwCombiner.Application", preparation, StringComparison.Ordinal);
        Assert.Contains("CompiledComposition.CreateV2", v2Compiler, StringComparison.Ordinal);
        Assert.Contains("CompiledComposition.CreateV2RuntimeExecutable", v2Compiler, StringComparison.Ordinal);
        Assert.DoesNotContain("NvtFwCombiner.Application", v2Compiler, StringComparison.Ordinal);
        Assert.Contains("CompiledComposition.CreateV2", logicalV2Compiler, StringComparison.Ordinal);
        Assert.DoesNotContain("CreateV2RuntimeExecutable", logicalV2Compiler, StringComparison.Ordinal);
        Assert.DoesNotContain("NvtFwCombiner.Application", logicalV2Compiler, StringComparison.Ordinal);
        Assert.Contains("CompiledComposition compiledComposition", request, StringComparison.Ordinal);
        Assert.Contains(
            "CompiledCompositionEligibility.LegacyRuntimeExecutable",
            request,
            StringComparison.Ordinal);
        Assert.Contains(
            "CompiledCompositionEligibility.V2RuntimeExecutable",
            request,
            StringComparison.Ordinal);
        Assert.Contains("ProfileBundleV2CompilationAuthority", request, StringComparison.Ordinal);
        Assert.DoesNotContain(
            "CompiledCompositionEligibility.V2PlanCompiled",
            request,
            StringComparison.Ordinal);
        Assert.DoesNotContain("CompositionRunProfile", request, StringComparison.Ordinal);
        Assert.DoesNotContain("CompositionPlan plan", request, StringComparison.Ordinal);
        Assert.Contains(
            "request.CompiledComposition.CompilationFingerprint",
            previewTokens,
            StringComparison.Ordinal);
        Assert.DoesNotContain("AppendPlanFingerprint", previewTokens, StringComparison.Ordinal);
        Assert.Contains("CompiledComposition compiledComposition", runner, StringComparison.Ordinal);
        Assert.DoesNotContain("CompositionProfileDefinition profile,", runner, StringComparison.Ordinal);
        Assert.DoesNotContain("compile.Plan", bootstrapSources, StringComparison.Ordinal);
        Assert.DoesNotContain("CompiledCompositionRunAdapter", bootstrapSources, StringComparison.Ordinal);
        Assert.DoesNotContain("CompositionRunProfile", bootstrapSources, StringComparison.Ordinal);
        Assert.DoesNotContain("V2CompositionPlanCompiler", bootstrapSources, StringComparison.Ordinal);
    }
}
