using System.Text;
using System.Text.Json.Nodes;
using NvtFwCombiner.Infrastructure.Bundles;
using NvtFwCombiner.TestSupport;

namespace NvtFwCombiner.Infrastructure.Tests.Bundles;

public sealed partial class ProfileBundleSchemaValidatorTests
{
    /// <summary>Verifies schema 1.2 admits only paired closed region templates and instances.</summary>
    [Theory]
    [InlineData("valid", true)]
    [InlineData("missing-templates", false)]
    [InlineData("missing-instances", false)]
    [InlineData("empty-bindings", false)]
    [InlineData("unknown-instance-field", false)]
    [InlineData("v11", false)]
    public void ValidateEntriesEnforcesPairedRegionInstancesForFamilyV12(
        string mutation,
        bool expectedValid)
    {
        JsonObject family = RegionInstanceFamily();
        switch (mutation)
        {
            case "valid":
                break;
            case "missing-templates":
                _ = RegionSet(family).Remove("regionTemplates");
                break;
            case "missing-instances":
                _ = RegionSet(family).Remove("regionInstances");
                break;
            case "empty-bindings":
                Assert.IsType<JsonObject>(Assert.IsType<JsonArray>(
                    RegionSet(family)["regionInstances"])[0])["resolvedRegionIds"] = new JsonArray();
                break;
            case "unknown-instance-field":
                Assert.IsType<JsonObject>(Assert.IsType<JsonArray>(
                    RegionSet(family)["regionInstances"])[0])["offset"] = 0;
                break;
            case "v11":
                family["schemaVersion"] = "1.1";
                break;
            default:
                throw new ArgumentOutOfRangeException(
                    nameof(mutation),
                    mutation,
                    "Unknown schema mutation.");
        }

        string schemaFileName = mutation == "v11"
            ? "firmware-family-v1.1-tp-header.schema.json"
            : "firmware-family-v1.2-bank-instances.schema.json";
        ProfileBundleEntrySnapshotCollection collection =
            CaptureFirmwareFamilyBankInstances(family.ToJsonString(), schemaFileName);
        if (expectedValid)
        {
            ProfileBundleSchemaValidator.ValidateEntries(collection, 32);
            return;
        }

        _ = Assert.Throws<InvalidDataException>(() =>
            ProfileBundleSchemaValidator.ValidateEntries(collection, 32));
    }

    private static JsonObject RegionInstanceFamily()
    {
        JsonObject family = Assert.IsType<JsonObject>(JsonNode.Parse(
            TrustedV2BundleTestDocuments.FamilyJson()));
        family["schemaVersion"] = "1.2";
        JsonObject regionSet = RegionSet(family);
        JsonObject root = Assert.IsType<JsonObject>(Assert.IsType<JsonArray>(regionSet["regions"])[0]);
        root["range"] = new JsonObject { ["start"] = 0, ["length"] = 32 };
        regionSet["regionTemplates"] = new JsonArray
        {
            new JsonObject
            {
                ["templateId"] = "ab-bank",
                ["capacityBytes"] = 16,
                ["regions"] = new JsonArray
                {
                    new JsonObject
                    {
                        ["regionId"] = "bank",
                        ["owner"] = "system",
                        ["kind"] = "image",
                        ["range"] = new JsonObject { ["start"] = 0, ["length"] = 16 },
                        ["writeConstraint"] = "explicit-range",
                        ["alignment"] = 1,
                    },
                },
            },
        };
        regionSet["regionInstances"] = new JsonArray
        {
            RegionInstance("a-bank", 0),
            RegionInstance("b-bank", 16),
        };
        JsonObject applicability = Assert.IsType<JsonObject>(Assert.IsType<JsonObject>(
            Assert.IsType<JsonArray>(family["imageMaps"])[0])["applicability"]);
        applicability["capacityBytes"] = 32;
        return family;
    }

    private static JsonObject RegionSet(JsonObject family)
    {
        return Assert.IsType<JsonObject>(Assert.IsType<JsonArray>(family["regionSets"])[0]);
    }

    private static JsonObject RegionInstance(string instanceId, int baseOffset)
    {
        return new JsonObject
        {
            ["instanceId"] = instanceId,
            ["templateId"] = "ab-bank",
            ["baseOffset"] = baseOffset,
            ["parentRegionId"] = "root",
            ["resolvedRegionIds"] = new JsonArray
            {
                new JsonObject
                {
                    ["templateRegionId"] = "bank",
                    ["resolvedRegionId"] = instanceId,
                },
            },
        };
    }

    private static ProfileBundleEntrySnapshotCollection CaptureFirmwareFamilyBankInstances(
        string family,
        string schemaFileName)
    {
        const string schemaId =
            "https://example.invalid/nfc/schemas/firmware-family-v1.schema.json";
        using var workspace = TempWorkspace.Create("nfc-firmware-family-v12-bank-instance-schema-validation");
        byte[] schemaBytes = File.ReadAllBytes(RepositoryPaths.FromRepositoryRoot(
            "docs",
            "contracts",
            schemaFileName));
        byte[] familyBytes = Encoding.UTF8.GetBytes(family);
        _ = workspace.Write("profile-bundle.json", Encoding.UTF8.GetBytes("{}"));
        _ = workspace.Write($"schemas/{schemaFileName}", schemaBytes);
        _ = workspace.Write("families/family.json", familyBytes);

        return ProfileBundleEntrySnapshotCollection.Capture(
            workspace.Root,
            "profile-bundle.json",
            new ProfileBundleManifest(
                "bundle",
                "1.0.0",
                new string('a', 64),
                "release-manifest",
                [
                    Entry(
                        "schema",
                        ProfileBundleEntryKind.Schema,
                        $"schemas/{schemaFileName}",
                        schemaBytes,
                        schemaId),
                    Entry(
                        "family",
                        ProfileBundleEntryKind.FirmwareFamily,
                        "families/family.json",
                        familyBytes,
                        schemaId),
                ]),
            new ProfileBundleEntrySnapshotLimits(8, 65536, 131072, 32));
    }
}
