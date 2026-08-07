using System.Text.Json.Nodes;
using NvtFwCombiner.Infrastructure.Bundles;
using NvtFwCombiner.TestSupport;

namespace NvtFwCombiner.Infrastructure.Tests.Bundles;

public sealed partial class ProfileBundleSchemaValidatorTests
{
    /// <summary>The trusted schema owns experience display-only vocabulary that never enters Domain.</summary>
    [Theory]
    [InlineData("audience")]
    [InlineData("topologyAuthoring")]
    [InlineData("displayNameKey")]
    public void ValidateEntriesRejectsInvalidExperienceDisplayVocabulary(string propertyName)
    {
        JsonObject profile = Assert.IsType<JsonObject>(JsonNode.Parse(
            TrustedV2BundleTestDocuments.ProfileJson(new string('c', 64))));
        JsonObject experience = Assert.IsType<JsonObject>(profile["experience"]);
        experience[propertyName] = propertyName == "displayNameKey" ? string.Empty : "future";

        _ = Assert.Throws<InvalidDataException>(() => ProfileBundleSchemaValidator.ValidateEntries(
            CaptureCompositionProfile(profile.ToJsonString()),
            32));
    }

    /// <summary>Verifies every admitted composition schema retains the canonical experience vocabulary.</summary>
    [Fact]
    public void AdmittedCompositionSchemasRetainCanonicalExperienceVocabulary()
    {
        string contractsPath = RepositoryPaths.FromRepositoryRoot("docs", "contracts");
        JsonObject expected = ExperienceSchema("composition-profile-v2.schema.json", contractsPath);

        foreach (string schemaPath in Directory.EnumerateFiles(
                     contractsPath,
                     "composition-profile-v2*.schema.json",
                     SearchOption.TopDirectoryOnly))
        {
            JsonObject actual = ExperienceSchema(Path.GetFileName(schemaPath), contractsPath);
            Assert.True(
                JsonNode.DeepEquals(expected, actual),
                $"Composition schema '{Path.GetFileName(schemaPath)}' drifted from the canonical experience vocabulary.");
        }
    }

    private static JsonObject ExperienceSchema(string schemaFileName, string contractsPath)
    {
        JsonObject schema = Assert.IsType<JsonObject>(JsonNode.Parse(File.ReadAllText(
            Path.Combine(contractsPath, schemaFileName))));
        return Assert.IsType<JsonObject>(Assert.IsType<JsonObject>(schema["$defs"])["experience"]);
    }
}
