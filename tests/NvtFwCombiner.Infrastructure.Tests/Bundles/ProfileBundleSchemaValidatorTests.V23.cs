using System.Text.Json.Nodes;
using NvtFwCombiner.Infrastructure.Bundles;
using NvtFwCombiner.TestSupport;

namespace NvtFwCombiner.Infrastructure.Tests.Bundles;

public sealed partial class ProfileBundleSchemaValidatorTests
{
    /// <summary>Verifies logical-output capacity is rejected by every schema published before 2.3.</summary>
    [Theory]
    [InlineData("2.0", "composition-profile-v2.schema.json")]
    [InlineData("2.1", "composition-profile-v2.1.schema.json")]
    [InlineData("2.2", "composition-profile-v2.2.schema.json")]
    public void ValidateEntriesRejectsRuntimeRequestCapacityBeforeV23(string schemaVersion, string schemaFileName)
    {
        JsonObject profile = Assert.IsType<JsonObject>(JsonNode.Parse(
            TrustedV2BundleTestDocuments.ProfileJson(new string('c', 64))));
        profile["schemaVersion"] = schemaVersion;
        Assert.IsType<JsonObject>(Assert.IsType<JsonArray>(profile["spaces"])[1])["capacity"] = new JsonObject
        {
            ["kind"] = "runtime-request",
        };

        _ = Assert.Throws<InvalidDataException>(() => ProfileBundleSchemaValidator.ValidateEntries(
            CaptureCompositionProfile(profile.ToJsonString(), schemaFileName),
            32));
    }

    /// <summary>Verifies logical-output capacity is admitted only by the pinned 2.3 profile schema.</summary>
    [Fact]
    public void ValidateEntriesAdmitsRuntimeRequestCapacityForV23OutputImage()
    {
        JsonObject profile = Assert.IsType<JsonObject>(JsonNode.Parse(
            TrustedV2BundleTestDocuments.ProfileJson(new string('c', 64))));
        profile["schemaVersion"] = "2.3";
        Assert.IsType<JsonObject>(Assert.IsType<JsonArray>(profile["spaces"])[1])["capacity"] = new JsonObject
        {
            ["kind"] = "runtime-request",
        };

        ProfileBundleSchemaValidator.ValidateEntries(
            CaptureCompositionProfile(profile.ToJsonString(), "composition-profile-v2.3.schema.json"),
            32);
    }

    /// <summary>Verifies schema 2.3 reserves runtime-request capacity for output images only.</summary>
    [Fact]
    public void ValidateEntriesRejectsRuntimeRequestCapacityForV23WorkBuffer()
    {
        JsonObject profile = Assert.IsType<JsonObject>(JsonNode.Parse(
            TrustedV2BundleTestDocuments.ProfileJson(new string('c', 64))));
        profile["schemaVersion"] = "2.3";
        Assert.IsType<JsonArray>(profile["spaces"]).Add(new JsonObject
        {
            ["spaceId"] = "work",
            ["kind"] = "work-buffer",
            ["capacity"] = new JsonObject { ["kind"] = "runtime-request" },
            ["initializer"] = new JsonObject
            {
                ["kind"] = "blank",
                ["fillByte"] = 0,
            },
        });

        _ = Assert.Throws<InvalidDataException>(() => ProfileBundleSchemaValidator.ValidateEntries(
            CaptureCompositionProfile(profile.ToJsonString(), "composition-profile-v2.3.schema.json"),
            32));
    }

    /// <summary>Verifies schema 2.3 retains the versioned legacy Combiner binding grammar.</summary>
    [Fact]
    public void ValidateEntriesAcceptsPublishedCombinerToolBindingInV23()
    {
        JsonObject profile = Assert.IsType<JsonObject>(JsonNode.Parse(
            TrustedV2BundleTestDocuments.ProfileJson(new string('c', 64))));
        profile["schemaVersion"] = "2.3";
        Assert.IsType<JsonArray>(profile["processorStages"]).Add(
            LegacyCombinerStage("legacy-combiner-1.13.0"));

        ProfileBundleSchemaValidator.ValidateEntries(
            CaptureCompositionProfile(profile.ToJsonString(), "composition-profile-v2.3.schema.json"),
            32);
    }
}
