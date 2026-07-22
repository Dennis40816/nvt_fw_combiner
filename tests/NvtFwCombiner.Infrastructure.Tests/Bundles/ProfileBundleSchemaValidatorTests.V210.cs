using System.Text.Json.Nodes;
using NvtFwCombiner.Infrastructure.Bundles;
using NvtFwCombiner.TestSupport;

namespace NvtFwCombiner.Infrastructure.Tests.Bundles;

public sealed partial class ProfileBundleSchemaValidatorTests
{
    /// <summary>Verifies schema 2.10 admits the complete closed declared-prefix shape for generic Merge sources.</summary>
    [Theory]
    [InlineData("tp-firmware")]
    [InlineData("dp-firmware")]
    [InlineData("auxiliary")]
    public void ValidateEntriesAcceptsDeclaredPrefixAuthorityForV210(string artifactClass)
    {
        JsonObject profile = DeclaredPrefixProfile(artifactClass);

        ProfileBundleSchemaValidator.ValidateEntries(
            CaptureCompositionProfile(profile.ToJsonString(), "composition-profile-v2.10.schema.json"),
            32);
    }

    /// <summary>Verifies schema 2.10 rejects incomplete, normalized, or ineligible declared-prefix sources.</summary>
    [Theory]
    [InlineData("missing-expectation")]
    [InlineData("normalized")]
    [InlineData("reference-image")]
    [InlineData("ctrlram-replacement")]
    [InlineData("unknown-field")]
    public void ValidateEntriesRejectsExpandedDeclaredPrefixAuthorityForV210(string mutation)
    {
        JsonObject profile = DeclaredPrefixProfile("auxiliary");
        JsonObject slot = Assert.IsType<JsonObject>(Assert.IsType<JsonArray>(profile["inputSlots"])[0]);
        JsonObject acceptance = Assert.IsType<JsonObject>(slot["acceptance"]);
        JsonObject lengthRule = Assert.IsType<JsonObject>(acceptance["lengthRule"]);
        switch (mutation)
        {
            case "missing-expectation":
                _ = lengthRule.Remove("expectedOuterLengths");
                break;
            case "normalized":
                acceptance["normalization"] = new JsonObject
                {
                    ["kind"] = "pad-shorter",
                    ["fillByte"] = 255,
                    ["evidenceRef"] = "synthetic-padding",
                };
                slot["artifactClass"] = "dp-firmware";
                break;
            case "reference-image":
            case "ctrlram-replacement":
                slot["artifactClass"] = mutation;
                break;
            case "unknown-field":
                lengthRule["allowTail"] = true;
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(mutation), mutation, "Unknown schema mutation.");
        }

        _ = Assert.Throws<InvalidDataException>(() => ProfileBundleSchemaValidator.ValidateEntries(
            CaptureCompositionProfile(profile.ToJsonString(), "composition-profile-v2.10.schema.json"),
            32));
    }

    /// <summary>Verifies immutable schema 2.9 cannot opt into the new declared-prefix authority.</summary>
    [Fact]
    public void ValidateEntriesRejectsDeclaredPrefixAuthorityForV29()
    {
        JsonObject profile = DeclaredPrefixProfile("auxiliary");
        profile["schemaVersion"] = "2.9";

        _ = Assert.Throws<InvalidDataException>(() => ProfileBundleSchemaValidator.ValidateEntries(
            CaptureCompositionProfile(profile.ToJsonString(), "composition-profile-v2.9.schema.json"),
            32));
    }

    private static JsonObject DeclaredPrefixProfile(string artifactClass)
    {
        JsonObject profile = Assert.IsType<JsonObject>(JsonNode.Parse(
            TrustedV2BundleTestDocuments.ProfileJson(new string('c', 64))));
        profile["schemaVersion"] = "2.10";
        profile["compilationContext"] = new JsonObject { ["kind"] = "resolved-map" };
        JsonObject slot = Assert.IsType<JsonObject>(Assert.IsType<JsonArray>(profile["inputSlots"])[0]);
        slot["artifactClass"] = artifactClass;
        Assert.IsType<JsonObject>(slot["acceptance"])["lengthRule"] = new JsonObject
        {
            ["kind"] = "declared-prefix-with-warning",
            ["requiredEndExclusive"] = 16,
            ["expectedOuterLengths"] = new JsonArray(16),
            ["shortInputIssueCode"] = "INPUT_SHORT",
            ["unexpectedOuterLengthIssueCode"] = "INPUT_OUTER_LENGTH",
        };
        return profile;
    }
}
