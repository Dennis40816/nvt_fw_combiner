using System.Text.Json.Nodes;
using NvtFwCombiner.Infrastructure.Bundles;
using NvtFwCombiner.TestSupport;

namespace NvtFwCombiner.Infrastructure.Tests.Bundles;

public sealed partial class ProfileBundleSchemaValidatorTests
{
    /// <summary>Verifies schema 2.13 admits source-view coverage for unnormalized section sources.</summary>
    [Theory]
    [InlineData("tp-firmware")]
    [InlineData("dp-firmware")]
    [InlineData("auxiliary")]
    public void ValidateEntriesAcceptsSourceViewCoverageForV213(string artifactClass)
    {
        JsonObject profile = SourceViewCoverageProfile(artifactClass);

        ProfileBundleSchemaValidator.ValidateEntries(
            CaptureCompositionProfile(profile.ToJsonString(), "composition-profile-v2.13.schema.json"),
            32);
    }

    /// <summary>Verifies schema 2.13 keeps optional outer-length diagnostics paired and advisory.</summary>
    [Fact]
    public void ValidateEntriesAcceptsPairedSourceViewOuterLengthDiagnosticsForV213()
    {
        JsonObject profile = SourceViewCoverageProfile("dp-firmware");
        JsonObject lengthRule = GetSourceViewCoverageLengthRule(profile);
        lengthRule["expectedOuterLengths"] = new JsonArray(262144, 524288);
        lengthRule["unexpectedOuterLengthIssueCode"] = "INPUT_OUTER_LENGTH";

        ProfileBundleSchemaValidator.ValidateEntries(
            CaptureCompositionProfile(profile.ToJsonString(), "composition-profile-v2.13.schema.json"),
            32);
    }

    /// <summary>Verifies incomplete, normalized, ineligible, or pre-2.13 declarations fail closed.</summary>
    [Theory]
    [InlineData("missing-issue")]
    [InlineData("missing-lengths")]
    [InlineData("normalized")]
    [InlineData("reference-image")]
    [InlineData("ctrlram-replacement")]
    [InlineData("unknown-field")]
    [InlineData("v212")]
    public void ValidateEntriesRejectsInvalidSourceViewCoverageAuthorityForV213(string mutation)
    {
        JsonObject profile = SourceViewCoverageProfile("auxiliary");
        JsonObject slot = Assert.IsType<JsonObject>(Assert.IsType<JsonArray>(profile["inputSlots"])[0]);
        JsonObject acceptance = Assert.IsType<JsonObject>(slot["acceptance"]);
        JsonObject lengthRule = Assert.IsType<JsonObject>(acceptance["lengthRule"]);
        lengthRule["expectedOuterLengths"] = new JsonArray(16);
        lengthRule["unexpectedOuterLengthIssueCode"] = "INPUT_OUTER_LENGTH";

        switch (mutation)
        {
            case "missing-issue":
                _ = lengthRule.Remove("unexpectedOuterLengthIssueCode");
                break;
            case "missing-lengths":
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
                lengthRule["requiredEndExclusive"] = 16;
                break;
            case "v212":
                profile["schemaVersion"] = "2.12";
                break;
            default:
                throw new ArgumentOutOfRangeException(
                    nameof(mutation),
                    mutation,
                    "Unknown schema mutation.");
        }

        string schemaFileName = mutation == "v212"
            ? "composition-profile-v2.12.schema.json"
            : "composition-profile-v2.13.schema.json";
        _ = Assert.Throws<InvalidDataException>(() =>
            ProfileBundleSchemaValidator.ValidateEntries(
                CaptureCompositionProfile(profile.ToJsonString(), schemaFileName),
                32));
    }

    private static JsonObject SourceViewCoverageProfile(string artifactClass)
    {
        JsonObject profile = Assert.IsType<JsonObject>(JsonNode.Parse(
            TrustedV2BundleTestDocuments.ProfileJson(new string('c', 64))));
        profile["schemaVersion"] = "2.13";
        profile["compilationContext"] = new JsonObject { ["kind"] = "resolved-map" };
        JsonObject slot = Assert.IsType<JsonObject>(Assert.IsType<JsonArray>(profile["inputSlots"])[0]);
        slot["artifactClass"] = artifactClass;
        Assert.IsType<JsonObject>(slot["acceptance"])["lengthRule"] = new JsonObject
        {
            ["kind"] = "source-view-coverage",
        };
        return profile;
    }

    private static JsonObject GetSourceViewCoverageLengthRule(JsonObject profile)
    {
        JsonObject slot = Assert.IsType<JsonObject>(Assert.IsType<JsonArray>(profile["inputSlots"])[0]);
        return Assert.IsType<JsonObject>(Assert.IsType<JsonObject>(slot["acceptance"])["lengthRule"]);
    }
}
