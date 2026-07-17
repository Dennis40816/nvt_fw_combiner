namespace NvtFwCombiner.Architecture.Tests;

public sealed partial class RepositoryBoundaryTests
{
    /// <summary>Verifies synthetic construction and inspection helpers never become production API.</summary>
    [Fact]
    public void SyntheticFirmwareHelpersStayInTestSupport()
    {
        string domain = ReadDomainSources();
        string testFactory = ReadText("tests/NvtFwCombiner.TestSupport/FirmwareImageMapTestFactory.cs");

        Assert.DoesNotContain("CreateDirect(", domain, StringComparison.Ordinal);
        Assert.DoesNotContain("HasSameShape(", domain, StringComparison.Ordinal);
        Assert.DoesNotContain("EnumerateFactProvenance(", domain, StringComparison.Ordinal);
        Assert.DoesNotContain("TryResolveField(", domain, StringComparison.Ordinal);
        Assert.Contains("public static FirmwareImageMap CreateDirect(", testFactory, StringComparison.Ordinal);
    }

    /// <summary>Verifies only the Domain resolver can materialize one payload-free resolved firmware map.</summary>
    [Fact]
    public void ResolvedFirmwareMapCreationStaysDomainResolverOwned()
    {
        string result = ReadText("src/NvtFwCombiner.Domain/Firmware/FirmwareMapResolutionResult.cs");
        string resolver = ReadText("src/NvtFwCombiner.Domain/Firmware/FirmwareFamilyResolutionDefinition.MapResolution.cs");
        string domain = ReadDomainSources();
        string profiles = ReadProfileSources();
        string bootstrap = ReadBootstrapSources();
        int tokenGuard = result.IndexOf(
            "ReferenceEquals(constructionToken, ResolvedMapConstructionToken)",
            StringComparison.Ordinal);
        int firstMetadataConsumption = result.IndexOf(
            "ArgumentNullException.ThrowIfNull(definition)",
            StringComparison.Ordinal);

        Assert.Contains("private static readonly object ResolvedMapConstructionToken", result, StringComparison.Ordinal);
        Assert.Contains("internal ResolvedFirmwareImageMap(", result, StringComparison.Ordinal);
        Assert.Contains("ReferenceEquals(constructionToken, ResolvedMapConstructionToken)", result, StringComparison.Ordinal);
        Assert.DoesNotContain("internal static ResolvedFirmwareImageMap Create", result, StringComparison.Ordinal);
        Assert.Contains("new ResolvedFirmwareImageMap(", resolver, StringComparison.Ordinal);
        Assert.Equal(1, CountOccurrences(domain, "new ResolvedFirmwareImageMap("));
        Assert.True(tokenGuard >= 0 && tokenGuard < firstMetadataConsumption);
        Assert.DoesNotContain("new ResolvedFirmwareImageMap(", profiles, StringComparison.Ordinal);
        Assert.DoesNotContain("new ResolvedFirmwareImageMap(", bootstrap, StringComparison.Ordinal);
    }
}
