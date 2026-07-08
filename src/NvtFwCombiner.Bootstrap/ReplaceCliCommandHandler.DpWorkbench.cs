using System.Diagnostics.CodeAnalysis;
using NvtFwCombiner.Application.Composition;
using NvtFwCombiner.Profiles;

namespace NvtFwCombiner.Bootstrap;

internal static partial class ReplaceCliCommandHandler
{
    private static async Task<int> RunWorkbenchDpReplaceAsync(
        string action,
        string icId,
        ParsedOptions options,
        TextWriter output,
        TextWriter error,
        CancellationToken cancellationToken)
    {
        if (!RequireOption(options, "--ic-num", error, out string? icNumber) ||
            !RequireOption(options, "--base", error, out string? basePath) ||
            !RequireOption(options, "--dp", error, out string? dpPath))
        {
            return UsageError;
        }

        if (options.Values.ContainsKey("--ic-family"))
        {
            error.WriteLine("error: --ic-family is used only by cascade IC num profiles");
            return UsageError;
        }

        if (!string.Equals(icNumber, "single", StringComparison.OrdinalIgnoreCase))
        {
            error.WriteLine($"error: {DpPerspectiveCatalog.FormatSupportedIcIds()} DP Replace requires --ic-num single");
            return UsageError;
        }

        if (!RejectUnusedDpWorkbenchOptions(options, error))
        {
            return UsageError;
        }

        Dictionary<string, string> slotPaths = new(StringComparer.Ordinal)
        {
            ["replace-base"] = Path.GetFullPath(basePath),
            ["replace-dp"] = Path.GetFullPath(dpPath),
        };

        InputArtifactBinding[] bindings = CreateWorkbenchBindings(slotPaths);
        CliOutputTarget outputTarget = CliCompositionRunSupport.ResolveOutputTarget(
            options.Values.GetValueOrDefault("--output"),
            WorkbenchCompositionService.GetReplaceDefaultOutputFileName(icId, WorkbenchReplaceModes.Dp));
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
            .RunReplaceAsync(icId, icNumber, WorkbenchReplaceModes.Dp, slotPaths, action == "build", cancellationToken, outputPath)
            .ConfigureAwait(false);
        await WriteWorkbenchReportFileIfRequestedAsync(
                result,
                options,
                bindings,
                action == "build" ? outputTarget.FullPath : null,
                output,
                cancellationToken)
            .ConfigureAwait(false);
        await PrintWorkbenchRunResultAsync(result, icId, "dp-replace", output, error).ConfigureAwait(false);
        return result.Succeeded ? Success : CompositionFailed;
    }

    private static bool TryResolveDpPerspectiveDpReplaceIc(
        string selector,
        [NotNullWhen(true)] out string? icId)
    {
        string normalized = selector.Trim();
        foreach (CompositionProfileDefinition profile in BuiltInReplaceProfiles.All.Where(profile =>
                     profile.ExperienceId == "dp-replace" &&
                     BuiltInReplaceProfiles.IsDpPerspectiveDpReplaceIc(profile.IcId)))
        {
            if (string.Equals(profile.ProfileId, normalized, StringComparison.OrdinalIgnoreCase) ||
                string.Equals(profile.IcId, normalized, StringComparison.OrdinalIgnoreCase) ||
                string.Equals(CliCompositionRunSupport.GetIcNumber(profile.IcId), normalized, StringComparison.OrdinalIgnoreCase))
            {
                icId = profile.IcId;
                return true;
            }
        }

        icId = null;
        return false;
    }

    private static bool RejectUnusedDpWorkbenchOptions(ParsedOptions options, TextWriter error)
    {
        foreach (string optionName in new[] { "--ld", "--ctrlram", "--input", "--source-start", "--target-start", "--length", "--mapping" })
        {
            if (options.Values.ContainsKey(optionName) || options.GetValues(optionName).Count > 0)
            {
                error.WriteLine($"error: option '{optionName}' is not used by {DpPerspectiveCatalog.FormatSupportedIcIds()} DP Replace");
                return false;
            }
        }

        return true;
    }
}
