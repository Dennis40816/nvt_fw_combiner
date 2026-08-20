using System.Security.Cryptography;
using System.Text;
using System.Text.Json.Nodes;
using NvtFwCombiner.Infrastructure.ExternalTools;

namespace NvtFwCombiner.Infrastructure.Tests.ExternalTools;

/// <summary>Tests strict loading of the hash-pinned built-in CtrlRAM postbuild profile data.</summary>
public sealed class BuiltInPostbuildProfileCatalogTests
{
    private const string RelativePath = "profiles/built-in/ctrlram-postbuild-v2/catalog.json";

    /// <summary>The deployed hash-pinned catalog exposes only runtime-approved profile rows.</summary>
    [Fact]
    public void LoadReadsEveryRuntimeBuiltInProfile()
    {
        Assert.Equal(11, BuiltInPostbuildProfileCatalog.All.Count);
        Assert.DoesNotContain(
            BuiltInPostbuildProfileCatalog.All,
            profile => profile.IcId is "NT51920" or "NT51925" or "NT51930" or "NT51931");
    }

    /// <summary>A catalog byte change cannot pass under the release-pinned hash.</summary>
    [Fact]
    public void LoadRejectsHashDrift()
    {
        byte[] bytes = ReadCatalog();

        _ = Assert.Throws<InvalidDataException>(() =>
            BuiltInPostbuildProfileCatalog.Load(bytes, new string('0', 64)));
    }

    /// <summary>Repository LF bytes and a Windows CRLF worktree share one canonical release pin.</summary>
    [Fact]
    public void LoadAcceptsCanonicalLfHashFromCrlfCheckout()
    {
        string lfText = Encoding.UTF8.GetString(ReadCatalog()).Replace("\r\n", "\n", StringComparison.Ordinal);
        byte[] lfBytes = Encoding.UTF8.GetBytes(lfText);
        byte[] crlfBytes = Encoding.UTF8.GetBytes(lfText.Replace("\n", "\r\n", StringComparison.Ordinal));

        Assert.Equal(11, BuiltInPostbuildProfileCatalog.Load(crlfBytes, Hash(lfBytes)).Count);
    }

    /// <summary>Canonical repository bytes do not create document-sized hash-normalization buffers.</summary>
    [Fact]
    public void CanonicalAsciiLfHashDoesNotAllocateWithDocumentSize()
    {
        const int byteCount = 256 * 1024;
        byte[] bytes = new byte[byteCount];
        Array.Fill(bytes, (byte)' ');
        for (int index = 79; index < bytes.Length; index += 80)
        {
            bytes[index] = (byte)'\n';
        }

        string expected = Hash(bytes);
        _ = PinnedJsonCatalogLoader.ComputeCanonicalSha256(bytes);

        string actual = string.Empty;
        long allocated = long.MaxValue;
        for (int iteration = 0; iteration < 5; iteration++)
        {
            long allocatedBefore = GC.GetAllocatedBytesForCurrentThread();
            string candidate = PinnedJsonCatalogLoader.ComputeCanonicalSha256(bytes);
            long sample = GC.GetAllocatedBytesForCurrentThread() - allocatedBefore;
            if (sample < allocated)
            {
                actual = candidate;
                allocated = sample;
            }
        }

        TestContext.Current.TestOutputHelper?.WriteLine(
            $"PINNED_CATALOG_HASH bytes={byteCount} allocated={allocated}");
        Assert.Equal(expected, actual);
        Assert.InRange(allocated, 0, 1024);
    }

    /// <summary>Noncanonical and Unicode input retains the complete line-ending normalization contract.</summary>
    [Fact]
    public void CanonicalHashNormalizesEverySupportedLineEndingOutsideFastPath()
    {
        const string source = "é\r\nCR\rFF\fNEL\u0085LS\u2028PS\u2029end";
        byte[] bytes = Encoding.UTF8.GetBytes(source);
        byte[] canonicalBytes = Encoding.UTF8.GetBytes(source.ReplaceLineEndings("\n"));

        Assert.Equal(Hash(canonicalBytes), PinnedJsonCatalogLoader.ComputeCanonicalSha256(bytes));
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

    /// <summary>Config cannot invent an availability classification that bypasses runtime review.</summary>
    [Fact]
    public void LoadRejectsUnknownProfileAvailability()
    {
        JsonObject root = Assert.IsType<JsonObject>(JsonNode.Parse(ReadCatalog()));
        JsonObject profile = Assert.IsType<JsonObject>(root["profiles"]![0]);
        profile["availability"] = "unreviewed";
        byte[] bytes = Encoding.UTF8.GetBytes(root.ToJsonString());

        _ = Assert.Throws<InvalidDataException>(() =>
            BuiltInPostbuildProfileCatalog.Load(bytes, Hash(bytes)));
    }

    /// <summary>Every catalog row records an explicit effective version, including evidence-only rows.</summary>
    [Fact]
    public void LoadRejectsMissingEffectiveCommonFwVersion()
    {
        JsonObject root = Assert.IsType<JsonObject>(JsonNode.Parse(ReadCatalog()));
        JsonObject profile = Assert.IsType<JsonObject>(root["profiles"]![0]);
        Assert.True(profile.Remove("effectiveCommonFwVersion"));
        byte[] bytes = Encoding.UTF8.GetBytes(root.ToJsonString());

        _ = Assert.Throws<InvalidDataException>(() =>
            BuiltInPostbuildProfileCatalog.Load(bytes, Hash(bytes)));
    }

    /// <summary>Every catalog profile must declare its supported plan topology explicitly.</summary>
    [Fact]
    public void LoadRejectsMissingPlanSelectors()
    {
        JsonObject root = Assert.IsType<JsonObject>(JsonNode.Parse(ReadCatalog()));
        JsonObject profile = Assert.IsType<JsonObject>(root["profiles"]![0]);
        Assert.True(profile.Remove("planSelectors"));
        byte[] bytes = Encoding.UTF8.GetBytes(root.ToJsonString());

        _ = Assert.Throws<InvalidDataException>(() =>
            BuiltInPostbuildProfileCatalog.Load(bytes, Hash(bytes)));
    }

    /// <summary>Count-range and generic selectors cannot claim the same multi-chip topology.</summary>
    [Fact]
    public void LoadRejectsOverlappingPlanSelectors()
    {
        JsonObject root = Assert.IsType<JsonObject>(JsonNode.Parse(ReadCatalog()));
        JsonObject profile = FindProfile(root, "nfc.nt51932.ctrlram-postbuild-v1");
        JsonArray selectors = Assert.IsType<JsonArray>(profile["planSelectors"]);
        selectors.Add(new JsonObject
        {
            ["kind"] = "generic-cascade",
            ["branch"] = "cascade",
        });
        byte[] bytes = Encoding.UTF8.GetBytes(root.ToJsonString());

        _ = Assert.Throws<InvalidDataException>(() =>
            BuiltInPostbuildProfileCatalog.Load(bytes, Hash(bytes)));
    }

    /// <summary>A one-count interval must be modeled as an exact selector, not a degenerate range.</summary>
    [Fact]
    public void LoadRejectsDegenerateCountRange()
    {
        JsonObject root = Assert.IsType<JsonObject>(JsonNode.Parse(ReadCatalog()));
        JsonObject profile = FindProfile(root, "nfc.nt51932.ctrlram-postbuild-v1");
        JsonObject range = Assert.IsType<JsonObject>(profile["planSelectors"]![1]);
        range["maximumCount"] = 2;
        byte[] bytes = Encoding.UTF8.GetBytes(root.ToJsonString());

        _ = Assert.Throws<InvalidDataException>(() =>
            BuiltInPostbuildProfileCatalog.Load(bytes, Hash(bytes)));
    }

    /// <summary>Runtime intervals for one IC cannot share an effective lower bound.</summary>
    [Fact]
    public void LoadRejectsDuplicateRuntimeEffectiveVersion()
    {
        JsonObject root = Assert.IsType<JsonObject>(JsonNode.Parse(ReadCatalog()));
        FindProfile(root, "nfc.nt51926.ctrlram-postbuild-v1")["effectiveCommonFwVersion"] = "1.0.0";
        byte[] bytes = Encoding.UTF8.GetBytes(root.ToJsonString());

        _ = Assert.Throws<InvalidDataException>(() =>
            BuiltInPostbuildProfileCatalog.Load(bytes, Hash(bytes)));
    }

    /// <summary>The first runtime interval for every IC begins at the owner-defined 1.0.0 minimum.</summary>
    [Fact]
    public void LoadRejectsRuntimeIntervalsStartingAfterMinimum()
    {
        JsonObject root = Assert.IsType<JsonObject>(JsonNode.Parse(ReadCatalog()));
        FindProfile(root, "nfc.nt51926.ctrlram-postbuild-fw1.4.1")["effectiveCommonFwVersion"] = "1.0.1";
        byte[] bytes = Encoding.UTF8.GetBytes(root.ToJsonString());

        _ = Assert.Throws<InvalidDataException>(() =>
            BuiltInPostbuildProfileCatalog.Load(bytes, Hash(bytes)));
    }

    /// <summary>Every profile must distinguish a reviewed FWConfig write route from an omitted field.</summary>
    [Fact]
    public void LoadRejectsMissingFirmwareConfigWriteRoute()
    {
        JsonObject root = Assert.IsType<JsonObject>(JsonNode.Parse(ReadCatalog()));
        JsonObject profile = Assert.IsType<JsonObject>(root["profiles"]![0]);
        Assert.True(profile.Remove("firmwareConfigWriteRoute"));
        byte[] bytes = Encoding.UTF8.GetBytes(root.ToJsonString());

        _ = Assert.Throws<InvalidDataException>(() =>
            BuiltInPostbuildProfileCatalog.Load(bytes, Hash(bytes)));
    }

    /// <summary>An unknown FWConfig write route cannot become implicit version-authoring authority.</summary>
    [Fact]
    public void LoadRejectsUnknownFirmwareConfigWriteRoute()
    {
        JsonObject root = Assert.IsType<JsonObject>(JsonNode.Parse(ReadCatalog()));
        JsonObject profile = Assert.IsType<JsonObject>(root["profiles"]![0]);
        profile["firmwareConfigWriteRoute"] = "infer-from-ic";
        byte[] bytes = Encoding.UTF8.GetBytes(root.ToJsonString());

        _ = Assert.Throws<InvalidDataException>(() =>
            BuiltInPostbuildProfileCatalog.Load(bytes, Hash(bytes)));
    }

    private static byte[] ReadCatalog()
    {
        return File.ReadAllBytes(Path.Combine(AppContext.BaseDirectory, RelativePath.Replace('/', Path.DirectorySeparatorChar)));
    }

    private static JsonObject FindProfile(JsonObject root, string processorId)
    {
        return root["profiles"]!
            .AsArray()
            .Select(Assert.IsType<JsonObject>)
            .Single(profile => profile["processorId"]!.GetValue<string>() == processorId);
    }

    private static string Hash(ReadOnlySpan<byte> bytes)
    {
        return Convert.ToHexStringLower(SHA256.HashData(bytes));
    }
}
