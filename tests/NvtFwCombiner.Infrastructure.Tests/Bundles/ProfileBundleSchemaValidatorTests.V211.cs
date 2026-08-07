using System.Text;
using System.Text.Json.Nodes;
using NvtFwCombiner.Infrastructure.Bundles;
using NvtFwCombiner.TestSupport;

namespace NvtFwCombiner.Infrastructure.Tests.Bundles;

public sealed partial class ProfileBundleSchemaValidatorTests
{
    /// <summary>Verifies schema-owned TP Header and shared-fact shapes reject drift at the bundle gateway.</summary>
    [Theory]
    [InlineData("missing-structure-kind")]
    [InlineData("missing-tp-header")]
    [InlineData("unknown-structure-kind")]
    [InlineData("inline-definition-reference")]
    [InlineData("unknown-subject")]
    [InlineData("unknown-role")]
    [InlineData("unknown-stored-address-basis")]
    [InlineData("drifted-coverage-policy")]
    public void ValidateEntriesEnforcesSchemaOwnedFirmwareFamilyShape(string mutation)
    {
        JsonObject baseline = LoadFirmwareFamilyWithTpHeader();
        ProfileBundleSchemaValidator.ValidateEntries(
            CaptureFirmwareFamilyV11TpHeader(baseline.ToJsonString()),
            32);

        JsonObject family = Assert.IsType<JsonObject>(baseline.DeepClone());
        JsonObject structure = FirstMetadataStructure(family);
        JsonObject semantics = Assert.IsType<JsonObject>(
            Assert.IsType<JsonArray>(
                Assert.IsType<JsonObject>(structure["tpFlashHeader"])["fieldSemantics"])[0]);
        switch (mutation)
        {
            case "missing-structure-kind":
                _ = structure.Remove("structureKind");
                break;
            case "missing-tp-header":
                _ = structure.Remove("tpFlashHeader");
                break;
            case "unknown-structure-kind":
                structure["structureKind"] = "future";
                break;
            case "inline-definition-reference":
                structure["definitionReference"] = new JsonObject
                {
                    ["familyId"] = "synthetic-family",
                    ["familyVersion"] = "1.0.0",
                    ["familyContentHash"] = new string('a', 64),
                    ["structureId"] = "synthetic-structure",
                };
                break;
            case "unknown-subject":
                semantics["subject"] = "future";
                break;
            case "unknown-role":
                semantics["role"] = "future";
                break;
            case "unknown-stored-address-basis":
                Assert.IsType<JsonObject>(
                    FindSemantics(
                        Assert.IsType<JsonArray>(
                            Assert.IsType<JsonObject>(structure["tpFlashHeader"])["fieldSemantics"]),
                        "ilm-destination-address-in-sram")["storedAddress"])["basis"] = "future";
                break;
            case "drifted-coverage-policy":
                Assert.IsType<JsonObject>(Assert.IsType<JsonArray>(family["imageMaps"])[0])[
                    "coveragePolicy"] = "future";
                break;
            default:
                throw new ArgumentOutOfRangeException(
                    nameof(mutation),
                    mutation,
                    "Unknown firmware-family schema mutation.");
        }

        _ = Assert.Throws<InvalidDataException>(() =>
            ProfileBundleSchemaValidator.ValidateEntries(
                CaptureFirmwareFamilyV11TpHeader(family.ToJsonString()),
                32));
    }

    /// <summary>Verifies the relationship schema owns nonblank and unique shared-fact identifiers.</summary>
    [Theory]
    [InlineData("blank-shared-fact-id")]
    [InlineData("duplicate-shared-fact")]
    public void ValidateEntriesEnforcesSchemaOwnedSharedFactIdentifiers(string mutation)
    {
        JsonObject baseline = LoadFirmwareFamilyWithRelationships();
        ProfileBundleSchemaValidator.ValidateEntries(
            CaptureFirmwareFamily(
                baseline.ToJsonString(),
                "firmware-family-v1.2-tp-header-subjects.schema.json"),
            32);

        JsonObject family = Assert.IsType<JsonObject>(baseline.DeepClone());
        JsonObject relationship = Assert.IsType<JsonObject>(
            Assert.IsType<JsonArray>(family["familyRelationships"])[1]);
        JsonArray sharedFacts = Assert.IsType<JsonArray>(relationship["sharedFactReferences"]);
        switch (mutation)
        {
            case "blank-shared-fact-id":
                Assert.IsType<JsonObject>(sharedFacts[0])["factId"] = string.Empty;
                break;
            case "duplicate-shared-fact":
                sharedFacts.Add(sharedFacts[0]!.DeepClone());
                break;
            default:
                throw new ArgumentOutOfRangeException(
                    nameof(mutation),
                    mutation,
                    "Unknown shared-fact schema mutation.");
        }

        _ = Assert.Throws<InvalidDataException>(() =>
            ProfileBundleSchemaValidator.ValidateEntries(
                CaptureFirmwareFamily(
                    family.ToJsonString(),
                    "firmware-family-v1.2-tp-header-subjects.schema.json"),
                32));
    }

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

    private static JsonObject LoadFirmwareFamilyWithTpHeader()
    {
        return Assert.IsType<JsonObject>(JsonNode.Parse(File.ReadAllText(
            RepositoryPaths.FromRepositoryRoot(
                "profiles",
                "built-in",
                "nt51929-standard-merge",
                "families",
                "nt51929-nt51932.json"))));
    }

    private static JsonObject LoadFirmwareFamilyWithRelationships()
    {
        return Assert.IsType<JsonObject>(JsonNode.Parse(File.ReadAllText(
            RepositoryPaths.FromRepositoryRoot(
                "profiles",
                "built-in",
                "nt51927-standard-merge",
                "families",
                "nt51927-nt51928.json"))));
    }

    private static JsonObject FirstMetadataStructure(JsonObject family)
    {
        return Assert.IsType<JsonObject>(
            Assert.IsType<JsonArray>(
                Assert.IsType<JsonObject>(
                    Assert.IsType<JsonArray>(family["metadataSets"])[0])[
                        "structures"])[0]);
    }

    private static ProfileBundleEntrySnapshotCollection CaptureFirmwareFamilyV11TpHeader(
        string family)
    {
        return CaptureFirmwareFamily(family, "firmware-family-v1.1-tp-header.schema.json");
    }

    private static ProfileBundleEntrySnapshotCollection CaptureFirmwareFamily(
        string family,
        string schemaFileName)
    {
        const string schemaId =
            "https://example.invalid/nfc/schemas/firmware-family-v1.schema.json";
        using var workspace = TempWorkspace.Create(
            "nfc-firmware-family-v11-tp-header-schema-validation");
        byte[] schemaBytes = File.ReadAllBytes(RepositoryPaths.FromRepositoryRoot(
            "docs",
            "contracts",
            schemaFileName));
        byte[] familyBytes = Encoding.UTF8.GetBytes(family);
        _ = workspace.Write("profile-bundle.json", Encoding.UTF8.GetBytes("{}"));
        _ = workspace.Write(
            $"schemas/{schemaFileName}",
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
