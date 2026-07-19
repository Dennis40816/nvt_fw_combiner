using System.Security.Cryptography;
using System.Text.Json;
using NvtFwCombiner.Infrastructure.Files;

namespace NvtFwCombiner.Infrastructure.Bundles;

/// <summary>One bounded private byte snapshot of a bundle manifest or listed entry file.</summary>
internal sealed class ProfileBundleFileSnapshot
{
    private readonly byte[] _content;

    private ProfileBundleFileSnapshot(string manifestPath, string actualSha256, byte[] ownedContent)
    {
        ManifestPath = manifestPath;
        ActualSha256 = actualSha256;
        _content = ownedContent;
    }

    internal string ManifestPath { get; }

    internal string ActualSha256 { get; }

    internal int Length => _content.Length;

    internal static ProfileBundleFileSnapshot ReadManifest(
        string bundleRoot,
        string manifestPath,
        int maximumBytes)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(manifestPath);
        return ReadCore(bundleRoot, manifestPath, maximumBytes);
    }

    internal static ProfileBundleFileSnapshot ReadEntry(
        string bundleRoot,
        ProfileBundleEntry entry,
        int maximumBytes)
    {
        ArgumentNullException.ThrowIfNull(entry);
        ProfileBundleFileSnapshot snapshot = ReadCore(bundleRoot, entry.Path, maximumBytes);
        return !StringComparer.Ordinal.Equals(entry.ContentHash, snapshot.ActualSha256)
            ? throw new InvalidDataException($"Bundle entry '{entry.Path}' content hash does not match its manifest.")
            : snapshot;
    }

    internal JsonDocument ParseStrictJson(int maximumDepth)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maximumDepth);
        return StrictJsonDocumentReader.ParseOwnedSnapshot(
            _content,
            Math.Max(1, _content.Length),
            maximumDepth);
    }

    private static ProfileBundleFileSnapshot ReadCore(
        string bundleRoot,
        string manifestPath,
        int maximumBytes)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(bundleRoot);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maximumBytes);
        string fullPath = FileSystemPathGuard.ResolveExistingManifestFileUnderRoot(manifestPath, bundleRoot);
        using var stream = new FileStream(fullPath, new FileStreamOptions
        {
            Mode = FileMode.Open,
            Access = FileAccess.Read,
            Share = FileShare.Read,
            Options = FileOptions.SequentialScan,
            BufferSize = 4096,
        });
        RegularFileGuard.RequireOpenHandle(stream.SafeFileHandle, manifestPath);
        long length = stream.Length;
        if (length > maximumBytes)
        {
            throw new InvalidDataException(
                $"Bundle file '{manifestPath}' exceeds the {maximumBytes}-byte limit.");
        }

        byte[] content = new byte[checked((int)length)];
        stream.ReadExactly(content);
        if (stream.ReadByte() != -1 || stream.Length != length)
        {
            throw new IOException($"Bundle file '{manifestPath}' changed while it was being read.");
        }

        _ = FileSystemPathGuard.ResolveExistingManifestFileUnderRoot(manifestPath, bundleRoot);
        string actualSha256 = Convert.ToHexString(SHA256.HashData(content)).ToLowerInvariant();
        return new ProfileBundleFileSnapshot(manifestPath, actualSha256, content);
    }
}
