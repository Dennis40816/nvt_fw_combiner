namespace NvtFwCombiner.Architecture.Tests;

public sealed partial class ProfileBoundaryTests : RepositoryBoundaryTestBase
{
    /// <summary>Firmware-family Profiles lowering consumes only the trusted schema-owned shape.</summary>
    [Fact]
    public void FirmwareFamilyShapeValidationStaysAtTheTrustedSchemaGateway()
    {
        string normalizerRoot = Path.Combine(
            Root.FullName,
            "src",
            "NvtFwCombiner.Profiles",
            "FirmwareFamilies");
        string normalizer = string.Join(
            Environment.NewLine,
            Directory.EnumerateFiles(normalizerRoot, "FirmwareFamilyResolutionNormalizer*.cs")
                .OrderBy(static path => path, StringComparer.Ordinal)
                .Select(File.ReadAllText));

        Assert.DoesNotContain("RequireList(", normalizer, StringComparison.Ordinal);
        Assert.DoesNotContain("Require(", normalizer, StringComparison.Ordinal);
        Assert.DoesNotContain("ValidateDistinctFactIds(", normalizer, StringComparison.Ordinal);
        Assert.DoesNotContain(".ValueKind", normalizer, StringComparison.Ordinal);
        Assert.DoesNotContain("document.SchemaVersion", normalizer, StringComparison.Ordinal);
        Assert.DoesNotContain("Required array is missing.", normalizer, StringComparison.Ordinal);
        Assert.DoesNotContain("must be declared together.", normalizer, StringComparison.Ordinal);
        Assert.DoesNotContain(
            "require firmware-family schema version",
            normalizer,
            StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(
            "Expected firmware-family schema version",
            normalizer,
            StringComparison.Ordinal);
        Assert.DoesNotContain("perfectFamilyMembers", normalizer, StringComparison.Ordinal);
        Assert.DoesNotContain("SharedBindingKey", normalizer, StringComparison.Ordinal);
        Assert.DoesNotContain("member-specific map", normalizer, StringComparison.Ordinal);
        Assert.DoesNotContain("more than one shared relationship", normalizer, StringComparison.Ordinal);
        Assert.DoesNotContain("NormalizeCoveragePolicy", normalizer, StringComparison.Ordinal);
        Assert.DoesNotContain("RequireFactId", normalizer, StringComparison.Ordinal);
        Assert.DoesNotContain(
            "public static partial class FirmwareFamilyResolutionNormalizer",
            normalizer,
            StringComparison.Ordinal);
        Assert.Equal(1, CountOccurrences(
            normalizer,
            "ArgumentNullException.ThrowIfNull(document);"));

        string definition = ReadText(
            "src/NvtFwCombiner.Domain/Firmware/FirmwareFamilyResolutionDefinition.cs");
        Assert.Contains("ValidateFamilyRelationships(", definition, StringComparison.Ordinal);
        Assert.Contains("HasMemberAlias(", definition, StringComparison.Ordinal);
    }

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
