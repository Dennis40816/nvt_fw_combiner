namespace NvtFwCombiner.TestSupport;

/// <summary>Disposable temporary workspace for filesystem-oriented tests.</summary>
public sealed class TempWorkspace : IDisposable
{
    private TempWorkspace(string root)
    {
        Root = root;
        _ = Directory.CreateDirectory(root);
    }

    /// <summary>Root directory for this test workspace.</summary>
    public string Root { get; }

    /// <summary>Creates a fresh temporary workspace using an NFC-specific prefix.</summary>
    public static TempWorkspace Create(string prefix = "nvt-fw-combiner")
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(prefix);

        string root = Path.Combine(Path.GetTempPath(), $"{prefix}-{Guid.NewGuid():N}");
        return new TempWorkspace(root);
    }

    /// <summary>Returns a path under the workspace root.</summary>
    public string PathFor(string relativePath)
    {
        ArgumentNullException.ThrowIfNull(relativePath);

        return Path.Combine(Root, RepositoryPaths.NormalizeRelativePath(relativePath));
    }

    /// <summary>Writes bytes under the workspace root and returns the resulting path.</summary>
    public string Write(string relativePath, byte[] bytes)
    {
        ArgumentNullException.ThrowIfNull(bytes);

        string path = PathFor(relativePath);
        string? directory = Path.GetDirectoryName(path);
        if (!string.IsNullOrEmpty(directory))
        {
            _ = Directory.CreateDirectory(directory);
        }

        File.WriteAllBytes(path, bytes);
        return path;
    }

    /// <inheritdoc />
    public void Dispose()
    {
        for (int attempt = 0; Directory.Exists(Root); attempt++)
        {
            try
            {
                Directory.Delete(Root, recursive: true);
                return;
            }
            catch (IOException) when (OperatingSystem.IsWindows() && attempt < 9)
            {
                Thread.Sleep(TimeSpan.FromMilliseconds(50));
            }
            catch (UnauthorizedAccessException) when (OperatingSystem.IsWindows() && attempt < 9)
            {
                Thread.Sleep(TimeSpan.FromMilliseconds(50));
            }
        }
    }
}
