using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using NvtFwCombiner.Infrastructure.Bundles;
using NvtFwCombiner.TestSupport;

namespace NvtFwCombiner.Infrastructure.Tests.Bundles;

/// <summary>Tests bounded private snapshots of bundle manifest and entry files.</summary>
public sealed class ProfileBundleFileSnapshotTests
{
    /// <summary>Verifies one listed file is read, hashed, and parsed from its private snapshot.</summary>
    [Fact]
    public void ReadEntryReturnsVerifiedStrictJsonSnapshot()
    {
        using var workspace = TempWorkspace.Create("nfc-bundle-snapshot");
        byte[] content = Encoding.UTF8.GetBytes(/*lang=json,strict*/ "{\"value\":1}");
        _ = workspace.Write("profiles/profile.json", content);

        var snapshot = ProfileBundleFileSnapshot.ReadEntry(
            workspace.Root,
            Entry(Hash(content)),
            1024);
        using JsonDocument document = snapshot.ParseStrictJson(16);

        Assert.Equal("profiles/profile.json", snapshot.ManifestPath);
        Assert.Equal(content.Length, snapshot.Length);
        Assert.Equal(Hash(content), snapshot.ActualSha256);
        Assert.Equal(1, document.RootElement.GetProperty("value").GetInt32());
    }

    /// <summary>Verifies declared entry hash mismatch fails before content can be parsed.</summary>
    [Fact]
    public void ReadEntryRejectsContentHashMismatch()
    {
        using var workspace = TempWorkspace.Create("nfc-bundle-snapshot");
        byte[] content = Encoding.UTF8.GetBytes("{}");
        _ = workspace.Write("profiles/profile.json", content);

        _ = Assert.Throws<InvalidDataException>(() => ProfileBundleFileSnapshot.ReadEntry(
            workspace.Root,
            Entry(new string('0', 64)),
            1024));
    }

    /// <summary>Verifies caller size limits are enforced before allocating the snapshot.</summary>
    [Fact]
    public void ReadEntryRejectsOversizedFile()
    {
        using var workspace = TempWorkspace.Create("nfc-bundle-snapshot");
        byte[] content = Encoding.UTF8.GetBytes(/*lang=json,strict*/ "{\"value\":1}");
        _ = workspace.Write("profiles/profile.json", content);

        _ = Assert.Throws<InvalidDataException>(() => ProfileBundleFileSnapshot.ReadEntry(
            workspace.Root,
            Entry(Hash(content)),
            content.Length - 1));
    }

    /// <summary>Verifies later source-file mutation cannot change an accepted snapshot.</summary>
    [Fact]
    public void ReadEntrySnapshotDoesNotObserveLaterFileMutation()
    {
        using var workspace = TempWorkspace.Create("nfc-bundle-snapshot");
        byte[] content = Encoding.UTF8.GetBytes(/*lang=json,strict*/ "{\"value\":1}");
        string path = workspace.Write("profiles/profile.json", content);
        var snapshot = ProfileBundleFileSnapshot.ReadEntry(
            workspace.Root,
            Entry(Hash(content)),
            1024);

        File.WriteAllText(path, /*lang=json,strict*/ "{\"value\":2}");
        using JsonDocument document = snapshot.ParseStrictJson(16);

        Assert.Equal(1, document.RootElement.GetProperty("value").GetInt32());
    }

    /// <summary>Verifies malformed JSON remains rejected after file hash verification.</summary>
    [Fact]
    public void ParseStrictJsonRejectsDuplicateKeys()
    {
        using var workspace = TempWorkspace.Create("nfc-bundle-snapshot");
        byte[] content = Encoding.UTF8.GetBytes(/*lang=json,strict*/ "{\"id\":1,\"id\":2}");
        _ = workspace.Write("profiles/profile.json", content);
        var snapshot = ProfileBundleFileSnapshot.ReadEntry(
            workspace.Root,
            Entry(Hash(content)),
            1024);

        _ = Assert.Throws<JsonException>(() => snapshot.ParseStrictJson(16));
    }

    /// <summary>Verifies byte and depth limits must be positive.</summary>
    [Fact]
    public void SnapshotRejectsInvalidLimits()
    {
        using var workspace = TempWorkspace.Create("nfc-bundle-snapshot");
        byte[] content = Encoding.UTF8.GetBytes("{}");
        _ = workspace.Write("profiles/profile.json", content);

        _ = Assert.Throws<ArgumentOutOfRangeException>(() => ProfileBundleFileSnapshot.ReadEntry(
            workspace.Root,
            Entry(Hash(content)),
            0));
        var snapshot = ProfileBundleFileSnapshot.ReadEntry(
            workspace.Root,
            Entry(Hash(content)),
            1024);
        _ = Assert.Throws<ArgumentOutOfRangeException>(() => snapshot.ParseStrictJson(0));
    }

    /// <summary>Verifies the manifest is privately captured with its actual hash but no self-trust claim.</summary>
    [Fact]
    public void ReadManifestReturnsActualHashAndPrivateSnapshot()
    {
        using var workspace = TempWorkspace.Create("nfc-bundle-snapshot");
        byte[] content = Encoding.UTF8.GetBytes(/*lang=json,strict*/ "{\"schemaVersion\":\"1.0\"}");
        string path = workspace.Write("profile-bundle.json", content);

        var snapshot = ProfileBundleFileSnapshot.ReadManifest(
            workspace.Root,
            "profile-bundle.json",
            1024);
        File.WriteAllText(path, /*lang=json,strict*/ "{\"schemaVersion\":\"changed\"}");
        using JsonDocument document = snapshot.ParseStrictJson(16);

        Assert.Equal("profile-bundle.json", snapshot.ManifestPath);
        Assert.Equal(Hash(content), snapshot.ActualSha256);
        Assert.Equal("1.0", document.RootElement.GetProperty("schemaVersion").GetString());
    }

    /// <summary>Verifies the kept strict root equals a fresh strict parse and outlives the parse that produced it.</summary>
    [Fact]
    public void GetStrictJsonRootReturnsADetachedTreeOfTheStrictParse()
    {
        using var workspace = TempWorkspace.Create("nfc-bundle-snapshot");
        ProfileBundleFileSnapshot snapshot = ReadSnapshot(
            workspace,
            /*lang=json,strict*/ "{\"value\":[1,{\"nested\":true}]}");

        JsonElement root = snapshot.GetStrictJsonRoot(16);
        using JsonDocument fresh = snapshot.ParseStrictJson(16);

        Assert.Equal(fresh.RootElement.GetRawText(), root.GetRawText());
        Assert.True(root.GetProperty("value")[1].GetProperty("nested").GetBoolean());
    }

    /// <summary>Verifies repeated readers at one depth share the kept parse instead of parsing the bytes again.</summary>
    [Fact]
    public void GetStrictJsonRootParsesOnceForRepeatedReadersAtOneDepth()
    {
        using var workspace = TempWorkspace.Create("nfc-bundle-snapshot");
        ProfileBundleFileSnapshot snapshot = ReadSnapshot(
            workspace,
            /*lang=json,strict*/ "{\"value\":[1,2,3],\"name\":\"text\"}");
        JsonElement first = snapshot.GetStrictJsonRoot(16);
        _ = snapshot.GetStrictJsonRoot(16);

        long before = GC.GetAllocatedBytesForCurrentThread();
        JsonElement repeated = snapshot.GetStrictJsonRoot(16);
        long allocated = GC.GetAllocatedBytesForCurrentThread() - before;

        Assert.Equal(0L, allocated);
        Assert.Equal(first.GetRawText(), repeated.GetRawText());
    }

    /// <summary>Verifies a failed strict parse is never kept, so every attempt fails with the strict reader's error.</summary>
    [Fact]
    public void GetStrictJsonRootRejectsDuplicateKeysOnEveryAttempt()
    {
        using var workspace = TempWorkspace.Create("nfc-bundle-snapshot");
        ProfileBundleFileSnapshot snapshot = ReadSnapshot(workspace, "{\"id\":1,\"id\":2}");

        for (int attempt = 0; attempt < 3; attempt++)
        {
            JsonException exception = Assert.Throws<JsonException>(() => snapshot.GetStrictJsonRoot(16));

            Assert.Equal("Duplicate JSON property 'id'.", exception.Message);
        }
    }

    /// <summary>Verifies a kept parse never bypasses the depth limit or argument check of a later request.</summary>
    [Fact]
    public void GetStrictJsonRootEnforcesTheLimitsOfEveryRequest()
    {
        using var workspace = TempWorkspace.Create("nfc-bundle-snapshot");
        ProfileBundleFileSnapshot snapshot = ReadSnapshot(
            workspace,
            /*lang=json,strict*/ "{\"value\":{\"nested\":{\"leaf\":1}}}");
        _ = snapshot.GetStrictJsonRoot(16);
        JsonException expected = Assert.ThrowsAny<JsonException>(() => snapshot.ParseStrictJson(2));

        JsonException actual = Assert.ThrowsAny<JsonException>(() => snapshot.GetStrictJsonRoot(2));

        Assert.Equal(expected.GetType(), actual.GetType());
        Assert.Equal(expected.Message, actual.Message);
        _ = Assert.Throws<ArgumentOutOfRangeException>(() => snapshot.GetStrictJsonRoot(0));
        Assert.Equal(
            1,
            snapshot.GetStrictJsonRoot(16).GetProperty("value").GetProperty("nested").GetProperty("leaf").GetInt32());
    }

    private static ProfileBundleFileSnapshot ReadSnapshot(TempWorkspace workspace, string json)
    {
        byte[] content = Encoding.UTF8.GetBytes(json);
        _ = workspace.Write("profiles/profile.json", content);
        return ProfileBundleFileSnapshot.ReadEntry(workspace.Root, Entry(Hash(content)), 1024);
    }

    private static ProfileBundleEntry Entry(string contentHash)
    {
        return new ProfileBundleEntry(
            "profile",
            ProfileBundleEntryKind.CompositionProfile,
            "profiles/profile.json",
            "https://example.invalid/nfc/schemas/composition-profile-v2.schema.json",
            contentHash);
    }

    private static string Hash(byte[] content)
    {
        return Convert.ToHexString(SHA256.HashData(content)).ToLowerInvariant();
    }
}
