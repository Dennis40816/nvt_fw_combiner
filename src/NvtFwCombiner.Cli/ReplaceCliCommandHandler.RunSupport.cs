using System.Diagnostics.CodeAnalysis;
using NvtFwCombiner.Domain.Composition;

namespace NvtFwCombiner.Cli;

internal static partial class ReplaceCliCommandHandler
{
    private static bool TryResolveReplaceIc(
        ICompositionCapabilityExperience capabilities,
        string command,
        string selector,
        [NotNullWhen(true)] out string? icId)
    {
        return (command == ExperienceIds.DpReplace &&
                TryResolveDpProfile(capabilities, selector, out icId)) ||
            TryResolveIc(capabilities, selector, out icId);
    }

    private static bool TryResolveDpProfile(
        ICompositionCapabilityExperience capabilities,
        string selector,
        [NotNullWhen(true)] out string? icId)
    {
        string normalized = selector.Trim();
        icId = capabilities.GetDpReplaceProfileSummaries()
            .FirstOrDefault(profile =>
                string.Equals(profile.ProfileId, normalized, StringComparison.OrdinalIgnoreCase) ||
                string.Equals(profile.IcId, normalized, StringComparison.OrdinalIgnoreCase) ||
                string.Equals(
                    CliCompositionRunSupport.GetIcNumber(profile.IcId),
                    normalized,
                    StringComparison.OrdinalIgnoreCase))?
            .IcId;
        return icId is not null;
    }

    private static bool TryResolveIc(
        ICompositionCapabilityExperience capabilities,
        string selector,
        [NotNullWhen(true)] out string? icId)
    {
        string normalized = selector.Trim();
        icId = capabilities.GetIcIds().FirstOrDefault(candidate =>
            string.Equals(candidate, normalized, StringComparison.OrdinalIgnoreCase) ||
            string.Equals(CliCompositionRunSupport.GetIcNumber(candidate), normalized, StringComparison.OrdinalIgnoreCase));
        return icId is not null;
    }

    private static InputArtifactBinding[] CreateBindings(IReadOnlyDictionary<string, string> slotPaths)
    {
        return [
            .. slotPaths
                .OrderBy(pair => pair.Key, StringComparer.Ordinal)
                .Select(pair => new InputArtifactBinding(
                    pair.Key == CompositionSlotIds.ReplaceBase ? CompositionAddressSpaceIds.ReferenceBase : pair.Key,
                    pair.Key,
                    pair.Value)),
        ];
    }

    private static async Task<int> CompleteReplaceRunAsync(
        string action,
        string icId,
        string workflowId,
        ParsedCliOptions options,
        IReadOnlyDictionary<string, string> protectedInputPaths,
        string defaultOutputFileName,
        Func<string?, bool, CancellationToken, ValueTask<CompositionRunResult>> run,
        TextWriter output,
        TextWriter error,
        CancellationToken cancellationToken)
    {
        InputArtifactBinding[] bindings = CreateBindings(protectedInputPaths);
        CliOutputTarget outputTarget = CliCompositionRunSupport.ResolveOutputTarget(
            options.Values.GetValueOrDefault("--output"),
            defaultOutputFileName);
        bool build = action == "build";
        if (build)
        {
            CliCompositionRunSupport.EnsureOutputDoesNotAliasInputs(outputTarget, bindings);
        }

        CliCompositionRunSupport.EnsureReportDoesNotAliasProtectedPaths(
            options.Values.GetValueOrDefault("--report"),
            bindings,
            outputTarget,
            build);

        string? outputPath = build ? outputTarget.FullPath : null;
        CompositionRunResult result = await run(
                outputPath,
                build,
                cancellationToken)
            .ConfigureAwait(false);

        if (result.HasRunReport &&
            options.Values.TryGetValue("--report", out string? reportPath))
        {
            string fullPath = Path.GetFullPath(reportPath);
            ProtectedPathGuard.EnsureDoesNotAlias(
                fullPath,
                "Report path",
                ProtectedPathGuard.CreateProtectedPaths(bindings, outputPath),
                nameof(reportPath));
            await CliCompositionRunSupport.WriteReportJsonAsync(
                    fullPath,
                    CompositionRunReportJson.Serialize(result),
                    output,
                    cancellationToken)
                .ConfigureAwait(false);
        }

        await PrintCompositionRunResultAsync(result, icId, workflowId, output, error).ConfigureAwait(false);
        return result.Succeeded ? Success : CompositionFailed;
    }

}
