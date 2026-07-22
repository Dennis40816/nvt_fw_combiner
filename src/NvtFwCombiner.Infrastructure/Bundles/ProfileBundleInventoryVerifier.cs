using NvtFwCombiner.Infrastructure.Files;

namespace NvtFwCombiner.Infrastructure.Bundles;

/// <summary>Verifies that one bundle root contains exactly its manifest-listed files.</summary>
internal static class ProfileBundleInventoryVerifier
{
    internal static void VerifyClosedInventory(
        string bundleRoot,
        string manifestPath,
        ProfileBundleManifest manifest,
        int maximumDirectoryCount)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(bundleRoot);
        ArgumentException.ThrowIfNullOrWhiteSpace(manifestPath);
        ArgumentNullException.ThrowIfNull(manifest);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maximumDirectoryCount);
        string root = FileSystemPathGuard.ResolveExistingRoot(bundleRoot);
        _ = FileSystemPathGuard.ResolveExistingManifestFileUnderRoot(manifestPath, root);

        var expectedPaths = new HashSet<string>(StringComparer.Ordinal) { manifestPath };
        foreach (ProfileBundleEntry entry in manifest.Entries)
        {
            if (!expectedPaths.Add(entry.Path))
            {
                throw new InvalidDataException(
                    $"Bundle manifest path '{manifestPath}' collides with a listed entry path.");
            }
        }

        var expectedByCaseInsensitive = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (string expectedPath in expectedPaths)
        {
            if (!expectedByCaseInsensitive.TryAdd(expectedPath, expectedPath))
            {
                throw new InvalidDataException($"Bundle expected paths case-collide at '{expectedPath}'.");
            }
        }

        var actualPaths = new HashSet<string>(StringComparer.Ordinal);
        var actualCaseInsensitivePaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var pendingDirectories = new Stack<DirectoryInfo>();
        pendingDirectories.Push(new DirectoryInfo(root));
        int discoveredDirectoryCount = 1;
        while (pendingDirectories.TryPop(out DirectoryInfo? directory))
        {
            foreach (FileSystemInfo child in directory.EnumerateFileSystemInfos())
            {
                if ((child.Attributes & FileAttributes.ReparsePoint) != 0)
                {
                    throw CreateReparsePointException(child.FullName);
                }

                if ((child.Attributes & FileAttributes.Directory) != 0)
                {
                    discoveredDirectoryCount++;
                    if (discoveredDirectoryCount > maximumDirectoryCount)
                    {
                        throw new InvalidDataException(
                            $"Bundle directory count exceeds the {maximumDirectoryCount}-directory limit.");
                    }

                    pendingDirectories.Push((DirectoryInfo)child);
                    continue;
                }

                string relativePath = Path.GetRelativePath(root, child.FullName).Replace('\\', '/');
                _ = FileSystemPathGuard.ResolveExistingManifestFileUnderRoot(relativePath, root);
                if (!actualCaseInsensitivePaths.Add(relativePath))
                {
                    throw new InvalidDataException(
                        $"Bundle inventory contains case-colliding path '{relativePath}'.");
                }

                if (!expectedPaths.Contains(relativePath))
                {
                    if (expectedByCaseInsensitive.TryGetValue(relativePath, out string? expectedPath))
                    {
                        throw new InvalidDataException(
                            $"Bundle file path '{relativePath}' does not match manifest case '{expectedPath}'.");
                    }

                    throw new InvalidDataException($"Bundle inventory contains unlisted file '{relativePath}'.");
                }

                _ = actualPaths.Add(relativePath);
            }
        }

        string? missingPath = expectedPaths.FirstOrDefault(path => !actualPaths.Contains(path));
        if (missingPath is not null)
        {
            throw new FileNotFoundException($"Bundle inventory is missing listed file '{missingPath}'.");
        }
    }

    internal static UnauthorizedAccessException CreateReparsePointException(string fullName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(fullName);
        return new UnauthorizedAccessException(
            $"Bundle inventory contains reparse point '{fullName}'. " +
            "Re-extract the complete portable package to a local non-synchronized directory " +
            "(for example, C:\\Tools) and retry; do not copy only the executable.");
    }
}
