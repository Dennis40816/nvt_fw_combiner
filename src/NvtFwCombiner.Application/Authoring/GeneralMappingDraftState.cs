using NvtFwCombiner.Domain.Composition;

namespace NvtFwCombiner.Application.Authoring;

/// <summary>Closed source kinds accepted by General Merge and General Replace authoring.</summary>
public enum GeneralMappingSourceKind
{
    /// <summary>Bytes come from one caller-selected file or artifact binding.</summary>
    FileArtifact,

    /// <summary>Bytes come from one validated inline hexadecimal overwrite.</summary>
    HexOverwrite,

    /// <summary>Bytes are materialized by repeating one validated hexadecimal byte.</summary>
    HexFill,
}

/// <summary>
/// Immutable source identity for one General mapping row. The Application
/// retains references and authoring payload text, never filesystem bytes.
/// </summary>
public sealed record GeneralMappingSource
{
    private GeneralMappingSource(
        GeneralMappingSourceKind kind,
        string reference,
        string? inlineValue)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(reference);
        if (kind != GeneralMappingSourceKind.FileArtifact &&
            string.IsNullOrWhiteSpace(inlineValue))
        {
            throw new ArgumentException(
                "Inline General mapping sources require a value.",
                nameof(inlineValue));
        }

        Kind = kind;
        Reference = reference;
        InlineValue = inlineValue;
    }

    /// <summary>Closed source kind used by adapters and compilers.</summary>
    public GeneralMappingSourceKind Kind { get; }

    /// <summary>Opaque selected-file reference or stable virtual-source identity.</summary>
    public string Reference { get; }

    /// <summary>Inline hexadecimal text for patch sources, otherwise null.</summary>
    public string? InlineValue { get; }

    /// <summary>Creates one file-backed source reference.</summary>
    public static GeneralMappingSource File(string reference)
    {
        return new GeneralMappingSource(
            GeneralMappingSourceKind.FileArtifact,
            reference,
            inlineValue: null);
    }

    /// <summary>Creates one inline hexadecimal overwrite source.</summary>
    public static GeneralMappingSource HexOverwrite(
        string value,
        string reference = "inline-overwrite")
    {
        return new GeneralMappingSource(
            GeneralMappingSourceKind.HexOverwrite,
            reference,
            value);
    }

    /// <summary>Creates one inline hexadecimal fill source.</summary>
    public static GeneralMappingSource HexFill(
        string value,
        string reference = "inline-fill")
    {
        return new GeneralMappingSource(
            GeneralMappingSourceKind.HexFill,
            reference,
            value);
    }
}

/// <summary>
/// One accepted typed General mapping row. Ranges are canonical half-open
/// values; operation order is the row order inside its draft.
/// </summary>
public sealed record GeneralMappingDraftRow
{
    /// <summary>Creates one locally valid typed mapping row.</summary>
    public GeneralMappingDraftRow(
        string mappingId,
        ExplicitMappingOperationKind operationKind,
        GeneralMappingSource source,
        ByteRange sourceRange,
        string targetAddressSpaceId,
        ByteRange targetRange,
        OverlapPolicy overlapPolicy,
        int alignment,
        string reason,
        string? targetRegionId = null,
        OperationProvenance? provenance = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(mappingId);
        ArgumentNullException.ThrowIfNull(source);
        ArgumentException.ThrowIfNullOrWhiteSpace(targetAddressSpaceId);
        ArgumentException.ThrowIfNullOrWhiteSpace(reason);
        if (!Enum.IsDefined(operationKind))
        {
            throw new ArgumentOutOfRangeException(
                nameof(operationKind),
                operationKind,
                "Unknown explicit mapping operation kind.");
        }

        if (!Enum.IsDefined(overlapPolicy))
        {
            throw new ArgumentOutOfRangeException(
                nameof(overlapPolicy),
                overlapPolicy,
                "Unknown mapping overlap policy.");
        }

        if (alignment <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(alignment),
                alignment,
                "Alignment must be positive.");
        }

        if (sourceRange.Length != targetRange.Length)
        {
            throw new ArgumentException(
                "Source and target mapping lengths must match.",
                nameof(targetRange));
        }

        if (targetRange.Start % alignment != 0)
        {
            throw new ArgumentException(
                "Target start must satisfy the mapping alignment.",
                nameof(targetRange));
        }

        if (operationKind == ExplicitMappingOperationKind.CopyRange &&
            source.Kind != GeneralMappingSourceKind.FileArtifact)
        {
            throw new ArgumentException(
                "Copy mappings require a file-backed source.",
                nameof(source));
        }

        if (source.Kind != GeneralMappingSourceKind.FileArtifact &&
            (operationKind != ExplicitMappingOperationKind.ReplaceRange ||
             sourceRange.Start != 0))
        {
            throw new ArgumentException(
                "Inline patch sources require ReplaceRange and source offset zero.",
                nameof(source));
        }

        MappingId = mappingId;
        OperationKind = operationKind;
        Source = source;
        SourceRange = sourceRange;
        TargetAddressSpaceId = targetAddressSpaceId;
        TargetRegionId = string.IsNullOrWhiteSpace(targetRegionId)
            ? null
            : targetRegionId;
        TargetRange = targetRange;
        OverlapPolicy = overlapPolicy;
        Alignment = alignment;
        Reason = reason;
        Provenance = provenance ??
            OperationProvenance.RuntimeGeneralMapping(mappingId);
    }

    /// <summary>Stable operation/mapping identity.</summary>
    public string MappingId { get; }

    /// <summary>Normal composition operation selected by the workflow.</summary>
    public ExplicitMappingOperationKind OperationKind { get; }

    /// <summary>Typed source role and immutable source reference.</summary>
    public GeneralMappingSource Source { get; }

    /// <summary>Half-open source range.</summary>
    public ByteRange SourceRange { get; }

    /// <summary>Named target address space.</summary>
    public string TargetAddressSpaceId { get; }

    /// <summary>Optional canonical target-region reference.</summary>
    public string? TargetRegionId { get; }

    /// <summary>Half-open target range.</summary>
    public ByteRange TargetRange { get; }

    /// <summary>Declared overlap policy preserved through compilation.</summary>
    public OverlapPolicy OverlapPolicy { get; }

    /// <summary>Required target alignment.</summary>
    public int Alignment { get; }

    /// <summary>Reviewable reason for this explicit mapping.</summary>
    public string Reason { get; }

    /// <summary>Traceable mapping or saved-rule origin.</summary>
    public OperationProvenance Provenance { get; }

    /// <summary>Derived read-only inclusive target end for display adapters.</summary>
    public long TargetEndInclusive =>
        AuthoringByteRangeCodec.GetEndInclusive(TargetRange);
}

/// <summary>
/// Deeply immutable typed draft shared by General Merge and General Replace
/// authoring adapters before validation and compilation.
/// </summary>
public sealed record GeneralMappingDraftState : AuthoringDraftState
{
    private readonly GeneralMappingDraftRow[] _rows;

    /// <summary>Creates one draft with unique stable mapping ids.</summary>
    public GeneralMappingDraftState(IEnumerable<GeneralMappingDraftRow> rows)
        : base(AuthoringDraftKind.GeneralMapping)
    {
        ArgumentNullException.ThrowIfNull(rows);
        _rows = [.. rows];
        if (_rows.Any(static row => row is null))
        {
            throw new ArgumentException(
                "General mapping draft rows cannot be null.",
                nameof(rows));
        }

        if (_rows.Select(static row => row.MappingId)
            .Distinct(StringComparer.Ordinal).Count() != _rows.Length)
        {
            throw new ArgumentException(
                "General mapping draft ids must be unique.",
                nameof(rows));
        }

        Rows = Array.AsReadOnly(_rows);
    }

    /// <summary>Ordered typed mapping rows; row order determines operation order.</summary>
    public IReadOnlyList<GeneralMappingDraftRow> Rows { get; }

    internal override AuthoringDraftState CreateImmutableSnapshot()
    {
        return this;
    }
}
