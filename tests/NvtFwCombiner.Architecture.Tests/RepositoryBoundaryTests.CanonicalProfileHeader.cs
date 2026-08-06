namespace NvtFwCombiner.Architecture.Tests;

public sealed partial class RepositoryBoundaryTests
{
    /// <summary>Profile headers retain canonical Domain vocabulary without Profiles-only wrapper objects.</summary>
    [Fact]
    public void ProfileHeadersUseCanonicalDomainVocabulary()
    {
        string profiles = ReadProfileSources();
        string domain = ReadDomainSources();
        string bootstrap = ReadBootstrapSources();

        Assert.DoesNotContain("class CompositionProfileCompilationContext", profiles, StringComparison.Ordinal);
        Assert.DoesNotContain("class ResolvedMapProfileCompilationContext", profiles, StringComparison.Ordinal);
        Assert.DoesNotContain("class LogicalOutputProfileCompilationContext", profiles, StringComparison.Ordinal);
        Assert.DoesNotContain("class RuntimeReferenceReplaceProfileCompilationContext", profiles, StringComparison.Ordinal);
        Assert.DoesNotContain("record CompositionProfileExperience", profiles, StringComparison.Ordinal);
        Assert.Contains("internal sealed record CompositionProfileHeader(", domain, StringComparison.Ordinal);
        Assert.Equal(1, CountOccurrences(profiles, "new CompositionProfileHeader("));
        Assert.Contains(
            "Array.AsReadOnly([.. compilationContext.LogicalOutputMemberIds])",
            profiles,
            StringComparison.Ordinal);
        Assert.Contains("internal V2CompilationContextKind CompilationContextKind => Header.CompilationContextKind;", domain, StringComparison.Ordinal);
        Assert.Contains("internal string ExperienceId => Header.ExperienceId;", domain, StringComparison.Ordinal);
        Assert.DoesNotContain(".Experience.ExperienceId", bootstrap, StringComparison.Ordinal);
    }
}
