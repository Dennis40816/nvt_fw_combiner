namespace NvtFwCombiner.Infrastructure.Files;

/// <summary>Rejects output/report paths that alias immutable composition inputs.</summary>
public static class ProtectedPathGuard
{
    private static readonly StringComparison PathComparison = OperatingSystem.IsWindows()
        ? StringComparison.OrdinalIgnoreCase
        : StringComparison.Ordinal;

    /// <summary>One path whose bytes must not be overwritten.</summary>
    public readonly record struct ProtectedPath(string Path, string Description);

    /// <summary>Rejects an output path that aliases an input artifact.</summary>
    public static void EnsureOutputDoesNotAliasInputs(
        string outputPath,
        IEnumerable<InputArtifactBinding> bindings,
        string parameterName)
    {
        EnsureDoesNotAlias(
            outputPath,
            "Output path",
            CreateProtectedPaths(bindings, outputPath: null),
            parameterName);
    }

    /// <summary>Rejects a report path that aliases an input or built output.</summary>
    public static void EnsureReportDoesNotAliasProtectedPaths(
        string reportPath,
        IEnumerable<InputArtifactBinding> bindings,
        string? outputPath,
        string parameterName)
    {
        EnsureDoesNotAlias(
            reportPath,
            "Report path",
            CreateProtectedPaths(bindings, outputPath),
            parameterName);
    }

    /// <summary>Projects input and optional output bindings into protected paths.</summary>
    public static List<ProtectedPath> CreateProtectedPaths(
        IEnumerable<InputArtifactBinding> bindings,
        string? outputPath)
    {
        ArgumentNullException.ThrowIfNull(bindings);
        List<ProtectedPath> protectedPaths =
        [
            .. bindings
                .Where(binding => !VirtualArtifactLocator.IsVirtual(binding.ArtifactId))
                .Select(binding =>
                new ProtectedPath(binding.ArtifactId, $"input artifact '{binding.BindingId}'")),
        ];
        if (!string.IsNullOrWhiteSpace(outputPath))
        {
            protectedPaths.Add(new ProtectedPath(outputPath, "built firmware output"));
        }

        return protectedPaths;
    }

    /// <summary>Rejects a candidate that resolves to any protected path identity.</summary>
    public static void EnsureDoesNotAlias(
        string candidatePath,
        string candidateDescription,
        IEnumerable<ProtectedPath> protectedPaths,
        string parameterName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(candidatePath);
        ArgumentException.ThrowIfNullOrWhiteSpace(candidateDescription);
        ArgumentNullException.ThrowIfNull(protectedPaths);
        ArgumentException.ThrowIfNullOrWhiteSpace(parameterName);

        string candidateFullPath = Path.GetFullPath(candidatePath);
        RejectExistingReparsePoints(candidateFullPath);

        foreach (ProtectedPath protectedPath in protectedPaths)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(protectedPath.Path);
            ArgumentException.ThrowIfNullOrWhiteSpace(protectedPath.Description);

            string protectedFullPath = Path.GetFullPath(protectedPath.Path);
            RejectExistingReparsePoints(protectedFullPath);

            if (string.Equals(candidateFullPath, protectedFullPath, PathComparison) ||
                ExistingFilesShareIdentity(candidateFullPath, protectedFullPath))
            {
                throw new ArgumentException(
                    $"{candidateDescription} must not overwrite {protectedPath.Description}.",
                    parameterName);
            }
        }
    }

    /// <summary>Combines and fully resolves one output directory and file name.</summary>
    public static string CombineFullPath(string directory, string fileName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(directory);
        ArgumentException.ThrowIfNullOrWhiteSpace(fileName);
        return Path.GetFullPath(Path.Combine(directory, fileName));
    }

    private static bool ExistingFilesShareIdentity(
        string candidateFullPath,
        string protectedFullPath)
    {
        if (!File.Exists(candidateFullPath) || !File.Exists(protectedFullPath))
        {
            return false;
        }

        try
        {
            using FileStream candidate = new(
                candidateFullPath,
                FileMode.Open,
                FileAccess.Read,
                FileShare.None);
            try
            {
                using FileStream protectedFile = new(
                    protectedFullPath,
                    FileMode.Open,
                    FileAccess.Read,
                    FileShare.ReadWrite | FileShare.Delete);
                return false;
            }
            catch (IOException)
            {
                return true;
            }
            catch (UnauthorizedAccessException)
            {
                return true;
            }
        }
        catch (Exception exception) when (
            exception is IOException or UnauthorizedAccessException)
        {
            throw new IOException(
                $"Could not verify path identity for '{candidateFullPath}'.",
                exception);
        }
    }

    private static void RejectExistingReparsePoints(string fullPath)
    {
        string? currentPath = File.Exists(fullPath) || Directory.Exists(fullPath)
            ? fullPath
            : Path.GetDirectoryName(fullPath);
        while (!string.IsNullOrWhiteSpace(currentPath))
        {
            if (File.Exists(currentPath) || Directory.Exists(currentPath))
            {
                FileAttributes attributes = File.GetAttributes(currentPath);
                if ((attributes & FileAttributes.ReparsePoint) != 0)
                {
                    throw new UnauthorizedAccessException(
                        "Reparse points are not allowed for protected output paths.");
                }
            }

            currentPath = Directory.GetParent(currentPath)?.FullName;
        }
    }
}
