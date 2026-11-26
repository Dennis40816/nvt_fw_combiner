using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Nodes;
using NvtFwCombiner.Infrastructure.Bundles;

namespace NvtFwCombiner.Bootstrap.Tests;

/// <summary>ADR 0077 decision-5 rows execute the same deployed host with independently damaged copies.</summary>
public sealed class PrebuiltProfileCatalogRejectionTests
{
    private const string Target = "nt51927-standard-merge";
    private static readonly string[] PackStates = ["intact", "absent", "corrupt"];
    private static readonly string[] InvalidIndexStates = ["missing", "malformed", "schema"];
    private static readonly string[] DiskStates = ["intact", "document", "identity"];

    /// <summary>M1 requires actual selected bytes and an unchanged complete publication.</summary>
    [Fact]
    public async Task M1MatchingInputsUsePrebuilt()
    {
        using var copy = new CatalogProbeCopy();
        JsonNode result = await copy.RunAsync();
        AssertSource(result, "prebuilt", 0);
        PrebuiltProfileCatalogEquivalenceTests.AssertPinned(result);
        Assert.Equal(26, result["bundles"]!.AsArray().Count);
        Assert.All(result["bundles"]!.AsArray(), b => Assert.Null(b!["error"]));
        Assert.Equal(0, result["entryValidationCalls"]!.GetValue<long>());
    }

    /// <summary>M2 ignores every rejected disk-damage class after accepting the build-bound pack.</summary>
    [Theory]
    [InlineData("missing")]
    [InlineData("document")]
    [InlineData("entries")]
    [InlineData("schema")]
    [InlineData("missing-schema")]
    [InlineData("extra")]
    [InlineData("anchor")]
    [InlineData("version")]
    [InlineData("manifest")]
    public async Task M2AcceptedPackIgnoresRejectedDiskDamage(string damage)
    {
        using var copy = new CatalogProbeCopy();
        string original = File.ReadAllText(ManifestPath(copy));
        Damage(copy, damage);
        JsonNode result = await copy.RunAsync();
        AssertSource(result, "prebuilt", 0);
        PrebuiltProfileCatalogEquivalenceTests.AssertPinned(result);
        Assert.Equal(original, System.Text.Encoding.UTF8.GetString(Convert.FromBase64String(TargetBundle(result)["manifest"]!.GetValue<string>())));
    }

    /// <summary>M3 retains build identity when disk bundleId remains admissible to the directory loader.</summary>
    [Fact]
    public async Task M3AcceptedPackKeepsBuildManifestIdentity()
    {
        using var copy = new CatalogProbeCopy();
        string original = JsonNode.Parse(File.ReadAllText(ManifestPath(copy)))!["bundleId"]!.GetValue<string>();
        Damage(copy, "identity");
        JsonNode result = await copy.RunAsync();
        AssertSource(result, "prebuilt", 0);
        Assert.Equal(original, TargetBundle(result)["bundleId"]!.GetValue<string>());
        PrebuiltProfileCatalogEquivalenceTests.AssertPinned(result);
    }

    /// <summary>M4 fallback emits exactly one warning without changing complete published state.</summary>
    [Theory]
    [InlineData("absent")]
    [InlineData("corrupt")]
    public async Task M4UnusedPackFallsBackWithoutPublicationDrift(string pack)
    {
        using var copy = new CatalogProbeCopy();
        SetPack(copy, pack);
        JsonNode result = await copy.RunAsync();
        AssertSource(result, "json", 1);
        Assert.Equal(pack == "absent" ? "missing" : "format", result["reason"]!.GetValue<string>());
        PrebuiltProfileCatalogEquivalenceTests.AssertPinned(result);
    }

    /// <summary>M5 both rejection directions preserve directory admission failures and publication timing.</summary>
    [Theory]
    [InlineData("missing")]
    [InlineData("document")]
    [InlineData("entries")]
    [InlineData("schema")]
    [InlineData("missing-schema")]
    [InlineData("extra")]
    [InlineData("anchor")]
    [InlineData("version")]
    [InlineData("manifest")]
    public async Task M5JsonFallbackPreservesRejectedDamageFailures(string damage)
    {
        using var absent = new CatalogProbeCopy();
        using var corrupt = new CatalogProbeCopy();
        SetPack(absent, "absent");
        SetPack(corrupt, "corrupt");
        Damage(absent, damage);
        Damage(corrupt, damage);
        JsonNode reference = await absent.RunAsync();
        JsonNode rejected = await corrupt.RunAsync();
        AssertSource(reference, "json", 1);
        AssertSource(rejected, "json", 1);
        Assert.False(reference["loaded"]!.GetValue<bool>());
        Assert.False(rejected["loaded"]!.GetValue<bool>());
        Assert.False(reference["hasPublishedSnapshot"]!.GetValue<bool>());
        Assert.False(rejected["hasPublishedSnapshot"]!.GetValue<bool>());
        Assert.True(JsonNode.DeepEquals(reference["publicationException"], rejected["publicationException"]));
        Assert.NotNull(TargetBundle(reference)["error"]);
        Assert.True(JsonNode.DeepEquals(reference["issues"], rejected["issues"]));
        Assert.Equal(TargetBundle(reference)["error"]!.GetValue<string>(), TargetBundle(rejected)["error"]!.GetValue<string>());
    }

    /// <summary>M6 both missing and rejected packs admit the edited identity exactly as directory JSON does.</summary>
    [Theory]
    [InlineData("absent")]
    [InlineData("corrupt")]
    public async Task M6JsonFallbackAdmitsEditedManifestIdentity(string pack)
    {
        using var copy = new CatalogProbeCopy();
        SetPack(copy, pack);
        Damage(copy, "identity");
        JsonNode result = await copy.RunAsync();
        AssertSource(result, "json", 1);
        Assert.Equal("edited-bundle-identity", TargetBundle(result)["bundleId"]!.GetValue<string>());
        Assert.Null(TargetBundle(result)["error"]);
        using var reference = new CatalogProbeCopy();
        SetPack(reference, "absent");
        Damage(reference, "identity");
        JsonNode expected = await reference.RunAsync();
        Assert.Equal(expected["loaded"]!.GetValue<bool>(), result["loaded"]!.GetValue<bool>());
        Assert.True(JsonNode.DeepEquals(expected["publication"], result["publication"]));
        Assert.True(JsonNode.DeepEquals(expected["issues"], result["issues"]));
    }

    /// <summary>M3/M6 also bind every affected Saved Rule parent to the selected manifest identity.</summary>
    [Theory]
    [InlineData("intact")]
    [InlineData("absent")]
    [InlineData("corrupt")]
    public async Task M3M6SavedRuleParentUsesSelectedManifestIdentity(string pack)
    {
        using var copy = new CatalogProbeCopy();
        string path = Path.Combine(copy.Root, "profiles", "built-in", "nt51923-nt51926-general-merge-logical-candidate", "profile-bundle.json");
        JsonNode manifest = JsonNode.Parse(File.ReadAllText(path))!;
        string original = manifest["bundleId"]!.GetValue<string>();
        manifest["bundleId"] = "edited-bundle-identity";
        File.WriteAllText(path, manifest.ToJsonString());
        SetPack(copy, pack);
        JsonNode result = await copy.RunAsync();
        AssertSource(result, pack == "intact" ? "prebuilt" : "json", pack == "intact" ? 0 : 1);
        JsonNode[] parents = [.. result["parents"]!.AsArray().Where(p =>
            p!["ProfileId"]!.GetValue<string>() is "nt51923-general-merge-logical-candidate" or "nt51926-general-merge-logical-candidate").Select(p => p!)];
        Assert.Equal(2, parents.Length);
        Assert.All(parents, p => Assert.Equal(pack == "intact" ? original : "edited-bundle-identity", p["BundleId"]!.GetValue<string>()));
    }

    /// <summary>M2/M5 distinguish unopened General Merge damage from a required cold-publication failure.</summary>
    [Theory]
    [InlineData("intact")]
    [InlineData("absent")]
    [InlineData("corrupt")]
    public async Task M2M5OnDemandDamageFailsOnlyWhenJsonBundleIsOpened(string pack)
    {
        using var copy = new CatalogProbeCopy();
        const string directory = "nt51923-nt51926-general-merge-logical-candidate";
        File.WriteAllText(Path.Combine(copy.Root, "profiles", "built-in", directory, "profile-bundle.json"), "{}");
        SetPack(copy, pack);
        JsonNode result = await copy.RunAsync();
        AssertSource(result, pack == "intact" ? "prebuilt" : "json", pack == "intact" ? 0 : 1);
        PrebuiltProfileCatalogEquivalenceTests.AssertPinned(result);
        JsonNode bundle = result["bundles"]!.AsArray().Single(b => b!["directory"]!.GetValue<string>() == directory)!;
        if (pack == "intact") { Assert.Null(bundle["error"]); }
        else { Assert.NotNull(bundle["error"]); }
    }

    /// <summary>M7 exact-byte and valid semantic index changes never permit the build pack.</summary>
    [Theory]
    [InlineData("intact", "whitespace", "intact")]
    [InlineData("intact", "version", "intact")]
    [InlineData("absent", "version", "document")]
    [InlineData("corrupt", "version", "identity")]
    [InlineData("intact", "version", "document")]
    [InlineData("intact", "version", "identity")]
    [InlineData("rebound", "version", "intact")]
    public async Task M7DifferentValidIndexNeverUsesPack(string pack, string index, string damage)
    {
        using var copy = new CatalogProbeCopy();
        SetPack(copy, pack);
        string text = File.ReadAllText(copy.IndexPath);
        if (index == "whitespace") { File.WriteAllText(copy.IndexPath, text + "\n"); }
        else
        {
            JsonNode changed = JsonNode.Parse(text)!;
            changed["trustIndexVersion"] = "1.1.10.8";
            File.WriteAllText(copy.IndexPath, changed.ToJsonString());
        }
        Damage(copy, damage);
        if (pack == "rebound")
        {
            MutatePack(copy, (header, _) =>
            {
                header["trustIndex"]!["sha256"] = Hash(File.ReadAllBytes(copy.IndexPath));
                header["trustIndex"]!["trustIndexVersion"] = "1.1.10.8";
            });
        }
        JsonNode result = await copy.RunAsync();
        AssertSource(result, "json", 1);
        if (pack is "intact" or "rebound") { Assert.Equal("trust-index-mismatch", result["reason"]!.GetValue<string>()); }
        if (damage == "intact") { PrebuiltProfileCatalogEquivalenceTests.AssertPinned(result); }
        if (damage == "document") { Assert.NotNull(TargetBundle(result)["error"]); }
        if (damage == "identity") { Assert.Equal("edited-bundle-identity", TargetBundle(result)["bundleId"]!.GetValue<string>()); }
    }

    /// <summary>All pack/index/source combinations for the fail-closed row.</summary>
    public static TheoryData<string, string, string> InvalidIndexCases
    {
        get
        {
            var data = new TheoryData<string, string, string>();
            foreach (string pack in PackStates)
            {
                foreach (string index in InvalidIndexStates)
                {
                    foreach (string damage in DiskStates) { data.Add(pack, index, damage); }
                }
            }
            return data;
        }
    }

    /// <summary>M8 index failure cannot be recovered by any pack state, and has no fabricated rejection fact.</summary>
    [Theory]
    [MemberData(nameof(InvalidIndexCases))]
    public async Task M8IndexFailureCannotRecoverFromPack(string pack, string index, string damage)
    {
        using var copy = new CatalogProbeCopy();
        SetPack(copy, pack);
        Damage(copy, damage);
        if (index == "missing") { File.Delete(copy.IndexPath); }
        else { File.WriteAllText(copy.IndexPath, index == "malformed" ? "{" : "{}"); }
        JsonNode result = await copy.RunAsync();
        Assert.False(result["loaded"]!.GetValue<bool>());
        Assert.Null(result["source"]);
        Assert.Equal(0, result["warnings"]!.GetValue<int>());
        Assert.False(result["hasPublishedSnapshot"]!.GetValue<bool>());
    }

    /// <summary>Repaired transport mutations must reach their intended acceptance check in the real child.</summary>
    [Theory]
    [InlineData("bound", "file-bound")]
    [InlineData("format", "format")]
    [InlineData("body", "body-integrity")]
    [InlineData("index", "trust-index-mismatch")]
    [InlineData("bundle", "bundle-set-mismatch")]
    [InlineData("manifest", "manifest-set-mismatch")]
    [InlineData("document", "document-integrity")]
    [InlineData("schema", "document-set-mismatch")]
    public async Task AcceptanceChecksUseRealFallbackAndPinnedPublication(string mutation, string reason)
    {
        using var copy = new CatalogProbeCopy();
        if (mutation == "bound")
        {
            using var stream = new FileStream(copy.PackPath, FileMode.Open, FileAccess.Write);
            stream.SetLength(4_194_305);
        }
        else
        {
            MutatePack(copy, (header, body) =>
            {
                JsonNode bundle = header["bundles"]![0]!;
                switch (mutation)
                {
                    case "format": header["formatVersion"] = 2; break;
                    case "body": body[0] ^= 1; break;
                    case "index": header["trustIndex"]!["sha256"] = new string('a', 64); break;
                    case "bundle": bundle["bundleVersion"] = "0.0.0"; break;
                    case "document": bundle["documents"]![0]!["contentHash"] = new string('a', 64); break;
                    case "schema": bundle["documents"]![0]!["schemaId"] = "https://example.invalid/changed"; break;
                    case "manifest":
                        JsonNode range = bundle["manifest"]!;
                        int offset = range["offset"]!.GetValue<int>(), length = range["length"]!.GetValue<int>();
                        Span<byte> manifest = body.AsSpan(offset, length);
                        byte[] marker = "\"bundleId\": \""u8.ToArray();
                        int start = manifest.IndexOf(marker) + marker.Length;
                        Assert.True(start >= marker.Length);
                        manifest[start] = (byte)'x';
                        range["sha256"] = Hash(manifest.ToArray());
                        header["body"]!["sha256"] = Hash(body);
                        break;
                    default: throw new ArgumentOutOfRangeException(nameof(mutation));
                }
            });
        }
        JsonNode result = await copy.RunAsync();
        AssertSource(result, "json", 1);
        Assert.Equal(reason, result["reason"]!.GetValue<string>());
        PrebuiltProfileCatalogEquivalenceTests.AssertPinned(result);
    }

    private static void MutatePack(CatalogProbeCopy copy, Action<JsonNode, byte[]> mutate)
    {
        byte[] pack = File.ReadAllBytes(copy.PackPath);
        int size = (int)BinaryPrimitives.ReadUInt32LittleEndian(pack.AsSpan(8, 4));
        JsonNode header = JsonNode.Parse(pack.AsSpan(12, size))!;
        byte[] body = pack[(12 + size)..];
        mutate(header, body);
        using JsonDocument document = JsonDocument.Parse(header.ToJsonString());
        byte[] encoded = PrebuiltProfileCatalogCanonicalJson.Encode(document.RootElement);
        byte[] changed = new byte[12 + encoded.Length + body.Length];
        "NFCPBCAT"u8.CopyTo(changed);
        BinaryPrimitives.WriteUInt32LittleEndian(changed.AsSpan(8), (uint)encoded.Length);
        encoded.CopyTo(changed.AsSpan(12));
        body.CopyTo(changed.AsSpan(12 + encoded.Length));
        File.WriteAllBytes(copy.PackPath, changed);
    }

    private static string Hash(byte[] bytes)
    {
        return Convert.ToHexStringLower(SHA256.HashData(bytes));
    }

    private static void AssertSource(JsonNode result, string source, int warnings)
    {
        Assert.Equal(source, result["source"]!.GetValue<string>());
        Assert.Equal(warnings, result["warnings"]!.GetValue<int>());
        Assert.True(result["sameDecisionAfterReload"]!.GetValue<bool>());
    }

    private static JsonNode TargetBundle(JsonNode result)
    {
        return result["bundles"]!.AsArray().Single(b => b!["directory"]!.GetValue<string>() == Target)!;
    }

    private static string ManifestPath(CatalogProbeCopy copy)
    {
        return Path.Combine(copy.Root, "profiles", "built-in", Target, "profile-bundle.json");
    }

    private static void SetPack(CatalogProbeCopy copy, string pack)
    {
        if (pack == "absent") { File.Delete(copy.PackPath); }
        if (pack == "corrupt") { File.WriteAllText(copy.PackPath, "corrupt"); }
    }

    private static void Damage(CatalogProbeCopy copy, string damage)
    {
        string path = ManifestPath(copy);
        JsonNode manifest = JsonNode.Parse(File.ReadAllText(path))!;
        string root = Path.GetDirectoryName(path)!;
        string document = Path.Combine(root, manifest["entries"]!.AsArray().First(e => e!["kind"]!.GetValue<string>() == "composition-profile")!["path"]!.GetValue<string>());
        string schema = Path.Combine(root, manifest["entries"]!.AsArray().First(e => e!["kind"]!.GetValue<string>() == "schema")!["path"]!.GetValue<string>());
        switch (damage)
        {
            case "missing": File.Delete(document); return;
            case "document": File.AppendAllText(document, " "); return;
            case "schema": File.WriteAllText(schema, "{}"); return;
            case "missing-schema": File.Delete(schema); return;
            case "extra": File.WriteAllText(Path.Combine(root, "extra.json"), "{}"); return;
            case "manifest": File.WriteAllText(path, "{}"); return;
            case "entries": manifest["entries"]!.AsArray().RemoveAt(0); break;
            case "anchor": manifest["trustAnchorBindingId"] = "wrong-anchor"; break;
            case "version": manifest["bundleVersion"] = "0.0.0"; break;
            case "identity": manifest["bundleId"] = "edited-bundle-identity"; break;
            case "intact": return;
            default: throw new ArgumentOutOfRangeException(nameof(damage));
        }
        File.WriteAllText(path, manifest.ToJsonString());
    }
}
