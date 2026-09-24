using NvtFwCombiner.Application.Ports;

namespace NvtFwCombiner.Infrastructure.Files;

/// <summary>Canonical platform-path identity; reparse targets and hardlink file ids are not resolved.</summary>
internal sealed class FileSystemCompositionArtifactIdentityPolicy :
    ICompositionArtifactIdentityPolicy
{
    public CompositionAcceptedArtifactIdentity Resolve(string artifactLocator)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(artifactLocator);
        string fullPath = Path.GetFullPath(artifactLocator);
        string fileName = Path.GetFileName(fullPath);
        if (string.IsNullOrWhiteSpace(fileName))
        {
            throw new ArgumentException(
                "Accepted artifact locator must identify one filesystem path.",
                nameof(artifactLocator));
        }

        string canonicalIdentity = OperatingSystem.IsWindows()
            ? fullPath.ToUpperInvariant()
            : fullPath;
        return new CompositionAcceptedArtifactIdentity(canonicalIdentity, fileName);
    }
}

/// <summary>Shared filesystem preflight for prepared bundle destinations.</summary>
internal sealed class FileSystemCompositionOutputBundleDestinationValidator :
    ICompositionOutputBundleDestinationValidator
{
    public CompositionOutputBundleValidationIssue? ValidateName(string value)
    {
        string? code = AtomicBundlePathRules.GetWindowsNameIssueCode(value);
        return code is null ? null : new(code, AtomicBundlePathRules.GetWindowsNameIssueMessage(code, "Name"));
    }

    public CompositionOutputBundleDestinationValidation Validate(
        CompositionOutputBundleIntent intent)
    {
        ArgumentNullException.ThrowIfNull(intent);
        List<CompositionOutputBundleValidationIssue> issues = [];
        string? resolvedDirectory = null;
        bool protectedAliasesChecked = intent.Sources.Count == 0;
        try
        {
            string parent = FileSystemPathGuard.ResolveExistingRoot(intent.ParentDirectory);
            ValidateName(intent.FolderName, "Bundle folder name", issues);
            ValidateName(intent.OutputFileName, "Primary output filename", issues);
            if (intent.AdditionalDelivery is { } additional)
            {
                ValidateName(additional.FileName, "Bundle additional-delivery filename", issues);
            }

            foreach (CompositionExecutionBundleSource source in intent.Sources)
            {
                ValidateName(source.Summary.OriginalFileName, "Bundle source filename", issues);
            }

            if (issues.Count == 0)
            {
                resolvedDirectory = ResolveAvailableDirectory(parent, intent.FolderName);
                ValidateName(Path.GetFileName(resolvedDirectory), "Bundle destination folder", issues);
                ValidatePath(resolvedDirectory, issues);
                List<string> artifactNames = AtomicBundlePathRules.AllocateArtifactNames(
                    intent.OutputFileName,
                    intent.AdditionalDelivery is null ? [] : [intent.AdditionalDelivery.FileName],
                    intent.Sources.Select(static source => source.Summary.OriginalFileName));
                ProtectedPathGuard.ProtectedPath[] protectedPaths =
                [
                    .. intent.Sources.Select(static source => new ProtectedPathGuard.ProtectedPath(
                        source.AcceptedIdentity, $"accepted source '{source.Summary.BindingId}'")),
                ];
                foreach (string name in artifactNames.Prepend(intent.OutputFileName))
                {
                    ValidateName(name, "Bundle child filename", issues);
                    string child = Path.Combine(resolvedDirectory, name);
                    ValidatePath(child, issues);
                    try
                    {
                        ProtectedPathGuard.EnsureDoesNotAlias(
                            child,
                            "Bundle child path",
                            protectedPaths,
                            nameof(intent));
                    }
                    catch (Exception exception) when (
                        exception is ArgumentException or IOException or UnauthorizedAccessException)
                    {
                        issues.Add(new CompositionOutputBundleValidationIssue(
                            CompositionOutputBundleValidationIssueCodes.ProtectedAlias,
                            exception.Message));
                    }
                }

                protectedAliasesChecked = true;
            }
        }
        catch (Exception exception) when (
            exception is ArgumentException or IOException or UnauthorizedAccessException)
        {
            issues.Add(new CompositionOutputBundleValidationIssue(
                CompositionOutputBundleValidationIssueCodes.ParentInvalid,
                exception.Message));
        }

        return new CompositionOutputBundleDestinationValidation(
            resolvedDirectory,
            issues,
            protectedAliasesChecked);
    }

    private static string ResolveAvailableDirectory(string parent, string folderName)
    {
        for (int suffix = 1; suffix <= 1000; suffix++)
        {
            string name = suffix == 1 ? folderName : $"{folderName} ({suffix})";
            string candidate = Path.GetFullPath(Path.Combine(parent, name));
            if (!File.Exists(candidate) && !Directory.Exists(candidate))
            {
                return candidate;
            }
        }

        throw new IOException("No available bundle destination exists within the suffix limit.");
    }

    private static void ValidateName(
        string value,
        string label,
        List<CompositionOutputBundleValidationIssue> issues)
    {
        string? issueCode = AtomicBundlePathRules.GetWindowsNameIssueCode(value);
        if (issueCode is not null)
        {
            issues.Add(new CompositionOutputBundleValidationIssue(
                issueCode,
                AtomicBundlePathRules.GetWindowsNameIssueMessage(issueCode, label)));
        }
    }

    private static void ValidatePath(
        string path,
        List<CompositionOutputBundleValidationIssue> issues)
    {
        if (AtomicBundlePathRules.ExceedsSupportedPathLength(path) &&
            !issues.Any(static issue =>
                issue.Code == CompositionOutputBundleValidationIssueCodes.PathTooLong))
        {
            issues.Add(new CompositionOutputBundleValidationIssue(
                CompositionOutputBundleValidationIssueCodes.PathTooLong,
                "Bundle destination exceeds the supported Windows path length."));
        }
    }

}

/// <summary>One shared platform rule owner used by destination preview and atomic commit.</summary>
internal static class AtomicBundlePathRules
{
    private const int MaximumWindowsPathLength = 259;
    private static readonly HashSet<string> WindowsReservedNames = CreateReservedNames();

    /// <summary>Reserves the primary name, then allocates additional and source names; callers validate components.</summary>
    internal static List<string> AllocateArtifactNames(
        string outputFileName,
        IEnumerable<string> additionalFileNames,
        IEnumerable<string> sourceFileNames)
    {
        HashSet<string> allocated = new(StringComparer.OrdinalIgnoreCase) { outputFileName };
        return [.. additionalFileNames.Concat(sourceFileNames)
            .Select(name => AllocateUniqueFileName(name, allocated))];
    }

    private static string AllocateUniqueFileName(string originalFileName, HashSet<string> allocated)
    {
        if (allocated.Add(originalFileName))
        {
            return originalFileName;
        }

        string extension = Path.GetExtension(originalFileName);
        string basename = Path.GetFileNameWithoutExtension(originalFileName);
        for (int suffix = 2; ; suffix++)
        {
            string candidate = $"{basename} ({suffix}){extension}";
            if (allocated.Add(candidate))
            {
                return candidate;
            }
        }
    }

    internal static string? GetWindowsNameIssueCode(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return CompositionOutputBundleValidationIssueCodes.NameInvalid;
        }

        if (value.Length > 255)
        {
            return CompositionOutputBundleValidationIssueCodes.PathTooLong;
        }

        bool isInvalid = value is "." or ".." ||
            value[^1] is ' ' or '.' ||
            value.Any(static character =>
                character is < (char)32 or '<' or '>' or ':' or '"' or '/' or '\\' or '|' or '?' or '*') ||
            !StringComparer.Ordinal.Equals(Path.GetFileName(value), value);
        return isInvalid
            ? CompositionOutputBundleValidationIssueCodes.NameInvalid
            : WindowsReservedNames.Contains(value.Split('.')[0])
                ? CompositionOutputBundleValidationIssueCodes.NameReserved
                : null;
    }

    internal static string GetWindowsNameIssueMessage(string code, string label)
    {
        return code switch
        {
            CompositionOutputBundleValidationIssueCodes.NameReserved => $"{label} uses a reserved Windows device name.",
            CompositionOutputBundleValidationIssueCodes.PathTooLong => $"{label} exceeds 255 UTF-16 code units (including any extension). Shorten the name.",
            _ => $"{label} is not a valid plain Windows name.",
        };
    }

    internal static void EnsureWindowsName(
        string value,
        string description,
        string parameterName)
    {
        string? issueCode = GetWindowsNameIssueCode(value);
        if (issueCode is not null)
        {
            throw new ArgumentException(
                GetWindowsNameIssueMessage(issueCode, description),
                parameterName);
        }
    }

    internal static bool ExceedsSupportedPathLength(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        return path.Length > MaximumWindowsPathLength;
    }

    internal static void EnsureSupportedPathLength(string path, string description)
    {
        if (ExceedsSupportedPathLength(path))
        {
            throw new PathTooLongException(
                $"{description} exceeds the supported Windows path length.");
        }
    }

    private static HashSet<string> CreateReservedNames()
    {
        HashSet<string> names = new(StringComparer.OrdinalIgnoreCase)
        {
            "CON", "PRN", "AUX", "NUL",
        };
        for (int index = 1; index <= 9; index++)
        {
            _ = names.Add($"COM{index}");
            _ = names.Add($"LPT{index}");
        }

        return names;
    }
}
