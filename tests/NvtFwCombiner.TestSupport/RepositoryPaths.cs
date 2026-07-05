using System.Text.Json;

namespace NvtFwCombiner.TestSupport;

/// <summary>Shared repository path helpers for tests that need committed fixtures.</summary>
public static class RepositoryPaths
{
    /// <summary>Finds the repository root from the current test binary location.</summary>
    public static string FindRepositoryRoot()
    {
        DirectoryInfo? directory = new(AppContext.BaseDirectory);
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
        string relativePath = manifestFile.GetProperty("path").GetString()!;
        return Path.Combine(root, NormalizeRelativePath(relativePath));
    }

    /// <summary>Normalizes slash-separated fixture paths for the host filesystem.</summary>
    public static string NormalizeRelativePath(string relativePath)
    {
        ArgumentNullException.ThrowIfNull(relativePath);

        return relativePath.Replace('/', Path.DirectorySeparatorChar);
    }
}
