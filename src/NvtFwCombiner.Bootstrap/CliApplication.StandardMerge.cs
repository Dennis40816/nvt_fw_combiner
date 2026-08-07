using System.Diagnostics.CodeAnalysis;
using NvtFwCombiner.Application.Capabilities;
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
            [CompositionAddressSpaceIds.LdcInput] = "--ldc",
            [CompositionAddressSpaceIds.DpAbInput] = "--dp-ab",
            [CompositionAddressSpaceIds.TpAInput] = "--tp-a",
            [CompositionAddressSpaceIds.TpBInput] = "--tp-b",
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

        string[] valueOptions = ["--profile", "--dp", "--tp", "--ldc", "--output", "--report"];
        if (!CliOptionParser.TryParse(
                args[1..],
                valueOptions,
                [],
                [],
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

        if (!TryFindStandardMergeProfileSummary(
                profileSelector,
                out CapabilityProfileSummary? selectedProfile))
        {
            await error.WriteLineAsync($"error: unknown standard merge profile '{profileSelector}'").ConfigureAwait(false);
            return UsageError;
        }

        if (!selectedProfile.CompileSucceeded)
        {
            _ = CanonicalCapabilityResolution.TryCompileStandardMerge(
                selectedProfile.IcId,
                dpInputLength: null,
                out _,
                out IReadOnlyList<CompositionIssue> compileIssues);
            await CliCompositionRunSupport.PrintIssuesAsync(error, compileIssues).ConfigureAwait(false);
            return SoftwareError;
        }

        IReadOnlyList<string> availableInputAddressSpaces =
            CanonicalAuthoringAdapter.GetStandardMergeInputAddressSpaces(selectedProfile.IcId);
        foreach ((string addressSpaceId, string optionName) in InputOptionsByAddressSpace)
        {
            if (options.Values.ContainsKey(optionName) &&
                !availableInputAddressSpaces.Contains(addressSpaceId, StringComparer.Ordinal))
            {
                await error.WriteLineAsync($"error: {optionName} is not used by this profile")
                    .ConfigureAwait(false);
                return UsageError;
            }
        }

        string? dpPath = options.Values.TryGetValue("--dp", out string? selectedDpPath)
            ? Path.GetFullPath(selectedDpPath)
            : null;
        if (!CompositionExecutionAdapter.TryGetStandardMergeDpInputLength(
                selectedProfile.IcId,
                dpPath,
                out long? dpInputLength,
                out CompositionIssue? inputIssue))
        {
            await CliCompositionRunSupport.PrintIssuesAsync(error, [inputIssue]).ConfigureAwait(false);
            return SoftwareError;
        }

        if (!CanonicalCapabilityResolution.TryCompileStandardMerge(
                selectedProfile.IcId,
                dpInputLength,
                [
                    .. InputOptionsByAddressSpace
                        .Where(pair => options.Values.ContainsKey(pair.Value))
                        .Select(static pair => pair.Key),
                ],
                out CompiledComposition? compiledComposition,
                out ResolvedCapability? resolvedCapability,
                out IReadOnlyList<CompositionIssue> issues))
        {
            await CliCompositionRunSupport.PrintIssuesAsync(error, issues).ConfigureAwait(false);
            return SoftwareError;
        }

        if (!TryCreateBindings(
                compiledComposition.Plan.RequiredInputAddressSpaceIds,
                options,
                error,
                out IReadOnlyList<InputArtifactBinding> bindings))
        {
            return UsageError;
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
            compiledComposition.V2Details.OutputNamingRequirement.FileNameTemplate);
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
            ? new AtomicFileCompositionOutputWriter(outputTarget.OutputDirectory, overwrite: true)
            : null;
        var service = new CompositionRunService(reader, new SystemClock(), writer, ExternalProcessorFactory.GetOrCreateOrNull());
        var request = new CompositionRunRequest(
            CreateRunId(action),
            compiledComposition,
            bindings,
            outputTarget.FileName,
            resolvedCapability: CanonicalCapabilityResolution.ResolveCanonicalCapabilityForRun(
                compiledComposition,
                resolvedCapability));

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
        out CapabilityProfileSummary? profile)
    {
        string normalized = selector.Trim();
        profile = CanonicalCapabilityProjection.GetStandardMergeProfileSummaries()
            .FirstOrDefault(candidate =>
            string.Equals(candidate.ProfileId, normalized, StringComparison.OrdinalIgnoreCase) ||
            string.Equals(candidate.IcId, normalized, StringComparison.OrdinalIgnoreCase) ||
            string.Equals(CliCompositionRunSupport.GetIcNumber(candidate.IcId), normalized, StringComparison.OrdinalIgnoreCase));
        return profile is not null;
    }
}
