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
    /// <summary>The generator reuses runtime admission, and product projects never reference its assembly.</summary>
    [Fact]
    public void PrebuiltGeneratorHasOneInwardAdmissionPath()
    {
        string generator = ReadText("eng/prebuilt-profile-catalog/PrebuiltProfileCatalogGenerator.cs");
        AssertContainsAll(generator, "ProfileBundleLoader.Load(", "bundle.CreateDocumentProjection()",
            "BuiltInProfileBundleAdmissionSettings.Limits", "BuiltInProfileBuildAdmissionIdentity.Read()",
            "projection.ManifestSnapshot", "projection.Documents");
        AssertDoesNotContainAny(generator, "new TrustedProfileBundle(", "BuiltInV2BundleRegistry",
            "JsonSerializer", "Normalizer", "Compiler", "SchemaValidator");
        string infrastructure = ReadText("src/NvtFwCombiner.Infrastructure/NvtFwCombiner.Infrastructure.csproj");
        Assert.DoesNotContain("ProjectReference Include=\"..\\..\\eng", infrastructure, StringComparison.Ordinal);
        string bootstrap = ReadText("src/NvtFwCombiner.Bootstrap/NvtFwCombiner.Bootstrap.csproj");
        var project = System.Xml.Linq.XDocument.Parse(bootstrap);
        System.Xml.Linq.XElement reference = Assert.Single(project.Descendants("ProjectReference"),
            e => e.Attribute("Include")!.Value.Contains("PrebuiltProfileCatalogGenerator", StringComparison.Ordinal));
        Assert.Equal("false", reference.Attribute("ReferenceOutputAssembly")?.Value);
        Assert.Equal("all", reference.Attribute("PrivateAssets")?.Value);
        string target = ReadText("eng/prebuilt-profile-catalog/NvtFwCombiner.ProfileAdmissionIdentity.targets");
        Assert.Contains("BeforeTargets=\"GetAssemblyAttributes;CreateGeneratedAssemblyInfoInputsCacheFile\"", target, StringComparison.Ordinal);
        Assert.DoesNotContain("PrebuiltProfileCatalogGenerator", target, StringComparison.Ordinal);
        string shared = ReadText("src/NvtFwCombiner.Infrastructure/Bundles/BuiltInProfileBundleAdmissionSettings.cs");
        AssertContainsAll(shared, "16384", "32", "16, 131072, 262144, 8");
        Assert.Contains("BuiltInProfileBundleAdmissionSettings.Limits",
            ReadText("src/NvtFwCombiner.Infrastructure/Composition/BuiltInV2Bundle.cs"), StringComparison.Ordinal);
    }
}
