using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using NvtFwCombiner.Application.Composition;

namespace NvtFwCombiner.Bootstrap;

internal static partial class ReplaceCliCommandHandler
{
    private static async Task<int> RunWorkbenchGeneralReplaceAsync(
        string action,
        string profileSelector,
        ParsedOptions options,
        TextWriter output,
        TextWriter error,
        CancellationToken cancellationToken)
    {
        if (!TryResolveWorkbenchIc(profileSelector, out string? icId))
        {
            return await UnknownReplaceProfileAsync("general-replace", profileSelector, error).ConfigureAwait(false);
        }

        if (!RequireOption(options, "--ic-num", error, out string? icNumber) ||
            !RequireOption(options, "--base", error, out string? basePath))
        {
            return UsageError;
        }

        if (!TryCreateWorkbenchGeneralMappings(options, error, out WorkbenchGeneralReplaceMappingInput[]? mappings))
        {
            return UsageError;
        }

        Dictionary<string, string> slotPaths = new(StringComparer.Ordinal)
        {
            ["replace-base"] = Path.GetFullPath(basePath),
        };
        Dictionary<string, string> protectedInputPaths = new(slotPaths, StringComparer.Ordinal);
        foreach (WorkbenchGeneralReplaceMappingInput mapping in mappings)
        {
            protectedInputPaths[mapping.MappingId] = Path.GetFullPath(mapping.FilePath);
        }

        InputArtifactBinding[] bindings = CreateWorkbenchBindings(protectedInputPaths);
        CliOutputTarget outputTarget = CliCompositionRunSupport.ResolveOutputTarget(
            options.Values.GetValueOrDefault("--output"),
            WorkbenchCompositionService.GetReplaceDefaultOutputFileName(icId, "General"));
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
                "General",
                slotPaths,
                mappings,
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
        await PrintWorkbenchRunResultAsync(result, icId, "general-replace", output, error).ConfigureAwait(false);
        return result.Succeeded ? Success : CompositionFailed;
    }

    private static bool TryCreateWorkbenchGeneralMappings(
        ParsedOptions options,
        TextWriter error,
        [NotNullWhen(true)] out WorkbenchGeneralReplaceMappingInput[]? mappings)
    {
        mappings = null;
        List<string> values = options.GetValues("--mapping");
        if (values.Count == 0)
        {
            error.WriteLine("error: at least one --mapping <target-start+length=path> value is required for real IC General Replace");
            return false;
        }

        List<WorkbenchGeneralReplaceMappingInput> items = [];
        for (int index = 0; index < values.Count; index++)
        {
            if (!TryParseWorkbenchGeneralMappingValue(
                    values[index],
                    index + 1,
                    error,
                    out WorkbenchGeneralReplaceMappingInput? mapping))
            {
                return false;
            }

            items.Add(mapping);
        }

        mappings = [.. items];
        return true;
    }

    private static bool TryParseWorkbenchGeneralMappingValue(
        string value,
        int index,
        TextWriter error,
        [NotNullWhen(true)] out WorkbenchGeneralReplaceMappingInput? mapping)
    {
        mapping = null;
        int separatorIndex = value.IndexOf('=', StringComparison.Ordinal);
        if (separatorIndex <= 0 || separatorIndex == value.Length - 1)
        {
            error.WriteLine(
                "error: real IC General Replace expects --mapping <target-start+length=path>; example: --mapping 0x100+0x20=C:\\path\\replacement.bin");
            return false;
        }

        string rangeText = value[..separatorIndex].Trim();
        string path = value[(separatorIndex + 1)..].Trim();
        if (path.Length == 0)
        {
            error.WriteLine("error: --mapping path must not be empty");
            return false;
        }

        int plusIndex = rangeText.IndexOf('+', StringComparison.Ordinal);
        if (plusIndex <= 0 || plusIndex == rangeText.Length - 1)
        {
            error.WriteLine("error: --mapping range must use <target-start+length>");
            return false;
        }

        if (!CliCompositionRunSupport.TryParseNonNegativeLong(rangeText[..plusIndex], out long start) ||
            !CliCompositionRunSupport.TryParseNonNegativeLong(rangeText[(plusIndex + 1)..], out long length) ||
            length <= 0)
        {
            error.WriteLine("error: --mapping start must be non-negative and length must be positive");
            return false;
        }

        long endInclusive;
        try
        {
            endInclusive = checked(start + length - 1);
        }
        catch (OverflowException)
        {
            error.WriteLine("error: --mapping range exceeds the supported address size");
            return false;
        }

        mapping = new WorkbenchGeneralReplaceMappingInput(
            string.Create(CultureInfo.InvariantCulture, $"general-map-{index}"),
            Path.GetFullPath(path),
            CliCompositionRunSupport.FormatHex(start),
            CliCompositionRunSupport.FormatHex(endInclusive));
        return true;
    }
}
