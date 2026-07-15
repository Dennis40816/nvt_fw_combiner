using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using NvtFwCombiner.Application.Composition;
using NvtFwCombiner.Profiles;

namespace NvtFwCombiner.Bootstrap;

internal static partial class ReplaceCliCommandHandler
{
    private static async Task<int> RunWorkbenchGeneralReplaceAsync(
        string action,
        string profileSelector,
        ParsedCliOptions options,
        TextWriter output,
        TextWriter error,
        CancellationToken cancellationToken)
    {
        if (!TryResolveWorkbenchIc(profileSelector, out string? icId))
        {
            return await UnknownReplaceProfileAsync(IcWorkflowIds.GeneralReplace, profileSelector, error).ConfigureAwait(false);
        }

        if (!RequireOption(options, "--ic-num", error, out string? icNumber) ||
            !RequireOption(options, "--base", error, out string? basePath))
        {
            return UsageError;
        }

        if (!TryCreateWorkbenchGeneralAuthoringInputs(
                options,
                error,
                out WorkbenchGeneralReplaceMappingInput[]? mappings,
                out WorkbenchGeneralReplacePatchInput[]? patches))
        {
            return UsageError;
        }

        Dictionary<string, string> slotPaths = new(StringComparer.Ordinal)
        {
            [WorkbenchSlotIds.ReplaceBase] = Path.GetFullPath(basePath),
        };
        Dictionary<string, string> protectedInputPaths = new(slotPaths, StringComparer.Ordinal);
        foreach (WorkbenchGeneralReplaceMappingInput mapping in mappings)
        {
            protectedInputPaths[mapping.MappingId] = Path.GetFullPath(mapping.FilePath);
        }

        InputArtifactBinding[] bindings = CreateWorkbenchBindings(protectedInputPaths);
        CliOutputTarget outputTarget = CliCompositionRunSupport.ResolveOutputTarget(
            options.Values.GetValueOrDefault("--output"),
            WorkbenchCompositionService.GetReplaceDefaultOutputFileName(icId, WorkbenchReplaceModes.General));
        string? outputPath = action == "build" ? outputTarget.FullPath : null;
        if (action == "build")
        {
            CliCompositionRunSupport.EnsureOutputDoesNotAliasInputs(outputTarget, bindings);
            if (!options.Flags.Contains("--overwrite") && File.Exists(outputTarget.FullPath))
            {
                await error.WriteLineAsync(
                        $"error: output file already exists: {outputTarget.FullPath}; pass --overwrite to replace it.")
                    .ConfigureAwait(false);
                return SoftwareError;
            }
        }

        CliCompositionRunSupport.EnsureReportDoesNotAliasProtectedPaths(
            options.Values.GetValueOrDefault("--report"),
            bindings,
            outputTarget,
            action == "build");

        WorkbenchRunResult result = await WorkbenchCompositionService
            .RunReplaceAsync(
                icId,
                icNumber,
                WorkbenchReplaceModes.General,
                slotPaths,
                mappings,
                patches,
                action == "build",
                cancellationToken,
                outputPath)
            .ConfigureAwait(false);
        await WriteWorkbenchReportFileIfRequestedAsync(
                result,
                options,
                bindings,
                action == "build" ? outputTarget.FullPath : null,
                output,
                cancellationToken)
            .ConfigureAwait(false);
        await PrintWorkbenchRunResultAsync(result, icId, IcWorkflowIds.GeneralReplace, output, error).ConfigureAwait(false);
        return result.Succeeded ? Success : CompositionFailed;
    }

    private static bool TryCreateWorkbenchGeneralAuthoringInputs(
        ParsedCliOptions options,
        TextWriter error,
        [NotNullWhen(true)] out WorkbenchGeneralReplaceMappingInput[]? mappings,
        [NotNullWhen(true)] out WorkbenchGeneralReplacePatchInput[]? patches)
    {
        List<string> mappingValues = options.GetValues("--mapping");
        List<string> patchValues = options.GetValues("--patch");
        List<string> fillValues = options.GetValues("--fill");
        if (mappingValues.Count == 0 && patchValues.Count == 0 && fillValues.Count == 0)
        {
            mappings = null;
            patches = null;
            error.WriteLine(
                "error: at least one --mapping <target-start+length=path>, --patch <target-start+length=hex>, or --fill <target-start+length=byte> value is required for real IC General Replace");
            return false;
        }

        List<WorkbenchGeneralReplaceMappingInput> mappingItems = [];
        for (int index = 0; index < mappingValues.Count; index++)
        {
            if (!TryParseWorkbenchGeneralMappingValue(
                    mappingValues[index],
                    index + 1,
                    error,
                    out WorkbenchGeneralReplaceMappingInput? mapping))
            {
                mappings = null;
                patches = null;
                return false;
            }

            mappingItems.Add(mapping);
        }

        List<WorkbenchGeneralReplacePatchInput> patchItems = [];
        if (!TryAppendWorkbenchGeneralPatches(
                "--patch",
                patchValues,
                WorkbenchGeneralReplacePatchKind.Overwrite,
                "general-patch",
                error,
                patchItems) ||
            !TryAppendWorkbenchGeneralPatches(
                "--fill",
                fillValues,
                WorkbenchGeneralReplacePatchKind.Fill,
                "general-fill",
                error,
                patchItems))
        {
            mappings = null;
            patches = null;
            return false;
        }

        mappings = [.. mappingItems];
        patches = [.. patchItems];
        return true;
    }

    private static bool TryAppendWorkbenchGeneralPatches(
        string optionName,
        List<string> values,
        WorkbenchGeneralReplacePatchKind kind,
        string idPrefix,
        TextWriter error,
        List<WorkbenchGeneralReplacePatchInput> patches)
    {
        for (int index = 0; index < values.Count; index++)
        {
            if (!TryParseWorkbenchGeneralRangeValue(
                    optionName,
                    values[index],
                    error,
                    out string? payload,
                    out string? start,
                    out string? endInclusive))
            {
                return false;
            }

            patches.Add(new WorkbenchGeneralReplacePatchInput(
                string.Create(CultureInfo.InvariantCulture, $"{idPrefix}-{index + 1}"),
                start,
                endInclusive,
                kind,
                payload));
        }

        return true;
    }

    private static bool TryParseWorkbenchGeneralMappingValue(
        string value,
        int index,
        TextWriter error,
        [NotNullWhen(true)] out WorkbenchGeneralReplaceMappingInput? mapping)
    {
        mapping = null;
        if (!TryParseWorkbenchGeneralRangeValue(
                "--mapping",
                value,
                error,
                out string? path,
                out string? start,
                out string? endInclusive))
        {
            return false;
        }

        if (string.IsNullOrWhiteSpace(path))
        {
            error.WriteLine("error: --mapping path must not be empty");
            return false;
        }

        mapping = new WorkbenchGeneralReplaceMappingInput(
            string.Create(CultureInfo.InvariantCulture, $"general-map-{index}"),
            Path.GetFullPath(path),
            start,
            endInclusive);
        return true;
    }

    private static bool TryParseWorkbenchGeneralRangeValue(
        string optionName,
        string value,
        TextWriter error,
        [NotNullWhen(true)] out string? payload,
        [NotNullWhen(true)] out string? start,
        [NotNullWhen(true)] out string? endInclusive)
    {
        payload = null;
        start = null;
        endInclusive = null;
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

        if (!BootstrapRangeText.TryParseNonNegativeLong(rangeText[..plusIndex], out long rangeStart) ||
            !BootstrapRangeText.TryParseNonNegativeLong(rangeText[(plusIndex + 1)..], out long length) ||
            length <= 0)
        {
            error.WriteLine($"error: {optionName} start must be non-negative and length must be positive");
            return false;
        }

        long rangeEndInclusive;
        try
        {
            rangeEndInclusive = checked(rangeStart + length - 1);
        }
        catch (OverflowException)
        {
            error.WriteLine($"error: {optionName} range exceeds the supported address size");
            return false;
        }

        start = BootstrapRangeText.FormatHex(rangeStart);
        endInclusive = BootstrapRangeText.FormatHex(rangeEndInclusive);
        return true;
    }
}
