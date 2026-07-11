using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using NvtFwCombiner.Infrastructure.Bundles;
using NvtFwCombiner.Infrastructure.Tests.Files;
using NvtFwCombiner.TestSupport;

namespace NvtFwCombiner.Infrastructure.Tests.Bundles;

/// <summary>Tests bounded hash-verified snapshots of bundle entry files.</summary>
public sealed class ProfileBundleFileSnapshotTests
{
    /// <summary>Verifies one listed file is read, hashed, and parsed from its private snapshot.</summary>
    [Fact]
    public void ReadReturnsVerifiedStrictJsonSnapshot()
    {
        using var workspace = TempWorkspace.Create("nfc-bundle-snapshot");
        byte[] content = Encoding.UTF8.GetBytes(/*lang=json,strict*/ "{\"value\":1}");
        _ = workspace.Write("profiles/profile.json", content);

        var snapshot = ProfileBundleFileSnapshot.Read(
            workspace.Root,
            Entry(Hash(content)),
            1024);
        using JsonDocument document = snapshot.ParseStrictJson(16);

        Assert.Equal("profiles/profile.json", snapshot.ManifestPath);
        Assert.Equal(content.Length, snapshot.Length);
        Assert.Equal(Hash(content), snapshot.ContentHash);
        Assert.Equal(1, document.RootElement.GetProperty("value").GetInt32());
    }

    /// <summary>Verifies manifest hash mismatch fails before content can be parsed.</summary>
    [Fact]
    public void ReadRejectsContentHashMismatch()
    {
        using var workspace = TempWorkspace.Create("nfc-bundle-snapshot");
        byte[] content = Encoding.UTF8.GetBytes("{}");
        _ = workspace.Write("profiles/profile.json", content);

        _ = Assert.Throws<InvalidDataException>(() => ProfileBundleFileSnapshot.Read(
            workspace.Root,
            Entry(new string('0', 64)),
            1024));
    }

    /// <summary>Verifies caller size limits are enforced before allocating the snapshot.</summary>
    [Fact]
    public void ReadRejectsOversizedEntry()
    {
        using var workspace = TempWorkspace.Create("nfc-bundle-snapshot");
        byte[] content = Encoding.UTF8.GetBytes(/*lang=json,strict*/ "{\"value\":1}");
        _ = workspace.Write("profiles/profile.json", content);

        _ = Assert.Throws<InvalidDataException>(() => ProfileBundleFileSnapshot.Read(
            workspace.Root,
            Entry(Hash(content)),
            content.Length - 1));
    }

    /// <summary>Verifies later source-file mutation cannot change an accepted snapshot.</summary>
    [Fact]
    public void ReadSnapshotDoesNotObserveLaterFileMutation()
    {
        using var workspace = TempWorkspace.Create("nfc-bundle-snapshot");
        byte[] content = Encoding.UTF8.GetBytes(/*lang=json,strict*/ "{\"value\":1}");
        string path = workspace.Write("profiles/profile.json", content);
        var snapshot = ProfileBundleFileSnapshot.Read(
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
        var snapshot = ProfileBundleFileSnapshot.Read(
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

        _ = Assert.Throws<ArgumentOutOfRangeException>(() => ProfileBundleFileSnapshot.Read(
            workspace.Root,
            Entry(Hash(content)),
            0));
        var snapshot = ProfileBundleFileSnapshot.Read(
            workspace.Root,
            Entry(Hash(content)),
            1024);
        _ = Assert.Throws<ArgumentOutOfRangeException>(() => snapshot.ParseStrictJson(0));
    }

    /// <summary>Verifies a Unix FIFO is rejected before a read-only open can block.</summary>
    [Fact(
        Skip = "Requires a Unix FIFO fixture.",
        SkipUnless = nameof(UnixSpecialFileTestFixture.IsUnix),
        SkipType = typeof(UnixSpecialFileTestFixture),
        Timeout = 5000)]
    public void ReadRejectsUnixFifoBeforeOpening()
    {
        using var workspace = TempWorkspace.Create("nfc-bundle-snapshot");
        _ = Directory.CreateDirectory(workspace.PathFor("profiles"));
        UnixSpecialFileTestFixture.CreateFifo(workspace.PathFor("profiles/profile.json"));

        UnauthorizedAccessException exception = Assert.Throws<UnauthorizedAccessException>(() =>
            ProfileBundleFileSnapshot.Read(
                workspace.Root,
                Entry(new string('0', 64)),
                1024));

        Assert.Contains("regular filesystem file", exception.Message, StringComparison.Ordinal);
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
