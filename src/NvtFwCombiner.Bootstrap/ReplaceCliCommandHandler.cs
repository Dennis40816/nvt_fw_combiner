using System.Diagnostics.CodeAnalysis;
using NvtFwCombiner.Application.Composition;
using NvtFwCombiner.Domain.Composition;
using NvtFwCombiner.Infrastructure.Files;
using NvtFwCombiner.Infrastructure.Time;
using NvtFwCombiner.Profiles;

namespace NvtFwCombiner.Bootstrap;

internal static partial class ReplaceCliCommandHandler
{
    private const int Success = 0;
    private const int CompositionFailed = 1;
    private const int UsageError = 64;
    private const int SoftwareError = 70;

    private static readonly Dictionary<string, string> FixedInputOptionsByAddressSpace =
        new(StringComparer.Ordinal)
        {
            ["reference-base"] = "--base",
            ["dp-replacement"] = "--dp",
            ["ld-replacement"] = "--ld",
            ["ctrlram-replacement"] = "--ctrlram",
            ["replacement-input"] = "--input",
        };

    internal static async Task<int> RunAsync(
        string command,
        string[] args,
        TextWriter output,
        TextWriter error,
        CancellationToken cancellationToken)
    {
        if (args.Length == 0 || args[0] is "--help")
        {
            await WriteUsageAsync(command, output).ConfigureAwait(false);
            return args.Length == 0 ? UsageError : Success;
        }

        string action = args[0];
        if (action is not ("preview" or "build"))
        {
            await error.WriteLineAsync($"error: unknown {command} command '{action}'").ConfigureAwait(false);
            return UsageError;
        }

        string[] valueOptions = [
            "--profile",
            "--ic-family",
            "--ic-num",
            "--base",
            "--dp",
            "--ld",
            "--ctrlram",
            "--input",
            "--source-start",
            "--target-start",
            "--length",
            "--mapping",
            "--output",
            "--report",
        ];
        string[] flagOptions = action == "build" ? ["--overwrite"] : [];
        string[] repeatableValueOptions = command switch
        {
            IcWorkflowIds.CtrlRamReplace => ["--ctrlram"],
            IcWorkflowIds.GeneralReplace => ["--mapping"],
            _ => [],
        };
        if (!TryParseOptions(args[1..], valueOptions, repeatableValueOptions, flagOptions, error, out ParsedOptions options))
        {
            return UsageError;
        }

        if (!options.Values.TryGetValue("--profile", out string? profileSelector))
        {
            await error.WriteLineAsync("error: --profile is required").ConfigureAwait(false);
            return UsageError;
        }

        if (command == IcWorkflowIds.DpReplace &&
            TryResolveDpPerspectiveDpReplaceIc(profileSelector, out string? dpWorkbenchIcId))
        {
            return await RunWorkbenchDpReplaceAsync(
                    action,
                    dpWorkbenchIcId,
                    options,
                    output,
                    error,
                    cancellationToken)
                .ConfigureAwait(false);
        }

        if (!TryFindReplaceProfile(command, profileSelector, out CompositionProfileDefinition? selectedProfile))
        {
            return command switch
            {
                IcWorkflowIds.CtrlRamReplace => await RunWorkbenchCtrlRamReplaceAsync(
                        action,
                        profileSelector,
                        options,
                        output,
                        error,
                        cancellationToken)
                    .ConfigureAwait(false),
                IcWorkflowIds.GeneralReplace => await RunWorkbenchGeneralReplaceAsync(
                        action,
                        profileSelector,
                        options,
                        output,
                        error,
                        cancellationToken)
                    .ConfigureAwait(false),
                _ => await UnknownReplaceProfileAsync(command, profileSelector, error).ConfigureAwait(false),
            };
        }

        if (command == IcWorkflowIds.CtrlRamReplace && options.GetValues("--ctrlram").Count > 1)
        {
            await error.WriteLineAsync(
                    "error: built-in CtrlRAM profiles accept one --ctrlram path; use --profile <IC> with repeated --ctrlram <slot-id=path> for multi-region replacement.")
                .ConfigureAwait(false);
            return UsageError;
        }

        if (!TryCreateIcNumberSelection(selectedProfile, options, error, out IcNumberSelection? icNumberSelection))
        {
            return UsageError;
        }

        if (!TryCompileProfile(selectedProfile, options, error, out ProfileCompileResult compile))
        {
            return UsageError;
        }

        if (!compile.IsSuccess)
        {
            await CliCompositionRunSupport.PrintIssuesAsync(error, compile.Issues).ConfigureAwait(false);
            return SoftwareError;
        }

        CompositionPlan plan = compile.Plan!;
        if (!TryCreateBindings(plan, options, error, out IReadOnlyList<InputArtifactBinding> bindings))
        {
            return UsageError;
        }

        CliOutputTarget outputTarget = CliCompositionRunSupport.ResolveOutputTarget(
            options.Values.GetValueOrDefault("--output"),
            selectedProfile.DefaultOutputFileName);
        if (action == "build")
        {
            CliCompositionRunSupport.EnsureOutputDoesNotAliasInputs(outputTarget, bindings);
        }

        CliCompositionRunSupport.EnsureReportDoesNotAliasProtectedPaths(
            options.Values.GetValueOrDefault("--report"),
            bindings,
            outputTarget,
            action == "build");

        string[] inputRoots = [.. bindings.Select(binding => Path.GetDirectoryName(binding.ArtifactId)!)
            .Distinct(StringComparer.OrdinalIgnoreCase)];
        var reader = new FileArtifactReader(inputRoots);
        AtomicFileCompositionOutputWriter? writer = action == "build"
            ? new AtomicFileCompositionOutputWriter(outputTarget.OutputDirectory, options.Flags.Contains("--overwrite"))
            : null;
        var service = new CompositionRunService(reader, new SystemClock(), writer, ExternalProcessorFactory.CreateOrNull());
        var request = new CompositionRunRequest(
            CreateRunId(command, action),
            CliCompositionRunSupport.ToRunProfile(selectedProfile),
            plan,
            bindings,
            outputTarget.FileName,
            icNumberSelection: icNumberSelection);

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

    private static bool TryCreateIcNumberSelection(
        CompositionProfileDefinition profile,
        ParsedOptions options,
        TextWriter error,
        [NotNullWhen(true)] out IcNumberSelection? selection)
    {
        selection = null;
        if (profile.IcNumberInputMode is null)
        {
            error.WriteLine($"error: replace profile '{profile.ProfileId}' does not declare an IC num input mode");
            return false;
        }

        if (!RequireOption(options, "--ic-num", error, out string? icNumber))
        {
            return false;
        }

        if (profile.IcNumberInputMode == IcNumberInputMode.SingleSelector)
        {
            if (options.Values.ContainsKey("--ic-family"))
            {
                error.WriteLine("error: --ic-family is used only by cascade IC num profiles");
                return false;
            }

            selection = new IcNumberSelection(IcNumberInputMode.SingleSelector, [icNumber]);
            return true;
        }

        if (profile.IcNumberInputMode == IcNumberInputMode.NumericSelector)
        {
            if (options.Values.ContainsKey("--ic-family"))
            {
                error.WriteLine("error: --ic-family is used only by cascade IC num profiles");
                return false;
            }

            if (!int.TryParse(icNumber, out int parsedIcNumber) || parsedIcNumber <= 0)
            {
                error.WriteLine("error: numeric --ic-num must be a positive integer");
                return false;
            }

            selection = new IcNumberSelection(IcNumberInputMode.NumericSelector, [icNumber]);
            return true;
        }

        if (!RequireOption(options, "--ic-family", error, out string? icFamily))
        {
            return false;
        }

        selection = new IcNumberSelection(IcNumberInputMode.CascadeSelector, [icFamily, icNumber]);
        return true;
    }

    private static bool TryCreateBindings(
        CompositionPlan plan,
        ParsedOptions options,
        TextWriter error,
        out IReadOnlyList<InputArtifactBinding> bindings)
    {
        List<InputArtifactBinding> items = [];
        HashSet<string> usedInputOptions = new(StringComparer.Ordinal);
        foreach (string addressSpaceId in plan.RequiredInputAddressSpaceIds.Order(StringComparer.Ordinal))
        {
            if (!FixedInputOptionsByAddressSpace.TryGetValue(addressSpaceId, out string? optionName))
            {
                error.WriteLine($"error: profile requires unsupported address space '{addressSpaceId}'");
                bindings = [];
                return false;
            }

            if (!RequireOption(options, optionName, error, out string? path))
            {
                bindings = [];
                return false;
            }

            string fullPath = Path.GetFullPath(path);
            items.Add(new InputArtifactBinding(addressSpaceId, addressSpaceId, fullPath));
            _ = usedInputOptions.Add(optionName);
        }

        foreach (string optionName in FixedInputOptionsByAddressSpace.Values.Order(StringComparer.Ordinal))
        {
            if (options.Values.ContainsKey(optionName) && !usedInputOptions.Contains(optionName))
            {
                error.WriteLine($"error: option '{optionName}' is not used by the selected replace profile");
                bindings = [];
                return false;
            }
        }

        bindings = items;
        return true;
    }

    private static bool TryFindReplaceProfile(
        string command,
        string selector,
        [NotNullWhen(true)] out CompositionProfileDefinition? profile)
    {
        string normalized = selector.Trim();
        profile = BuiltInReplaceProfiles.All.FirstOrDefault(candidate =>
            string.Equals(candidate.ExperienceId, command, StringComparison.Ordinal) &&
            (string.Equals(candidate.ProfileId, normalized, StringComparison.OrdinalIgnoreCase) ||
                string.Equals(candidate.IcId, normalized, StringComparison.OrdinalIgnoreCase) ||
                string.Equals(CliCompositionRunSupport.GetIcNumber(candidate.IcId), normalized, StringComparison.OrdinalIgnoreCase)));
        return profile is not null;
    }
}
