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
            ValidateName(intent.FolderName, issues);
            if (issues.Count == 0)
            {
                resolvedDirectory = ResolveAvailableDirectory(parent, intent.FolderName);
                ValidatePath(resolvedDirectory, issues);
                ValidatePath(
                    Path.Combine(resolvedDirectory, intent.OutputFileName),
                    issues);
                foreach (CompositionExecutionBundleSource source in intent.Sources)
                {
                    string child = Path.Combine(resolvedDirectory, source.Summary.OriginalFileName);
                    ValidatePath(child, issues);
                    try
                    {
                        ProtectedPathGuard.EnsureDoesNotAlias(
                            child,
                            "Bundle child path",
                            [new ProtectedPathGuard.ProtectedPath(
                                source.AcceptedIdentity,
                                $"accepted source '{source.Summary.BindingId}'")],
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
        List<CompositionOutputBundleValidationIssue> issues)
    {
        string? issueCode = AtomicBundlePathRules.GetWindowsNameIssueCode(value);
        if (issueCode is not null)
        {
            issues.Add(new CompositionOutputBundleValidationIssue(
                issueCode,
                issueCode == CompositionOutputBundleValidationIssueCodes.NameReserved
                    ? "Bundle folder name uses a reserved Windows device name."
                    : "Bundle folder name is not a valid plain Windows name."));
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

    internal static string? GetWindowsNameIssueCode(string value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value);
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

    internal static void EnsureWindowsName(
        string value,
        string description,
        string parameterName)
    {
        string? issueCode = GetWindowsNameIssueCode(value);
        if (issueCode is not null)
        {
            throw new ArgumentException(
                issueCode == CompositionOutputBundleValidationIssueCodes.NameReserved
                    ? $"{description} uses a reserved Windows device name."
                    : $"{description} is not a valid plain Windows name.",
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
