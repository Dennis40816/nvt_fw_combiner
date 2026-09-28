namespace NvtFwCombiner.Application.Diagnostics;

/// <summary>The completed process-lifetime built-in admission decision, never an inferred file state.</summary>
public enum BuiltInProfileAdmissionSource
{
    /// <summary>All built-in documents passed prebuilt acceptance.</summary>
    Prebuilt,
    /// <summary>The existing JSON admission path was selected.</summary>
    Json,
}

/// <summary>Closed, path-free rejection categories in acceptance-check order.</summary>
public enum BuiltInProfileAdmissionRejectionReason
{
    /// <summary>The pack is absent.</summary>
    Missing,
    /// <summary>The pack cannot be read.</summary>
    FileAccess,
    /// <summary>The file exceeds its bound.</summary>
    FileBound,
    /// <summary>The container format is invalid.</summary>
    Format,
    /// <summary>The body digest is invalid.</summary>
    BodyIntegrity,
    /// <summary>The exact trust index does not match.</summary>
    TrustIndexMismatch,
    /// <summary>The admitted bundle set does not match.</summary>
    BundleSetMismatch,
    /// <summary>The exact manifest set does not match.</summary>
    ManifestSetMismatch,
    /// <summary>A carried manifest failed admission.</summary>
    ManifestAdmission,
    /// <summary>The carried document set does not match.</summary>
    DocumentSetMismatch,
    /// <summary>A carried document digest is invalid.</summary>
    DocumentIntegrity,
    /// <summary>An entry violates the admission limits.</summary>
    EntryLimits,
}

/// <summary>Immutable observation only; it cannot authorize loading or constructing bundles.</summary>
public sealed record BuiltInProfileAdmission
{
    /// <summary>Creates a closed source/reason fact; an accepted pack cannot have a rejection.</summary>
    public BuiltInProfileAdmission(BuiltInProfileAdmissionSource source,
        BuiltInProfileAdmissionRejectionReason? rejectionReason = null)
    {
        if (!Enum.IsDefined(source))
        {
            throw new ArgumentOutOfRangeException(nameof(source));
        }
        if (rejectionReason is { } reason && !Enum.IsDefined(reason))
        {
            throw new ArgumentOutOfRangeException(nameof(rejectionReason));
        }
        if (source == BuiltInProfileAdmissionSource.Prebuilt && rejectionReason is not null)
        {
            throw new ArgumentException("Prebuilt admission cannot have a rejection.", nameof(rejectionReason));
        }
        Source = source;
        RejectionReason = rejectionReason;
    }

    /// <summary>Actual source selected by the registry.</summary>
    public BuiltInProfileAdmissionSource Source { get; }
    /// <summary>Optional path-free rejection reason.</summary>
    public BuiltInProfileAdmissionRejectionReason? RejectionReason { get; }
    /// <summary>Stable protocol token.</summary>
    public string SourceToken => Source == BuiltInProfileAdmissionSource.Prebuilt ? "prebuilt" : "json";
    /// <summary>Stable closed rejection token.</summary>
    public string? RejectionCode => RejectionReason switch
    {
        null => null,
        BuiltInProfileAdmissionRejectionReason.Missing => "missing",
        BuiltInProfileAdmissionRejectionReason.FileAccess => "file-access",
        BuiltInProfileAdmissionRejectionReason.FileBound => "file-bound",
        BuiltInProfileAdmissionRejectionReason.Format => "format",
        BuiltInProfileAdmissionRejectionReason.BodyIntegrity => "body-integrity",
        BuiltInProfileAdmissionRejectionReason.TrustIndexMismatch => "trust-index-mismatch",
        BuiltInProfileAdmissionRejectionReason.BundleSetMismatch => "bundle-set-mismatch",
        BuiltInProfileAdmissionRejectionReason.ManifestSetMismatch => "manifest-set-mismatch",
        BuiltInProfileAdmissionRejectionReason.ManifestAdmission => "manifest-admission",
        BuiltInProfileAdmissionRejectionReason.DocumentSetMismatch => "document-set-mismatch",
        BuiltInProfileAdmissionRejectionReason.DocumentIntegrity => "document-integrity",
        BuiltInProfileAdmissionRejectionReason.EntryLimits => "entry-limits",
        _ => throw new InvalidOperationException("Invalid admission observation."),
    };
}
