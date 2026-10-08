using System.Security.Cryptography;
using System.Text;
using System.Text.Json.Nodes;
using NvtFwCombiner.Application.FlashMaps;

namespace NvtFwCombiner.Infrastructure.Tests.FlashMaps;

/// <summary>Creates synthetic schema 1.0 maps without firmware or shipped map details.</summary>
internal static class TpFlashMapCatalogTestData
{
    /// <summary>Creates one made-up IC entry with an optional effective version.</summary>
    internal static JsonObject Profile(string icId = "TEST-IC-A", string? version = null)
    {
        var profile = new JsonObject
        {
            ["icId"] = icId,
            ["overviewSource"] = "synthetic",
            ["firmwareConfigPrimaryStart"] = 0,
            ["tpPrefixLength"] = 16,
            ["fullFlashCapacities"] = new JsonArray(32),
            ["baseShapeEvidence"] = "synthetic",
            ["evidence"] = "synthetic",
            ["regions"] = new JsonArray(new JsonObject
            {
                ["regionId"] = "test-region",
                ["displayName"] = "Synthetic region",
                ["kind"] = "other",
                ["start"] = 0,
                ["length"] = 8,
                ["visibility"] = "always",
            }),
        };
        if (version is not null)
        {
            profile["effectiveCommonFwVersion"] = version;
        }

        return profile;
    }

    /// <summary>Loads only synthesized entries through the production hash-checked loader.</summary>
    internal static IReadOnlyList<TpFlashMapProfile> Load(params JsonObject[] profiles)
    {
        var document = new JsonObject
        {
            ["schemaVersion"] = "1.0",
            ["profiles"] = new JsonArray(profiles.Select(profile => (JsonNode)profile).ToArray()),
        };
        byte[] bytes = Encoding.UTF8.GetBytes(document.ToJsonString());
        return BuiltInTpFlashMapCatalog.Load(bytes, Convert.ToHexStringLower(SHA256.HashData(bytes)));
    }
}
