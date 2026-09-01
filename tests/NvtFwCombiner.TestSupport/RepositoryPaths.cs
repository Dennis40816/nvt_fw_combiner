using System.Text.Json;

namespace NvtFwCombiner.TestSupport;

/// <summary>Shared repository path helpers for tests that need committed fixtures.</summary>
public static class RepositoryPaths
{
    private const string RepositoryRootEnvironmentVariable = "NFC_TEST_REPOSITORY_ROOT";

    /// <summary>Finds the verifier-selected root or falls back from a direct-local test binary.</summary>
    public static string FindRepositoryRoot()
    {
        return FindRepositoryRoot(
            Environment.GetEnvironmentVariable(RepositoryRootEnvironmentVariable),
            new DirectoryInfo(AppContext.BaseDirectory));
    }

    internal static string FindRepositoryRoot(
        string? configuredRoot,
        DirectoryInfo startingDirectory)
    {
        ArgumentNullException.ThrowIfNull(startingDirectory);

        if (configuredRoot is not null)
        {
            return ValidateConfiguredRepositoryRoot(configuredRoot);
        }

        DirectoryInfo? directory = startingDirectory;
        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "NvtFwCombiner.slnx")))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        throw new DirectoryNotFoundException("Could not locate repository root.");
    }

    private static string ValidateConfiguredRepositoryRoot(string configuredRoot)
    {
        if (string.IsNullOrWhiteSpace(configuredRoot) ||
            !Path.IsPathFullyQualified(configuredRoot))
        {
            throw new InvalidOperationException(
                $"{RepositoryRootEnvironmentVariable} must name a fully qualified directory.");
        }

        string fullRoot = Path.GetFullPath(configuredRoot);
        var directory = new DirectoryInfo(fullRoot);
        if (!directory.Exists)
        {
            throw new InvalidOperationException(
                $"{RepositoryRootEnvironmentVariable} directory does not exist: {fullRoot}");
        }

        var marker = new FileInfo(Path.Combine(fullRoot, "NvtFwCombiner.slnx"));
        return marker.Exists && (marker.Attributes & FileAttributes.ReparsePoint) == 0
            ? directory.FullName
            : throw new InvalidOperationException(
                $"{RepositoryRootEnvironmentVariable} does not contain a regular NvtFwCombiner.slnx: {fullRoot}");
    }

    /// <summary>Builds a path from the repository root and normalizes manifest-style separators.</summary>
    public static string FromRepositoryRoot(params string[] segments)
    {
        ArgumentNullException.ThrowIfNull(segments);

        string path = FindRepositoryRoot();
        foreach (string segment in segments)
        {
            path = Path.Combine(path, NormalizeRelativePath(segment));
        }

        return path;
    }

    /// <summary>Builds a path for a manifest object containing a string <c>path</c> property.</summary>
    public static string ManifestPath(string root, JsonElement manifestFile)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(root);

        string relativePath = manifestFile.GetProperty("path").GetString()!;
        return PathFromRelative(root, relativePath);
    }

    /// <summary>Builds a contained path from a fixture root and a manifest-style relative path.</summary>
    public static string PathFromRelative(string root, string relativePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(root);
        ArgumentException.ThrowIfNullOrWhiteSpace(relativePath);

        string candidate = Path.GetFullPath(Path.Combine(root, NormalizeRelativePath(relativePath)));
        string fullRoot = Path.GetFullPath(root);
        string relativeToRoot = Path.GetRelativePath(fullRoot, candidate);
        return IsWithinRoot(relativeToRoot)
            ? candidate
            : throw new InvalidOperationException($"Fixture path escapes root: {relativePath}");
    }

    /// <summary>Normalizes slash-separated fixture paths for the host filesystem.</summary>
    public static string NormalizeRelativePath(string relativePath)
    {
        ArgumentNullException.ThrowIfNull(relativePath);

        return relativePath.Replace('/', Path.DirectorySeparatorChar);
    }

    private static bool IsWithinRoot(string relativePath)
    {
        return !Path.IsPathRooted(relativePath) &&
            !relativePath.Equals("..", StringComparison.Ordinal) &&
            !relativePath.StartsWith($"..{Path.DirectorySeparatorChar}", StringComparison.Ordinal) &&
            !relativePath.StartsWith($"..{Path.AltDirectorySeparatorChar}", StringComparison.Ordinal);
    }
}
