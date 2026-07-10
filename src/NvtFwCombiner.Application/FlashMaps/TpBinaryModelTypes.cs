using NvtFwCombiner.Domain.Composition;

namespace NvtFwCombiner.Application.FlashMaps;

/// <summary>Stable category ids for the top-level TP flash image model.</summary>
public static class TpBinaryCategoryIds
{
    /// <summary>Primary and copied TP flash-header structures.</summary>
    public const string TpFlashHeader = "tp-flash-header";

    /// <summary>FW Config primary metadata and documented backup regions.</summary>
    public const string FirmwareConfiguration = "firmware-configuration";

    /// <summary>CtrlRAM payload regions.</summary>
    public const string CtrlRam = "ctrlram";

    /// <summary>Display/DP payload regions.</summary>
    public const string Display = "display";

    /// <summary>Project identity regions.</summary>
    public const string ProjectIdentity = "project-identity";

    /// <summary>Customer or production information regions.</summary>
    public const string CustomerInformation = "customer-information";

    /// <summary>FW Information regions exposed to the host or protected by the profile.</summary>
    public const string FirmwareInformation = "firmware-information";

    /// <summary>Remaining TP Overview rows without a more specific category.</summary>
    public const string OtherDocumentedRegion = "other-documented-region";

    /// <summary>Stable display and serialization order for every supported IC.</summary>
    public static IReadOnlyList<string> All { get; } =
    [
        TpFlashHeader,
        FirmwareConfiguration,
        CtrlRam,
        Display,
        ProjectIdentity,
        CustomerInformation,
        FirmwareInformation,
        OtherDocumentedRegion,
    ];
}

/// <summary>Evidence status for a TP header layout projection.</summary>
public enum TpHeaderModelStatus
{
    /// <summary>Fields come directly from a named TDDI Flash Header workbook worksheet.</summary>
    Workbook,

    /// <summary>Only fields common to several workbook variants are represented.</summary>
    WorkbookCommonFields,

    /// <summary>The layout is inherited through an explicitly documented IC alias.</summary>
    DocumentedAlias,
}

/// <summary>One top-level TP flash image model for a selectable IC.</summary>
public sealed class TpBinaryModel
{
    /// <summary>Stable root id used by every TP flash image model.</summary>
    public const string DefaultRootId = "tp-flash-image";

    private readonly TpBinaryCategory[] _categories;

    /// <summary>Creates a TP flash image model.</summary>
    public TpBinaryModel(
        string icId,
        string rootDisplayName,
        IEnumerable<TpBinaryCategory> categories,
        string evidence)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(icId);
        ArgumentException.ThrowIfNullOrWhiteSpace(rootDisplayName);
        ArgumentNullException.ThrowIfNull(categories);
        ArgumentException.ThrowIfNullOrWhiteSpace(evidence);

        _categories = [.. categories];
        if (_categories.Length != TpBinaryCategoryIds.All.Count ||
            !_categories.Select(category => category.CategoryId).SequenceEqual(TpBinaryCategoryIds.All, StringComparer.Ordinal))
        {
            throw new ArgumentException(
                "A TP binary model must expose every category once in the stable category order.",
                nameof(categories));
        }

        IcId = icId;
        RootId = DefaultRootId;
        RootDisplayName = rootDisplayName;
        Evidence = evidence;
    }

    /// <summary>IC id represented by the model.</summary>
    public string IcId { get; }

    /// <summary>Stable top-level node id.</summary>
    public string RootId { get; }

    /// <summary>Human-facing root node label.</summary>
    public string RootDisplayName { get; }

    /// <summary>Evidence for the category projection.</summary>
    public string Evidence { get; }

    /// <summary>Top-level categories in stable display order.</summary>
    public IReadOnlyList<TpBinaryCategory> Categories => _categories;
}

/// <summary>One category beneath a TP flash image model.</summary>
public sealed class TpBinaryCategory
{
    private readonly TpFlashMapRegion[] _regions;
    private readonly TpBinaryAddressAnchor[] _anchors;

    /// <summary>Creates a TP binary category.</summary>
    public TpBinaryCategory(
        string categoryId,
        string displayName,
        IEnumerable<TpFlashMapRegion>? regions = null,
        IEnumerable<TpBinaryAddressAnchor>? anchors = null,
        TpHeaderLayout? headerLayout = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(categoryId);
        ArgumentException.ThrowIfNullOrWhiteSpace(displayName);

        CategoryId = categoryId;
        DisplayName = displayName;
        _regions = regions is null ? [] : [.. regions];
        _anchors = anchors is null ? [] : [.. anchors];
        HeaderLayout = headerLayout;
    }

    /// <summary>Stable category id.</summary>
    public string CategoryId { get; }

    /// <summary>Human-facing category label.</summary>
    public string DisplayName { get; }

    /// <summary>TP Overview regions classified beneath this category.</summary>
    public IReadOnlyList<TpFlashMapRegion> Regions => _regions;

    /// <summary>Documented address anchors that do not claim an unknown range length.</summary>
    public IReadOnlyList<TpBinaryAddressAnchor> Anchors => _anchors;

    /// <summary>Header layout fields when this category represents the TP flash header.</summary>
    public TpHeaderLayout? HeaderLayout { get; }

    /// <summary>True when the category has a header layout, a region, or an address anchor.</summary>
    public bool HasDocumentedContent => HeaderLayout is not null || _regions.Length > 0 || _anchors.Length > 0;
}

/// <summary>A documented address without an inferred range length.</summary>
public sealed class TpBinaryAddressAnchor
{
    /// <summary>Creates an address anchor.</summary>
    public TpBinaryAddressAnchor(string anchorId, string displayName, long address)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(anchorId);
        ArgumentException.ThrowIfNullOrWhiteSpace(displayName);
        ArgumentOutOfRangeException.ThrowIfNegative(address);

        AnchorId = anchorId;
        DisplayName = displayName;
        Address = address;
    }

    /// <summary>Stable anchor id.</summary>
    public string AnchorId { get; }

    /// <summary>Human-facing anchor label.</summary>
    public string DisplayName { get; }

    /// <summary>Absolute flash address.</summary>
    public long Address { get; }
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
