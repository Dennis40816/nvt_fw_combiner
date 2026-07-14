using System.Text.Json.Nodes;
using NvtFwCombiner.Infrastructure.Bundles;

namespace NvtFwCombiner.Infrastructure.Tests.Bundles;

public sealed partial class ProfileBundleSchemaValidatorTests
{
    /// <summary>Verifies schema 2.5 admits canonical IC member identities for logical General Merge profiles.</summary>
    [Fact]
    public void ValidateEntriesAcceptsCanonicalLogicalOutputMemberForV25()
    {
        JsonObject profile = LogicalOutputProfile();
        profile["schemaVersion"] = "2.5";
        Assert.IsType<JsonObject>(profile["logicalOutputBinding"])["memberIds"] = new JsonArray("NT00001");

        ProfileBundleSchemaValidator.ValidateEntries(
            CaptureCompositionProfile(profile.ToJsonString(), "composition-profile-v2.5.schema.json"),
            32);
    }

    /// <summary>Verifies schema 2.5 rejects a generic identifier where a canonical IC identity is required.</summary>
    [Fact]
    public void ValidateEntriesRejectsGenericLogicalOutputMemberForV25()
    {
        JsonObject profile = LogicalOutputProfile();
        profile["schemaVersion"] = "2.5";

        _ = Assert.Throws<InvalidDataException>(() => ProfileBundleSchemaValidator.ValidateEntries(
            CaptureCompositionProfile(profile.ToJsonString(), "composition-profile-v2.5.schema.json"),
            32));
    }
}
