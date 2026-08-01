using System.Text;
using System.Text.Json.Nodes;
using NvtFwCombiner.Infrastructure.Bundles;
using NvtFwCombiner.TestSupport;

namespace NvtFwCombiner.Infrastructure.Tests.Bundles;

public sealed partial class ProfileBundleSchemaValidatorTests
{
    /// <summary>Verifies the TP Header successor schema keeps stored-address meaning closed and role-specific.</summary>
    [Theory]
    [InlineData("valid", true)]
    [InlineData("missing-address-basis", false)]
    [InlineData("basis-on-size", false)]
    [InlineData("destination-relative", false)]
    [InlineData("tp-start-absolute", false)]
    public void ValidateEntriesEnforcesTpHeaderStoredAddressSemantics(
        string mutation,
        bool expectedValid)
    {
        JsonObject family = Assert.IsType<JsonObject>(JsonNode.Parse(File.ReadAllText(
            RepositoryPaths.FromRepositoryRoot(
                "profiles",
                "built-in",
                "nt51929-standard-merge",
                "families",
                "nt51929-nt51932.json"))));
        JsonObject structure = Assert.IsType<JsonObject>(
            Assert.IsType<JsonArray>(
                Assert.IsType<JsonObject>(
                    Assert.IsType<JsonArray>(family["metadataSets"])[0])["structures"])[0]);
        JsonArray semantics = Assert.IsType<JsonArray>(
            Assert.IsType<JsonObject>(structure["tpFlashHeader"])["fieldSemantics"]);
        JsonObject destination = FindSemantics(semantics, "ilm-destination-address-in-sram");
        JsonObject size = FindSemantics(semantics, "ilm-size");
        JsonObject tpStart = FindSemantics(semantics, "ilm-start-address-in-bin");

        switch (mutation)
        {
            case "valid":
                break;
            case "missing-address-basis":
                _ = destination.Remove("storedAddress");
                break;
            case "basis-on-size":
                size["storedAddress"] = new JsonObject
                {
                    ["addressSpaceId"] = "sram",
                    ["basis"] = "absolute",
                };
                break;
            case "destination-relative":
                Assert.IsType<JsonObject>(destination["storedAddress"])["basis"] = "tp-bin-offset";
                break;
            case "tp-start-absolute":
                Assert.IsType<JsonObject>(tpStart["storedAddress"])["basis"] = "absolute";
                break;
            default:
                throw new ArgumentOutOfRangeException(
                    nameof(mutation),
                    mutation,
                    "Unknown TP Header schema mutation.");
        }

        ProfileBundleEntrySnapshotCollection collection =
            CaptureFirmwareFamilyV11TpHeader(family.ToJsonString());
        if (expectedValid)
        {
            ProfileBundleSchemaValidator.ValidateEntries(collection, 32);
            return;
        }

        _ = Assert.Throws<InvalidDataException>(() =>
            ProfileBundleSchemaValidator.ValidateEntries(collection, 32));
    }

    private static JsonObject FindSemantics(JsonArray semantics, string fieldId)
    {
        return Assert.IsType<JsonObject>(Assert.Single(semantics, node =>
            string.Equals(
                Assert.IsType<JsonObject>(node)["fieldId"]?.GetValue<string>(),
                fieldId,
                StringComparison.Ordinal)));
    }

    private static ProfileBundleEntrySnapshotCollection CaptureFirmwareFamilyV11TpHeader(
        string family)
    {
        const string schemaId =
            "https://example.invalid/nfc/schemas/firmware-family-v1.schema.json";
        using var workspace = TempWorkspace.Create(
            "nfc-firmware-family-v11-tp-header-schema-validation");
        byte[] schemaBytes = File.ReadAllBytes(RepositoryPaths.FromRepositoryRoot(
            "docs",
            "contracts",
            "firmware-family-v1.1-tp-header.schema.json"));
        byte[] familyBytes = Encoding.UTF8.GetBytes(family);
        _ = workspace.Write("profile-bundle.json", Encoding.UTF8.GetBytes("{}"));
        _ = workspace.Write(
            "schemas/firmware-family-v1.1-tp-header.schema.json",
            schemaBytes);
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
                        "schemas/firmware-family-v1.1-tp-header.schema.json",
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
