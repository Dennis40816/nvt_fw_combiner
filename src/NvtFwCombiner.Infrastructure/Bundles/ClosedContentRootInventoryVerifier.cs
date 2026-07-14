using NvtFwCombiner.Infrastructure.Files;

namespace NvtFwCombiner.Infrastructure.Bundles;

/// <summary>Verifies that a manifest and its listed files form one closed filesystem inventory.</summary>
internal static class ClosedContentRootInventoryVerifier
{
    internal static void VerifyClosedInventory(
        string rootDirectory,
        string manifestPath,
        IReadOnlyList<string> listedPaths,
        int maximumDirectoryCount)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(rootDirectory);
        ArgumentException.ThrowIfNullOrWhiteSpace(manifestPath);
        ArgumentNullException.ThrowIfNull(listedPaths);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maximumDirectoryCount);
        string root = FileSystemPathGuard.ResolveExistingRoot(rootDirectory);
        _ = FileSystemPathGuard.ResolveExistingManifestFileUnderRoot(manifestPath, root);

        var expectedPaths = new HashSet<string>(StringComparer.Ordinal) { manifestPath };
        foreach (string listedPath in listedPaths)
        {
            if (!expectedPaths.Add(listedPath))
            {
                throw new InvalidDataException(
                    $"Closed-root manifest path '{manifestPath}' collides with a listed entry path.");
            }
        }

        var expectedByCaseInsensitive = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (string expectedPath in expectedPaths)
        {
            if (!expectedByCaseInsensitive.TryAdd(expectedPath, expectedPath))
            {
                throw new InvalidDataException($"Closed-root expected paths case-collide at '{expectedPath}'.");
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
                    throw new UnauthorizedAccessException(
                        $"Closed-root inventory contains reparse point '{child.FullName}'.");
                }

                if ((child.Attributes & FileAttributes.Directory) != 0)
                {
                    discoveredDirectoryCount++;
                    if (discoveredDirectoryCount > maximumDirectoryCount)
                    {
                        throw new InvalidDataException(
                            $"Closed-root directory count exceeds the {maximumDirectoryCount}-directory limit.");
                    }

                    pendingDirectories.Push((DirectoryInfo)child);
                    continue;
                }

                string relativePath = Path.GetRelativePath(root, child.FullName).Replace('\\', '/');
                _ = FileSystemPathGuard.ResolveExistingManifestFileUnderRoot(relativePath, root);
                if (!actualCaseInsensitivePaths.Add(relativePath))
                {
                    throw new InvalidDataException(
                        $"Closed-root inventory contains case-colliding path '{relativePath}'.");
                }

                if (!expectedPaths.Contains(relativePath))
                {
                    if (expectedByCaseInsensitive.TryGetValue(relativePath, out string? expectedPath))
                    {
                        throw new InvalidDataException(
                            $"Closed-root file path '{relativePath}' does not match manifest case '{expectedPath}'.");
                    }

                    throw new InvalidDataException($"Closed-root inventory contains unlisted file '{relativePath}'.");
                }

                _ = actualPaths.Add(relativePath);
            }
        }

        string? missingPath = expectedPaths.FirstOrDefault(path => !actualPaths.Contains(path));
        if (missingPath is not null)
        {
            throw new FileNotFoundException($"Closed-root inventory is missing listed file '{missingPath}'.");
        }
    }
}
