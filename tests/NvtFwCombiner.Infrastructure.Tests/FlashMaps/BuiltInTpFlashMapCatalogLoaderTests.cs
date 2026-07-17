using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using NvtFwCombiner.TestSupport;

namespace NvtFwCombiner.Infrastructure.Tests.FlashMaps;

/// <summary>Tests strict loading and TP/full-Flash shape facts for the built-in TP flash map.</summary>
public sealed class BuiltInTpFlashMapCatalogLoaderTests
{
    private const string RelativePath = "profiles/built-in/ctrlram-postbuild-v2/flash-map.json";

    /// <summary>Every selectable Postbuild IC has exactly one hash-pinned map profile.</summary>
    [Fact]
    public void LoadReadsEveryBuiltInProfile()
    {
        Assert.Equal(13, BuiltInTpFlashMapCatalog.IcIds.Count);
        Assert.Equal(BuiltInTpFlashMapCatalog.IcIds.Count, BuiltInTpFlashMapCatalog.IcIds.Distinct().Count());
    }

    /// <summary>Declared TP prefixes and full-Flash containers remain separate config facts.</summary>
    [Theory]
    [InlineData("NT51917", 0x35000, 0x40000)]
    [InlineData("NT51919", 0x40000, 0x40000)]
    [InlineData("NT51920", 0x30000, 0x40000)]
    [InlineData("NT51923", 0x3C000, 0x40000)]
    [InlineData("NT51926", 0x3C000, 0x40000)]
    [InlineData("NT51927", 0x35000, 0x40000)]
    [InlineData("NT51928", 0x35000, 0x80000)]
    [InlineData("NT51929", 0x40000, 0x40000)]
    [InlineData("NT51930", 0x40000, 0x40000)]
    [InlineData("NT51931", 0x3C000, 0x40000)]
    [InlineData("NT51932", 0x40000, 0x40000)]
    [InlineData("NT51950", 0x37000, 0x40000)]
    [InlineData("NT51951", 0x37000, 0x80000)]
    public void ProfilesDeclareTpAndFullFlashShapes(string icId, long tpPrefix, long evidencedFlashCapacity)
    {
        Assert.True(BuiltInTpFlashMapCatalog.TryFind(icId, out Application.FlashMaps.TpFlashMapProfile? profile));
        Assert.Equal(tpPrefix, profile!.TpPrefixLength);
        Assert.Contains(evidencedFlashCapacity, profile.FullFlashCapacities);
        Assert.False(string.IsNullOrWhiteSpace(profile.BaseShapeEvidence));
    }

    /// <summary>Every direct owner-approved Standard Merge fixture shape must be declared by config.</summary>
    [Fact]
    public void DirectGoldenFixtureShapesMatchConfig()
    {
        string manifestPath = RepositoryPaths.FromRepositoryRoot(
            "testdata",
            "golden",
            "standard-merge-gen-flash",
            "manifest.json");
        using var manifest = JsonDocument.Parse(File.ReadAllText(manifestPath));

        foreach (JsonElement fixtureCase in manifest.RootElement.GetProperty("cases").EnumerateArray())
        {
            string icId = $"NT{fixtureCase.GetProperty("ic").GetString()}";
            long tpLength = fixtureCase.GetProperty("inputs").GetProperty("tp-input").GetProperty("size").GetInt64();
            long flashLength = fixtureCase.GetProperty("expectedOutput").GetProperty("size").GetInt64();

            Assert.True(BuiltInTpFlashMapCatalog.TryFind(icId, out Application.FlashMaps.TpFlashMapProfile? profile));
            Assert.Equal(tpLength, profile!.TpPrefixLength);
            Assert.Contains(flashLength, profile.FullFlashCapacities);
        }
    }

    /// <summary>A map byte change cannot pass under the release-pinned hash.</summary>
    [Fact]
    public void LoadRejectsHashDrift()
    {
        byte[] bytes = ReadCatalog();

        _ = Assert.Throws<InvalidDataException>(() =>
            BuiltInTpFlashMapCatalog.Load(bytes, new string('0', 64)));
    }

    /// <summary>Unknown config fields fail closed after their exact bytes are explicitly trusted.</summary>
    [Fact]
    public void LoadRejectsUnknownFields()
    {
        JsonObject root = Assert.IsType<JsonObject>(JsonNode.Parse(ReadCatalog()));
        root["unexpected"] = true;
        byte[] bytes = Encoding.UTF8.GetBytes(root.ToJsonString());

        _ = Assert.Throws<InvalidDataException>(() => BuiltInTpFlashMapCatalog.Load(bytes, Hash(bytes)));
    }

    /// <summary>A full-Flash capacity shorter than the TP prefix is not a valid container declaration.</summary>
    [Fact]
    public void LoadRejectsContainerShorterThanTpPrefix()
    {
        JsonObject root = Assert.IsType<JsonObject>(JsonNode.Parse(ReadCatalog()));
        JsonObject profile = Assert.IsType<JsonObject>(root["profiles"]![0]);
        profile["fullFlashCapacities"] = new JsonArray(1);
        byte[] bytes = Encoding.UTF8.GetBytes(root.ToJsonString());

        _ = Assert.Throws<ArgumentException>(() => BuiltInTpFlashMapCatalog.Load(bytes, Hash(bytes)));
    }

    private static byte[] ReadCatalog()
    {
        return File.ReadAllBytes(Path.Combine(
            AppContext.BaseDirectory,
            RelativePath.Replace('/', Path.DirectorySeparatorChar)));
    }

    private static string Hash(ReadOnlySpan<byte> bytes)
    {
        return Convert.ToHexStringLower(SHA256.HashData(bytes));
    }
}
