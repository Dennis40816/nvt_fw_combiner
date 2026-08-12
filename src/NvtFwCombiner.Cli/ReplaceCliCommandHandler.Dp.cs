using NvtFwCombiner.Application.Authoring;
using NvtFwCombiner.Application.Capabilities;
using NvtFwCombiner.Domain.Composition;

namespace NvtFwCombiner.Cli;

internal static partial class ReplaceCliCommandHandler
{
    private static async Task<int> RunDpReplaceAsync(
        CompositionHostServices host,
        string action,
        string icId,
        ParsedCliOptions options,
        TextWriter output,
        TextWriter error,
        CancellationToken cancellationToken)
    {
        if (!RequireOption(options, "--ic-num", error, out string? icNumber) ||
            !RequireOption(options, "--base", error, out string? basePath))
        {
            return UsageError;
        }

        if (!IcNumberSelectionTokens.IsSingle(icNumber))
        {
            string supportedIcIds = string.Join(
                "/",
                host.CompositionCapabilityExperience.GetDpReplaceProfileSummaries()
                    .Select(static profile => profile.IcId)
                    .Distinct(StringComparer.Ordinal)
                    .Order(StringComparer.Ordinal));
            error.WriteLine($"error: {supportedIcIds} DP Replace requires --ic-num {IcNumberSelectionTokens.SingleChip}");
            return UsageError;
        }

        Dictionary<string, string> slotPaths = new(StringComparer.Ordinal)
        {
            [CompositionSlotIds.ReplaceBase] = Path.GetFullPath(basePath),
        };
        CompiledAuthoringSelectionSnapshot discovery =
            host.DpReplaceAuthoring.GetAuthoringSnapshot(
                icId,
                [],
                new Dictionary<string, FileStamp>(StringComparer.Ordinal),
                new AuthoringRevision(1));
        CompiledAuthoringInputBinding replacementBinding = discovery.InputBindings.Single(binding =>
            !StringComparer.Ordinal.Equals(
                binding.AddressSpaceId,
                CompositionAddressSpaceIds.ReferenceBase) &&
            !StringComparer.Ordinal.Equals(
                binding.AddressSpaceId,
                CompositionAddressSpaceIds.LdcReplacement));
        bool replacementSelectionGroup = discovery.Slots.Any(slot =>
            StringComparer.Ordinal.Equals(slot.SlotId, replacementBinding.SlotId) &&
            !slot.IsRequired);
        if (options.Values.TryGetValue("--dp", out string? dpPath))
        {
            slotPaths[CompositionSlotIds.ReplaceDp] = Path.GetFullPath(dpPath);
        }
        else if (!replacementSelectionGroup)
        {
            error.WriteLine("error: --dp is required");
            return UsageError;
        }

        bool requiresLdc = discovery.InputBindings.Any(static binding =>
            StringComparer.Ordinal.Equals(
                binding.AddressSpaceId,
                CompositionAddressSpaceIds.LdcReplacement));
        if (requiresLdc)
        {
            if (options.Values.TryGetValue("--ldc", out string? ldcPath))
            {
                slotPaths[CompositionSlotIds.ReplaceLdc] = Path.GetFullPath(ldcPath);
            }
        }
        else if (options.Values.ContainsKey("--ldc"))
        {
            error.WriteLine($"error: --ldc is not declared by the {icId} DP Replace profile");
            return UsageError;
        }

        var session = new AuthoringSessionState(ExperienceIds.DpReplace);
        CompiledAuthoringSelectedInput[] selectedInputs =
        [
            .. slotPaths.Select(pair =>
            {
                string addressSpaceId = pair.Key switch
                {
                    CompositionSlotIds.ReplaceBase => CompositionAddressSpaceIds.ReferenceBase,
                    CompositionSlotIds.ReplaceLdc => CompositionAddressSpaceIds.LdcReplacement,
                    _ => replacementBinding.AddressSpaceId,
                };
                return new CompiledAuthoringSelectedInput(
                    addressSpaceId,
                    pair.Value,
                    File.ReadAllBytes(pair.Value));
            }),
        ];
        CompiledAuthoringSessionPreparation prepared =
            host.DpReplaceAuthoring.PrepareSession(
                session,
                icId,
                selectedInputs);
        if (!prepared.Succeeded)
        {
            IReadOnlyList<CompositionIssue> issues = CreatePreparationIssues(
                prepared,
                out bool preparationUsageError);
            await CliCompositionRunSupport.PrintIssuesAsync(error, issues)
                .ConfigureAwait(false);
            return preparationUsageError ? UsageError : CompositionFailed;
        }

        ActiveSessionSnapshot acceptedSession = prepared.Snapshot!;
        string defaultOutputFileName = host.CompositionOutputNaming
            .ResolveAcceptedOutput(acceptedSession)
            .OutputName.FileName;
        ValueTask<CompositionRunResult> RunAcceptedAsync(
            string? outputPath,
            bool build,
            CancellationToken token)
        {
            return host.CompositionExecution.ExecuteAsync(
                new AcceptedCompositionExecutionRequest(
                    acceptedSession,
                    slotPaths,
                    build,
                    outputPath: outputPath),
                new CompositionRunProgressFeed(),
                token);
        }

        int exitCode = await CompleteReplaceRunAsync(
                action,
                icId,
                ExperienceIds.DpReplace,
                options,
                slotPaths,
                defaultOutputFileName,
                RunAcceptedAsync,
                output,
                error,
                cancellationToken)
            .ConfigureAwait(false);
        return exitCode;
    }

    private static IReadOnlyList<CompositionIssue> CreatePreparationIssues(
        CompiledAuthoringSessionPreparation prepared,
        out bool usageError)
    {
        InputSelectionMemberReadiness[] readinessIssues =
        [
            .. prepared.Selection.Slots.Where(static readiness =>
                readiness.IssueCode is not null),
        ];
        if (readinessIssues.Length != 0)
        {
            usageError = readinessIssues.Any(static readiness =>
                readiness.IssueCode == InputSelectionReadinessIssueCodes.SelectionPending);
            return
            [
                .. readinessIssues.Select(static readiness => new CompositionIssue(
                    readiness.IssueCode!,
                    readiness.Reason ?? "The selected DP Replace input is not ready.")),
            ];
        }

        usageError = false;
        if (prepared.Issues.Count != 0)
        {
            return prepared.Issues;
        }

        if (prepared.SessionIssue is { } sessionIssue)
        {
            return [new CompositionIssue(
                sessionIssue.Code,
                sessionIssue.Message,
                sessionIssue.Subject)];
        }

        CompositionIssue[] inspectionIssues =
        [
            .. prepared.Snapshot?.InputSlotStatuses
                .Where(static status => status.BlocksBuild)
                .Select(static status => new CompositionIssue(
                    status.InspectionIssueCode ??
                        InputSelectionReadinessIssueCodes.SelectionNotApplicable,
                    status.SelectionReadiness.Reason ??
                        $"Input '{status.SlotId}' did not pass its compiled inspection.")) ?? [],
        ];
        return inspectionIssues.Length == 0
            ? [new CompositionIssue(
                InputSelectionReadinessIssueCodes.SelectionNotApplicable,
                "DP Replace authoring preparation did not produce one current exact session.")]
            : inspectionIssues;
    }
}
