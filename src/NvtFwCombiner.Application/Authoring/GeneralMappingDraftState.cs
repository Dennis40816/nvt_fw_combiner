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
/// File authoring presets over the same explicit Start + Length operation.
/// They do not introduce execution-time range behavior.
/// </summary>
public enum GeneralMappingFileRangePreset
{
    /// <summary>An explicit, potentially non-zero file source start.</summary>
    SourceSlice,

    /// <summary>Source start is fixed at zero.</summary>
    FromFileStart,
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
        string? inlineValue,
        FileStamp? acceptedFileStamp)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(reference);
        if (kind != GeneralMappingSourceKind.FileArtifact &&
            string.IsNullOrWhiteSpace(inlineValue))
        {
            throw new ArgumentException(
                "Inline General mapping sources require a value.",
                nameof(inlineValue));
        }

        if (kind != GeneralMappingSourceKind.FileArtifact &&
            acceptedFileStamp is not null)
        {
            throw new ArgumentException(
                "Only file-backed General sources can carry an accepted content stamp.",
                nameof(acceptedFileStamp));
        }

        Kind = kind;
        Reference = reference;
        InlineValue = inlineValue;
        AcceptedFileStamp = acceptedFileStamp;
    }

    /// <summary>Closed source kind used by adapters and compilers.</summary>
    public GeneralMappingSourceKind Kind { get; }

    /// <summary>Opaque selected-file reference or stable virtual-source identity.</summary>
    public string Reference { get; }

    /// <summary>Inline hexadecimal text for patch sources, otherwise null.</summary>
    public string? InlineValue { get; }

    /// <summary>
    /// Accepted complete-file content identity. Null means the selected file
    /// still requires explicit inspection.
    /// </summary>
    public FileStamp? AcceptedFileStamp { get; }

    /// <summary>Creates one file-backed source reference.</summary>
    public static GeneralMappingSource File(
        string reference,
        FileStamp? acceptedFileStamp = null)
    {
        return new GeneralMappingSource(
            GeneralMappingSourceKind.FileArtifact,
            reference,
            inlineValue: null,
            acceptedFileStamp);
    }

    /// <summary>Creates one inline hexadecimal overwrite source.</summary>
    public static GeneralMappingSource HexOverwrite(
        string value,
        string reference = "inline-overwrite")
    {
        return new GeneralMappingSource(
            GeneralMappingSourceKind.HexOverwrite,
            reference,
            value,
            acceptedFileStamp: null);
    }

    /// <summary>Creates one inline hexadecimal fill source.</summary>
    public static GeneralMappingSource HexFill(
        string value,
        string reference = "inline-fill")
    {
        return new GeneralMappingSource(
            GeneralMappingSourceKind.HexFill,
            reference,
            value,
            acceptedFileStamp: null);
    }

    /// <summary>Returns the same selected reference bound to accepted bytes.</summary>
    public GeneralMappingSource WithAcceptedFileStamp(FileStamp fileStamp)
    {
        return Kind == GeneralMappingSourceKind.FileArtifact
            ? File(Reference, fileStamp)
            : throw new InvalidOperationException(
                "Only file-backed General sources can accept a content stamp.");
    }

    /// <summary>Returns a newly selected file reference pending inspection.</summary>
    public GeneralMappingSource RebindSelectedFile(string selectedPath)
    {
        return Kind == GeneralMappingSourceKind.FileArtifact
            ? File(selectedPath)
            : throw new InvalidOperationException(
                "Only file-backed General sources can be rebound.");
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
        OperationProvenance? provenance = null,
        GeneralMappingFileRangePreset? fileRangePreset = null)
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

        if (source.Kind != GeneralMappingSourceKind.FileArtifact &&
            fileRangePreset is not null)
        {
            throw new ArgumentException(
                "Only file-backed General mappings can declare a file range preset.",
                nameof(fileRangePreset));
        }

        GeneralMappingFileRangePreset? effectiveFileRangePreset =
            source.Kind == GeneralMappingSourceKind.FileArtifact
                ? fileRangePreset ??
                    (sourceRange.Start == 0
                        ? GeneralMappingFileRangePreset.FromFileStart
                        : GeneralMappingFileRangePreset.SourceSlice)
                : null;
        if (effectiveFileRangePreset ==
                GeneralMappingFileRangePreset.FromFileStart &&
            sourceRange.Start != 0)
        {
            throw new ArgumentException(
                "From File Start requires source start zero.",
                nameof(fileRangePreset));
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
        FileRangePreset = effectiveFileRangePreset;
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

    /// <summary>
    /// Authoring preset inferred from the concrete source start. Both presets
    /// compile through this row's same explicit operation.
    /// </summary>
    public GeneralMappingFileRangePreset? FileRangePreset { get; }

    /// <summary>Derived read-only inclusive target end for display adapters.</summary>
    public long TargetEndInclusive =>
        AuthoringByteRangeCodec.GetEndInclusive(TargetRange);

    /// <summary>Returns this row bound to one accepted complete-file stamp.</summary>
    public GeneralMappingDraftRow WithAcceptedFileStamp(FileStamp fileStamp)
    {
        return new GeneralMappingDraftRow(
            MappingId,
            OperationKind,
            Source.WithAcceptedFileStamp(fileStamp),
            SourceRange,
            TargetAddressSpaceId,
            TargetRange,
            OverlapPolicy,
            Alignment,
            Reason,
            TargetRegionId,
            Provenance,
            FileRangePreset);
    }

    /// <summary>Returns this row rebound to a selected file pending inspection.</summary>
    public GeneralMappingDraftRow RebindSelectedFile(string selectedPath)
    {
        return new GeneralMappingDraftRow(
            MappingId,
            OperationKind,
            Source.RebindSelectedFile(selectedPath),
            SourceRange,
            TargetAddressSpaceId,
            TargetRange,
            OverlapPolicy,
            Alignment,
            Reason,
            TargetRegionId,
            Provenance,
            FileRangePreset);
    }

    internal GeneralMappingDraftRow WithLength(long length)
    {
        return new GeneralMappingDraftRow(
            MappingId,
            OperationKind,
            Source,
            new ByteRange(SourceRange.Start, length),
            TargetAddressSpaceId,
            new ByteRange(TargetRange.Start, length),
            OverlapPolicy,
            Alignment,
            Reason,
            TargetRegionId,
            Provenance,
            FileRangePreset);
    }
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

    /// <summary>
    /// Materializes the currently accepted full file length into one From File
    /// Start row. Reload never reapplies this helper.
    /// </summary>
    public GeneralMappingDraftState MaterializeFullFileLength(string mappingId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(mappingId);
        GeneralMappingDraftRow selected = _rows.SingleOrDefault(row =>
            StringComparer.Ordinal.Equals(row.MappingId, mappingId)) ??
            throw new ArgumentException(
                "The General mapping draft does not contain the requested row.",
                nameof(mappingId));
        FileStamp stamp =
            selected.Source.Kind == GeneralMappingSourceKind.FileArtifact &&
            selected.FileRangePreset ==
                GeneralMappingFileRangePreset.FromFileStart &&
            selected.Source.AcceptedFileStamp is { } accepted
                ? accepted
                : throw new InvalidOperationException(
                    "Use full file length requires an inspected From File Start row.");

        return stamp.AcceptedLength > 0
            ? new GeneralMappingDraftState(
                _rows.Select(row =>
                    ReferenceEquals(row, selected)
                        ? row.WithLength(stamp.AcceptedLength)
                        : row))
            : throw new InvalidOperationException(
                "Use full file length requires a non-empty accepted file.");
    }

    internal override AuthoringDraftState CreateImmutableSnapshot()
    {
        return this;
    }
}
