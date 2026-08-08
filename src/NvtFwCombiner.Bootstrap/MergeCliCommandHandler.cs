using NvtFwCombiner.Application.Authoring;
namespace NvtFwCombiner.Bootstrap;

internal static partial class MergeCliCommandHandler
{
    private const int Success = 0;
    private const int CompositionFailed = 1;
    private const int UsageError = 64;
    internal static async Task<int> RunAsync(
        string[] args,
        TextWriter output,
        TextWriter error,
        CancellationToken cancellationToken)
    {
        if (args.Length == 0 || args[0] is "--help")
        {
            await WriteUsageAsync(output).ConfigureAwait(false);
            return args.Length == 0 ? UsageError : Success;
        }

        string action = args[0];
        if (action is not ("preview" or "build"))
        {
            await error.WriteLineAsync($"error: unknown general-merge command '{action}'").ConfigureAwait(false);
            return UsageError;
        }

        if (!TryParseOptions(args[1..], action == "build", error, out ParsedOptions options))
        {
            return UsageError;
        }

        if (!RequireOption(options, "--profile", error, out string? profileSelector))
        {
            return UsageError;
        }

        if (!TryResolveIc(profileSelector, out string? icId))
        {
            await error.WriteLineAsync($"error: General Merge profile '{profileSelector}' is not available").ConfigureAwait(false);
            return UsageError;
        }

        bool usesSavedRule = options.Values.ContainsKey("--rule");
        if (usesSavedRule && options.Values.ContainsKey("--size"))
        {
            await error.WriteLineAsync(
                "error: --size cannot override a saved-rule initializer")
                .ConfigureAwait(false);
            return UsageError;
        }

        if (usesSavedRule && options.Values.ContainsKey("--fill"))
        {
            await error.WriteLineAsync(
                "error: --fill cannot override a saved-rule initializer")
                .ConfigureAwait(false);
            return UsageError;
        }

        if (!TryCreateGeneralMergeDraft(
                options,
                icId,
                error,
                out GeneralMergeDraftState? draft,
                out GeneralSavedRuleResourcePolicy? savedRulePolicy))
        {
            return UsageError;
        }

        CliOutputTarget outputTarget = CliCompositionRunSupport.ResolveOutputTarget(
            options.Values.GetValueOrDefault("--output"),
            CanonicalAuthoringAdapter.GetGeneralMergeDefaultOutputFileName(icId));
        string? outputPath = action == "build" ? outputTarget.FullPath : null;
        List<ProtectedPathGuard.ProtectedPath> protectedPaths =
        [
            .. draft.Mappings.Rows.Select(mapping => new ProtectedPathGuard.ProtectedPath(
                Path.GetFullPath(mapping.Source.Reference),
                $"input mapping '{mapping.MappingId}'")),
        ];
        if (options.Values.TryGetValue("--rule", out string? savedRulePath))
        {
            protectedPaths.Add(new ProtectedPathGuard.ProtectedPath(
                Path.GetFullPath(savedRulePath),
                "saved-rule input"));
        }

        if (action == "build")
        {
            ProtectedPathGuard.EnsureDoesNotAlias(
                outputTarget.FullPath,
                "Output path",
                protectedPaths,
                "--output");
        }

        if (options.Values.TryGetValue("--report", out string? reportPath))
        {
            ProtectedPathGuard.EnsureDoesNotAlias(
                reportPath,
                "Report path",
                action == "build"
                    ? [.. protectedPaths, new ProtectedPathGuard.ProtectedPath(outputTarget.FullPath, "built firmware output")]
                    : protectedPaths,
                "--report");
        }

        WorkbenchRunResult result = await CompositionExecutionAdapter.RunGeneralMergeEphemeralDraftAsync(
                icId,
                draft!,
                savedRulePolicy,
                action == "build",
                cancellationToken,
                outputPath)
            .ConfigureAwait(false);
        bool reportWritten = options.Values.TryGetValue("--report", out string? requestedReportPath);
        if (reportWritten)
        {
            await CliCompositionRunSupport.WriteReportJsonAsync(
                    requestedReportPath!,
                    result.ReportJson,
                    output,
                    cancellationToken)
                .ConfigureAwait(false);
        }

        await PrintResultAsync(result, icId, output, error, reportWritten).ConfigureAwait(false);
        return result.Succeeded ? Success : CompositionFailed;
    }
}
