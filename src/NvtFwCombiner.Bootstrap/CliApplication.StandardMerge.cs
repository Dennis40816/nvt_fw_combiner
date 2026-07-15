using System.Diagnostics.CodeAnalysis;
using NvtFwCombiner.Application.Composition;
using NvtFwCombiner.Domain.Composition;
using NvtFwCombiner.Infrastructure.Files;
using NvtFwCombiner.Infrastructure.Time;

namespace NvtFwCombiner.Bootstrap;

public static partial class CliApplication
{
    private static readonly Dictionary<string, string> InputOptionsByAddressSpace =
        new(StringComparer.Ordinal)
        {
            [CompositionAddressSpaceIds.DpInput] = "--dp",
            [CompositionAddressSpaceIds.TpInput] = "--tp",
            [CompositionAddressSpaceIds.LdInput] = "--ld",
        };

    private static async Task<int> RunStandardMergeAsync(
        string[] args,
        TextWriter output,
        TextWriter error,
        CancellationToken cancellationToken)
    {
        if (args.Length == 0 || args[0] is "--help")
        {
            await WriteStandardMergeUsageAsync(output).ConfigureAwait(false);
            return args.Length == 0 ? UsageError : Success;
        }

        string action = args[0];
        if (action is not ("preview" or "build"))
        {
            await error.WriteLineAsync($"error: unknown standard-merge command '{action}'").ConfigureAwait(false);
            return UsageError;
        }

        string[] valueOptions = ["--profile", "--dp", "--tp", "--ld", "--output", "--report"];
        string[] flagOptions = action == "build" ? ["--overwrite"] : [];
        if (!CliOptionParser.TryParse(
                args[1..],
                valueOptions,
                [],
                flagOptions,
                error,
                out ParsedCliOptions options))
        {
            return UsageError;
        }

        if (!options.Values.TryGetValue("--profile", out string? profileSelector))
        {
            await error.WriteLineAsync("error: --profile is required").ConfigureAwait(false);
            return UsageError;
        }

        if (!TryFindStandardMergeProfileSummary(profileSelector, out WorkbenchProfileSummary? selectedProfile))
        {
            await error.WriteLineAsync($"error: unknown standard merge profile '{profileSelector}'").ConfigureAwait(false);
            return UsageError;
        }

        if (!selectedProfile.CompileSucceeded)
        {
            _ = WorkbenchCompositionService.TryCompileStandardMerge(
                selectedProfile.IcId,
                dpInputLength: null,
                out _,
                out IReadOnlyList<CompositionIssue> compileIssues);
            await CliCompositionRunSupport.PrintIssuesAsync(error, compileIssues).ConfigureAwait(false);
            return SoftwareError;
        }

        if (!TryCreateBindings(
                selectedProfile.RequiredInputAddressSpaceIds,
                options,
                error,
                out IReadOnlyList<InputArtifactBinding> bindings))
        {
            return UsageError;
        }

        string? dpPath = bindings.FirstOrDefault(binding =>
            string.Equals(binding.AddressSpaceId, CompositionAddressSpaceIds.DpInput, StringComparison.Ordinal))?.ArtifactId;
        if (!WorkbenchCompositionService.TryGetStandardMergeDpInputLength(
                selectedProfile.IcId,
                dpPath,
                out long? dpInputLength,
                out CompositionIssue? inputIssue))
        {
            await CliCompositionRunSupport.PrintIssuesAsync(error, [inputIssue]).ConfigureAwait(false);
            return SoftwareError;
        }

        if (!WorkbenchCompositionService.TryCompileStandardMerge(
                selectedProfile.IcId,
                dpInputLength,
                out CompiledComposition? compiledComposition,
                out IReadOnlyList<CompositionIssue> issues))
        {
            await CliCompositionRunSupport.PrintIssuesAsync(error, issues).ConfigureAwait(false);
            return SoftwareError;
        }

        if (!selectedProfile.RequiredInputAddressSpaceIds.SequenceEqual(
                compiledComposition.Plan.RequiredInputAddressSpaceIds,
                StringComparer.Ordinal))
        {
            await error.WriteLineAsync(
                    $"error: Standard Merge profile '{selectedProfile.ProfileId}' changed required input address spaces during DP-length resolution.")
                .ConfigureAwait(false);
            return SoftwareError;
        }

        bindings =
        [
            .. bindings.Select(binding => CompiledCompositionInputBindingFactory.Create(
                compiledComposition,
                binding.AddressSpaceId,
                binding.ArtifactId)),
        ];

        CliOutputTarget outputTarget = CliCompositionRunSupport.ResolveOutputTarget(
            options.Values.GetValueOrDefault("--output"),
            compiledComposition.DefaultOutputFileName);
        if (action == "build")
        {
            CliCompositionRunSupport.EnsureOutputDoesNotAliasInputs(outputTarget, bindings);
        }

        CliCompositionRunSupport.EnsureReportDoesNotAliasProtectedPaths(
            options.Values.GetValueOrDefault("--report"),
            bindings,
            outputTarget,
            action == "build");

        string[] inputRoots = [.. bindings
            .Select(binding => Path.GetDirectoryName(binding.ArtifactId)!)];
        var reader = new FileArtifactReader(inputRoots);
        AtomicFileCompositionOutputWriter? writer = action == "build"
            ? new AtomicFileCompositionOutputWriter(outputTarget.OutputDirectory, options.Flags.Contains("--overwrite"))
            : null;
        var service = new CompositionRunService(reader, new SystemClock(), writer, ExternalProcessorFactory.CreateOrNull());
        var request = new CompositionRunRequest(
            CreateRunId(action),
            compiledComposition,
            bindings,
            outputTarget.FileName);

        CompositionRunResult result = await CompositionRunExecutionSupport
            .PreviewOrBuildAsync(service, request, action == "build", cancellationToken)
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

    private static bool TryCreateBindings(
        IReadOnlyList<string> requiredInputAddressSpaceIds,
        ParsedCliOptions options,
        TextWriter error,
        out IReadOnlyList<InputArtifactBinding> bindings)
    {
        List<InputArtifactBinding> items = [];
        HashSet<string> requiredAddressSpaces = [.. requiredInputAddressSpaceIds];
        foreach (string addressSpaceId in requiredAddressSpaces.Order(StringComparer.Ordinal))
        {
            if (!InputOptionsByAddressSpace.TryGetValue(addressSpaceId, out string? optionName))
            {
                error.WriteLine($"error: profile requires unsupported address space '{addressSpaceId}'");
                bindings = [];
                return false;
            }

            if (!options.Values.TryGetValue(optionName, out string? path))
            {
                error.WriteLine($"error: {optionName} is required for address space '{addressSpaceId}'");
                bindings = [];
                return false;
            }

            string fullPath = Path.GetFullPath(path);
            items.Add(new InputArtifactBinding(addressSpaceId, addressSpaceId, fullPath));
        }

        foreach ((string addressSpaceId, string optionName) in InputOptionsByAddressSpace)
        {
            if (options.Values.ContainsKey(optionName) && !requiredAddressSpaces.Contains(addressSpaceId))
            {
                error.WriteLine($"error: {optionName} is not used by this profile");
                bindings = [];
                return false;
            }
        }

        bindings = items;
        return true;
    }

    private static bool TryFindStandardMergeProfileSummary(
        string selector,
        [NotNullWhen(true)]
        out WorkbenchProfileSummary? profile)
    {
        string normalized = selector.Trim();
        profile = WorkbenchCompositionService.GetStandardMergeProfileSummaries().FirstOrDefault(candidate =>
            string.Equals(candidate.ProfileId, normalized, StringComparison.OrdinalIgnoreCase) ||
            string.Equals(candidate.IcId, normalized, StringComparison.OrdinalIgnoreCase) ||
            string.Equals(CliCompositionRunSupport.GetIcNumber(candidate.IcId), normalized, StringComparison.OrdinalIgnoreCase));
        return profile is not null;
    }
}
