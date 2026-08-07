namespace NvtFwCombiner.Architecture.Tests;

public sealed partial class RepositoryBoundaryTests
{
    /// <summary>Profiles consumes only schema-validated DTO shape and retains only semantic token lowering.</summary>
    [Fact]
    public void CompositionProfileShapeValidationStaysAtTheTrustedSchemaGateway()
    {
        string loader = ReadText(
            "src/NvtFwCombiner.Infrastructure/Bundles/ProfileBundleLoader.cs");
        string projection = ReadText(
            "src/NvtFwCombiner.Infrastructure/Bundles/TrustedProfileBundleDocumentProjection.cs");
        string normalizerRoot = Path.Combine(
            Root.FullName,
            "src",
            "NvtFwCombiner.Profiles",
            "V2");
        string normalizer = string.Join(
            Environment.NewLine,
            Directory.EnumerateFiles(normalizerRoot, "CompositionProfileNormalizer*.cs")
                .OrderBy(static path => path, StringComparer.Ordinal)
                .Select(File.ReadAllText));
        int schemaValidation = loader.IndexOf(
            "ProfileBundleSchemaValidator.ValidateEntries",
            StringComparison.Ordinal);
        int trustedBundle = loader.IndexOf(
            "return new TrustedProfileBundle(",
            StringComparison.Ordinal);

        Assert.True(schemaValidation >= 0 && schemaValidation < trustedBundle);
        Assert.Contains("ValidateAndClone(", projection, StringComparison.Ordinal);
        Assert.False(File.Exists(Path.Combine(
            Root.FullName,
            "src/NvtFwCombiner.Profiles/V2/CompositionProfileValueRules.cs")));
        Assert.DoesNotContain("RequireConstant(", normalizer, StringComparison.Ordinal);
        Assert.DoesNotContain("RequireList(", normalizer, StringComparison.Ordinal);
        Assert.DoesNotContain("RequireObject(", normalizer, StringComparison.Ordinal);
        Assert.DoesNotContain("RequireText(", normalizer, StringComparison.Ordinal);
        Assert.DoesNotContain("private static JsonElement Require(", normalizer, StringComparison.Ordinal);
        Assert.DoesNotContain("?? throw Error(", normalizer, StringComparison.Ordinal);
        Assert.DoesNotContain(" is missing.", normalizer, StringComparison.Ordinal);
        Assert.DoesNotContain(
            "requires composition-profile schema version",
            normalizer,
            StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(
            "Expected composition-profile schema version",
            normalizer,
            StringComparison.Ordinal);
    }
}
