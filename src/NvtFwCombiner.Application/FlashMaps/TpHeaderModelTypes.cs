using NvtFwCombiner.Domain.Composition;

namespace NvtFwCombiner.Application.FlashMaps;

/// <summary>Stable category ids emitted by TP output-difference semantics.</summary>
public static class TpSemanticCategoryIds
{
    /// <summary>Primary and copied TP flash-header structures.</summary>
    public const string TpFlashHeader = "tp-flash-header";

    /// <summary>FW Config primary metadata and documented backup regions.</summary>
    public const string FirmwareConfiguration = "firmware-configuration";

    /// <summary>CtrlRAM payload regions.</summary>
    public const string CtrlRam = "ctrlram";

    /// <summary>Remaining TP Overview rows without a more specific category.</summary>
    public const string OtherDocumentedRegion = "other-documented-region";
}

/// <summary>Evidence status for a TP header layout projection.</summary>
public enum TpHeaderModelStatus
{
    /// <summary>Fields come directly from a named TDDI Flash Header workbook worksheet.</summary>
    Workbook,

    /// <summary>
    /// The workbook establishes the descriptor pattern and an approved postbuild plan establishes its continued
    /// source coverage. This is inspection/report evidence only.
    /// </summary>
    WorkbookWithPostbuildContinuation,

    /// <summary>Only fields common to several workbook variants are represented.</summary>
    WorkbookCommonFields,

    /// <summary>The layout is inherited through an explicitly documented IC alias.</summary>
    DocumentedAlias,
}

/// <summary>One named field in a TP flash-header layout.</summary>
public sealed class TpHeaderField
{
    /// <summary>Creates a TP header field.</summary>
    public TpHeaderField(string fieldId, string displayName, ByteRange range)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(fieldId);
        ArgumentException.ThrowIfNullOrWhiteSpace(displayName);

        FieldId = fieldId;
        DisplayName = displayName;
        Range = range;
    }

    /// <summary>Stable field id within a header layout.</summary>
    public string FieldId { get; }

    /// <summary>Human-facing field label.</summary>
    public string DisplayName { get; }

    /// <summary>Absolute half-open field range in the TP image.</summary>
    public ByteRange Range { get; }
}

/// <summary>One IC-family TP header layout used only for semantic reporting and inspection.</summary>
public sealed class TpHeaderLayout
{
    private readonly ByteRange[] _ranges;
    private readonly TpHeaderField[] _fields;

    /// <summary>Creates a TP header layout.</summary>
    public TpHeaderLayout(
        string layoutId,
        string displayName,
        TpHeaderModelStatus status,
        IEnumerable<ByteRange> ranges,
        IEnumerable<TpHeaderField> fields,
        string evidence)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(layoutId);
        ArgumentException.ThrowIfNullOrWhiteSpace(displayName);
        ArgumentNullException.ThrowIfNull(ranges);
        ArgumentNullException.ThrowIfNull(fields);
        ArgumentException.ThrowIfNullOrWhiteSpace(evidence);

        _ranges = [.. ranges];
        _fields = [.. fields.OrderBy(field => field.Range.Start).ThenBy(field => field.Range.Length)];
        if (_ranges.Length == 0 || _fields.Length == 0)
        {
            throw new ArgumentException("A TP header layout requires documented ranges and fields.");
        }

        if (_fields.Select(field => field.FieldId).Distinct(StringComparer.Ordinal).Count() != _fields.Length)
        {
            throw new ArgumentException("TP header field ids must be unique.", nameof(fields));
        }

        if (_fields.Any(field => !_ranges.Any(range => range.Contains(field.Range))))
        {
            throw new ArgumentException("Every TP header field must be inside a documented header range.", nameof(fields));
        }

        for (int index = 1; index < _fields.Length; index++)
        {
            if (_fields[index - 1].Range.Overlaps(_fields[index].Range))
            {
                throw new ArgumentException("TP header fields cannot overlap.", nameof(fields));
            }
        }

        LayoutId = layoutId;
        DisplayName = displayName;
        Status = status;
        Evidence = evidence;
    }

    /// <summary>Stable layout id.</summary>
    public string LayoutId { get; }

    /// <summary>Human-facing layout label.</summary>
    public string DisplayName { get; }

    /// <summary>Evidence confidence for this layout.</summary>
    public TpHeaderModelStatus Status { get; }

    /// <summary>Workbook or alias evidence supporting the layout.</summary>
    public string Evidence { get; }

    /// <summary>Absolute header ranges that this layout documents.</summary>
    public IReadOnlyList<ByteRange> Ranges => _ranges;

    /// <summary>Named documented fields in address order.</summary>
    public IReadOnlyList<TpHeaderField> Fields => _fields;

    /// <summary>Finds the single documented field that fully contains a changed range.</summary>
    public bool TryFindField(ByteRange range, out TpHeaderField? field)
    {
        field = _fields.SingleOrDefault(candidate => candidate.Range.Contains(range));
        return field is not null;
    }
}
