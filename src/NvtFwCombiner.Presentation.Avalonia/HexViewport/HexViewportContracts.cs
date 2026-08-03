using System.Collections.ObjectModel;

namespace NvtFwCombiner.Presentation.Avalonia.HexViewport;

[Flags]
internal enum HexViewportColumn
{
    None = 0,
    Address = 1 << 0,
    Hexadecimal = 1 << 1,
    Ascii = 1 << 2,
}

[Flags]
internal enum HexViewportInteraction
{
    None = 0,
    Inspect = 1 << 0,
    Select = 1 << 1,
    Overwrite = 1 << 2,
    StructuralEdit = 1 << 3,
}

internal enum HexViewportComparison
{
    None,
    OptionalOriginalRows,
}

[Flags]
internal enum HexViewportNavigation
{
    None = 0,
    AddressJump = 1 << 0,
    DocumentScroll = 1 << 1,
    SemanticRanges = 1 << 2,
    RangeScroll = 1 << 3,
}

[Flags]
internal enum HexViewportDecorationCapability
{
    None = 0,
    DataChange = 1 << 0,
    StructuralChange = 1 << 1,
    Search = 1 << 2,
    SemanticVerdict = 1 << 3,
}

/// <summary>Closed, validated rendering and interaction capabilities for one Hex viewport host.</summary>
internal sealed record HexViewportCapabilityProfile
{
    private HexViewportCapabilityProfile(
        string id,
        HexViewportColumn columns,
        HexViewportInteraction interaction,
        HexViewportComparison comparison,
        HexViewportNavigation navigation,
        HexViewportDecorationCapability decorations,
        int initialRows,
        int maximumRows)
    {
        if (string.IsNullOrWhiteSpace(id))
        {
            throw new ArgumentException("A Hex viewport profile id is required.", nameof(id));
        }

        if (columns != (HexViewportColumn.Address | HexViewportColumn.Hexadecimal | HexViewportColumn.Ascii))
        {
            throw new ArgumentOutOfRangeException(nameof(columns), "The initial viewport requires address, hexadecimal, and ASCII columns.");
        }

        if (initialRows <= 0 || maximumRows < initialRows)
        {
            throw new ArgumentOutOfRangeException(nameof(initialRows), "The row budget must be positive and bounded.");
        }

        Id = id;
        Columns = columns;
        Interaction = interaction;
        Comparison = comparison;
        Navigation = navigation;
        Decorations = decorations;
        InitialRows = initialRows;
        MaximumRows = maximumRows;
    }

    /// <summary>The exact profile used by the standalone Raw Hex Editor host.</summary>
    public static HexViewportCapabilityProfile RawEditor { get; } = new(
        "RawEditor",
        HexViewportColumn.Address | HexViewportColumn.Hexadecimal | HexViewportColumn.Ascii,
        HexViewportInteraction.Inspect |
        HexViewportInteraction.Select |
        HexViewportInteraction.Overwrite |
        HexViewportInteraction.StructuralEdit,
        HexViewportComparison.OptionalOriginalRows,
        HexViewportNavigation.AddressJump | HexViewportNavigation.DocumentScroll,
        HexViewportDecorationCapability.DataChange |
        HexViewportDecorationCapability.StructuralChange |
        HexViewportDecorationCapability.Search,
        initialRows: 12,
        maximumRows: 28);

    /// <summary>The exact profile used by the persisted Report Diff host.</summary>
    public static HexViewportCapabilityProfile ReportDiff { get; } = new(
        "ReportDiff",
        HexViewportColumn.Address | HexViewportColumn.Hexadecimal | HexViewportColumn.Ascii,
        HexViewportInteraction.Inspect | HexViewportInteraction.Select,
        HexViewportComparison.OptionalOriginalRows,
        HexViewportNavigation.SemanticRanges | HexViewportNavigation.RangeScroll,
        HexViewportDecorationCapability.DataChange | HexViewportDecorationCapability.SemanticVerdict,
        initialRows: 12,
        maximumRows: 28);

    /// <summary>The exact profile used by resolved-metadata BIN inspection hosts.</summary>
    public static HexViewportCapabilityProfile BinInspector { get; } = new(
        "BinInspector",
        HexViewportColumn.Address | HexViewportColumn.Hexadecimal | HexViewportColumn.Ascii,
        HexViewportInteraction.Inspect | HexViewportInteraction.Select,
        HexViewportComparison.None,
        HexViewportNavigation.SemanticRanges | HexViewportNavigation.RangeScroll,
        HexViewportDecorationCapability.None,
        initialRows: 12,
        maximumRows: 28);

    public string Id { get; }

    public HexViewportColumn Columns { get; }

    public HexViewportInteraction Interaction { get; }

    public HexViewportComparison Comparison { get; }

    public HexViewportNavigation Navigation { get; }

    public HexViewportDecorationCapability Decorations { get; }

    public int InitialRows { get; }

    public int MaximumRows { get; }
}

[Flags]
internal enum HexViewportCellDecoration
{
    None = 0,
    DataChange = 1 << 0,
    StructuralChange = 1 << 1,
    StructuralBoundaryStart = 1 << 2,
    StructuralBoundaryEnd = 1 << 3,
    Search = 1 << 4,
    HistoryFeedback = 1 << 5,
}

/// <summary>One immutable numeric byte cell in a bounded viewport snapshot.</summary>
internal readonly record struct HexViewportCell(
    long Address,
    byte PrimaryValue,
    byte? ComparisonValue,
    HexViewportCellDecoration Decorations,
    int StructuralBlockIndex = -1)
{
    public bool IsDataChanged => Decorations.HasFlag(HexViewportCellDecoration.DataChange);

    public bool IsStructuralChanged => Decorations.HasFlag(HexViewportCellDecoration.StructuralChange);

    public bool IsStructuralBoundaryStart => Decorations.HasFlag(HexViewportCellDecoration.StructuralBoundaryStart);

    public bool IsStructuralBoundaryEnd => Decorations.HasFlag(HexViewportCellDecoration.StructuralBoundaryEnd);

    public bool IsStructuralBoundary => IsStructuralBoundaryStart || IsStructuralBoundaryEnd;

    public bool HasStructuralBlock => StructuralBlockIndex >= 0;

    public bool IsSearchMatch => Decorations.HasFlag(HexViewportCellDecoration.Search);

    public bool HasHistoryFeedback => Decorations.HasFlag(HexViewportCellDecoration.HistoryFeedback);

    public bool IsChanged => IsDataChanged || IsStructuralChanged;
}

/// <summary>One defensively copied row with at most the fixed 16 visible bytes.</summary>
internal sealed class HexViewportRow
{
    public HexViewportRow(long address, IEnumerable<HexViewportCell> cells)
        : this(address, CopyCells(cells))
    {
    }

    private HexViewportRow(long address, HexViewportCell[] cells)
    {
        ArgumentNullException.ThrowIfNull(cells);
        ArgumentOutOfRangeException.ThrowIfNegative(address);

        if (cells.Length is < 1 or > HexViewportSnapshot.BytesPerRow)
        {
            throw new ArgumentOutOfRangeException(nameof(cells), "A Hex viewport row contains between 1 and 16 bytes.");
        }

        for (int index = 0; index < cells.Length; index++)
        {
            if (cells[index].Address != checked(address + index))
            {
                throw new ArgumentException("Hex viewport cell addresses must be contiguous within a row.", nameof(cells));
            }
        }

        Address = address;
        Cells = Array.AsReadOnly(cells);
        HasDataChanges = cells.Any(cell => cell.IsDataChanged);
        HasStructuralChanges = cells.Any(cell => cell.IsStructuralChanged);
        HasStructuralBoundary = cells.Any(cell => cell.IsStructuralBoundary);
        HasComparison = cells.Any(cell => cell.IsChanged);
    }

    internal static HexViewportRow CreateOwned(long address, HexViewportCell[] cells)
    {
        return new HexViewportRow(address, cells);
    }

    private static HexViewportCell[] CopyCells(IEnumerable<HexViewportCell> cells)
    {
        ArgumentNullException.ThrowIfNull(cells);
        return [.. cells];
    }

    public long Address { get; }

    public ReadOnlyCollection<HexViewportCell> Cells { get; }

    public bool HasDataChanges { get; }

    public bool HasStructuralChanges { get; }

    public bool HasStructuralBoundary { get; }

    public bool HasComparison { get; }
}

/// <summary>
/// Immutable visible-window input for the shared read-only renderer. It contains no strings,
/// brushes, commands, filesystem handles, editor state, or firmware conclusions.
/// </summary>
internal sealed class HexViewportSnapshot
{
    internal const int BytesPerRow = 16;

    public HexViewportSnapshot(
        HexViewportCapabilityProfile profile,
        string addressSpaceId,
        long documentLength,
        long startAddress,
        IEnumerable<HexViewportRow> rows,
        long? selectedAddress,
        bool showComparisonRows,
        int decorationVersion = 0)
        : this(
            profile,
            addressSpaceId,
            documentLength,
            startAddress,
            CopyRows(rows),
            selectedAddress,
            showComparisonRows,
            decorationVersion)
    {
    }

    private HexViewportSnapshot(
        HexViewportCapabilityProfile profile,
        string addressSpaceId,
        long documentLength,
        long startAddress,
        HexViewportRow[] rows,
        long? selectedAddress,
        bool showComparisonRows,
        int decorationVersion)
    {
        ArgumentNullException.ThrowIfNull(profile);
        ArgumentNullException.ThrowIfNull(rows);
        if (string.IsNullOrWhiteSpace(addressSpaceId))
        {
            throw new ArgumentException("A source-neutral address-space id is required.", nameof(addressSpaceId));
        }

        ArgumentOutOfRangeException.ThrowIfNegative(documentLength);

        ArgumentOutOfRangeException.ThrowIfNegative(startAddress);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(startAddress, documentLength);

        if (selectedAddress is long selected)
        {
            ArgumentOutOfRangeException.ThrowIfNegative(selected);
            ArgumentOutOfRangeException.ThrowIfGreaterThanOrEqual(
                selected,
                documentLength,
                nameof(selectedAddress));
        }

        if (showComparisonRows && profile.Comparison == HexViewportComparison.None)
        {
            throw new ArgumentException("The selected capability profile does not admit comparison rows.", nameof(showComparisonRows));
        }

        if (rows.Length > profile.MaximumRows)
        {
            throw new ArgumentOutOfRangeException(nameof(rows), "The snapshot exceeds its named row budget.");
        }

        long expectedAddress = startAddress;
        foreach (HexViewportRow row in rows)
        {
            if (profile.Navigation.HasFlag(HexViewportNavigation.DocumentScroll) && row.Address != expectedAddress)
            {
                throw new ArgumentException("Document-scroll rows must form one contiguous visible window.", nameof(rows));
            }

            long rowEnd = checked(row.Address + row.Cells.Count);
            if (rowEnd > documentLength)
            {
                throw new ArgumentException("A Hex viewport row exceeds the declared document length.", nameof(rows));
            }

            expectedAddress = rowEnd;
        }

        Profile = profile;
        AddressSpaceId = addressSpaceId;
        DocumentLength = documentLength;
        StartAddress = startAddress;
        Rows = Array.AsReadOnly(rows);
        SelectedAddress = selectedAddress;
        ShowComparisonRows = showComparisonRows;
        DecorationVersion = decorationVersion;
    }

    public HexViewportCapabilityProfile Profile { get; }

    public string AddressSpaceId { get; }

    public long DocumentLength { get; }

    public long StartAddress { get; }

    public ReadOnlyCollection<HexViewportRow> Rows { get; }

    public long? SelectedAddress { get; }

    public bool ShowComparisonRows { get; }

    public int DecorationVersion { get; }

    public HexViewportSnapshot WithSelectedAddress(long? selectedAddress)
    {
        if (selectedAddress is long selected)
        {
            ArgumentOutOfRangeException.ThrowIfNegative(selected);
            ArgumentOutOfRangeException.ThrowIfGreaterThanOrEqual(selected, DocumentLength, nameof(selectedAddress));
        }

        return selectedAddress == SelectedAddress
            ? this
            : new HexViewportSnapshot(this, selectedAddress);
    }

    private HexViewportSnapshot(HexViewportSnapshot source, long? selectedAddress)
    {
        Profile = source.Profile;
        AddressSpaceId = source.AddressSpaceId;
        DocumentLength = source.DocumentLength;
        StartAddress = source.StartAddress;
        Rows = source.Rows;
        SelectedAddress = selectedAddress;
        ShowComparisonRows = source.ShowComparisonRows;
        DecorationVersion = source.DecorationVersion;
    }

    public static HexViewportSnapshot Empty(HexViewportCapabilityProfile profile, string addressSpaceId)
    {
        return new HexViewportSnapshot(profile, addressSpaceId, 0, 0, [], null, false);
    }

    internal static HexViewportSnapshot CreateOwned(
        HexViewportCapabilityProfile profile,
        string addressSpaceId,
        long documentLength,
        long startAddress,
        HexViewportRow[] rows,
        long? selectedAddress,
        bool showComparisonRows,
        int decorationVersion)
    {
        return new HexViewportSnapshot(
            profile,
            addressSpaceId,
            documentLength,
            startAddress,
            rows,
            selectedAddress,
            showComparisonRows,
            decorationVersion);
    }

    private static HexViewportRow[] CopyRows(IEnumerable<HexViewportRow> rows)
    {
        ArgumentNullException.ThrowIfNull(rows);
        return [.. rows];
    }
}
