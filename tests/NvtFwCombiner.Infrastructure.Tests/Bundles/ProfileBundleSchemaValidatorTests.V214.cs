using System.Text.Json.Nodes;
using NvtFwCombiner.Infrastructure.Bundles;

namespace NvtFwCombiner.Infrastructure.Tests.Bundles;

public sealed partial class ProfileBundleSchemaValidatorTests
{
    /// <summary>Verifies schema 2.14 admits canonical region-template sources and instance deltas.</summary>
    [Theory]
    [InlineData("instance-delta", true)]
    [InlineData("fixed-numeric", true)]
    [InlineData("missing-selector-instance", false)]
    [InlineData("missing-selector-region", false)]
    [InlineData("missing-delta-source", false)]
    [InlineData("missing-delta-target", false)]
    [InlineData("unknown-delta-kind", false)]
    [InlineData("signed-value", false)]
    [InlineData("wrapping-overflow", false)]
    [InlineData("v213", false)]
    public void ValidateEntriesEnforcesRegionTemplateCompositionAuthorityForV214(
        string mutation,
        bool expectedValid)
    {
        JsonObject profile = SourceViewCoverageProfile("tp-firmware");
        profile["schemaVersion"] = "2.14";
        JsonObject sourceView = Assert.IsType<JsonObject>(Assert.IsType<JsonArray>(profile["views"])[0]);
        sourceView["selector"] = new JsonObject
        {
            ["kind"] = "region-template-range",
            ["regionInstanceId"] = "a-bank",
            ["templateRegionId"] = "tp-code",
        };
        JsonObject operation = Assert.IsType<JsonObject>(Assert.IsType<JsonArray>(profile["operations"])[0]);
        operation["kind"] = "transform-scalar";
        operation["widthBytes"] = 4;
        operation["byteOrder"] = "little";
        operation["valueInterpretation"] = "unsigned";
        operation["addend"] = new JsonObject
        {
            ["kind"] = "region-instance-delta",
            ["sourceRegionInstanceId"] = "a-bank",
            ["targetRegionInstanceId"] = "b-bank",
        };
        operation["overflowPolicy"] = "reject";

        switch (mutation)
        {
            case "instance-delta":
                break;
            case "fixed-numeric":
                operation["addend"] = 262144;
                break;
            case "missing-selector-instance":
                _ = Assert.IsType<JsonObject>(sourceView["selector"]).Remove("regionInstanceId");
                break;
            case "missing-selector-region":
                _ = Assert.IsType<JsonObject>(sourceView["selector"]).Remove("templateRegionId");
                break;
            case "missing-delta-source":
                _ = Assert.IsType<JsonObject>(operation["addend"]).Remove("sourceRegionInstanceId");
                break;
            case "missing-delta-target":
                _ = Assert.IsType<JsonObject>(operation["addend"]).Remove("targetRegionInstanceId");
                break;
            case "unknown-delta-kind":
                Assert.IsType<JsonObject>(operation["addend"])["kind"] = "future";
                break;
            case "signed-value":
                operation["valueInterpretation"] = "signed";
                break;
            case "wrapping-overflow":
                operation["overflowPolicy"] = "wrap";
                break;
            case "v213":
                profile["schemaVersion"] = "2.13";
                break;
            default:
                throw new ArgumentOutOfRangeException(
                    nameof(mutation),
                    mutation,
                    "Unknown schema mutation.");
        }

        string schemaFileName = mutation == "v213"
            ? "composition-profile-v2.13.schema.json"
            : "composition-profile-v2.14.schema.json";
        ProfileBundleEntrySnapshotCollection collection =
            CaptureCompositionProfile(profile.ToJsonString(), schemaFileName);
        if (expectedValid)
        {
            ProfileBundleSchemaValidator.ValidateEntries(collection, 32);
            return;
        }

        _ = Assert.Throws<InvalidDataException>(() =>
            ProfileBundleSchemaValidator.ValidateEntries(collection, 32));
    }
}
