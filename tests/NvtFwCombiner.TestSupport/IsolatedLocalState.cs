namespace NvtFwCombiner.TestSupport;

/// <summary>
/// Supplies local-state directories for test composition roots. Every call returns a new directory under one
/// per-process root, so two allocations never share preferences, report history, toolchain runtime or Event Buffer
/// format files (a shared fixture host still shares its one directory among its tests). A normal process exit
/// attempts to delete the per-process root; a killed or hung process, or a failed delete, can leave it behind.
/// </summary>
public static class IsolatedLocalState
{
    private const string ParentDirectoryName = "nvt-fw-combiner-local-state";
    private static readonly Lazy<string> ProcessRoot = new(CreateProcessRoot);

    /// <summary>Returns a fresh fully qualified directory under the test temporary root; it is created on first write.</summary>
    public static string CreateDirectory(string owner = "host")
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(owner);
        return Path.Combine(ProcessRoot.Value, $"{owner}-{Guid.NewGuid():N}");
    }

    /// <summary>Returns whether a full path lies inside the given directory.</summary>
    public static bool Contains(string directory, string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(directory);
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        string parent = Path.TrimEndingDirectorySeparator(Path.GetFullPath(directory)) + Path.DirectorySeparatorChar;
        return Path.GetFullPath(path).StartsWith(
            parent,
            OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal);
    }

    private static string CreateProcessRoot()
    {
        string root = Path.GetFullPath(Path.Combine(
            Path.GetTempPath(),
            ParentDirectoryName,
            $"{Environment.ProcessId}-{Guid.NewGuid():N}"));
        AppDomain.CurrentDomain.ProcessExit += (_, _) => DeleteBestEffort(root);
        return root;
    }

    private static void DeleteBestEffort(string root)
    {
        try
        {
            if (Directory.Exists(root))
            {
                Directory.Delete(root, recursive: true);
            }
        }
        catch (IOException)
        {
        }
        catch (UnauthorizedAccessException)
        {
        }
    }
}
