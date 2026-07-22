using System.Diagnostics.CodeAnalysis;
using NvtFwCombiner.Application.Composition;
using NvtFwCombiner.Domain.Composition;
using NvtFwCombiner.Infrastructure.Files;
using NvtFwCombiner.Infrastructure.Time;

namespace NvtFwCombiner.Bootstrap;

public static partial class CliApplication
{
    private static async Task<int> RunAbMergeAsync(
        string[] args,
        TextWriter output,
        TextWriter error,
        CancellationToken cancellationToken)
    {
        if (args.Length == 0 || args[0] is "--help")
        {
            await WriteAbMergeUsageAsync(output).ConfigureAwait(false);
            return args.Length == 0 ? UsageError : Success;
        }

        string action = args[0];
        if (action is not ("preview" or "build"))
        {
            await error.WriteLineAsync($"error: unknown ab-merge command '{action}'").ConfigureAwait(false);
            return UsageError;
        }

        string[] valueOptions = ["--profile", "--dp-ab", "--tp-a", "--tp-b", "--output", "--report"];
        string[] flagOptions = action == "build" ? ["--overwrite"] : [];
        if (!CliOptionParser.TryParse(args[1..], valueOptions, [], flagOptions, error, out ParsedCliOptions options))
        {
            return UsageError;
        }

        if (!options.Values.TryGetValue("--profile", out string? profileSelector))
        {
            await error.WriteLineAsync("error: --profile is required").ConfigureAwait(false);
            return UsageError;
        }

        if (!TryFindAbMergeProfileSummary(profileSelector, out WorkbenchProfileSummary? selectedProfile))
        {
            await error.WriteLineAsync($"error: unknown AB Merge profile '{profileSelector}'").ConfigureAwait(false);
            return UsageError;
        }

        if (!AbMergeWorkbenchCompositionService.TryCompileAbMerge(
                selectedProfile.IcId,
                out CompiledComposition? composition,
                out IReadOnlyList<CompositionIssue> issues))
        {
            await CliCompositionRunSupport.PrintIssuesAsync(error, issues).ConfigureAwait(false);
            return SoftwareError;
        }

        if (!TryCreateBindings(composition.Plan.RequiredInputAddressSpaceIds, options, error, out IReadOnlyList<InputArtifactBinding> bindings))
        {
            return UsageError;
        }

        bindings =
        [
            .. bindings.Select(binding => CompiledCompositionInputBindingFactory.Create(
                composition,
                binding.AddressSpaceId,
                binding.ArtifactId)),
        ];
        CliOutputTarget outputTarget = CliCompositionRunSupport.ResolveOutputTarget(
            options.Values.GetValueOrDefault("--output"),
            composition.DefaultOutputFileName);
        if (action == "build")
        {
            CliCompositionRunSupport.EnsureOutputDoesNotAliasInputs(outputTarget, bindings);
        }

        CliCompositionRunSupport.EnsureReportDoesNotAliasProtectedPaths(
            options.Values.GetValueOrDefault("--report"),
            bindings,
            outputTarget,
            action == "build");
        string[] inputRoots = [.. bindings.Select(binding => Path.GetDirectoryName(binding.ArtifactId)!)];
        var reader = new FileArtifactReader(inputRoots);
        AtomicFileCompositionOutputWriter? writer = action == "build"
            ? new AtomicFileCompositionOutputWriter(outputTarget.OutputDirectory, options.Flags.Contains("--overwrite"))
            : null;
        var service = new CompositionRunService(reader, new SystemClock(), writer, externalProcessor: null);
        var request = new CompositionRunRequest(
            CreateRunId(action),
            composition,
            bindings,
            outputTarget.FileName);

        CompositionRunResult result = await service
            .PreviewOrBuildAsync(request, action == "build", cancellationToken)
            .ConfigureAwait(false);
        await CliCompositionRunSupport.WriteReportFileIfRequestedAsync(
                result,
                options.Values.GetValueOrDefault("--report"),
                bindings,
                outputTarget,
                action == "build",
                output,
                cancellationToken)
            .ConfigureAwait(false);
        await PrintRunResultAsync(result, output, error).ConfigureAwait(false);
        return result.Status == CompositionExecutionStatus.Succeeded ? Success : CompositionFailed;
    }

    private static bool TryFindAbMergeProfileSummary(
        string selector,
        [NotNullWhen(true)] out WorkbenchProfileSummary? profile)
    {
        string normalized = selector.Trim();
        profile = WorkbenchCompositionService.GetAbMergeProfileSummaries().FirstOrDefault(candidate =>
            string.Equals(candidate.ProfileId, normalized, StringComparison.OrdinalIgnoreCase) ||
            string.Equals(candidate.IcId, normalized, StringComparison.OrdinalIgnoreCase) ||
            string.Equals(CliCompositionRunSupport.GetIcNumber(candidate.IcId), normalized, StringComparison.OrdinalIgnoreCase));
        return profile is not null;
    }
}
