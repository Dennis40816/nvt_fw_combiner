using NvtFwCombiner.Domain.Composition;

namespace NvtFwCombiner.Application.ExternalTools;

/// <summary>Approved postbuild write range plus the TP flash/header section it belongs to.</summary>
public sealed class LegacyCombinerPostbuildWriteRange
{
    /// <summary>Creates an approved postbuild write range section.</summary>
    public LegacyCombinerPostbuildWriteRange(
        ByteRange range,
        string sectionId,
        ByteRange? sourceRange = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sectionId);
        if (sourceRange is { } source && source.Length != range.Length)
        {
            throw new ArgumentException(
                "A postbuild copy source range must have the same length as its destination range.",
                nameof(sourceRange));
        }

        Range = range;
        SectionId = sectionId.Trim();
        SourceRange = sourceRange;
    }

    /// <summary>Half-open write range in firmware coordinates.</summary>
    public ByteRange Range { get; }

    /// <summary>Stable section identifier for report projection.</summary>
    public string SectionId { get; }

    /// <summary>
    /// Firmware source range copied into <see cref="Range"/> when this section represents a Combiner copy.
    /// Direct integrity writes and staged replacement BINs have no source range.
    /// </summary>
    public ByteRange? SourceRange { get; }

    /// <summary>Maps a destination subrange back to its copied firmware source range.</summary>
    public bool TryMapRangeToSourceRange(ByteRange range, out ByteRange sourceRange)
    {
        if (SourceRange is not { } source || !Range.Contains(range))
        {
            sourceRange = default;
            return false;
        }

        long offset = checked(range.Start - Range.Start);
        sourceRange = new ByteRange(checked(source.Start + offset), range.Length);
        return true;
    }
}
