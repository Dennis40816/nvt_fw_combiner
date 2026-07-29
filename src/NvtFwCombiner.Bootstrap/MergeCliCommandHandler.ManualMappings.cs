using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using NvtFwCombiner.Application.Authoring;
using NvtFwCombiner.Domain.Composition;

namespace NvtFwCombiner.Bootstrap;

internal static partial class MergeCliCommandHandler
{
    private static bool TryCreateGeneralMergeDraft(
        ParsedOptions options,
        string icId,
        TextWriter error,
        [NotNullWhen(true)] out GeneralMergeDraftState? draft)
    {
        draft = null;
        List<string> values = options.GetValues("--mapping");
        bool usesRule = options.Values.TryGetValue("--rule", out string? rulePath);
        if (usesRule)
        {
            if (values.Count > 0)
            {
                error.WriteLine("error: --rule cannot be combined with manual --mapping values");
                return false;
            }

            return TryCreateDraftFromSavedRule(
                rulePath!,
                options.GetValues("--slot"),
                icId,
                error,
                out draft);
        }

        if (options.GetValues("--slot").Count > 0)
        {
            error.WriteLine("error: --slot can be used only with --rule");
            return false;
        }

        if (values.Count == 0)
        {
            error.WriteLine("error: at least one --mapping <source-start+target-start+length=path> value or --rule <rule.json> is required for General Merge");
            return false;
        }

        if (!RequireOption(options, "--size", error, out string? outputLength))
        {
            return false;
        }

        if (!new GeneralMergeInitializerInput(
                outputLength,
                options.Values.GetValueOrDefault("--fill")).TryResolve(
                out GeneralMergeOutputInitializer? initializer,
                out CompositionIssue? initializationIssue))
        {
            error.WriteLine(
                $"error: {initializationIssue!.Code}: {initializationIssue.Message}");
            return false;
        }

        List<GeneralMappingDraftRow> items = [];
        for (int index = 0; index < values.Count; index++)
        {
            if (!TryParseMappingValue(values[index], index + 1, error, out GeneralMappingDraftRow? mapping))
            {
                return false;
            }

            items.Add(mapping);
        }

        try
        {
            draft = new GeneralMergeDraftState(
                initializer!,
                new GeneralMappingDraftState(items));
            return true;
        }
        catch (ArgumentException exception)
        {
            error.WriteLine($"error: {exception.Message}");
            return false;
        }
    }

    private static bool TryParseMappingValue(
        string value,
        int index,
        TextWriter error,
        [NotNullWhen(true)] out GeneralMappingDraftRow? mapping)
    {
        mapping = null;
        int separatorIndex = value.IndexOf('=', StringComparison.Ordinal);
        if (separatorIndex <= 0 || separatorIndex == value.Length - 1)
        {
            error.WriteLine(
                "error: General Merge expects --mapping <source-start+target-start+length=path>; example: --mapping 0x0+0x100+0x20=C:\\path\\source.bin");
            return false;
        }

        string rangeText = value[..separatorIndex].Trim();
        string path = value[(separatorIndex + 1)..].Trim();
        if (path.Length == 0)
        {
            error.WriteLine("error: --mapping path must not be empty");
            return false;
        }

        string[] parts = rangeText.Split('+', StringSplitOptions.TrimEntries);
        if (parts.Length != 3 ||
            !AuthoringByteRangeCodec.TryParseStartAndLength(
                parts.ElementAtOrDefault(0),
                parts.ElementAtOrDefault(2),
                out ByteRange sourceRange,
                out _) ||
            !AuthoringByteRangeCodec.TryParseStartAndLength(
                parts.ElementAtOrDefault(1),
                parts.ElementAtOrDefault(2),
                out ByteRange targetRange,
                out _))
        {
            error.WriteLine("error: --mapping must use non-negative source start, non-negative target start, and positive length");
            return false;
        }

        mapping = new GeneralMappingDraftRow(
            string.Create(CultureInfo.InvariantCulture, $"general-merge-map-{index}"),
            ExplicitMappingOperationKind.CopyRange,
            GeneralMappingSource.File(Path.GetFullPath(path)),
            sourceRange,
            CompositionAddressSpaceIds.OutputImage,
            targetRange,
            OverlapPolicy.Reject,
            alignment: 1,
            "Copy explicit General Merge mapping.",
            WorkbenchGeneralMergeIds.OutputRegionId);
        return true;
    }

    private static bool TryResolveIc(string selector, [NotNullWhen(true)] out string? icId)
    {
        string normalized = selector.Trim();
        icId = WorkbenchCompositionService.GetSupportedIcIds().FirstOrDefault(candidate =>
            string.Equals(candidate, normalized, StringComparison.OrdinalIgnoreCase) ||
            string.Equals(candidate.Replace("NT", string.Empty, StringComparison.Ordinal), normalized, StringComparison.OrdinalIgnoreCase));
        return icId is not null;
    }
}
