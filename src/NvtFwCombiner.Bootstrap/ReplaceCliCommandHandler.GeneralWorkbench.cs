using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using NvtFwCombiner.Application.Authoring;
using NvtFwCombiner.Domain.Composition;
using NvtFwCombiner.Profiles;

namespace NvtFwCombiner.Bootstrap;

internal static partial class ReplaceCliCommandHandler
{
    private static async Task<int> RunWorkbenchGeneralReplaceAsync(
        string action,
        string icId,
        ParsedCliOptions options,
        TextWriter output,
        TextWriter error,
        CancellationToken cancellationToken)
    {
        if (!RequireOption(options, "--ic-num", error, out string? icNumber) ||
            !RequireOption(options, "--base", error, out string? basePath))
        {
            return UsageError;
        }

        if (!TryCreateWorkbenchGeneralAuthoringInputs(
                options,
                error,
                out GeneralMappingDraftState? mappingDraft))
        {
            return UsageError;
        }

        Dictionary<string, string> slotPaths = new(StringComparer.Ordinal)
        {
            [WorkbenchSlotIds.ReplaceBase] = Path.GetFullPath(basePath),
        };
        Dictionary<string, string> protectedInputPaths = new(slotPaths, StringComparer.Ordinal);
        foreach (GeneralMappingDraftRow mapping in mappingDraft.Rows.Where(
                     static row => row.Source.Kind == GeneralMappingSourceKind.FileArtifact))
        {
            protectedInputPaths[mapping.MappingId] = Path.GetFullPath(mapping.Source.Reference);
        }

        return await RunWorkbenchReplaceAsync(
                action,
                icId,
                WorkbenchReplaceModes.General,
                IcWorkflowIds.GeneralReplace,
                options,
                protectedInputPaths,
                (build, outputPath, token) => WorkbenchCompositionService.RunGeneralReplaceEphemeralDraftAsync(
                    icId,
                    icNumber,
                    slotPaths,
                    mappingDraft,
                    build,
                    token,
                    outputPath),
                output,
                error,
                cancellationToken)
            .ConfigureAwait(false);
    }

    private static bool TryCreateWorkbenchGeneralAuthoringInputs(
        ParsedCliOptions options,
        TextWriter error,
        [NotNullWhen(true)] out GeneralMappingDraftState? mappingDraft)
    {
        List<string> mappingValues = options.GetValues("--mapping");
        List<string> patchValues = options.GetValues("--patch");
        List<string> fillValues = options.GetValues("--fill");
        if (mappingValues.Count == 0 && patchValues.Count == 0 && fillValues.Count == 0)
        {
            mappingDraft = null;
            error.WriteLine(
                "error: at least one --mapping <target-start+length=path>, --patch <target-start+length=hex>, or --fill <target-start+length=byte> value is required for real IC General Replace");
            return false;
        }

        List<GeneralMappingDraftRow> items = [];
        for (int index = 0; index < mappingValues.Count; index++)
        {
            if (!TryParseWorkbenchGeneralMappingValue(
                    mappingValues[index],
                    index + 1,
                    error,
                    out GeneralMappingDraftRow? mapping))
            {
                mappingDraft = null;
                return false;
            }

            items.Add(mapping);
        }

        if (!TryAppendWorkbenchGeneralPatches(
                "--patch",
                patchValues,
                WorkbenchGeneralReplacePatchKind.Overwrite,
                "general-patch",
                error,
                items) ||
            !TryAppendWorkbenchGeneralPatches(
                "--fill",
                fillValues,
                WorkbenchGeneralReplacePatchKind.Fill,
                "general-fill",
                error,
                items))
        {
            mappingDraft = null;
            return false;
        }

        if (!WorkbenchCompositionService.TryCreateGeneralReplaceDraft(
                items,
                out mappingDraft,
                out IReadOnlyList<CompositionIssue> issues))
        {
            foreach (CompositionIssue issue in issues)
            {
                error.WriteLine($"error: {issue.Message}");
            }

            return false;
        }

        return true;
    }

    private static bool TryAppendWorkbenchGeneralPatches(
        string optionName,
        List<string> values,
        WorkbenchGeneralReplacePatchKind kind,
        string idPrefix,
        TextWriter error,
        List<GeneralMappingDraftRow> rows)
    {
        for (int index = 0; index < values.Count; index++)
        {
            if (!TryParseWorkbenchGeneralRangeValue(
                    optionName,
                    values[index],
                    error,
                    out string? payload,
                    out ByteRange targetRange))
            {
                return false;
            }

            string mappingId = string.Create(
                CultureInfo.InvariantCulture,
                $"{idPrefix}-{index + 1}");
            GeneralMappingSource source = kind switch
            {
                WorkbenchGeneralReplacePatchKind.Overwrite =>
                    GeneralMappingSource.HexOverwrite(payload, mappingId),
                WorkbenchGeneralReplacePatchKind.Fill =>
                    GeneralMappingSource.HexFill(payload, mappingId),
                _ => throw new ArgumentOutOfRangeException(
                    nameof(kind),
                    kind,
                    "Unknown General Replace patch kind."),
            };
            rows.Add(new GeneralMappingDraftRow(
                mappingId,
                ExplicitMappingOperationKind.ReplaceRange,
                source,
                new ByteRange(0, targetRange.Length),
                CompositionAddressSpaceIds.OutputImage,
                targetRange,
                OverlapPolicy.Reject,
                alignment: 1,
                kind == WorkbenchGeneralReplacePatchKind.Fill
                    ? "Fill hexadecimal General range."
                    : "Overwrite hexadecimal General range."));
        }

        return true;
    }

    private static bool TryParseWorkbenchGeneralMappingValue(
        string value,
        int index,
        TextWriter error,
        [NotNullWhen(true)] out GeneralMappingDraftRow? mapping)
    {
        mapping = null;
        if (!TryParseWorkbenchGeneralRangeValue(
                "--mapping",
                value,
                error,
                out string? path,
                out ByteRange targetRange))
        {
            return false;
        }

        if (string.IsNullOrWhiteSpace(path))
        {
            error.WriteLine("error: --mapping path must not be empty");
            return false;
        }

        string mappingId = string.Create(
            CultureInfo.InvariantCulture,
            $"general-map-{index}");
        mapping = new GeneralMappingDraftRow(
            mappingId,
            ExplicitMappingOperationKind.ReplaceRange,
            GeneralMappingSource.File(Path.GetFullPath(path)),
            new ByteRange(0, targetRange.Length),
            CompositionAddressSpaceIds.OutputImage,
            targetRange,
            OverlapPolicy.Reject,
            alignment: 1,
            "Replace explicit General range.",
            fileRangePreset: GeneralMappingFileRangePreset.FromFileStart);
        return true;
    }

    private static bool TryParseWorkbenchGeneralRangeValue(
        string optionName,
        string value,
        TextWriter error,
        [NotNullWhen(true)] out string? payload,
        out ByteRange targetRange)
    {
        payload = null;
        targetRange = default;
        int separatorIndex = value.IndexOf('=', StringComparison.Ordinal);
        if (separatorIndex <= 0)
        {
            error.WriteLine(
                $"error: {optionName} expects <target-start+length=value>; example: {optionName} 0x100+0x20=value");
            return false;
        }

        string rangeText = value[..separatorIndex].Trim();
        payload = value[(separatorIndex + 1)..].Trim();
        int plusIndex = rangeText.IndexOf('+', StringComparison.Ordinal);
        if (plusIndex <= 0 || plusIndex == rangeText.Length - 1)
        {
            error.WriteLine($"error: {optionName} range must use <target-start+length>");
            return false;
        }

        if (!AuthoringByteRangeCodec.TryParseStartAndLength(
                rangeText[..plusIndex],
                rangeText[(plusIndex + 1)..],
                out targetRange,
                out AuthoringRangeTextIssue? issue))
        {
            error.WriteLine($"error: {optionName} {issue!.Message}");
            return false;
        }

        return true;
    }
}
