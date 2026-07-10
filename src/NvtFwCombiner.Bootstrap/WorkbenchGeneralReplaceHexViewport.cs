using NvtFwCombiner.Domain.Composition;

namespace NvtFwCombiner.Bootstrap;

/// <summary>Read-only 16-byte-row view of a base BIN with staged General Replace patches overlaid in memory.</summary>
public sealed record WorkbenchGeneralReplaceHexViewport(
    IReadOnlyList<WorkbenchGeneralReplaceHexViewportRow> Rows,
    IReadOnlyList<CompositionIssue> Issues,
    long BaseLength,
    long ViewportStart,
    long ViewportLength);

/// <summary>One fixed-width row in a General Replace hexadecimal viewport.</summary>
public sealed record WorkbenchGeneralReplaceHexViewportRow(
    long Address,
    IReadOnlyList<WorkbenchGeneralReplaceHexByte> Bytes,
    string BeforeAscii,
    string AfterAscii);

/// <summary>One byte cell with immutable base and virtual staged values.</summary>
public sealed record WorkbenchGeneralReplaceHexByte(
    long Address,
    byte Before,
    byte After)
{
    /// <summary>True when a staged patch changes the base byte.</summary>
    public bool IsChanged => Before != After;
}
