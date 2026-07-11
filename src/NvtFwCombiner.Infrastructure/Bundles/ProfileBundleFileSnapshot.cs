using System.Security.Cryptography;
using System.Text.Json;
using NvtFwCombiner.Infrastructure.Files;

namespace NvtFwCombiner.Infrastructure.Bundles;

/// <summary>One hash-verified private byte snapshot of a manifest-listed bundle file.</summary>
internal sealed class ProfileBundleFileSnapshot
{
    private readonly byte[] _content;

    private ProfileBundleFileSnapshot(string manifestPath, string contentHash, byte[] ownedContent)
    {
        ManifestPath = manifestPath;
        ContentHash = contentHash;
        _content = ownedContent;
    }

    internal string ManifestPath { get; }

    internal string ContentHash { get; }

    internal int Length => _content.Length;

    internal static ProfileBundleFileSnapshot Read(
        string bundleRoot,
        ProfileBundleEntry entry,
        int maximumBytes)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(bundleRoot);
        ArgumentNullException.ThrowIfNull(entry);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maximumBytes);
        string fullPath = FileSystemPathGuard.ResolveExistingManifestFileUnderRoot(entry.Path, bundleRoot);
        using var stream = new FileStream(fullPath, new FileStreamOptions
        {
            Mode = FileMode.Open,
            Access = FileAccess.Read,
            Share = FileShare.Read,
            Options = FileOptions.SequentialScan,
            BufferSize = 4096,
        });
        RegularFileGuard.RequireOpenHandle(stream.SafeFileHandle, entry.Path);
        long length = stream.Length;
        if (length > maximumBytes)
        {
            throw new InvalidDataException(
                $"Bundle entry '{entry.Path}' exceeds the {maximumBytes}-byte limit.");
        }

        byte[] content = new byte[checked((int)length)];
        stream.ReadExactly(content);
        if (stream.ReadByte() != -1 || stream.Length != length)
        {
            throw new IOException($"Bundle entry '{entry.Path}' changed while it was being read.");
        }

        _ = FileSystemPathGuard.ResolveExistingManifestFileUnderRoot(entry.Path, bundleRoot);
        string contentHash = Convert.ToHexString(SHA256.HashData(content)).ToLowerInvariant();
        return !StringComparer.Ordinal.Equals(entry.ContentHash, contentHash)
            ? throw new InvalidDataException($"Bundle entry '{entry.Path}' content hash does not match its manifest.")
            : new ProfileBundleFileSnapshot(entry.Path, contentHash, content);
    }

    internal JsonDocument ParseStrictJson(int maximumDepth)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maximumDepth);
        return StrictJsonDocumentReader.Parse(_content, Math.Max(1, _content.Length), maximumDepth);
    }
}
