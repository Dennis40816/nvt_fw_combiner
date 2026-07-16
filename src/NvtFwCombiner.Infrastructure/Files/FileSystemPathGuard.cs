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
        RejectReparsePoints(fullPath);
        DirectoryInfo directory = Directory.CreateDirectory(fullPath);
        RejectReparsePoints(directory.FullName);
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

        RejectReparsePoints(fullPath);
        return EnsureTrailingSeparator(fullPath);
    }

    internal static string ResolveExistingFileUnderRoots(
        string path,
        IReadOnlyList<string> allowedRoots)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        ArgumentNullException.ThrowIfNull(allowedRoots);

        string fullPath = Path.GetFullPath(path);
        RequireFile(fullPath, "Artifact file was not found.");
        EnsureUnderAnyRoot(fullPath, allowedRoots);
        RejectReparsePoints(fullPath);
        return fullPath;
    }

    internal static string ResolveExistingManifestFileUnderRoot(
        string manifestPath,
        string rootDirectory)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(manifestPath);
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
        RequireFile(fullPath, "Bundle entry file was not found.");
        RejectReparsePoints(fullPath);
        RegularFileGuard.RequirePath(fullPath);
        return fullPath;
    }

    internal static string ResolveFileNameUnderRoot(string fileName, string rootDirectory)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(fileName);
        return fileName.IndexOfAny(['/', '\\', ':']) >= 0 ||
            fileName is "." or ".." ||
            Path.GetFileName(fileName) != fileName
            ? throw new ArgumentException("File name must be a plain filename without path syntax.", nameof(fileName))
            : Path.GetFullPath(Path.Combine(rootDirectory, fileName));
    }

    private static void EnsureUnderAnyRoot(string fullPath, IReadOnlyList<string> allowedRoots)
    {
        if (!allowedRoots.Any(root => fullPath.StartsWith(
                EnsureTrailingSeparator(Path.GetFullPath(root)),
                PathComparison)))
        {
            throw new UnauthorizedAccessException("Path is outside the configured root.");
        }
    }

    private static void RequireFile(string fullPath, string message)
    {
        if (!File.Exists(fullPath))
        {
            throw new FileNotFoundException(message, fullPath);
        }
    }

    private static void RejectReparsePoints(string path)
    {
        string? currentPath = path;
        while (currentPath is not null && !File.Exists(currentPath) && !Directory.Exists(currentPath))
        {
            currentPath = Path.GetDirectoryName(currentPath);
        }

        for (; currentPath is not null; currentPath = Path.GetDirectoryName(currentPath))
        {
            if ((File.GetAttributes(currentPath) & FileAttributes.ReparsePoint) != 0)
            {
                throw new UnauthorizedAccessException("Reparse points are not allowed.");
            }
        }
    }

    private static string EnsureTrailingSeparator(string path)
    {
        return Path.EndsInDirectorySeparator(path)
            ? path
            : path + Path.DirectorySeparatorChar;
    }
}
