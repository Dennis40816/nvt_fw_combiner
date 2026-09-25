using System.Text.Json.Nodes;
using NvtFwCombiner.Infrastructure.Bundles;
using NvtFwCombiner.TestSupport;

namespace NvtFwCombiner.Infrastructure.Tests.Bundles;

public sealed partial class ProfileBundleSchemaValidatorTests
{
    /// <summary>Closed AB declaration accepts both booleans, and cannot hide invalid or unrelated values.</summary>
    [Theory]
    [InlineData("true", true)]
    [InlineData("false", true)]
    [InlineData("absent", true)]
    [InlineData("missing", false)]
    [InlineData("null", false)]
    [InlineData("wrong-type", false)]
    [InlineData("unknown", false)]
    [InlineData("non-ab", false)]
    public void ValidateEntriesEnforcesAbCharacteristicsV217(string mutation, bool valid)
    {
        JsonObject profile = Assert.IsType<JsonObject>(JsonNode.Parse(File.ReadAllText(
            RepositoryPaths.FromRepositoryRoot("profiles", "built-in",
                "nt51919-nt51929-nt51932-ab-merge", "profiles", "nt51929-ab-merge.json"))));
        profile["ab"] = mutation switch
        {
            "missing" => JsonNode.Parse("{}"),
            "null" => null,
            "wrong-type" => new JsonObject { ["isFullySymmetric"] = "true" },
            "unknown" => new JsonObject { ["isFullySymmetric"] = true, ["supportsAb"] = true },
            _ => new JsonObject { ["isFullySymmetric"] = mutation != "false" },
        };
        if (mutation == "absent") { _ = profile.Remove("ab"); }
        if (mutation == "non-ab") { profile["experience"]!["experienceId"] = "standard-merge"; }
        ProfileBundleEntrySnapshotCollection entries = CaptureCompositionProfile(profile.ToJsonString(), "composition-profile-v2.17.schema.json");
        if (valid) { ProfileBundleSchemaValidator.ValidateEntries(entries, 32); }
        else { _ = Assert.Throws<InvalidDataException>(() => ProfileBundleSchemaValidator.ValidateEntries(entries, 32)); }
    }
}
