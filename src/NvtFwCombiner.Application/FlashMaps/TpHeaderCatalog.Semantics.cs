using NvtFwCombiner.Domain.Composition;

namespace NvtFwCombiner.Application.FlashMaps;

public static partial class TpHeaderCatalog
{
    /// <summary>Finds a modeled TP header field that fully contains a physical output-difference range.</summary>
    public static bool TryFindField(string icId, ByteRange range, out TpHeaderField? field)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(icId);

        if (HeaderLayoutsByIc.TryGetValue(icId, out TpHeaderLayout? layout))
        {
            return layout.TryFindField(range, out field);
        }

        field = null;
        return false;
    }

    /// <summary>Returns true when a write section represents header structure rather than a payload replacement.</summary>
    public static bool IsHeaderSection(string? sectionId)
    {
        return sectionId is TpHeaderSectionIds.FlashHeaderCrc or
            TpHeaderSectionIds.HeaderCopyMaster or
            TpHeaderSectionIds.HeaderCopyRight or
            TpHeaderSectionIds.HeaderCopyLeft or
            TpHeaderSectionIds.HeaderCopyFinal or
            TpHeaderSectionIds.HeaderCopyFinalBackup or
            TpHeaderSectionIds.HeaderCopy;
    }
}
