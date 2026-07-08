using NvtFwCombiner.Application.FlashMaps;
using NvtFwCombiner.Domain.Composition;

namespace NvtFwCombiner.Application.Composition;

public sealed partial class CompositionRunService
{
    private static string FormatProcessorWriteSectionLabel(ExternalProcessorInvocation invocation, ByteRange range)
    {
        ExternalProcessorWriteRangeSection? section = invocation.AllowedWriteRangeSections.LastOrDefault(candidate =>
            candidate.Range.Contains(range));
        return section is null
            ? "Header / CRC refresh"
            : FormatProcessorWriteSectionLabel(section.SectionId);
    }

    private static string FormatProcessorWriteSectionLabel(string sectionId)
    {
        return TpHeaderCatalog.GetDisplayName(sectionId);
    }

    private static string FormatDifferenceSectionLabel(string sourceSpaceId)
    {
        return DynamicCtrlRamReplacementIds.TryFormatDisplayLabel(sourceSpaceId, out string label)
            ? label
            : "Declared replacement";
    }
}
