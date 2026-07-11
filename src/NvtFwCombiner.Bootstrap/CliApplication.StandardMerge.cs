using System.Diagnostics.CodeAnalysis;
using NvtFwCombiner.Application.Composition;
using NvtFwCombiner.Domain.Composition;
using NvtFwCombiner.Infrastructure.Files;
using NvtFwCombiner.Infrastructure.Time;
using NvtFwCombiner.Profiles;

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
        if (!TryParseOptions(args[1..], valueOptions, flagOptions, error, out ParsedOptions options))
        {
            return UsageError;
        }

        if (!options.Values.TryGetValue("--profile", out string? profileSelector))
        {
            await error.WriteLineAsync("error: --profile is required").ConfigureAwait(false);
            return UsageError;
        }

        if (!TryFindStandardMergeProfile(profileSelector, out CompositionProfileDefinition? selectedProfile))
        {
            await error.WriteLineAsync($"error: unknown standard merge profile '{profileSelector}'").ConfigureAwait(false);
            return UsageError;
        }

        ProfileCompileResult compile = CompositionProfileCompiler.Compile(selectedProfile, []);
        if (!compile.IsSuccess)
        {
            await CliCompositionRunSupport.PrintIssuesAsync(error, compile.Issues).ConfigureAwait(false);
            return SoftwareError;
        }

        CompiledComposition compiledComposition = compile.CompiledComposition!;
        CompositionPlan plan = compiledComposition.Plan;
        if (!TryCreateBindings(plan, options, error, out IReadOnlyList<InputArtifactBinding> bindings))
        {
            return UsageError;
        }

        selectedProfile = ResolveStandardMergeProfileForBindings(selectedProfile, bindings);
        compile = CompositionProfileCompiler.Compile(selectedProfile, []);
        if (!compile.IsSuccess)
        {
            await CliCompositionRunSupport.PrintIssuesAsync(error, compile.Issues).ConfigureAwait(false);
            return SoftwareError;
        }

        compiledComposition = compile.CompiledComposition!;
        plan = compiledComposition.Plan;
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
            CompiledCompositionRunAdapter.ToLegacyRunProfile(compiledComposition),
            compiledComposition.Plan,
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
        CompositionPlan plan,
        ParsedOptions options,
        TextWriter error,
        out IReadOnlyList<InputArtifactBinding> bindings)
    {
        List<InputArtifactBinding> items = [];
        HashSet<string> requiredAddressSpaces = [.. plan.RequiredInputAddressSpaceIds];
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

    private static CompositionProfileDefinition ResolveStandardMergeProfileForBindings(
        CompositionProfileDefinition profile,
        IReadOnlyList<InputArtifactBinding> bindings)
    {
        if (!BuiltInStandardMergeProfiles.IsDpPerspectiveStandardMergeProfile(profile))
        {
            return profile;
        }

        InputArtifactBinding? dpBinding = bindings.FirstOrDefault(binding =>
            string.Equals(binding.AddressSpaceId, CompositionAddressSpaceIds.DpInput, StringComparison.Ordinal));
        return dpBinding is null || !File.Exists(dpBinding.ArtifactId)
            ? profile
            : BuiltInStandardMergeProfiles.CreateDpPerspectiveProfileForInputLength(
                profile.IcId,
                new FileInfo(dpBinding.ArtifactId).Length);
    }

    private static bool TryFindStandardMergeProfile(
        string selector,
        [NotNullWhen(true)]
        out CompositionProfileDefinition? profile)
    {
        string normalized = selector.Trim();
        profile = BuiltInStandardMergeProfiles.ExecutableStandardMergeProfiles.FirstOrDefault(candidate =>
            string.Equals(candidate.ProfileId, normalized, StringComparison.OrdinalIgnoreCase) ||
            string.Equals(candidate.IcId, normalized, StringComparison.OrdinalIgnoreCase) ||
            string.Equals(CliCompositionRunSupport.GetIcNumber(candidate.IcId), normalized, StringComparison.OrdinalIgnoreCase));
        return profile is not null;
    }
}
