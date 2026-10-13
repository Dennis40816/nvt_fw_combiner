using System.Security.Cryptography;

namespace NvtFwCombiner.TestSupport;

/// <summary>Resolves hash-pinned test evidence that is not part of the public tree.</summary>
/// <remarks>
/// A reference is found in two places. The held directory is the first: CI assembles it from the private
/// test-data repository (<c>docs/references/ci-held/&lt;sha256&gt;</c>, pinned by the Golden tree hash).
/// The private inventory named by <c>NVT_PRIVATE_ASSETS</c> is the second. Every read checks the complete
/// bytes against the requested SHA-256.
/// </remarks>
public static class ConfidentialReferenceTestData
{
    private const string PrivateAssetsEnvironmentVariable = "NVT_PRIVATE_ASSETS";

    /// <summary>Whether a reference source exists for this test process: a held directory or a private inventory.</summary>
    public static bool IsConfigured => !string.IsNullOrWhiteSpace(PrivateAssetsRoot) ||
        Directory.Exists(RepositoryPaths.FromRepositoryRoot("docs", "references", "ci-held"));

    private static string? PrivateAssetsRoot => Environment.GetEnvironmentVariable(PrivateAssetsEnvironmentVariable);

    /// <summary>Reads a private reference selected by its public SHA-256 identity.</summary>
    public static byte[] ReadVerifiedBytes(string sha256)
    {
        return ResolveConfigured(sha256).Bytes;
    }

    /// <summary>Reads from an explicit root for isolated inventory tests.</summary>
    public static byte[] ReadVerifiedBytes(string sha256, string privateAssetsRoot)
    {
        return ReadVerifiedReference(sha256, privateAssetsRoot).Bytes;
    }

    /// <summary>Resolves a private reference only after validating its complete bytes.</summary>
    public static string ResolveVerifiedPath(string sha256)
    {
        return ResolveConfigured(sha256).Path;
    }

    /// <summary>Resolves from an explicit root for isolated inventory tests.</summary>
    public static string ResolveVerifiedPath(string sha256, string privateAssetsRoot)
    {
        return ReadVerifiedReference(sha256, privateAssetsRoot).Path;
    }

    /// <summary>Reads a held reference (file name = its SHA-256) from an explicit directory.</summary>
    public static byte[] ReadVerifiedHeldBytes(string sha256, string heldDirectory)
    {
        return ReadVerifiedHeld(sha256, heldDirectory).Bytes;
    }

    /// <summary>Resolves from explicit sources: the held directory first, the private inventory second.</summary>
    public static byte[] ReadVerifiedBytesFromSources(
        string sha256, string heldDirectory, string? privateAssetsRoot)
    {
        return ResolveFromSources(sha256, heldDirectory, privateAssetsRoot).Bytes;
    }

    private static (string Path, byte[] Bytes) ResolveConfigured(string sha256)
    {
        return ResolveFromSources(
            sha256, RepositoryPaths.FromRepositoryRoot("docs", "references", "ci-held"), PrivateAssetsRoot);
    }

    private static (string Path, byte[] Bytes) ResolveFromSources(
        string sha256, string heldDirectory, string? privateAssetsRoot)
    {
        if (File.Exists(HeldPath(sha256, heldDirectory)))
        {
            return ReadVerifiedHeld(sha256, heldDirectory);
        }

        if (string.IsNullOrWhiteSpace(privateAssetsRoot))
        {
            throw new InvalidOperationException(
                $"The reference is not in the held directory and {PrivateAssetsEnvironmentVariable} is not set.");
        }

        return ReadVerifiedReference(sha256, privateAssetsRoot);
    }

    private static string HeldPath(string sha256, string heldDirectory)
    {
        ValidateDigest(sha256);
        return Path.Combine(Path.GetFullPath(heldDirectory), sha256.ToLowerInvariant());
    }

    private static (string Path, byte[] Bytes) ReadVerifiedHeld(string sha256, string heldDirectory)
    {
        string path = HeldPath(sha256, heldDirectory);
        if (!File.Exists(path))
        {
            throw new FileNotFoundException("The held reference file does not exist.");
        }

        if ((File.GetAttributes(path) & FileAttributes.ReparsePoint) != 0)
        {
            throw new InvalidDataException("Held reference path is a reparse point.");
        }

        return ReadAndVerify(path, sha256);
    }

    private static (string Path, byte[] Bytes) ReadVerifiedReference(string sha256, string privateAssetsRoot)
    {
        ValidateDigest(sha256);
        ArgumentException.ThrowIfNullOrWhiteSpace(privateAssetsRoot);

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

        return ReadAndVerify(path, sha256);
    }

    private static void ValidateDigest(string sha256)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sha256);
        if (sha256.Length != 64 || !sha256.All(Uri.IsHexDigit))
        {
            throw new ArgumentException("Expected a SHA-256 hexadecimal digest.", nameof(sha256));
        }
    }

    private static (string Path, byte[] Bytes) ReadAndVerify(string path, string sha256)
    {
        byte[] bytes = GoldenFileIo.ReadAllBytes(path);
        string actual = Convert.ToHexStringLower(SHA256.HashData(bytes));
        return string.Equals(actual, sha256, StringComparison.OrdinalIgnoreCase)
            ? (path, bytes)
            : throw new InvalidDataException("Private reference SHA-256 drift.");
    }
}
