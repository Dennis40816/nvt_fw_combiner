using System.Security.Cryptography;
using System.Net.Sockets;
using System.Text;
using NvtFwCombiner.Infrastructure.Bundles;
using NvtFwCombiner.Infrastructure.Tests.Files;
using NvtFwCombiner.TestSupport;

namespace NvtFwCombiner.Infrastructure.Tests.Bundles;

/// <summary>Tests closed filesystem inventory verification for profile bundles.</summary>
public sealed class ProfileBundleInventoryVerifierTests
{
    /// <summary>Verifies the manifest file and every listed entry form one closed inventory.</summary>
    [Fact]
    public void VerifyClosedInventoryAcceptsExactFiles()
    {
        using TempWorkspace workspace = BundleWorkspace();

        ProfileBundleInventoryVerifier.VerifyClosedInventory(
            workspace.Root,
            "profile-bundle.json",
            Manifest(),
            8);
    }

    /// <summary>Verifies any unlisted file fails the closed allowlist.</summary>
    [Fact]
    public void VerifyClosedInventoryRejectsUnlistedFile()
    {
        using TempWorkspace workspace = BundleWorkspace();
        _ = workspace.Write("profiles/unlisted.json", Encoding.UTF8.GetBytes("{}"));

        InvalidDataException exception = Assert.Throws<InvalidDataException>(() =>
            ProfileBundleInventoryVerifier.VerifyClosedInventory(
                workspace.Root,
                "profile-bundle.json",
                Manifest(),
                8));

        Assert.Contains("unlisted file", exception.Message, StringComparison.Ordinal);
    }

    /// <summary>Verifies every listed entry and the manifest file must exist.</summary>
    [Fact]
    public void VerifyClosedInventoryRejectsMissingFiles()
    {
        using TempWorkspace workspace = BundleWorkspace();
        File.Delete(workspace.PathFor("profiles/profile.json"));

        _ = Assert.Throws<FileNotFoundException>(() => ProfileBundleInventoryVerifier.VerifyClosedInventory(
            workspace.Root,
            "profile-bundle.json",
            Manifest(),
            8));
        _ = Assert.Throws<FileNotFoundException>(() => ProfileBundleInventoryVerifier.VerifyClosedInventory(
            workspace.Root,
            "missing-manifest.json",
            Manifest(),
            8));
    }

    /// <summary>Verifies actual file casing must exactly match manifest casing on every platform.</summary>
    [Fact]
    public void VerifyClosedInventoryRejectsPathCaseMismatch()
    {
        using var workspace = TempWorkspace.Create("nfc-bundle-inventory");
        _ = workspace.Write("profile-bundle.json", Encoding.UTF8.GetBytes("{}"));
        _ = workspace.Write("profiles/Profile.json", Encoding.UTF8.GetBytes("{}"));

        InvalidDataException exception = Assert.Throws<InvalidDataException>(() =>
            ProfileBundleInventoryVerifier.VerifyClosedInventory(
                workspace.Root,
                "profile-bundle.json",
                Manifest(),
                8));

        Assert.Contains("does not match manifest case", exception.Message, StringComparison.Ordinal);
    }

    /// <summary>Verifies directory enumeration is bounded independently from file count.</summary>
    [Fact]
    public void VerifyClosedInventoryRejectsExcessDirectories()
    {
        using TempWorkspace workspace = BundleWorkspace();
        _ = Directory.CreateDirectory(workspace.PathFor("empty/one/two"));

        _ = Assert.Throws<InvalidDataException>(() => ProfileBundleInventoryVerifier.VerifyClosedInventory(
            workspace.Root,
            "profile-bundle.json",
            Manifest(),
            3));
    }

    /// <summary>Verifies sibling directories count against the limit before they enter the work stack.</summary>
    [Fact]
    public void VerifyClosedInventoryRejectsWideDirectoryFanOut()
    {
        using TempWorkspace workspace = BundleWorkspace();
        _ = Directory.CreateDirectory(workspace.PathFor("empty-a"));
        _ = Directory.CreateDirectory(workspace.PathFor("empty-b"));
        _ = Directory.CreateDirectory(workspace.PathFor("empty-c"));

        _ = Assert.Throws<InvalidDataException>(() => ProfileBundleInventoryVerifier.VerifyClosedInventory(
            workspace.Root,
            "profile-bundle.json",
            Manifest(),
            5));
    }

    /// <summary>Verifies a Unix domain socket cannot satisfy one manifest file entry.</summary>
    [Fact(
        Skip = "Requires a Unix domain socket fixture.",
        SkipUnless = nameof(UnixSpecialFileTestFixture.IsUnix),
        SkipType = typeof(UnixSpecialFileTestFixture),
        Timeout = 5000)]
    public void VerifyClosedInventoryRejectsUnixDomainSocket()
    {
        using TempWorkspace workspace = BundleWorkspace();
        string path = workspace.PathFor("profiles/profile.json");
        File.Delete(path);
        using var socket = new Socket(AddressFamily.Unix, SocketType.Stream, ProtocolType.Unspecified);
        socket.Bind(new UnixDomainSocketEndPoint(path));

        UnauthorizedAccessException exception = Assert.Throws<UnauthorizedAccessException>(() =>
            ProfileBundleInventoryVerifier.VerifyClosedInventory(
                workspace.Root,
                "profile-bundle.json",
                Manifest(),
                8));

        Assert.Contains("regular filesystem file", exception.Message, StringComparison.Ordinal);
    }

    /// <summary>Verifies the manifest file cannot also be a listed content entry.</summary>
    [Fact]
    public void VerifyClosedInventoryRejectsManifestEntryCollision()
    {
        using TempWorkspace workspace = BundleWorkspace();
        ProfileBundleManifest manifest = Manifest(
            new ProfileBundleEntry(
                "manifest",
                ProfileBundleEntryKind.CompositionProfile,
                "profile-bundle.json",
                SchemaId,
                Hash(Encoding.UTF8.GetBytes("{}"))));

        _ = Assert.Throws<InvalidDataException>(() => ProfileBundleInventoryVerifier.VerifyClosedInventory(
            workspace.Root,
            "profile-bundle.json",
            manifest,
            8));
    }

    /// <summary>Reparse failures preserve the safety gate and explain the portable-package workaround.</summary>
    [Fact]
    public void ReparseFailureExplainsNonSynchronizedExtraction()
    {
        UnauthorizedAccessException exception =
            ProfileBundleInventoryVerifier.CreateReparsePointException(
                @"C:\Users\owner\OneDrive\NvtFwCombiner\profiles\built-in\bundle\families");

        Assert.Contains("reparse point", exception.Message, StringComparison.Ordinal);
        Assert.Contains("local non-synchronized directory", exception.Message, StringComparison.Ordinal);
        Assert.Contains(@"C:\Tools", exception.Message, StringComparison.Ordinal);
        Assert.Contains("do not copy only the executable", exception.Message, StringComparison.Ordinal);
    }

    private const string SchemaId =
        "https://example.invalid/nfc/schemas/composition-profile-v2.schema.json";

    private static TempWorkspace BundleWorkspace()
    {
        var workspace = TempWorkspace.Create("nfc-bundle-inventory");
        _ = workspace.Write("profile-bundle.json", Encoding.UTF8.GetBytes("{}"));
        _ = workspace.Write("schemas/composition-profile-v2.schema.json", Encoding.UTF8.GetBytes("{}"));
        _ = workspace.Write("profiles/profile.json", Encoding.UTF8.GetBytes("{}"));
        return workspace;
    }

    private static ProfileBundleManifest Manifest(ProfileBundleEntry? additionalEntry = null)
    {
        var entries = new List<ProfileBundleEntry>
        {
            new(
                "profile",
                ProfileBundleEntryKind.CompositionProfile,
                "profiles/profile.json",
                SchemaId,
                Hash(Encoding.UTF8.GetBytes("{}"))),
            new(
                "schema",
                ProfileBundleEntryKind.Schema,
                "schemas/composition-profile-v2.schema.json",
                SchemaId,
                Hash(Encoding.UTF8.GetBytes("{}"))),
        };
        if (additionalEntry is not null)
        {
            entries.Add(additionalEntry);
        }

        return new ProfileBundleManifest(
            "bundle",
            "1.0.0",
            new string('a', 64),
            "release-manifest",
            entries);
    }

    private static string Hash(byte[] content)
    {
        return Convert.ToHexString(SHA256.HashData(content)).ToLowerInvariant();
    }
}
