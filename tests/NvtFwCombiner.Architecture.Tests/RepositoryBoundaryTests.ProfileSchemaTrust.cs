namespace NvtFwCombiner.Architecture.Tests;

public sealed partial class RepositoryBoundaryTests
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

        AssertDoesNotContainAny(normalizer, "RequireList(", "Require(", "ValidateDistinctFactIds(",
            ".ValueKind", "document.SchemaVersion", "Required array is missing.",
            "must be declared together.");
        Assert.DoesNotContain(
            "require firmware-family schema version",
            normalizer,
            StringComparison.OrdinalIgnoreCase);
        AssertDoesNotContainAny(normalizer, "Expected firmware-family schema version",
            "perfectFamilyMembers", "SharedBindingKey", "member-specific map",
            "more than one shared relationship", "NormalizeCoveragePolicy", "RequireFactId",
            "public static partial class FirmwareFamilyResolutionNormalizer");
        Assert.Equal(1, CountOccurrences(
            normalizer,
            "ArgumentNullException.ThrowIfNull(document);"));

        string definition = ReadText(
            "src/NvtFwCombiner.Domain/Firmware/FirmwareFamilyResolutionDefinition.cs");
        AssertContainsAll(definition, "ValidateFamilyRelationships(", "HasMemberAlias(");
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
        AssertContainsAll(projection, "ValidateAndClone(");
        Assert.False(File.Exists(Path.Combine(
            Root.FullName,
            "src/NvtFwCombiner.Profiles/V2/CompositionProfileValueRules.cs")));
        AssertDoesNotContainAny(normalizer, "RequireConstant(", "RequireList(", "RequireObject(",
            "RequireText(", "private static JsonElement Require(", "?? throw Error(", " is missing.");
        Assert.DoesNotContain(
            "requires composition-profile schema version",
            normalizer,
            StringComparison.OrdinalIgnoreCase);
        AssertDoesNotContainAny(normalizer, "Expected composition-profile schema version");
    }
}
