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

    /// <summary>Creates a short, atomically reserved workspace for copied SDK graphs.</summary>
    public static TempWorkspace CreateShort()
    {
        return new TempWorkspace(Directory.CreateTempSubdirectory().FullName);
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
        long retryDeadline = Environment.TickCount64 + 450;
        while (Directory.Exists(Root))
        {
            try
            {
                Directory.Delete(Root, recursive: true);
                return;
            }
            catch (IOException) when (OperatingSystem.IsWindows() && Environment.TickCount64 < retryDeadline)
            {
            }
            catch (UnauthorizedAccessException) when (OperatingSystem.IsWindows() && Environment.TickCount64 < retryDeadline)
            {
            }

            long remainingMilliseconds = retryDeadline - Environment.TickCount64;
            if (remainingMilliseconds > 0)
            {
                Thread.Sleep((int)Math.Min(50, remainingMilliseconds));
            }
        }
    }
}
