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
        string normalized = sourceSpaceId ?? string.Empty;
        const string prefix = "replace-ctrlram-";
        if (!normalized.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
        {
            return "Declared replacement";
        }

        normalized = normalized[prefix.Length..];
        string[] parts = normalized.Split('-', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (parts.Length == 0)
        {
            return "Declared replacement";
        }

        string region = parts[0].ToUpperInvariant() switch
        {
            "NF" => "NF",
            "MP" => "MP",
            "VN" => "VN",
            "NORMAL" => "Normal",
            _ => parts[0],
        };
        string side = parts.Length >= 2 && string.Equals(parts[1], "master", StringComparison.OrdinalIgnoreCase)
            ? "master"
            : parts.Length >= 3 && string.Equals(parts[1], "slave", StringComparison.OrdinalIgnoreCase)
                ? $"slave {parts[2].ToUpperInvariant()}"
                : string.Empty;
        return string.IsNullOrWhiteSpace(side) ? $"{region} CtrlRAM" : $"{region} CtrlRAM ({side})";
    }
}
