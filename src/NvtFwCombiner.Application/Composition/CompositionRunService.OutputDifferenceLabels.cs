using NvtFwCombiner.Application.FlashMaps;
using NvtFwCombiner.Domain.Composition;

namespace NvtFwCombiner.Application.Composition;

public sealed partial class CompositionRunService
{
    private static string? FindProcessorWriteSectionId(ExternalProcessorInvocation invocation, ByteRange range)
    {
        ExternalProcessorWriteRangeSection? section = invocation.AllowedWriteRangeSections.LastOrDefault(candidate =>
            candidate.Range.Contains(range));
        return section?.SectionId;
    }

    private static string FormatProcessorWriteSectionLabel(string? sectionId)
    {
        return string.IsNullOrWhiteSpace(sectionId)
            ? "Header / CRC refresh"
            : TpHeaderCatalog.GetDisplayName(sectionId);
    }

    private static string FormatDifferenceSectionLabel(string? sourceSpaceId, string reason)
    {
        return string.Equals(reason, "Overwrite hexadecimal General range.", StringComparison.Ordinal) ||
               reason.StartsWith("Fill hexadecimal General range", StringComparison.Ordinal)
            ? "Hex patch"
            : sourceSpaceId is not null && DynamicCtrlRamReplacementIds.TryFormatDisplayLabel(sourceSpaceId, out string label)
                ? label
                : "Declared replacement";
    }
}
