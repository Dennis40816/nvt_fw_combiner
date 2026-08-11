using System.Diagnostics.CodeAnalysis;
using NvtFwCombiner.Application.Authoring;
using NvtFwCombiner.Application.Capabilities;
using NvtFwCombiner.Domain.Composition;

namespace NvtFwCombiner.Cli;

public static partial class CliApplication
{
    private static readonly Dictionary<string, string> InputOptionsByAddressSpace =
        new(StringComparer.Ordinal)
        {
            [CompositionAddressSpaceIds.DpInput] = "--dp",
            [CompositionAddressSpaceIds.TpInput] = "--tp",
            [CompositionAddressSpaceIds.LdcInput] = "--ldc",
        };

    private static async Task<int> RunStandardMergeAsync(
        CompositionHostServices host,
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
                host.CompositionCapabilityExperience,
                profileSelector,
                out CapabilityProfileSummary? selectedProfile))
        {
            await error.WriteLineAsync($"error: unknown standard merge profile '{profileSelector}'").ConfigureAwait(false);
            return UsageError;
        }

        if (!selectedProfile.CompileSucceeded)
        {
            CompiledAuthoringSelectionSnapshot unavailable =
                host.StandardMergeAuthoring.GetAuthoringSnapshot(
                selectedProfile.IcId,
                [],
                new Dictionary<string, FileStamp>(StringComparer.Ordinal),
                new AuthoringRevision(1));
            await CliCompositionRunSupport.PrintIssuesAsync(error, unavailable.Issues)
                .ConfigureAwait(false);
            return SoftwareError;
        }

        IReadOnlyList<string> availableInputAddressSpaces =
            host.StandardMergeAuthoring.GetInputAddressSpaces(
                selectedProfile.IcId);
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

        var slotPaths = InputOptionsByAddressSpace
            .Where(pair => options.Values.ContainsKey(pair.Value))
            .ToDictionary(
                static pair => pair.Key,
                pair => Path.GetFullPath(options.Values[pair.Value]),
                StringComparer.Ordinal);
        if (!slotPaths.ContainsKey(CompositionAddressSpaceIds.DpInput))
        {
            await error.WriteLineAsync(
                    $"error: {InputOptionsByAddressSpace[CompositionAddressSpaceIds.DpInput]} is required for address space '{CompositionAddressSpaceIds.DpInput}'")
                .ConfigureAwait(false);
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
                            StringComparer.Ordinal.Equals(
                                    slotId,
                                    CompositionAddressSpaceIds.DpInput) &&
                                !File.Exists(path)
                                    ? $"Selected DP BIN path does not exist for {selectedProfile.IcId} Standard Merge."
                                    : exception.Message,
                            slotId)])
                    .ConfigureAwait(false);
                return SoftwareError;
            }
        }
        var session = new AuthoringSessionState(ExperienceIds.StandardMerge);
        CompiledAuthoringSessionPreparation prepared =
            host.StandardMergeAuthoring.PrepareSession(
                session,
                selectedProfile.IcId,
                inputs);
        ResolvedCapability? acceptedCapability = prepared.Snapshot?.ExactCapability;
        if (acceptedCapability is not null)
        {
            foreach (string required in acceptedCapability.CompiledComposition.Plan
                .RequiredInputAddressSpaceIds.Where(required => !slotPaths.ContainsKey(required)))
            {
                string option = InputOptionsByAddressSpace.GetValueOrDefault(required, required);
                await error.WriteLineAsync(
                        $"error: {option} is required for address space '{required}'")
                    .ConfigureAwait(false);
                return UsageError;
            }
        }
        if (!prepared.Succeeded || acceptedCapability is null)
        {
            await CliCompositionRunSupport.PrintIssuesAsync(
                    error,
                    CreateStandardMergePreparationIssues(prepared))
                .ConfigureAwait(false);
            return SoftwareError;
        }

        CompiledComposition compiledComposition = acceptedCapability.CompiledComposition;
        InputArtifactBinding[] bindings =
        [
            .. slotPaths.Select(static pair => new InputArtifactBinding(
                pair.Key,
                pair.Key,
                pair.Value)),
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

        bool build = action == "build";
        CompositionRunResult result = await host.CompositionExecution
            .ExecuteAsync(
                new AcceptedCompositionExecutionRequest(
                    prepared.Snapshot!,
                    slotPaths,
                    build,
                    outputPath: build ? outputTarget.FullPath : null,
                    previewOutputFileName: !build && options.Values.ContainsKey("--output")
                        ? outputTarget.FileName
                        : null),
                new CompositionRunProgressFeed(),
                cancellationToken)
            .ConfigureAwait(false);
        string? reportPath = options.Values.GetValueOrDefault("--report");
        if (!string.IsNullOrWhiteSpace(reportPath))
        {
            await CliCompositionRunSupport.WriteReportJsonAsync(
                    reportPath,
                    CompositionRunReportJson.Serialize(result),
                    output,
                    cancellationToken)
                .ConfigureAwait(false);
        }
        await PrintRunResultAsync(result, selectedProfile.IcId, output, error)
            .ConfigureAwait(false);
        return result.Succeeded ? Success : CompositionFailed;
    }

    private static IReadOnlyList<CompositionIssue> CreateStandardMergePreparationIssues(
        CompiledAuthoringSessionPreparation prepared)
    {
        InputSelectionMemberReadiness[] readiness =
        [
            .. prepared.Selection.Slots.Where(static member => member.IssueCode is not null),
        ];
        return readiness.Length != 0
            ?
            [
                .. readiness.Select(static member => new CompositionIssue(
                    member.IssueCode!,
                    member.Reason ?? "Standard Merge selection is not ready.")),
            ]
            : prepared.Issues.Count != 0
                ? prepared.Issues
                : [new CompositionIssue(
                    prepared.SessionIssue?.Code ?? AuthoringSessionIssueCodes.StaleInspection,
                    prepared.SessionIssue?.Message ??
                        "Standard Merge preparation did not produce one accepted session.")];
    }

    private static bool TryFindStandardMergeProfileSummary(
        ICompositionCapabilityExperience capabilities,
        string selector,
        [NotNullWhen(true)]
        out CapabilityProfileSummary? profile)
    {
        string normalized = selector.Trim();
        profile = capabilities.GetStandardMergeProfileSummaries()
            .FirstOrDefault(candidate =>
            string.Equals(candidate.ProfileId, normalized, StringComparison.OrdinalIgnoreCase) ||
            string.Equals(candidate.IcId, normalized, StringComparison.OrdinalIgnoreCase) ||
            string.Equals(CliCompositionRunSupport.GetIcNumber(candidate.IcId), normalized, StringComparison.OrdinalIgnoreCase));
        return profile is not null;
    }
}
