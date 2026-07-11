namespace NvtFwCombiner.Infrastructure.Files;

internal static class FileSystemPathGuard
{
    private static readonly StringComparison PathComparison = OperatingSystem.IsWindows()
        ? StringComparison.OrdinalIgnoreCase
        : StringComparison.Ordinal;

    internal static string ResolveRoot(string rootDirectory)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(rootDirectory);

        string fullPath = Path.GetFullPath(rootDirectory);
        RejectExistingParentReparsePoints(fullPath);
        DirectoryInfo directory = Directory.CreateDirectory(fullPath);
        RejectReparsePoint(directory.FullName);
        return EnsureTrailingSeparator(directory.FullName);
    }

    internal static string ResolveExistingRoot(string rootDirectory)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(rootDirectory);

        string fullPath = Path.GetFullPath(rootDirectory);
        if (!Directory.Exists(fullPath))
        {
            throw new DirectoryNotFoundException($"Allowed root was not found: {fullPath}");
        }

        RejectReparsePoint(fullPath);
        return EnsureTrailingSeparator(fullPath);
    }

    internal static string ResolveExistingFileUnderRoots(
        string path,
        IReadOnlyList<string> allowedRoots)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        ArgumentNullException.ThrowIfNull(allowedRoots);

        string fullPath = Path.GetFullPath(path);
        if (!File.Exists(fullPath))
        {
            throw new FileNotFoundException("Artifact file was not found.", fullPath);
        }

        EnsureUnderAnyRoot(fullPath, allowedRoots);
        RejectReparsePoint(fullPath);
        return fullPath;
    }

    internal static string ResolveExistingManifestFileUnderRoot(
        string manifestPath,
        string rootDirectory)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(manifestPath);
        ArgumentException.ThrowIfNullOrWhiteSpace(rootDirectory);
        if (Path.IsPathFullyQualified(manifestPath) ||
            manifestPath.IndexOfAny(['\\', ':', '\0']) >= 0)
        {
            throw new ArgumentException(
                "Bundle manifest paths must be relative and use forward slashes.",
                nameof(manifestPath));
        }

        string[] segments = manifestPath.Split('/');
        if (segments.Any(static segment => string.IsNullOrEmpty(segment) || segment is "." or ".."))
        {
            throw new ArgumentException(
                "Bundle manifest paths cannot contain empty, current, or parent segments.",
                nameof(manifestPath));
        }

        string root = ResolveExistingRoot(rootDirectory);
        string fullPath = Path.GetFullPath(Path.Combine([root, .. segments]));
        EnsureUnderRoot(fullPath, root);
        if (!File.Exists(fullPath))
        {
            throw new FileNotFoundException("Bundle entry file was not found.", fullPath);
        }

        RejectReparsePoint(fullPath);
        return fullPath;
    }

    internal static string ResolveFileNameUnderRoot(string fileName, string rootDirectory)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(fileName);
        ArgumentException.ThrowIfNullOrWhiteSpace(rootDirectory);
        if (fileName.IndexOfAny(['/', '\\', ':']) >= 0 ||
            fileName is "." or ".." ||
            Path.GetFileName(fileName) != fileName)
        {
            throw new ArgumentException("File name must be a plain filename without path syntax.", nameof(fileName));
        }

        string root = EnsureTrailingSeparator(Path.GetFullPath(rootDirectory));
        string fullPath = Path.GetFullPath(Path.Combine(root, fileName));
        EnsureUnderRoot(fullPath, root);
        return fullPath;
    }

    private static void EnsureUnderAnyRoot(string fullPath, IReadOnlyList<string> allowedRoots)
    {
        if (allowedRoots.Count == 0)
        {
            throw new InvalidOperationException("At least one allowed root is required.");
        }

        foreach (string root in allowedRoots)
        {
            string normalizedRoot = EnsureTrailingSeparator(Path.GetFullPath(root));
            if (fullPath.StartsWith(normalizedRoot, PathComparison))
            {
                return;
            }
        }

        throw new UnauthorizedAccessException("Path is outside the configured root.");
    }

    private static void EnsureUnderRoot(string fullPath, string root)
    {
        string normalizedRoot = EnsureTrailingSeparator(Path.GetFullPath(root));
        if (!fullPath.StartsWith(normalizedRoot, PathComparison))
        {
            throw new UnauthorizedAccessException("Path is outside the configured root.");
        }
    }

    private static void RejectReparsePoint(string path)
    {
        FileAttributes attributes = File.GetAttributes(path);
        if ((attributes & FileAttributes.ReparsePoint) != 0)
        {
            throw new UnauthorizedAccessException("Reparse points are not allowed.");
        }

        string? directoryPath = Directory.Exists(path)
            ? path
            : Path.GetDirectoryName(path);
        while (!string.IsNullOrWhiteSpace(directoryPath))
        {
            FileAttributes directoryAttributes = File.GetAttributes(directoryPath);
            if ((directoryAttributes & FileAttributes.ReparsePoint) != 0)
            {
                throw new UnauthorizedAccessException("Reparse points are not allowed.");
            }

            directoryPath = Directory.GetParent(directoryPath)?.FullName;
        }
    }

    private static void RejectExistingParentReparsePoints(string path)
    {
        string? currentPath = path;
        while (!string.IsNullOrWhiteSpace(currentPath) &&
               !File.Exists(currentPath) &&
               !Directory.Exists(currentPath))
        {
            currentPath = Path.GetDirectoryName(currentPath);
        }

        if (!string.IsNullOrWhiteSpace(currentPath))
        {
            RejectReparsePoint(currentPath);
        }
    }

    private static string EnsureTrailingSeparator(string path)
    {
        return Path.EndsInDirectorySeparator(path)
            ? path
            : path + Path.DirectorySeparatorChar;
    }
}
