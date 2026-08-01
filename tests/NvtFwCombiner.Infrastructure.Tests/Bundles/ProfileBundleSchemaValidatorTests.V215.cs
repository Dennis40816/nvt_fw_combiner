using System.Text.Json.Nodes;
using NvtFwCombiner.Infrastructure.Bundles;
using NvtFwCombiner.TestSupport;

namespace NvtFwCombiner.Infrastructure.Tests.Bundles;

public sealed partial class ProfileBundleSchemaValidatorTests
{
    /// <summary>Verifies schema 2.15 owns complete typed output naming authority.</summary>
    [Theory]
    [InlineData("canonical", true)]
    [InlineData("missing-rule", false)]
    [InlineData("missing-metadata-binding", false)]
    [InlineData("missing-placeholder", false)]
    [InlineData("binding-on-clock", false)]
    [InlineData("unknown-artifact", false)]
    [InlineData("replace-underscore", false)]
    [InlineData("v214", false)]
    public void ValidateEntriesEnforcesTypedOutputNamingForV215(
        string mutation,
        bool expectedValid)
    {
        JsonObject profile = Assert.IsType<JsonObject>(JsonNode.Parse(
            TrustedV2BundleTestDocuments.ProfileJson(new string('c', 64))));
        profile["schemaVersion"] = "2.15";
        profile["compilationContext"] =
            new JsonObject { ["kind"] = "resolved-map" };
        JsonObject output = Assert.IsType<JsonObject>(profile["output"]);
        output["fileNameTemplate"] =
            "{ic}_FlashCode_D{dp-version}T{tp-version}_{date}.bin";
        output["invalidCharacterPolicy"] = "reject";
        output["requiredTokenIds"] =
            new JsonArray("date", "dp-version", "ic", "tp-version");
        output["ruleId"] = "normal-flashcode-v1";
        output["outputArtifactType"] = "flash-code";
        output["tokenRequirements"] = new JsonArray(
            Token("date", "run-date-utc", "block"),
            Token(
                "dp-version",
                "dpcmi-version",
                "use-placeholder",
                "dpcmi-inspection",
                "xxxx"),
            Token("ic", "compiled-ic", "block"),
            Token(
                "tp-version",
                "firmware-config-tp-version",
                "use-placeholder",
                "firmware-config-inspection",
                "xxxx"));

        JsonArray requirements = Assert.IsType<JsonArray>(
            output["tokenRequirements"]);
        switch (mutation)
        {
            case "canonical":
                break;
            case "missing-rule":
                _ = output.Remove("ruleId");
                break;
            case "missing-metadata-binding":
                _ = Source(requirements, 1).Remove("metadataBindingId");
                break;
            case "missing-placeholder":
                _ = Assert.IsType<JsonObject>(requirements[1])
                    .Remove("placeholder");
                break;
            case "binding-on-clock":
                Source(requirements, 0)["metadataBindingId"] = "clock";
                break;
            case "unknown-artifact":
                output["outputArtifactType"] = "future";
                break;
            case "replace-underscore":
                output["invalidCharacterPolicy"] = "replace-underscore";
                break;
            case "v214":
                profile["schemaVersion"] = "2.14";
                break;
            default:
                throw new ArgumentOutOfRangeException(
                    nameof(mutation),
                    mutation,
                    "Unknown schema mutation.");
        }

        string schemaFileName = mutation == "v214"
            ? "composition-profile-v2.14.schema.json"
            : "composition-profile-v2.15.schema.json";
        ProfileBundleEntrySnapshotCollection collection =
            CaptureCompositionProfile(
                profile.ToJsonString(),
                schemaFileName);
        if (expectedValid)
        {
            ProfileBundleSchemaValidator.ValidateEntries(collection, 32);
            return;
        }

        _ = Assert.Throws<InvalidDataException>(() =>
            ProfileBundleSchemaValidator.ValidateEntries(collection, 32));
    }

    private static JsonObject Token(
        string tokenId,
        string sourceKind,
        string missingPolicy,
        string? metadataBindingId = null,
        string? placeholder = null)
    {
        var source = new JsonObject { ["kind"] = sourceKind };
        if (metadataBindingId is not null)
        {
            source["metadataBindingId"] = metadataBindingId;
        }

        var token = new JsonObject
        {
            ["tokenId"] = tokenId,
            ["source"] = source,
            ["missingPolicy"] = missingPolicy,
        };
        if (placeholder is not null)
        {
            token["placeholder"] = placeholder;
        }

        return token;
    }

    private static JsonObject Source(JsonArray requirements, int index)
    {
        return Assert.IsType<JsonObject>(
            Assert.IsType<JsonObject>(requirements[index])["source"]);
    }
}
