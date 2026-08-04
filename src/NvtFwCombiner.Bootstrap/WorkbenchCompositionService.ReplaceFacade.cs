using NvtFwCombiner.Application.Authoring;
using NvtFwCombiner.Application.Capabilities;
using NvtFwCombiner.Application.Composition;
using NvtFwCombiner.Domain.Composition;
using NvtFwCombiner.Profiles;

namespace NvtFwCombiner.Bootstrap;

public static partial class WorkbenchCompositionService
{
    /// <summary>Gets the default output file name for a Replace build.</summary>
    public static string GetReplaceDefaultOutputFileName(string icId, string replaceMode)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(icId);
        ArgumentException.ThrowIfNullOrWhiteSpace(replaceMode);

        string normalizedIc = icId.ToLowerInvariant();
        return replaceMode switch
        {
            WorkbenchReplaceModes.Dp => $"{normalizedIc}-dp-replace.bin",
            WorkbenchReplaceModes.CtrlRam => $"{normalizedIc}-ctrlram-replace.bin",
            WorkbenchReplaceModes.General => $"{normalizedIc}-general-replace.bin",
            _ => "nvt-fw-combiner-replace.bin",
        };
    }

    /// <summary>Returns true when the IC support catalog exposes the selected Replace workflow.</summary>
    public static bool IsReplaceWorkflowSupported(string icId, string replaceMode)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(icId);
        ArgumentException.ThrowIfNullOrWhiteSpace(replaceMode);
        return GetReplaceWorkflowId(replaceMode) is { } workflowId &&
            IcSupportCatalog.SupportsWorkflow(icId, workflowId);
    }

    /// <summary>Gets selected Replace availability and golden readiness from the IC support catalog.</summary>
    public static WorkbenchWorkflowReadiness GetReplaceWorkflowReadiness(string icId, string replaceMode)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(icId);
        ArgumentException.ThrowIfNullOrWhiteSpace(replaceMode);
        string? workflowId = GetReplaceWorkflowId(replaceMode);
        bool isDpReplace = string.Equals(replaceMode, WorkbenchReplaceModes.Dp, StringComparison.Ordinal);
        string unsupportedReason = isDpReplace
            ? "No owner-approved DP Replace profile/map is registered for this IC."
            : "No owner-approved executable and safety contract is registered for this IC and Replace mode.";
        string openCondition = isDpReplace
            ? "Add the IC-specific DP map/profile, full-byte golden parity, and firmware-owner review."
            : "Owner must reactivate the scope with a safe executable contract, direct evidence, and firmware-owner review.";
        if (workflowId is null || !IcSupportCatalog.TryFind(icId, out IcSupportEntry? entry) || entry is null)
        {
            return new WorkbenchWorkflowReadiness(
                false,
                WorkbenchWorkflowEvidenceStatus.NotAvailable,
                "The selected IC or Replace mode is not declared by the support catalog.",
                "Add an owner-reviewed catalog entry, profile/safety contract, and full-byte evidence.");
        }

        IcWorkflowEvidenceStatus evidenceStatus = entry.GetWorkflowEvidenceStatus(workflowId);
        return evidenceStatus switch
        {
            IcWorkflowEvidenceStatus.GoldenVerified => new WorkbenchWorkflowReadiness(
                true,
                WorkbenchWorkflowEvidenceStatus.GoldenVerified,
                "Direct or owner-approved fact-scoped golden parity is recorded for this workflow.",
                "Golden verification does not grant product support; firmware-owner release review remains separate."),
            IcWorkflowEvidenceStatus.EvidenceGated => new WorkbenchWorkflowReadiness(
                true,
                WorkbenchWorkflowEvidenceStatus.EvidenceGated,
                "The workflow is available, but its direct/fact-scoped golden or owner review is not closed.",
                "Close the current evidence gaps and firmware-owner review; pending evidence alone does not ban authoring."),
            IcWorkflowEvidenceStatus.NotAvailable => new WorkbenchWorkflowReadiness(
                false,
                WorkbenchWorkflowEvidenceStatus.NotAvailable,
                entry.SupportsWorkflow(IcWorkflowIds.DpReplace) ||
                    entry.SupportsWorkflow(IcWorkflowIds.CtrlRamReplace) ||
                    entry.SupportsWorkflow(IcWorkflowIds.GeneralReplace)
                    ? unsupportedReason
                    : entry.Notes ?? unsupportedReason,
                openCondition),
            _ => throw new InvalidOperationException($"Unknown workflow evidence status '{evidenceStatus}'."),
        };
    }

    /// <summary>Gets the owner-defined perfect/partial IC family relation for display.</summary>
    public static WorkbenchIcFamilySummary GetIcFamilySummary(string icId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(icId);
        return IcSupportCatalog.TryFind(icId, out IcSupportEntry? entry) && entry is not null
            ? new WorkbenchIcFamilySummary(
                entry.FamilyId,
                entry.FamilySourceIcId,
                entry.FamilyRelationship switch
                {
                    IcFamilyRelationship.Standalone => WorkbenchIcFamilyRelationship.Standalone,
                    IcFamilyRelationship.Canonical => WorkbenchIcFamilyRelationship.Canonical,
                    IcFamilyRelationship.PerfectAlias => WorkbenchIcFamilyRelationship.PerfectAlias,
                    IcFamilyRelationship.PartialAlias => WorkbenchIcFamilyRelationship.PartialAlias,
                    _ => throw new InvalidOperationException($"Unknown IC family relationship '{entry.FamilyRelationship}'."),
                },
                entry.FamilyScope)
            : new WorkbenchIcFamilySummary(null, null, WorkbenchIcFamilyRelationship.Standalone, null);
    }

    /// <summary>
    /// Returns whether two ICs are owner-declared perfect members of the same family.
    /// This relationship may reconcile a detected UI catalog context without a confirmation prompt;
    /// it does not grant workflow, profile, or byte-write authority.
    /// </summary>
    public static bool ArePerfectFamilyMembers(string firstIcId, string secondIcId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(firstIcId);
        ArgumentException.ThrowIfNullOrWhiteSpace(secondIcId);

        WorkbenchIcFamilySummary first = GetIcFamilySummary(firstIcId);
        WorkbenchIcFamilySummary second = GetIcFamilySummary(secondIcId);
        return !string.IsNullOrWhiteSpace(first.FamilyId) &&
            string.Equals(first.FamilyId, second.FamilyId, StringComparison.Ordinal) &&
            IsPerfectFamilyRelationship(first.Relationship) &&
            IsPerfectFamilyRelationship(second.Relationship);
    }

    private static bool IsPerfectFamilyRelationship(WorkbenchIcFamilyRelationship relationship)
    {
        return relationship is WorkbenchIcFamilyRelationship.Canonical or
            WorkbenchIcFamilyRelationship.PerfectAlias;
    }

    /// <summary>Gets current profile-derived DP Replace Reference FlashCode capacities.</summary>
    public static string? GetDpReplaceReferenceCapacityLabel(string icId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(icId);
        return TryResolveBuiltInV2DpReplaceDisplay(icId, baseCapacity: null, out BuiltInV2DpReplaceDisplay? display) &&
            display.Issues.Count == 0
                ? BuiltInV2Bundle.FormatCapacities(display.SupportedBaseCapacities)
                : null;
    }

    /// <summary>
    /// Ephemeral CLI/Saved Rule boundary: inspect once, then execute the exact
    /// content-bound draft through the strict General Replace runner.
    /// </summary>
    public static ValueTask<WorkbenchRunResult> RunGeneralReplaceEphemeralDraftAsync(
        string icId,
        string number,
        IReadOnlyDictionary<string, string> slotPaths,
        GeneralMappingDraftState mappingDraft,
        bool build,
        CancellationToken cancellationToken,
        string? outputPath = null)
    {
        return RunGeneralReplaceEphemeralDraftAsync(
            icId,
            number,
            slotPaths,
            mappingDraft,
            build,
            outputPath,
            savedRulePolicy: null,
            cancellationToken);
    }

    /// <summary>Internal Saved Rule boundary after host-resolved lifecycle admission.</summary>
    internal static ValueTask<WorkbenchRunResult> RunGeneralReplaceEphemeralDraftAsync(
        string icId,
        string number,
        IReadOnlyDictionary<string, string> slotPaths,
        GeneralMappingDraftState mappingDraft,
        bool build,
        string? outputPath,
        GeneralSavedRuleResourcePolicy? savedRulePolicy,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(icId);
        ArgumentException.ThrowIfNullOrWhiteSpace(number);
        ArgumentNullException.ThrowIfNull(slotPaths);
        ArgumentNullException.ThrowIfNull(mappingDraft);
        return !IcSupportCatalog.SupportsWorkflow(icId, IcWorkflowIds.GeneralReplace)
            ? ValueTask.FromResult(CreateGeneralReplaceUnavailableResult(icId, build))
            : RunGeneralReplaceWithInitialInspectionAsync(
                icId,
                number,
                slotPaths,
                mappingDraft,
                new AuthoringRevision(1),
                build,
                outputPath,
                progress: null,
                cancellationToken,
                savedRulePolicy);
    }

    /// <summary>
    /// Runs General Replace from the exact content-bound draft returned by an
    /// earlier desktop Preview or explicit Reload/Rebind.
    /// </summary>
    public static ValueTask<WorkbenchRunResult> RunGeneralReplaceAcceptedSessionWithProgressAsync(
        string icId,
        string number,
        IReadOnlyDictionary<string, string> slotPaths,
        ActiveSessionSnapshot acceptedSession,
        bool build,
        CompositionRunProgressFeed progress,
        CancellationToken cancellationToken,
        string? outputPath = null)
    {
        ResolvedCapability capability = RequireAcceptedCapability(
            acceptedSession,
            IcWorkflowIds.GeneralReplace,
            icId,
            AuthoringDerivedResultKind.Validation);
        GeneralMappingDraftState draft = acceptedSession.DraftState as GeneralMappingDraftState ??
            throw new InvalidOperationException(
                "The accepted General Replace session has no exact typed draft.");
        return !IcSupportCatalog.SupportsWorkflow(icId, IcWorkflowIds.GeneralReplace)
            ? ValueTask.FromResult(CreateGeneralReplaceUnavailableResult(icId, build))
            : RunGeneralReplaceDraftCoreAsync(
                icId, number, slotPaths, draft, acceptedSession.AuthoringRevision,
                build, outputPath, progress, cancellationToken,
                acceptedCapability: capability);
    }

    private static WorkbenchRunResult CreateGeneralReplaceUnavailableResult(
        string icId,
        bool build)
    {
        return CreateReplaceReportRunResult(
            icId,
            WorkbenchReplaceModes.General,
            new Dictionary<string, string>(StringComparer.Ordinal),
            build,
            [],
            [new CompositionIssue(
                WorkbenchIssueCodes.ReplaceWorkflowNotSupported,
                $"{IcSupportCatalog.NormalizeIcId(icId)} General Replace is Not available under the current IC workflow policy.",
                IcWorkflowIds.GeneralReplace)],
            GetReplaceDefaultOutputFileName(icId, WorkbenchReplaceModes.General));
    }

    internal static async ValueTask<WorkbenchRunResult>
        RunGeneralReplaceWithInitialInspectionAsync(
            string icId,
            string number,
            IReadOnlyDictionary<string, string> slotPaths,
            GeneralMappingDraftState mappingDraft,
            AuthoringRevision inspectionRevision,
            bool build,
        string? outputPath,
        CompositionRunProgressFeed? progress,
        CancellationToken cancellationToken,
        GeneralSavedRuleResourcePolicy? savedRulePolicy = null,
        GeneralReplacePostbuildReadinessOverride? postbuildReadinessOverride = null,
        SavedRuleV2GeneralReplaceExactParent? exactParentOverride = null)
    {
        if (!TryCreateGeneralReplaceRunContext(
                icId,
                number,
                slotPaths,
                mappingDraft,
                build,
                out GeneralReplaceRunContext? context,
                out WorkbenchRunResult? failure))
        {
            return failure!;
        }

        GeneralSelectedFileBindingResult accepted =
            await InspectGeneralSelectedFilesAsync(
                mappingDraft,
                inspectionRevision,
                cancellationToken)
            .ConfigureAwait(false);
        return !accepted.Succeeded
            ? CreateReplaceReportRunResult(
                icId,
                WorkbenchReplaceModes.General,
                context!.ReportSlotPaths,
                build,
                [],
                accepted.Issues,
                GetReplaceDefaultOutputFileName(
                    icId,
                    WorkbenchReplaceModes.General))
            : await RunGeneralReplaceDraftCoreAsync(
                icId,
                number,
                slotPaths,
                accepted.Draft!,
                inspectionRevision,
                build,
                outputPath,
                progress,
                cancellationToken,
                savedRulePolicy,
                postbuildReadinessOverride,
                exactParentOverride).ConfigureAwait(false);
    }

    /// <summary>Runs a Replace preview or build through the workbench Replace facade.</summary>
    public static ValueTask<WorkbenchRunResult> RunReplaceAsync(
        string icId,
        string number,
        string replaceMode,
        IReadOnlyDictionary<string, string> slotPaths,
        bool build,
        CancellationToken cancellationToken,
        string? outputPath = null,
        WorkbenchCtrlRamFirmwareVersionEdit? ctrlRamFirmwareVersionEdit = null)
    {
        return RunReplaceCoreAsync(
            icId,
            number,
            replaceMode,
            slotPaths,
            build,
            outputPath,
            ctrlRamFirmwareVersionEdit,
            progress: null,
            acceptedSession: null,
            cancellationToken);
    }

    /// <summary>Runs Replace and publishes bounded Application-owned lifecycle phases.</summary>
    public static async ValueTask<WorkbenchRunResult> RunReplaceWithProgressAsync(
        string icId,
        string number,
        string replaceMode,
        IReadOnlyDictionary<string, string> slotPaths,
        bool build,
        CompositionRunProgressFeed progress,
        CancellationToken cancellationToken,
        string? outputPath = null,
        WorkbenchCtrlRamFirmwareVersionEdit? ctrlRamFirmwareVersionEdit = null)
    {
        ArgumentNullException.ThrowIfNull(progress);
        return await RunReplaceCoreAsync(
            icId,
            number,
            replaceMode,
            slotPaths,
            build,
            outputPath,
            ctrlRamFirmwareVersionEdit,
            progress,
            acceptedSession: null,
            cancellationToken).ConfigureAwait(false);
    }

    /// <summary>Runs DP or CtrlRAM Replace from one exact accepted desktop session.</summary>
    public static async ValueTask<WorkbenchRunResult> RunReplaceAcceptedSessionWithProgressAsync(
        string icId,
        string number,
        string replaceMode,
        IReadOnlyDictionary<string, string> slotPaths,
        ActiveSessionSnapshot acceptedSession,
        bool build,
        CompositionRunProgressFeed progress,
        CancellationToken cancellationToken,
        string? outputPath = null,
        WorkbenchCtrlRamFirmwareVersionEdit? ctrlRamFirmwareVersionEdit = null)
    {
        ArgumentNullException.ThrowIfNull(progress);
        return await RunReplaceCoreAsync(
            icId, number, replaceMode, slotPaths, build, outputPath,
            ctrlRamFirmwareVersionEdit, progress, acceptedSession, cancellationToken)
            .ConfigureAwait(false);
    }

    /// <summary>
    /// Projects one rejected desktop Replace attempt through the typed workbench
    /// diagnostics without executing a composition or external processor.
    /// </summary>
    public static WorkbenchRunResult CreateRejectedReplaceAttemptResult(
        string icId,
        string number,
        string replaceMode,
        IReadOnlyDictionary<string, string> slotPaths,
        IReadOnlyList<CompositionIssue> authoringIssues,
        bool build)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(icId);
        ArgumentException.ThrowIfNullOrWhiteSpace(number);
        ArgumentException.ThrowIfNullOrWhiteSpace(replaceMode);
        ArgumentNullException.ThrowIfNull(slotPaths);
        ArgumentNullException.ThrowIfNull(authoringIssues);

        IReadOnlyList<OperationRunSummary> operations = [];
        IReadOnlyList<CompositionIssue> issues = authoringIssues;
        if (StringComparer.Ordinal.Equals(replaceMode, WorkbenchReplaceModes.CtrlRam))
        {
            CtrlRamReplaceRunContext context = CreateCtrlRamReplaceRunContext(
                icId, number, slotPaths, firmwareVersionEdit: null);
            operations = CreateCtrlRamPlanningOperations(
                icId,
                context.Selection,
                context.Sources,
                slotPaths,
                runnablePreview: false,
                context.PostbuildProfile,
                context.CommandPlan);
            if (context.ValidationIssues.Count != 0)
            {
                issues = context.ValidationIssues;
            }
        }

        if (issues.Count == 0)
        {
            issues = [new CompositionIssue(
                InputSelectionReadinessIssueCodes.SelectionNotApplicable,
                "The exact current selected-input inspection has not been accepted.")];
        }

        return CreateReplaceReportRunResult(
            icId,
            replaceMode,
            slotPaths,
            build,
            operations,
            issues,
            GetReplaceDefaultOutputFileName(icId, replaceMode));
    }

    private static async ValueTask<WorkbenchRunResult> RunReplaceCoreAsync(
        string icId,
        string number,
        string replaceMode,
        IReadOnlyDictionary<string, string> slotPaths,
        bool build,
        string? outputPath,
        WorkbenchCtrlRamFirmwareVersionEdit? ctrlRamFirmwareVersionEdit,
        CompositionRunProgressFeed? progress,
        ActiveSessionSnapshot? acceptedSession,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(icId);
        ArgumentException.ThrowIfNullOrWhiteSpace(number);
        ArgumentException.ThrowIfNullOrWhiteSpace(replaceMode);
        ArgumentNullException.ThrowIfNull(slotPaths);
        string? workflowId = GetReplaceWorkflowId(replaceMode);
        ResolvedCapability? acceptedCapability = acceptedSession is null
            ? null
            : RequireAcceptedCapability(
                acceptedSession,
                workflowId ?? throw new InvalidOperationException(
                    "The selected Replace mode has no canonical workflow."),
                icId,
                AuthoringDerivedResultKind.Inspection);
        return workflowId is not null && !IcSupportCatalog.SupportsWorkflow(icId, workflowId)
            ? CreateReplaceReportRunResult(
                icId,
                replaceMode,
                new Dictionary<string, string>(StringComparer.Ordinal),
                build,
                [],
                [new CompositionIssue(
                    WorkbenchIssueCodes.ReplaceWorkflowNotSupported,
                    $"{IcSupportCatalog.NormalizeIcId(icId)} {replaceMode} Replace is Not available under the current IC workflow policy.",
                    workflowId)],
                GetReplaceDefaultOutputFileName(icId, replaceMode))
            : ctrlRamFirmwareVersionEdit is not null &&
            (!build || !string.Equals(replaceMode, WorkbenchReplaceModes.CtrlRam, StringComparison.Ordinal))
            ? throw new ArgumentException(
                "TP FW version editing is available only for CtrlRAM Replace Build.",
                nameof(ctrlRamFirmwareVersionEdit))
            : replaceMode switch
            {
                WorkbenchReplaceModes.Dp when HasBuiltInV2DpReplace(icId) => await RunBuiltInV2DpReplaceAsync(
                    icId,
                    number,
                    slotPaths,
                    build,
                    outputPath,
                    progress,
                    acceptedCapability,
                    acceptedSession,
                    cancellationToken).ConfigureAwait(false),
                WorkbenchReplaceModes.Dp => CreatePlanningRunResult(
                    icId,
                    replaceMode,
                    slotPaths,
                    build,
                    WorkbenchIssueCodes.ReplaceDpProfilePending,
                    $"DP Replace output is enabled only for {FormatBuiltInV2DpReplaceIcIds()} until per-IC DP source mapping and golden evidence are approved."),
                WorkbenchReplaceModes.CtrlRam => await RunCtrlRamReplaceAsync(
                    icId,
                    number,
                    slotPaths,
                    build,
                    outputPath,
                    ctrlRamFirmwareVersionEdit,
                    progress,
                    acceptedSession,
                    cancellationToken).ConfigureAwait(false),
                WorkbenchReplaceModes.General => CreatePlanningRunResult(
                    icId,
                    replaceMode,
                    slotPaths,
                    build,
                    WorkbenchIssueCodes.ReplaceWorkflowNotSupported,
                    "General Replace requires a canonical typed mapping draft."),
                _ => CreatePlanningRunResult(
                    icId,
                    replaceMode,
                    slotPaths,
                    build,
                    WorkbenchIssueCodes.ReplaceModeUnknown,
                    $"Unknown Replace mode '{replaceMode}'."),
            };
    }

    private static string? GetReplaceWorkflowId(string replaceMode)
    {
        return replaceMode switch
        {
            WorkbenchReplaceModes.Dp => IcWorkflowIds.DpReplace,
            WorkbenchReplaceModes.CtrlRam => IcWorkflowIds.CtrlRamReplace,
            WorkbenchReplaceModes.General => IcWorkflowIds.GeneralReplace,
            _ => null,
        };
    }
}
