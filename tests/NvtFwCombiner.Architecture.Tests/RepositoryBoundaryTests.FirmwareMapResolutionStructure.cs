namespace NvtFwCombiner.Architecture.Tests;

public sealed partial class RepositoryBoundaryTests
{
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
