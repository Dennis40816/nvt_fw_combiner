using System.Text.Json.Nodes;
using NvtFwCombiner.Infrastructure.Bundles;
using NvtFwCombiner.TestSupport;

namespace NvtFwCombiner.Infrastructure.Tests.Bundles;

public sealed partial class ProfileBundleSchemaValidatorTests
{
    /// <summary>Verifies only schema 2.7 admits a legacy Combiner stage that stages sources without artifacts.</summary>
    [Theory]
    [InlineData("2.6", "composition-profile-v2.6.schema.json", false)]
    [InlineData("2.7", "composition-profile-v2.7.schema.json", true)]
    public void ValidateEntriesAdmitsSourceOnlyLegacyCombinerStageOnlyInV27(
        string schemaVersion,
        string schemaFileName,
        bool expectedValid)
    {
        JsonObject profile = Assert.IsType<JsonObject>(JsonNode.Parse(
            TrustedV2BundleTestDocuments.ProfileJson(new string('c', 64))));
        profile["schemaVersion"] = schemaVersion;
        profile["compilationContext"] = new JsonObject { ["kind"] = "resolved-map" };
        JsonObject stage = LegacyCombinerStage("legacy-combiner-1.13.0");
        stage["stagedSourceBindings"] = new JsonArray(new JsonObject
        {
            ["sourceViewId"] = "tp-code",
            ["targetViewId"] = "output-code",
        });
        stage["stagedArtifactBindings"] = new JsonArray();
        Assert.IsType<JsonArray>(profile["processorStages"]).Add(stage);

        ProfileBundleEntrySnapshotCollection collection = CaptureCompositionProfile(
            profile.ToJsonString(),
            schemaFileName);
        if (expectedValid)
        {
            ProfileBundleSchemaValidator.ValidateEntries(collection, 32);
            return;
        }

        _ = Assert.Throws<InvalidDataException>(() => ProfileBundleSchemaValidator.ValidateEntries(collection, 32));
    }
}
