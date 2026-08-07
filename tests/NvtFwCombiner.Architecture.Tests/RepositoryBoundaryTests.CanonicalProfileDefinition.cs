using System.Text.RegularExpressions;

namespace NvtFwCombiner.Architecture.Tests;

public sealed partial class RepositoryBoundaryTests
{
    /// <summary>The normalized profile aggregate and its semantic values are Domain-owned.</summary>
    [Fact]
    public void CanonicalProfileDefinitionIsDomainOwned()
    {
        string[] definitionFiles =
        [
            "CanonicalProfileValueRules.cs",
            "CompositionProfileDefinition.cs",
            "CompositionProfileDefinition.Graph.cs",
            "CompositionProfileHeader.cs",
            "CompositionProfileMapBinding.cs",
            "CompositionProfileProcessor.cs",
            "CompositionProfileSpace.cs",
            "CompositionProfileView.cs",
        ];

        foreach (string file in definitionFiles)
        {
            Assert.True(File.Exists(Path.Combine(
                Root.FullName,
                "src",
                "NvtFwCombiner.Domain",
                "Composition",
                file)));
            Assert.False(File.Exists(Path.Combine(
                Root.FullName,
                "src",
                "NvtFwCombiner.Profiles",
                "V2",
                file)));
        }

        Assert.Contains(
            "namespace NvtFwCombiner.Domain.Composition;",
            ReadText("src/NvtFwCombiner.Domain/Composition/CompositionProfileDefinition.cs"),
            StringComparison.Ordinal);

        string domainProfileSources = string.Join(
            Environment.NewLine,
            definitionFiles.Select(file => ReadText($"src/NvtFwCombiner.Domain/Composition/{file}")));
        Assert.DoesNotContain("schemaVersion", domainProfileSources, StringComparison.Ordinal);
        Assert.DoesNotContain("NvtFwCombiner.Contracts", domainProfileSources, StringComparison.Ordinal);
        Assert.DoesNotContain("NvtFwCombiner.Profiles", domainProfileSources, StringComparison.Ordinal);
        Assert.DoesNotContain("System.Text.Json", domainProfileSources, StringComparison.Ordinal);

        string profileSources = ReadProfileSources();
        Assert.DoesNotMatch(ProfileDefinitionDeclarationRegex(), profileSources);
        Assert.DoesNotContain(
            "IReadOnlyList<CompositionInputSlotDefinition> InputSlots { get; }",
            profileSources,
            StringComparison.Ordinal);
        Assert.DoesNotContain(
            "IReadOnlyList<CompositionOperationDefinition> Operations { get; }",
            profileSources,
            StringComparison.Ordinal);

        Assert.False(File.Exists(Path.Combine(
            Root.FullName,
            "src",
            "NvtFwCombiner.Profiles",
            "V2",
            "CompositionProfileValueRules.cs")));
        Assert.DoesNotContain("SemanticVersionPattern", profileSources, StringComparison.Ordinal);
        Assert.DoesNotContain("ExternalToolBindingIdPattern", profileSources, StringComparison.Ordinal);
        Assert.DoesNotContain("LegacyCombinerInvocationProfileIdPattern", profileSources, StringComparison.Ordinal);
        Assert.DoesNotMatch(ProfileRangeValidatorDeclarationRegex(), profileSources);
    }

    /// <summary>The plan compiler lowers closed semantic kinds without workflow-name branches or private range algebra.</summary>
    [Fact]
    public void V2PlanCompilerUsesClosedDomainSemantics()
    {
        string compilerRoot = Path.Combine(
            Root.FullName,
            "src",
            "NvtFwCombiner.Profiles",
            "V2");
        string compilerSources = string.Join(
            Environment.NewLine,
            Directory.EnumerateFiles(compilerRoot, "V2CompositionPlanCompiler*.cs")
                .OrderBy(static path => path, StringComparer.Ordinal)
                .Select(File.ReadAllText));

        Assert.DoesNotContain("ExperienceIds.", compilerSources, StringComparison.Ordinal);
        string trustedCompiler = ReadText(
            "src/NvtFwCombiner.Profiles/V2/TrustedProfileBundleCatalog.Compilation.cs");
        string compilationSources = string.Join(
            Environment.NewLine,
            compilerSources,
            trustedCompiler);
        Assert.DoesNotContain("ExperienceIds.", trustedCompiler, StringComparison.Ordinal);
        Assert.DoesNotContain("TrustedV2CompositionCompiler", trustedCompiler, StringComparison.Ordinal);
        foreach (string workflowLiteral in new[]
                 {
                     "\"standard-merge\"",
                     "\"ab-merge\"",
                     "\"general-merge\"",
                     "\"dp-replace\"",
                     "\"ctrlram-replace\"",
                     "\"general-replace\"",
                 })
        {
            Assert.DoesNotContain(workflowLiteral, compilationSources, StringComparison.Ordinal);
        }
        Assert.Empty(CompiledIcIdentityRegex().Matches(compilationSources));

        string profileSources = ReadProfileSources();
        int planConstructionCount = CompositionPlanConstructionRegex().Count(compilerSources);
        int detailsConstructionCount = CompiledDetailsConstructionRegex().Count(compilerSources);
        Assert.NotEqual(0, planConstructionCount);
        Assert.NotEqual(0, detailsConstructionCount);
        Assert.Equal(
            planConstructionCount,
            CompositionPlanConstructionRegex().Count(profileSources));
        Assert.Equal(
            detailsConstructionCount,
            CompiledDetailsConstructionRegex().Count(profileSources));

        Assert.Equal(1, CountOccurrences(compilerSources, ".ExperienceId"));
        Assert.Equal(0, CountOccurrences(compilerSources, ".ModeId"));
        Assert.Equal(1, CountOccurrences(compilerSources, ".ProfileId"));
        Assert.Equal(1, CountOccurrences(compilerSources, ".FamilyId"));
        string contractLowering = ReadText(
            "src/NvtFwCombiner.Profiles/V2/V2CompositionPlanCompiler.ContractLowering.cs");
        Assert.Contains("profile.ProfileId,", contractLowering, StringComparison.Ordinal);
        Assert.Contains("profile.ExperienceId,", contractLowering, StringComparison.Ordinal);
        Assert.Contains(
            "profile.Output.AllowsRuntimeExecution(profile.CompositionKind)",
            ReadText("src/NvtFwCombiner.Profiles/V2/V2CompositionPlanCompiler.Admission.cs"),
            StringComparison.Ordinal);
        Assert.Contains(
            "!output.AllowsRuntimeExecution(compositionKind)",
            ReadText("src/NvtFwCombiner.Domain/Composition/CompiledComposition.cs"),
            StringComparison.Ordinal);
        Assert.DoesNotContain(
            "private static IEnumerable<ByteRange> Subtract(",
            compilerSources,
            StringComparison.Ordinal);
    }

    /// <summary>Locks trusted compilation and bundle identity to their existing catalog owners.</summary>
    [Fact]
    public void TrustedCatalogDoesNotReintroduceCompilerOrBundleIdentityFacades()
    {
        Assert.False(File.Exists(Path.Combine(
            Root.FullName,
            "src",
            "NvtFwCombiner.Profiles",
            "V2",
            "TrustedV2CompositionCompiler.cs")));

        string source = ReadText(
            "src/NvtFwCombiner.Profiles/V2/TrustedProfileBundleCatalogSource.cs");
        _ = Assert.Single(BundleIdentityParameterRegex().Matches(source));
        Assert.Empty(LooseBundleIdentityParameterRegex().Matches(source));
        Assert.Empty(CatalogSourceConstructionRegex().Matches(ReadProfileSources()));
        _ = Assert.Single(CatalogSourceConstructionRegex().Matches(ReadBootstrapSources()));
    }

    [GeneratedRegex(
        @"\bnew\s+CompositionPlan\s*\(",
        RegexOptions.CultureInvariant | RegexOptions.NonBacktracking)]
    private static partial Regex CompositionPlanConstructionRegex();

    [GeneratedRegex(
        @"\bnew\s+V2CompiledCompositionDetails\s*\(",
        RegexOptions.CultureInvariant | RegexOptions.NonBacktracking)]
    private static partial Regex CompiledDetailsConstructionRegex();

    [GeneratedRegex(
        @"\bProfileBundleIdentity\s+bundleIdentity\b",
        RegexOptions.CultureInvariant | RegexOptions.NonBacktracking)]
    private static partial Regex BundleIdentityParameterRegex();

    [GeneratedRegex(
        @"\bstring\s+(?:bundleId|bundleVersion|bundleContentHash|trustAnchorBindingId)\s*,",
        RegexOptions.CultureInvariant | RegexOptions.NonBacktracking)]
    private static partial Regex LooseBundleIdentityParameterRegex();

    [GeneratedRegex(
        @"\bNT\d{5}\b",
        RegexOptions.CultureInvariant | RegexOptions.NonBacktracking)]
    private static partial Regex CompiledIcIdentityRegex();

    [GeneratedRegex(
        @"\bnew\s+TrustedProfileBundleCatalogSource\s*\(",
        RegexOptions.CultureInvariant | RegexOptions.NonBacktracking)]
    private static partial Regex CatalogSourceConstructionRegex();

    [GeneratedRegex(
        @"(?m)^\s*internal\s+(?:(?:sealed|abstract|partial)\s+)*(?:class|record)\s+\w*(?:ProfileDefinition|CompositionDefinition)\b",
        RegexOptions.CultureInvariant | RegexOptions.NonBacktracking)]
    private static partial Regex ProfileDefinitionDeclarationRegex();

    [GeneratedRegex(
        @"(?m)^\s*(?:internal|private)\s+static\s+ByteRange\s+RequireRange\s*\(",
        RegexOptions.CultureInvariant | RegexOptions.NonBacktracking)]
    private static partial Regex ProfileRangeValidatorDeclarationRegex();
}
