using System.Security.Cryptography;
using System.Text;
using System.Text.Json.Nodes;
using NvtFwCombiner.Infrastructure.ExternalTools;

namespace NvtFwCombiner.Infrastructure.Tests.ExternalTools;

/// <summary>Tests strict loading of the hash-pinned built-in CtrlRAM postbuild profile data.</summary>
public sealed class BuiltInPostbuildProfileCatalogTests
{
    private const string RelativePath = "profiles/built-in/ctrlram-postbuild-v2/catalog.json";

    /// <summary>The deployed hash-pinned catalog constructs all reviewed profile rows.</summary>
    [Fact]
    public void LoadReadsEveryBuiltInProfile()
    {
        Assert.Equal(15, BuiltInPostbuildProfileCatalog.All.Count);
    }

    /// <summary>A catalog byte change cannot pass under the release-pinned hash.</summary>
    [Fact]
    public void LoadRejectsHashDrift()
    {
        byte[] bytes = ReadCatalog();

        _ = Assert.Throws<InvalidDataException>(() =>
            BuiltInPostbuildProfileCatalog.Load(bytes, new string('0', 64)));
    }

    /// <summary>Unknown config fields fail closed after their exact bytes are explicitly trusted.</summary>
    [Fact]
    public void LoadRejectsUnknownFields()
    {
        JsonObject root = Assert.IsType<JsonObject>(JsonNode.Parse(ReadCatalog()));
        root["unexpected"] = true;
        byte[] bytes = Encoding.UTF8.GetBytes(root.ToJsonString());

        _ = Assert.Throws<InvalidDataException>(() =>
            BuiltInPostbuildProfileCatalog.Load(bytes, Hash(bytes)));
    }

    /// <summary>Config cannot introduce a syntactically valid but unreviewed Combiner mode.</summary>
    [Fact]
    public void LoadRejectsUnreviewedCombinerArguments()
    {
        JsonObject root = Assert.IsType<JsonObject>(JsonNode.Parse(ReadCatalog()));
        JsonObject command = Assert.IsType<JsonObject>(root["profiles"]![0]!["singleCommands"]![0]);
        command["modeArgument"] = "UNREVIEWED_MODE";
        byte[] bytes = Encoding.UTF8.GetBytes(root.ToJsonString());

        _ = Assert.Throws<InvalidDataException>(() =>
            BuiltInPostbuildProfileCatalog.Load(bytes, Hash(bytes)));
    }

    private static byte[] ReadCatalog()
    {
        return File.ReadAllBytes(Path.Combine(AppContext.BaseDirectory, RelativePath.Replace('/', Path.DirectorySeparatorChar)));
    }

    private static string Hash(ReadOnlySpan<byte> bytes)
    {
        return Convert.ToHexStringLower(SHA256.HashData(bytes));
    }
}
