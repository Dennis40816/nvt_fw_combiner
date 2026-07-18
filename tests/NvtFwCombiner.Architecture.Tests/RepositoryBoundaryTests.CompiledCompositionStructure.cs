namespace NvtFwCombiner.Architecture.Tests;

public sealed partial class RepositoryBoundaryTests
{
    /// <summary>Domain normalization reuses only its own caller-isolated immutable input backing.</summary>
    [Fact]
    public void CompositionInputNormalizationRetainsTheDomainOwnershipBarrier()
    {
        string input = ReadText(
            "src/NvtFwCombiner.Domain/Composition/CompositionExecutionInput.cs");
        string normalization = ReadText(
            "src/NvtFwCombiner.Domain/Composition/CompositionEngine.Inputs.cs");

        Assert.Contains("_addressSpaceBytes.Add(item.Key, [.. item.Value]);", input, StringComparison.Ordinal);
        Assert.Contains("TryGetImmutableBuffer", input, StringComparison.Ordinal);
        Assert.Contains("buffer = immutableBytes;", normalization, StringComparison.Ordinal);
        Assert.Contains("immutableBytes.CopyTo(buffer, 0);", normalization, StringComparison.Ordinal);
        Assert.DoesNotContain("bytes.ToArray()", normalization, StringComparison.Ordinal);
    }

    /// <summary>Engine-created staging ranges avoid a second copy without weakening public isolation.</summary>
    [Fact]
    public void ExternalProcessorStagingRetainsOnlyEngineOwnedRanges()
    {
        string engine = ReadText(
            "src/NvtFwCombiner.Domain/Composition/CompositionEngine.ExternalProcessors.cs");
        string artifact = ReadText(
            "src/NvtFwCombiner.Domain/Composition/ExternalProcessorStagedArtifact.cs");
        string source = ReadText(
            "src/NvtFwCombiner.Domain/Composition/ExternalProcessorStagedSource.cs");
        string normalizedEngine = engine.ReplaceLineEndings("\n");

        Assert.Contains(
            "byte[] sourceBytes = ReadSlice(sourceBuffer, binding.SourceRange);\n"
                + "            stagedSources.Add(ExternalProcessorStagedSource.FromOwnedBytes("
                + "binding.FirmwareRange, sourceBytes));",
            normalizedEngine,
            StringComparison.Ordinal);
        Assert.Contains(
            "stagedArtifacts.Add(ExternalProcessorStagedArtifact.FromOwnedBytes(\n"
                + "                binding.ArtifactId,\n"
                + "                ReadSlice(sourceBuffer, binding.SourceRange)));",
            normalizedEngine,
            StringComparison.Ordinal);
        Assert.Contains(": this(artifactId, ClonePublicBytes(artifactId, bytes))", artifact, StringComparison.Ordinal);
        Assert.Contains(": this(firmwareRange, ClonePublicBytes(firmwareRange, bytes))", source, StringComparison.Ordinal);
        Assert.Contains(": bytes.ToArray();", artifact, StringComparison.Ordinal);
        Assert.Contains(": bytes.ToArray();", source, StringComparison.Ordinal);
    }

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
        string runtimeReferenceV2Compiler = ReadText(
            "src/NvtFwCombiner.Profiles/V2/V2CompositionPlanCompiler.RuntimeReferenceReplace.cs");
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
        Assert.Equal(3, CountOccurrences(profileSources, "CompiledComposition.CreateV2("));
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
        Assert.Contains("CompiledComposition.CreateV2", runtimeReferenceV2Compiler, StringComparison.Ordinal);
        Assert.DoesNotContain(
            "CreateV2RuntimeExecutable",
            runtimeReferenceV2Compiler,
            StringComparison.Ordinal);
        Assert.DoesNotContain("NvtFwCombiner.Application", runtimeReferenceV2Compiler, StringComparison.Ordinal);
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
        Assert.Contains(
            "CompiledCompositionEligibility.V2PlanCompiled",
            request,
            StringComparison.Ordinal);
        Assert.Contains("LogicalOutputV2CompilationContext", request, StringComparison.Ordinal);
        Assert.Contains("CompiledProfilePromotionStage.ExecutableCandidate", request, StringComparison.Ordinal);
        Assert.DoesNotContain("CompiledComposition.CreateV2(", request, StringComparison.Ordinal);
        Assert.DoesNotContain("CompiledComposition.CreateV2RuntimeExecutable(", request, StringComparison.Ordinal);
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
