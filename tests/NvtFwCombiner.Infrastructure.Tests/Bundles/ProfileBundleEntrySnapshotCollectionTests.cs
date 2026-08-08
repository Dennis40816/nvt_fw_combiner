using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using NvtFwCombiner.Infrastructure.Bundles;
using NvtFwCombiner.TestSupport;

namespace NvtFwCombiner.Infrastructure.Tests.Bundles;

/// <summary>Tests bounded immutable capture of every manifest-listed bundle entry.</summary>
public sealed class ProfileBundleEntrySnapshotCollectionTests
{
    /// <summary>Verifies capture preserves manifest order and isolates bytes from later source mutation.</summary>
    [Fact]
    public void CaptureReturnsDeterministicImmutableEntrySnapshots()
    {
        using TempWorkspace workspace = BundleWorkspace(out byte[] schemaBytes, out byte[] profileBytes);
        ProfileBundleManifest manifest = Manifest(schemaBytes, profileBytes);

        var collection = ProfileBundleEntrySnapshotCollection.Capture(
            workspace.Root,
            "profile-bundle.json",
            manifest,
            Limits());
        File.WriteAllText(workspace.PathFor("profiles/profile.json"), /*lang=json,strict*/ "{\"changed\":true}");

        Assert.Same(manifest, collection.Manifest);
        Assert.Equal(
            schemaBytes.Length + profileBytes.Length,
            collection.Entries.Sum(static entry => entry.FileSnapshot.Length));
        Assert.Equal(["profile", "schema"], collection.Entries.Select(static entry => entry.Entry.EntryId));
        ProfileBundleEntrySnapshot profile = Assert.Single(
            collection.Entries,
            static entry => entry.Entry.EntryId == "profile");
        using JsonDocument document = profile.FileSnapshot.ParseStrictJson(16);
        Assert.Equal(1, document.RootElement.GetProperty("value").GetInt32());
    }

    /// <summary>Verifies entry count is bounded before filesystem inventory and file allocation.</summary>
    [Fact]
    public void CaptureRejectsEntryCountAboveLimit()
    {
        using TempWorkspace workspace = BundleWorkspace(out byte[] schemaBytes, out byte[] profileBytes);

        InvalidDataException exception = Assert.Throws<InvalidDataException>(() =>
            ProfileBundleEntrySnapshotCollection.Capture(
                workspace.Root,
                "profile-bundle.json",
                Manifest(schemaBytes, profileBytes),
                new ProfileBundleEntrySnapshotLimits(1, 1024, 2048, 8)));

        Assert.Contains("entry count", exception.Message, StringComparison.Ordinal);
    }

    /// <summary>Verifies aggregate bytes are bounded across individually valid entry files.</summary>
    [Fact]
    public void CaptureRejectsTotalEntryBytesAboveLimit()
    {
        using TempWorkspace workspace = BundleWorkspace(out byte[] schemaBytes, out byte[] profileBytes);

        _ = Assert.Throws<InvalidDataException>(() => ProfileBundleEntrySnapshotCollection.Capture(
            workspace.Root,
            "profile-bundle.json",
            Manifest(schemaBytes, profileBytes),
            new ProfileBundleEntrySnapshotLimits(
                8,
                1024,
                schemaBytes.Length + profileBytes.Length - 1,
                8)));
    }

    /// <summary>Verifies capture enforces the closed inventory before reading listed entries.</summary>
    [Fact]
    public void CaptureRejectsUnlistedFile()
    {
        using TempWorkspace workspace = BundleWorkspace(out byte[] schemaBytes, out byte[] profileBytes);
        _ = workspace.Write("profiles/unlisted.json", Encoding.UTF8.GetBytes("{}"));

        _ = Assert.Throws<InvalidDataException>(() => ProfileBundleEntrySnapshotCollection.Capture(
            workspace.Root,
            "profile-bundle.json",
            Manifest(schemaBytes, profileBytes),
            Limits()));
    }

    /// <summary>Verifies every resource limit must be positive.</summary>
    [Theory]
    [InlineData(0, 1, 1, 1)]
    [InlineData(1, 0, 1, 1)]
    [InlineData(1, 1, 0, 1)]
    [InlineData(1, 1, 1, 0)]
    public void SnapshotLimitsRejectNonPositiveValues(
        int maximumEntryCount,
        int maximumEntryBytes,
        int maximumTotalEntryBytes,
        int maximumDirectoryCount)
    {
        _ = Assert.Throws<ArgumentOutOfRangeException>(() => new ProfileBundleEntrySnapshotLimits(
            maximumEntryCount,
            maximumEntryBytes,
            maximumTotalEntryBytes,
            maximumDirectoryCount));
    }

    private const string SchemaId =
        "https://example.invalid/nfc/schemas/composition-profile-v2.schema.json";

    private static ProfileBundleEntrySnapshotLimits Limits()
    {
        return new ProfileBundleEntrySnapshotLimits(8, 1024, 2048, 8);
    }

    private static TempWorkspace BundleWorkspace(out byte[] schemaBytes, out byte[] profileBytes)
    {
        var workspace = TempWorkspace.Create("nfc-bundle-snapshots");
        schemaBytes = Encoding.UTF8.GetBytes(/*lang=json,strict*/ "{\"type\":\"object\"}");
        profileBytes = Encoding.UTF8.GetBytes(/*lang=json,strict*/ "{\"value\":1}");
        _ = workspace.Write("profile-bundle.json", Encoding.UTF8.GetBytes("{}"));
        _ = workspace.Write("schemas/composition-profile-v2.schema.json", schemaBytes);
        _ = workspace.Write("profiles/profile.json", profileBytes);
        return workspace;
    }

    private static ProfileBundleManifest Manifest(byte[] schemaBytes, byte[] profileBytes)
    {
        return new ProfileBundleManifest(
            "bundle",
            "1.0.0",
            new string('a', 64),
            "release-manifest",
            [
                new ProfileBundleEntry(
                    "schema",
                    ProfileBundleEntryKind.Schema,
                    "schemas/composition-profile-v2.schema.json",
                    SchemaId,
                    Hash(schemaBytes)),
                new ProfileBundleEntry(
                    "profile",
                    ProfileBundleEntryKind.CompositionProfile,
                    "profiles/profile.json",
                    SchemaId,
                    Hash(profileBytes)),
            ]);
    }

    private static string Hash(byte[] content)
    {
        return Convert.ToHexString(SHA256.HashData(content)).ToLowerInvariant();
    }
}
