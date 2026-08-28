namespace NvtFwCombiner.Application.Composition;

/// <summary>Stable issue codes for one host bundle destination.</summary>
public static class CompositionOutputBundleValidationIssueCodes
{
    /// <summary>The parent directory is absent or unsafe.</summary>
    public const string ParentInvalid = "bundle-destination.parent.invalid";
    /// <summary>The requested folder name contains invalid syntax.</summary>
    public const string NameInvalid = "bundle-destination.name.invalid";
    /// <summary>The requested folder name is a reserved device name.</summary>
    public const string NameReserved = "bundle-destination.name.reserved";
    /// <summary>The directory or one child exceeds the supported path limit.</summary>
    public const string PathTooLong = "bundle-destination.path.too-long";
    /// <summary>A proposed child aliases one immutable accepted source.</summary>
    public const string ProtectedAlias = "bundle-destination.protected-alias";
}

/// <summary>One inline-safe destination validation issue.</summary>
public sealed record CompositionOutputBundleValidationIssue(
    string Code,
    string Message);

/// <summary>Typed preflight result shared by host UI and commit.</summary>
public sealed class CompositionOutputBundleDestinationValidation
{
    /// <summary>Creates one immutable validation result.</summary>
    public CompositionOutputBundleDestinationValidation(
        string? resolvedDirectoryPreview,
        IReadOnlyList<CompositionOutputBundleValidationIssue> issues,
        bool protectedInputAliasesChecked)
    {
        ArgumentNullException.ThrowIfNull(issues);
        ResolvedDirectoryPreview = resolvedDirectoryPreview;
        Issues = Array.AsReadOnly([.. issues]);
        ProtectedInputAliasesChecked = protectedInputAliasesChecked;
    }

    /// <summary>True when destination and every child pass current platform preflight.</summary>
    public bool IsValid => Issues.Count == 0;

    /// <summary>Current suffix-resolved destination preview when one can be resolved safely.</summary>
    public string? ResolvedDirectoryPreview { get; }

    /// <summary>Stable inline validation issues.</summary>
    public IReadOnlyList<CompositionOutputBundleValidationIssue> Issues { get; }

    /// <summary>Evidence that every accepted source identity participated in alias preflight.</summary>
    public bool ProtectedInputAliasesChecked { get; }
}
