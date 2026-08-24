using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using NvtFwCombiner.Application.Authoring;
using NvtFwCombiner.Application.Capabilities;
using NvtFwCombiner.Domain.Composition;

namespace NvtFwCombiner.Cli;

/// <summary>Focused CLI adapter for the owner-approved AB Merge pilot.</summary>
internal static class AbMergeCliCommandHandler
{
    private const int Success = 0;
    private const int CompositionFailed = 1;
    private const int UsageError = 64;
    private const int SoftwareError = 70;
    private const string IncludeAFlashCodeOption = "--include-a-flashcode";

    private static readonly Dictionary<string, string> InputOptionsByAddressSpace =
        new(StringComparer.Ordinal)
        {
            [CompositionAddressSpaceIds.DpAbInput] = "--dp-ab",
            [CompositionAddressSpaceIds.TpAInput] = "--tp-a",
            [CompositionAddressSpaceIds.TpBInput] = "--tp-b",
        };

    internal static async Task<int> RunAsync(
        CliCompositionServices services,
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
            await error.WriteLineAsync($"error: unknown ab-merge command '{action}'").ConfigureAwait(false);
            return UsageError;
        }

        string[] valueOptions = ["--profile", "--dp-ab", "--tp-a", "--tp-b", "--ab-topology", "--output", "--report", CliBundleOptions.ParentOption, CliBundleOptions.NameOption];
        if (!CliOptionParser.TryParse(
                args[1..],
                valueOptions,
                [],
                [IncludeAFlashCodeOption],
                error,
                out ParsedCliOptions options))
        {
            return UsageError;
        }

        if (!CliBundleOptions.TryValidateCombination(action, options.Values, error))
        {
            return UsageError;
        }

        bool includeAFlashCode = options.Flags.Contains(IncludeAFlashCodeOption);
        if (includeAFlashCode && action != "build")
        {
            await error.WriteLineAsync(
                    $"error: {IncludeAFlashCodeOption} is available only for build")
                .ConfigureAwait(false);
            return UsageError;
        }

        if (includeAFlashCode && !CliBundleOptions.IsEnabled(options.Values))
        {
            await error.WriteLineAsync(
                    $"error: {IncludeAFlashCodeOption} requires {CliBundleOptions.ParentOption}")
                .ConfigureAwait(false);
            return UsageError;
        }

        if (!options.Values.TryGetValue("--profile", out string? profileSelector))
        {
            await error.WriteLineAsync("error: --profile is required").ConfigureAwait(false);
            return UsageError;
        }

        if (!TryFindProfile(
                services.Capabilities,
                profileSelector,
                out CapabilityProfileSummary? profile))
        {
            await error.WriteLineAsync($"error: unknown AB Merge profile '{profileSelector}'").ConfigureAwait(false);
            return UsageError;
        }

        if (!profile.CompileSucceeded)
        {
            CompiledAuthoringSelectionSnapshot unavailable =
                services.AbMergeAuthoring.GetAuthoringSnapshot(
                profile.IcId,
                topologyToken: null,
                [],
                new Dictionary<string, FileStamp>(StringComparer.Ordinal),
                new AuthoringRevision(1));
            await CliCompositionRunSupport.PrintIssuesAsync(error, unavailable.Issues)
                .ConfigureAwait(false);
            return SoftwareError;
        }

        if (!TryCreateSlotPaths(profile.RequiredInputAddressSpaceIds, options, error, out IReadOnlyDictionary<string, string> slotPaths))
        {
            return UsageError;
        }

        IReadOnlyList<CapabilityTopologyChoice> topologyChoices =
            services.AbMergeAuthoring.GetTopologyChoices(profile.IcId);
        if (!TryCreateTopologySelection(
                topologyChoices,
                options,
                error,
                out _))
        {
            return UsageError;
        }

        List<CompiledAuthoringSelectedInput> inputs = [];
        foreach ((string slotId, string path) in slotPaths)
        {
            try
            {
                inputs.Add(new CompiledAuthoringSelectedInput(
                    slotId,
                    path,
                    File.ReadAllBytes(path)));
            }
            catch (Exception exception) when (
                exception is IOException or UnauthorizedAccessException)
            {
                await CliCompositionRunSupport.PrintIssuesAsync(
                        error,
                        [new CompositionIssue(
                            CompositionPlanningIssueCodes.InputArtifactReadFailed,
                            exception.Message,
                            slotId)])
                    .ConfigureAwait(false);
                return SoftwareError;
            }
        }

        var session = new AuthoringSessionState(ExperienceIds.AbMerge);
        CompiledAuthoringSessionPreparation prepared =
            services.AbMergeAuthoring.PrepareSession(
                session,
                profile.IcId,
                options.Values.GetValueOrDefault("--ab-topology"),
                inputs);
        InputArtifactBinding[] bindings =
        [
            .. slotPaths.Select(pair => new InputArtifactBinding(pair.Key, pair.Key, pair.Value)),
        ];
        bool build = action == "build";
        bool bundleBuild = CliBundleOptions.IsEnabled(options.Values);
        bool hasExplicitOutput = options.Values.ContainsKey("--output");
        CliOutputTarget outputTarget = CliCompositionRunSupport.ResolveOutputTarget(
            options.Values.GetValueOrDefault("--output"),
            profile.DefaultOutputFileName);
        string? reportPath = options.Values.GetValueOrDefault("--report");
        if (build && !hasExplicitOutput && !bundleBuild)
        {
            try
            {
                CompositionOutputPreparation preparation = await services.OutputNaming
                    .PrepareAutomaticOutputAsync(
                        prepared.Snapshot!,
                        cancellationToken)
                    .ConfigureAwait(false);
                string automaticOutputFileName = preparation.OutputName.FileName;
                outputTarget = new CliOutputTarget(outputTarget.OutputDirectory, automaticOutputFileName);
            }
            catch (InvalidOperationException)
            {
                // Preserve the normal failed composition result and its requested report. The
                // runner repeats the same admission before it can commit any output.
            }
        }

        if (build && !bundleBuild)
        {
            CliCompositionRunSupport.EnsureOutputDoesNotAliasInputs(outputTarget, bindings);
        }

        CliCompositionRunSupport.EnsureReportDoesNotAliasProtectedPaths(
            reportPath,
            bindings,
            outputTarget,
            build && !bundleBuild);

        if (!prepared.Succeeded)
        {
            await CliCompositionRunSupport.PrintIssuesAsync(
                    error,
                    CreateAbMergePreparationIssues(prepared))
                .ConfigureAwait(false);
            return CompositionFailed;
        }

        if (!CliBundleOptions.TryCreateIntent(
                services.OutputNaming,
                prepared.Snapshot!,
                options.Values,
                error,
                out CompositionOutputBundleIntent? outputBundle,
                additionalDeliveryKind: includeAFlashCode
                    ? CompiledAdditionalDelivery.AbAFlashCodeKind
                    : null))
        {
            return UsageError;
        }

        CompositionRunResult result = await services.Execution
            .ExecuteAsync(
                new AcceptedCompositionExecutionRequest(
                    prepared.Snapshot!,
                    slotPaths,
                    build,
                    outputPath: build && hasExplicitOutput && !bundleBuild ? outputTarget.FullPath : null,
                    previewOutputFileName: !build && hasExplicitOutput
                        ? outputTarget.FileName
                        : null,
                    automaticOutputDirectory: build && !hasExplicitOutput && !bundleBuild
                        ? outputTarget.OutputDirectory
                        : null,
                    reportPath: build ? reportPath : null,
                    outputBundle: outputBundle),
                new CompositionRunProgressFeed(),
                cancellationToken)
            .ConfigureAwait(false);
        CliCompositionRunSupport.EnsureReportDoesNotAliasProtectedPaths(
            reportPath,
            bindings,
            new CliOutputTarget(outputTarget.OutputDirectory, result.OutputFileName),
            build && !bundleBuild);
        if (!string.IsNullOrWhiteSpace(reportPath))
        {
            await CliCompositionRunSupport.WriteReportJsonAsync(
                    reportPath,
                    CompositionRunReportJson.Serialize(result),
                    output,
                    cancellationToken)
                .ConfigureAwait(false);
        }

        await PrintResultAsync(result, profile.IcId, output, error).ConfigureAwait(false);
        await CliBundleOptions.PrintReceiptAsync(result, output).ConfigureAwait(false);
        return result.Succeeded ? Success : CompositionFailed;
    }

    private static IReadOnlyList<CompositionIssue> CreateAbMergePreparationIssues(
        CompiledAuthoringSessionPreparation prepared)
    {
        return prepared.Issues.Count != 0
            ? prepared.Issues
            : [new CompositionIssue(
                prepared.SessionIssue?.Code ?? AuthoringSessionIssueCodes.StaleInspection,
                prepared.SessionIssue?.Message ??
                    "AB Merge preparation did not produce one accepted session.")];
    }

    private static bool TryCreateSlotPaths(
        IReadOnlyList<string> requiredAddressSpaceIds,
        ParsedCliOptions options,
        TextWriter error,
        out IReadOnlyDictionary<string, string> slotPaths)
    {
        var paths = new Dictionary<string, string>(StringComparer.Ordinal);
        HashSet<string> requiredAddressSpaces = [.. requiredAddressSpaceIds];
        foreach (string addressSpaceId in requiredAddressSpaces.Order(StringComparer.Ordinal))
        {
            if (!InputOptionsByAddressSpace.TryGetValue(addressSpaceId, out string? optionName))
            {
                error.WriteLine($"error: AB Merge profile requires unsupported address space '{addressSpaceId}'");
                slotPaths = new Dictionary<string, string>();
                return false;
            }

            if (!options.Values.TryGetValue(optionName, out string? path))
            {
                error.WriteLine($"error: {optionName} is required for address space '{addressSpaceId}'");
                slotPaths = new Dictionary<string, string>();
                return false;
            }

            paths.Add(addressSpaceId, Path.GetFullPath(path));
        }

        foreach ((string addressSpaceId, string optionName) in InputOptionsByAddressSpace)
        {
            if (options.Values.ContainsKey(optionName) && !requiredAddressSpaces.Contains(addressSpaceId))
            {
                error.WriteLine($"error: {optionName} is not used by this profile");
                slotPaths = new Dictionary<string, string>();
                return false;
            }
        }

        slotPaths = paths;
        return true;
    }

    private static bool TryCreateTopologySelection(
        IReadOnlyList<CapabilityTopologyChoice> choices,
        ParsedCliOptions options,
        TextWriter error,
        out Domain.Firmware.TopologySelection? selection)
    {
        selection = null;
        bool hasOption = options.Values.TryGetValue("--ab-topology", out string? token);
        if (choices.Count == 0)
        {
            if (!hasOption)
            {
                return true;
            }

            error.WriteLine("error: --ab-topology is not used by this AB Merge profile");
            return false;
        }

        if (!hasOption)
        {
            error.WriteLine("error: --ab-topology is required; use single or cascade");
            return false;
        }

        CapabilityTopologyChoice? choice = choices.SingleOrDefault(candidate =>
            StringComparer.OrdinalIgnoreCase.Equals(candidate.Token, token!.Trim()));
        if (choice is null)
        {
            error.WriteLine("error: --ab-topology must be single or cascade");
            return false;
        }

        selection = choice.Selection;
        return true;
    }

    private static bool TryFindProfile(
        ICompositionCapabilityExperience capabilities,
        string selector,
        [NotNullWhen(true)] out CapabilityProfileSummary? profile)
    {
        string normalized = selector.Trim();
        profile = capabilities.GetAbMergeProfileSummaries()
            .FirstOrDefault(candidate =>
            string.Equals(candidate.ProfileId, normalized, StringComparison.OrdinalIgnoreCase) ||
            string.Equals(candidate.IcId, normalized, StringComparison.OrdinalIgnoreCase) ||
            string.Equals(
                CliCompositionRunSupport.GetIcNumber(candidate.IcId),
                normalized,
                StringComparison.OrdinalIgnoreCase));
        return profile is not null;
    }

    private static async Task PrintResultAsync(
        CompositionRunResult result,
        string icId,
        TextWriter output,
        TextWriter error)
    {
        await output.WriteLineAsync($"Status: {result.OutcomeStatus}").ConfigureAwait(false);
        await output.WriteLineAsync($"Profile: {result.ProfileId} ({icId})").ConfigureAwait(false);
        await output.WriteLineAsync("Experience: ab-merge").ConfigureAwait(false);
        await output.WriteLineAsync($"Output: {result.OutputFileName}").ConfigureAwait(false);
        await output.WriteLineAsync($"Size: {result.OutputSize.ToString(CultureInfo.InvariantCulture)} bytes").ConfigureAwait(false);
        await output.WriteLineAsync($"SHA256: {result.OutputSha256}").ConfigureAwait(false);
        if (result.CommittedOutputId is not null)
        {
            await output.WriteLineAsync($"Committed: {result.CommittedOutputId}").ConfigureAwait(false);
        }

        if (result.Report.Issues.Count == 0)
        {
            return;
        }

        await error.WriteLineAsync("Issues:").ConfigureAwait(false);
        foreach (CompositionIssue issue in result.Report.Issues)
        {
            string operation = string.IsNullOrWhiteSpace(issue.OperationId)
                ? string.Empty
                : $" [{issue.OperationId}]";
            await error.WriteLineAsync($"  {issue.Code}{operation}: {issue.Message}").ConfigureAwait(false);
        }
    }

    private static async Task WriteUsageAsync(TextWriter output)
    {
        await output.WriteLineAsync("Usage:").ConfigureAwait(false);
        await output.WriteLineAsync("  nvt_fw_combiner ab-merge preview --profile <id|ic> --dp-ab <path> --tp-a <path> --tp-b <path> [--ab-topology <single|cascade>] [--output <path>] [--report <path>]").ConfigureAwait(false);
        await output.WriteLineAsync("  nvt_fw_combiner ab-merge build --profile <id|ic> --dp-ab <path> --tp-a <path> --tp-b <path> [--ab-topology <single|cascade>] [--output <path> | --bundle-parent <existing-directory> [--bundle-name <plain-folder-name>] [--include-a-flashcode]] [--report <path>]").ConfigureAwait(false);
    }
}
