namespace NvtFwCombiner.Architecture.Tests;

public sealed partial class RepositoryBoundaryTests
{
    /// <summary>The exact immutable catalog entry is the selection; duplicate token and result models cannot return.</summary>
    [Fact]
    public void TrustedCatalogSelectionUsesTheCanonicalEntryReference()
    {
        string profiles = ReadProfileSources();

        Assert.DoesNotContain("class ProfileSelection", profiles, StringComparison.Ordinal);
        Assert.DoesNotContain("class ProfileSelectionResult", profiles, StringComparison.Ordinal);
        Assert.DoesNotContain("class V2CompositionPreparationRequest", profiles, StringComparison.Ordinal);
        Assert.DoesNotContain("TryResolveSelection", profiles, StringComparison.Ordinal);
        Assert.Equal(1, CountOccurrences(profiles, "new ProfileBundleEntryIdentity("));
        Assert.Contains(
            "TrustedCompositionProfileCatalogEntry? SelectProfile(",
            profiles,
            StringComparison.Ordinal);
        Assert.Contains("Profiles.Any(candidate => ReferenceEquals(candidate, profile))", profiles, StringComparison.Ordinal);
    }
}
