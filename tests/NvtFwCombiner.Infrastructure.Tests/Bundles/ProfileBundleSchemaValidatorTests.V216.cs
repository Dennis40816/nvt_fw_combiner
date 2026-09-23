using System.Text.Json.Nodes;
using NvtFwCombiner.Infrastructure.Bundles;
using NvtFwCombiner.TestSupport;

namespace NvtFwCombiner.Infrastructure.Tests.Bundles;

public sealed partial class ProfileBundleSchemaValidatorTests
{
    /// <summary>Schema 2.16 accepts only a bound source extent, explicit template, and source-sized output.</summary>
    [Theory]
    [InlineData("complete", true)]
    [InlineData("missing-template", false)]
    [InlineData("missing-output-slot", false)]
    [InlineData("missing-binding", false)]
    [InlineData("old-schema", false)]
    public void ValidateEntriesEnforcesSourceEnvelopeV216(string mutation, bool valid)
    {
        JsonObject profile = Assert.IsType<JsonObject>(JsonNode.Parse(
            TrustedV2BundleTestDocuments.ProfileJson(new string('c', 64))));
        profile["schemaVersion"] = "2.16";
        profile["compilationContext"] = new JsonObject { ["kind"] = "resolved-map" };
        JsonArray slots = Assert.IsType<JsonArray>(profile["inputSlots"]);
        JsonObject dpSlot = Assert.IsType<JsonObject>(slots[0]!.DeepClone());
        dpSlot["slotId"] = "dp-input";
        dpSlot["role"] = "dp";
        dpSlot["artifactClass"] = "dp-firmware";
        Assert.IsType<JsonObject>(dpSlot["acceptance"])["lengthRule"] =
            new JsonObject { ["kind"] = "exact-resolved-map-capacity" };
        slots.Add(dpSlot);
        JsonArray spaces = Assert.IsType<JsonArray>(profile["spaces"]);
        spaces.Add(new JsonObject
        {
            ["spaceId"] = "dp-input",
            ["kind"] = "input-artifact",
            ["slotId"] = "dp-input",
            ["instancePolicy"] = "singleton",
        });
        profile["sourceEnvelopeBinding"] = new JsonObject
        {
            ["sourceSlotId"] = "dp-input",
            ["layoutTemplateMapId"] = "map",
            ["rootRegionId"] = "dp-container",
            ["whenSourceAbsent"] = "reject",
            ["expectedOuterLengths"] = new JsonArray(0x40000, 0x80000, 0x100000),
            ["unexpectedLengthIssueCode"] = "DP_NONSTANDARD_SIZE_WARNING",
        };
        JsonObject output = Assert.Single(
            spaces.Select(static item => Assert.IsType<JsonObject>(item)),
            static space => space["kind"]?.GetValue<string>() == "output-image");
        output["capacity"] = new JsonObject { ["kind"] = "source-slot", ["sourceSlotId"] = "dp-input" };
        Assert.IsType<JsonObject>(output["initializer"])["fillByte"] = 0;
        JsonObject naming = Assert.IsType<JsonObject>(profile["output"]);
        naming["fileNameTemplate"] = "{ic}_FlashCode_D{dp-version}T{tp-version}_{date}.bin";
        naming["invalidCharacterPolicy"] = "reject";
        naming["requiredTokenIds"] = new JsonArray("date", "dp-version", "ic", "tp-version");
        naming["ruleId"] = "normal-flashcode-v1";
        naming["outputArtifactType"] = "flash-code";
        naming["tokenRequirements"] = new JsonArray(
            Token("date", "run-date-utc", "block"),
            Token("dp-version", "dpcmi-version", "use-placeholder", "dpcmi-inspection", "xxxx"),
            Token("ic", "compiled-ic", "block"),
            Token("tp-version", "firmware-config-tp-version", "use-placeholder", "firmware-config-inspection", "xxxx"));

        switch (mutation)
        {
            case "complete": break;
            case "missing-template":
                _ = Assert.IsType<JsonObject>(profile["sourceEnvelopeBinding"]).Remove("layoutTemplateMapId");
                break;
            case "missing-output-slot":
                _ = Assert.IsType<JsonObject>(output["capacity"]).Remove("sourceSlotId");
                break;
            case "missing-binding":
                _ = profile.Remove("sourceEnvelopeBinding");
                break;
            case "old-schema":
                profile["schemaVersion"] = "2.15";
                break;
            default: throw new ArgumentOutOfRangeException(nameof(mutation), mutation, "Unknown mutation.");
        }

        ProfileBundleEntrySnapshotCollection collection = CaptureCompositionProfile(
            profile.ToJsonString(), mutation == "old-schema"
                ? "composition-profile-v2.15.schema.json"
                : "composition-profile-v2.16.schema.json");
        if (valid)
        {
            ProfileBundleSchemaValidator.ValidateEntries(collection, 32);
        }
        else
        {
            _ = Assert.Throws<InvalidDataException>(() => ProfileBundleSchemaValidator.ValidateEntries(collection, 32));
        }
    }
}
