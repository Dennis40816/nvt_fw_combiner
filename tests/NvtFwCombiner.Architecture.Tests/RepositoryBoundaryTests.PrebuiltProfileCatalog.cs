namespace NvtFwCombiner.Architecture.Tests;

public sealed partial class RepositoryBoundaryTests
{
    /// <summary>The derived byte-container owner cannot select, admit, normalize or compile profiles.</summary>
    [Fact]
    public void PrebuiltCatalogCodecRemainsPureTransport()
    {
        string codec = ReadText("src/NvtFwCombiner.Infrastructure/Bundles/PrebuiltProfileCatalogCodec.cs");
        string format = ReadText("src/NvtFwCombiner.Infrastructure/Bundles/PrebuiltProfileCatalogFormat.cs");

        AssertDoesNotContainAny(codec + format,
            "ProfileBundleLoader", "TrustedProfileBundle", "ProfileBundleSchemaValidator",
            "Normalizer", "NvtFwCombiner.Application", "NvtFwCombiner.Profiles",
            "JsonSerializer", "File.", "Directory.", "Process.", "BuiltInV2Bundle");
    }
}
