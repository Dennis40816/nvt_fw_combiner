using System.Security.Cryptography;

namespace NvtFwCombiner.TestSupport;

/// <summary>Resolves hash-pinned test evidence from the private reference inventory.</summary>
public static class ConfidentialReferenceTestData
{
    private const string PrivateAssetsEnvironmentVariable = "NVT_PRIVATE_ASSETS";

    /// <summary>Whether the private inventory was configured for this test process.</summary>
    public static bool IsConfigured => !string.IsNullOrWhiteSpace(
        Environment.GetEnvironmentVariable(PrivateAssetsEnvironmentVariable));

    /// <summary>Reads a private reference selected by its public SHA-256 identity.</summary>
    public static byte[] ReadVerifiedBytes(string sha256)
    {
        string root = Environment.GetEnvironmentVariable(PrivateAssetsEnvironmentVariable)
            ?? throw new InvalidOperationException($"{PrivateAssetsEnvironmentVariable} is not set.");
        return ReadVerifiedBytes(sha256, root);
    }

    /// <summary>Reads from an explicit root for isolated inventory tests.</summary>
    public static byte[] ReadVerifiedBytes(string sha256, string privateAssetsRoot)
    {
        return ReadVerifiedReference(sha256, privateAssetsRoot).Bytes;
    }

    /// <summary>Resolves a private reference only after validating its complete bytes.</summary>
    public static string ResolveVerifiedPath(string sha256)
    {
        string root = Environment.GetEnvironmentVariable(PrivateAssetsEnvironmentVariable)
            ?? throw new InvalidOperationException($"{PrivateAssetsEnvironmentVariable} is not set.");
        return ResolveVerifiedPath(sha256, root);
    }

    /// <summary>Resolves from an explicit root for isolated inventory tests.</summary>
    public static string ResolveVerifiedPath(string sha256, string privateAssetsRoot)
    {
        return ReadVerifiedReference(sha256, privateAssetsRoot).Path;
    }

    private static (string Path, byte[] Bytes) ReadVerifiedReference(string sha256, string privateAssetsRoot)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sha256);
        ArgumentException.ThrowIfNullOrWhiteSpace(privateAssetsRoot);
        if (sha256.Length != 64 || !sha256.All(Uri.IsHexDigit))
        {
            throw new ArgumentException("Expected a SHA-256 hexadecimal digest.", nameof(sha256));
        }

        string referencesRoot = Path.GetFullPath(Path.Combine(privateAssetsRoot, "nfc", "references"));
        string sumsPath = Path.Combine(referencesRoot, "SHA256SUMS");
        string? relativePath = null;
        foreach (string line in File.ReadLines(sumsPath))
        {
            if (line.Length < 67 || line[64] != ' ' || line[65] != ' ' ||
                !string.Equals(line[..64], sha256, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            relativePath = line[66..];
            break;
        }

        if (string.IsNullOrWhiteSpace(relativePath))
        {
            throw new FileNotFoundException("SHA-256 was not found in the private reference inventory.");
        }

        string path = RepositoryPaths.PathFromRelative(referencesRoot, relativePath);
        string cursor = referencesRoot;
        foreach (string segment in Path.GetRelativePath(referencesRoot, path)
            .Split(Path.DirectorySeparatorChar, StringSplitOptions.RemoveEmptyEntries))
        {
            cursor = Path.Combine(cursor, segment);
            if ((File.GetAttributes(cursor) & FileAttributes.ReparsePoint) != 0)
            {
                throw new InvalidDataException("Private reference path contains a reparse point.");
            }
        }

        byte[] bytes = File.ReadAllBytes(path);
        string actual = Convert.ToHexStringLower(SHA256.HashData(bytes));
        return string.Equals(actual, sha256, StringComparison.OrdinalIgnoreCase)
            ? (path, bytes)
            : throw new InvalidDataException("Private reference SHA-256 drift.");
    }
}
