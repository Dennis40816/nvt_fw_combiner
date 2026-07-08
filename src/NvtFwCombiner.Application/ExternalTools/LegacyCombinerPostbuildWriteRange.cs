using NvtFwCombiner.Domain.Composition;

namespace NvtFwCombiner.Application.ExternalTools;

/// <summary>Approved postbuild write range plus the TP flash/header section it belongs to.</summary>
public sealed class LegacyCombinerPostbuildWriteRange
{
    /// <summary>Creates an approved postbuild write range section.</summary>
    public LegacyCombinerPostbuildWriteRange(ByteRange range, string sectionId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sectionId);

        Range = range;
        SectionId = sectionId.Trim();
    }

    /// <summary>Half-open write range in firmware coordinates.</summary>
    public ByteRange Range { get; }

    /// <summary>Stable section identifier for report projection.</summary>
    public string SectionId { get; }
}
