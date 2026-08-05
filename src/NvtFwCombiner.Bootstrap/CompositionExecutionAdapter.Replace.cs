using NvtFwCombiner.Application.Authoring;
using NvtFwCombiner.Application.Capabilities;
using NvtFwCombiner.Domain.Composition;
using NvtFwCombiner.Profiles;

namespace NvtFwCombiner.Bootstrap;

public static partial class CompositionExecutionAdapter
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

    /// <summary>
    /// Ephemeral CLI/Saved Rule boundary: inspect once, then execute the exact
    /// content-bound draft through the strict General Replace runner.
    /// </summary>
    public static ValueTask<WorkbenchRunResult> PreviewGeneralReplaceEphemeralDraftAsync(
        string icId,
        string number,
        IReadOnlyDictionary<string, string> slotPaths,
        GeneralMappingDraftState mappingDraft,
        CancellationToken cancellationToken)
    {
        return RunGeneralReplaceEphemeralDraftAsync(
            icId,
            number,
            slotPaths,
            mappingDraft,
            PreviewGeneralReplaceStrategy,
            outputPath: null,
            savedRulePolicy: null,
            cancellationToken);
    }

    /// <summary>Builds one ephemeral General Replace draft after strict inspection.</summary>
    public static ValueTask<WorkbenchRunResult> BuildGeneralReplaceEphemeralDraftAsync(
        string icId,
        string number,
        IReadOnlyDictionary<string, string> slotPaths,
        GeneralMappingDraftState mappingDraft,
        string? outputPath,
        CancellationToken cancellationToken)
    {
        return RunGeneralReplaceEphemeralDraftAsync(
            icId,
            number,
            slotPaths,
            mappingDraft,
            BuildGeneralReplaceStrategy,
            outputPath,
            savedRulePolicy: null,
            cancellationToken);
    }

    /// <summary>Internal Saved Rule boundary after host-resolved lifecycle admission.</summary>
    internal static ValueTask<WorkbenchRunResult> PreviewGeneralReplaceEphemeralDraftAsync(
        string icId,
        string number,
        IReadOnlyDictionary<string, string> slotPaths,
        GeneralMappingDraftState mappingDraft,
        GeneralSavedRuleResourcePolicy? savedRulePolicy,
        CancellationToken cancellationToken)
    {
        return RunGeneralReplaceEphemeralDraftAsync(
            icId,
            number,
            slotPaths,
            mappingDraft,
            PreviewGeneralReplaceStrategy,
            outputPath: null,
            savedRulePolicy,
            cancellationToken);
    }

    /// <summary>Internal Saved Rule build boundary after host-resolved lifecycle admission.</summary>
    internal static ValueTask<WorkbenchRunResult> BuildGeneralReplaceEphemeralDraftAsync(
        string icId,
        string number,
        IReadOnlyDictionary<string, string> slotPaths,
        GeneralMappingDraftState mappingDraft,
        string? outputPath,
        GeneralSavedRuleResourcePolicy? savedRulePolicy,
        CancellationToken cancellationToken)
    {
        return RunGeneralReplaceEphemeralDraftAsync(
            icId,
            number,
            slotPaths,
            mappingDraft,
            BuildGeneralReplaceStrategy,
            outputPath,
            savedRulePolicy,
            cancellationToken);
    }

    private static ValueTask<WorkbenchRunResult> RunGeneralReplaceEphemeralDraftAsync(
        string icId,
        string number,
        IReadOnlyDictionary<string, string> slotPaths,
        GeneralMappingDraftState mappingDraft,
        GeneralReplaceRunActionStrategy strategy,
        string? outputPath,
        GeneralSavedRuleResourcePolicy? savedRulePolicy,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(icId);
        ArgumentException.ThrowIfNullOrWhiteSpace(number);
        ArgumentNullException.ThrowIfNull(slotPaths);
        ArgumentNullException.ThrowIfNull(mappingDraft);
        return !CanonicalCapabilityProjection.IsReplaceWorkflowAvailable(
                icId,
                WorkbenchReplaceModes.General)
            ? ValueTask.FromResult(CreateGeneralReplaceUnavailableResult(icId, strategy))
            : RunGeneralReplaceWithInitialInspectionAsync(
                icId,
                number,
                slotPaths,
                mappingDraft,
                new AuthoringRevision(1),
                strategy,
                outputPath,
                progress: null,
                cancellationToken,
                savedRulePolicy);
    }

    /// <summary>
    /// Runs General Replace from the exact content-bound draft returned by an
    /// earlier desktop Preview or explicit Reload/Rebind.
    /// </summary>
    public static ValueTask<WorkbenchRunResult> PreviewGeneralReplaceAcceptedSessionWithProgressAsync(
        string icId,
        string number,
        IReadOnlyDictionary<string, string> slotPaths,
        ActiveSessionSnapshot acceptedSession,
        CompositionRunProgressFeed progress,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(acceptedSession);
        return RunGeneralReplaceAcceptedSessionWithProgressAsync(
            icId,
            number,
            slotPaths,
            acceptedSession,
            PreviewGeneralReplaceStrategy,
            progress,
            outputPath: null,
            cancellationToken);
    }

    /// <summary>Builds from one exact accepted General Replace session.</summary>
    public static ValueTask<WorkbenchRunResult> BuildGeneralReplaceAcceptedSessionWithProgressAsync(
        string icId,
        string number,
        IReadOnlyDictionary<string, string> slotPaths,
        ActiveSessionSnapshot acceptedSession,
        CompositionRunProgressFeed progress,
        string? outputPath,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(acceptedSession);
        return RunGeneralReplaceAcceptedSessionWithProgressAsync(
            icId,
            number,
            slotPaths,
            acceptedSession,
            BuildGeneralReplaceStrategy,
            progress,
            outputPath,
            cancellationToken);
    }

    private static ValueTask<WorkbenchRunResult> RunGeneralReplaceAcceptedSessionWithProgressAsync(
        string icId,
        string number,
        IReadOnlyDictionary<string, string> slotPaths,
        ActiveSessionSnapshot acceptedSession,
        GeneralReplaceRunActionStrategy strategy,
        CompositionRunProgressFeed progress,
        string? outputPath,
        CancellationToken cancellationToken)
    {
        ResolvedCapability capability = AcceptedAuthoringSessionBinding.RequireCapability(
            acceptedSession,
            IcWorkflowIds.GeneralReplace,
            icId,
            AuthoringDerivedResultKind.Validation);
        GeneralMappingDraftState draft = acceptedSession.DraftState as GeneralMappingDraftState ??
            throw new InvalidOperationException(
                "The accepted General Replace session has no exact typed draft.");
        return RunGeneralReplaceDraftCoreAsync(
            icId,
            number,
            slotPaths,
            draft,
            acceptedSession.AuthoringRevision,
            strategy,
            outputPath,
            progress,
            cancellationToken,
            acceptedCapability: capability,
            acceptedSession: acceptedSession);
    }

    private static WorkbenchRunResult CreateGeneralReplaceUnavailableResult(
        string icId,
        GeneralReplaceRunActionStrategy strategy)
    {
        return strategy.RenderFailure(new GeneralReplaceRunFailure(
            icId,
            new Dictionary<string, string>(StringComparer.Ordinal),
            AcceptedDraft: null,
            [],
            [new CompositionIssue(
                WorkbenchIssueCodes.ReplaceWorkflowNotSupported,
                $"{IcIdentifier.Normalize(icId)} General Replace is Not available under the current IC workflow policy.",
                IcWorkflowIds.GeneralReplace)],
            GetReplaceDefaultOutputFileName(icId, WorkbenchReplaceModes.General),
            Admission: null));
    }

    internal static ValueTask<WorkbenchRunResult> PreviewGeneralReplaceWithInitialInspectionAsync(
        string icId,
        string number,
        IReadOnlyDictionary<string, string> slotPaths,
        GeneralMappingDraftState mappingDraft,
        AuthoringRevision inspectionRevision,
        CancellationToken cancellationToken,
        GeneralSavedRuleResourcePolicy? savedRulePolicy = null,
        CompositionAuthoringSessionAdapter.GeneralReplacePostbuildReadinessOverride? postbuildReadinessOverride = null,
        SavedRuleV2GeneralReplaceExactParent? exactParentOverride = null)
    {
        return RunGeneralReplaceWithInitialInspectionAsync(
            icId, number, slotPaths, mappingDraft, inspectionRevision,
            PreviewGeneralReplaceStrategy, outputPath: null, progress: null,
            cancellationToken, savedRulePolicy, postbuildReadinessOverride, exactParentOverride);
    }

    internal static ValueTask<WorkbenchRunResult> BuildGeneralReplaceWithInitialInspectionAsync(
        string icId,
        string number,
        IReadOnlyDictionary<string, string> slotPaths,
        GeneralMappingDraftState mappingDraft,
        AuthoringRevision inspectionRevision,
        string? outputPath,
        CompositionRunProgressFeed? progress,
        CancellationToken cancellationToken,
        GeneralSavedRuleResourcePolicy? savedRulePolicy = null,
        CompositionAuthoringSessionAdapter.GeneralReplacePostbuildReadinessOverride? postbuildReadinessOverride = null,
        SavedRuleV2GeneralReplaceExactParent? exactParentOverride = null)
    {
        return RunGeneralReplaceWithInitialInspectionAsync(
            icId, number, slotPaths, mappingDraft, inspectionRevision,
            BuildGeneralReplaceStrategy, outputPath, progress, cancellationToken,
            savedRulePolicy, postbuildReadinessOverride, exactParentOverride);
    }

    private static async ValueTask<WorkbenchRunResult>
        RunGeneralReplaceWithInitialInspectionAsync(
            string icId,
            string number,
            IReadOnlyDictionary<string, string> slotPaths,
            GeneralMappingDraftState mappingDraft,
            AuthoringRevision inspectionRevision,
            GeneralReplaceRunActionStrategy strategy,
            string? outputPath,
            CompositionRunProgressFeed? progress,
            CancellationToken cancellationToken,
            GeneralSavedRuleResourcePolicy? savedRulePolicy = null,
            CompositionAuthoringSessionAdapter.GeneralReplacePostbuildReadinessOverride? postbuildReadinessOverride = null,
            SavedRuleV2GeneralReplaceExactParent? exactParentOverride = null)
    {
        if (!CompositionPlanningAdapter.TryCreateGeneralReplaceRunContext(
                icId,
                number,
                slotPaths,
                mappingDraft,
                out GeneralReplaceRunContext? context,
                out IReadOnlyDictionary<string, string> reportSlotPaths,
                out CompositionIssue? contextIssue))
        {
            return strategy.RenderFailure(new GeneralReplaceRunFailure(
                icId,
                reportSlotPaths,
                AcceptedDraft: null,
                [],
                [contextIssue!],
                GetReplaceDefaultOutputFileName(icId, WorkbenchReplaceModes.General),
                Admission: null));
        }

        GeneralSelectedFileBindingResult accepted =
            await CanonicalAuthoringAdapter.InspectGeneralSelectedFilesAsync(
                mappingDraft,
                inspectionRevision,
                cancellationToken)
            .ConfigureAwait(false);
        return !accepted.Succeeded
            ? strategy.RenderFailure(new GeneralReplaceRunFailure(
                icId,
                context!.ReportSlotPaths,
                AcceptedDraft: null,
                [],
                accepted.Issues,
                GetReplaceDefaultOutputFileName(
                    icId,
                    WorkbenchReplaceModes.General),
                Admission: null))
            : await RunGeneralReplaceDraftCoreAsync(
                icId,
                number,
                slotPaths,
                accepted.Draft!,
                inspectionRevision,
                strategy,
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
            CompositionPlanningAdapter.CtrlRamReplaceRunContext context = CompositionPlanningAdapter.CreateCtrlRamReplaceRunContext(
                icId, number, slotPaths, firmwareVersionEdit: null);
            operations = CompositionPlanningAdapter.CreateCtrlRamPlanningOperations(
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
        string? workflowId = CanonicalCapabilityProjection.GetReplaceWorkflowId(replaceMode);
        ResolvedCapability? acceptedCapability = acceptedSession is null
            ? null
            : AcceptedAuthoringSessionBinding.RequireCapability(
                acceptedSession,
                workflowId ?? throw new InvalidOperationException(
                    "The selected Replace mode has no canonical workflow."),
                icId,
                AuthoringDerivedResultKind.Inspection);
        return workflowId is not null &&
            !CanonicalCapabilityProjection.IsReplaceWorkflowAvailable(icId, replaceMode)
            ? CreateReplaceReportRunResult(
                icId,
                replaceMode,
                new Dictionary<string, string>(StringComparer.Ordinal),
                build,
                [],
                [new CompositionIssue(
                    WorkbenchIssueCodes.ReplaceWorkflowNotSupported,
                    $"{IcIdentifier.Normalize(icId)} {replaceMode} Replace is Not available under the current IC workflow policy.",
                    workflowId)],
                GetReplaceDefaultOutputFileName(icId, replaceMode))
            : ctrlRamFirmwareVersionEdit is not null &&
            (!build || !string.Equals(replaceMode, WorkbenchReplaceModes.CtrlRam, StringComparison.Ordinal))
            ? throw new ArgumentException(
                "TP FW version editing is available only for CtrlRAM Replace Build.",
                nameof(ctrlRamFirmwareVersionEdit))
            : replaceMode switch
            {
                WorkbenchReplaceModes.Dp when CanonicalCapabilityProjection.HasBuiltInV2DpReplace(icId) => await RunBuiltInV2DpReplaceAsync(
                    icId,
                    number,
                    slotPaths,
                    build,
                    outputPath,
                    progress,
                    acceptedCapability,
                    acceptedSession,
                    cancellationToken).ConfigureAwait(false),
                WorkbenchReplaceModes.Dp => CompositionPlanningAdapter.CreatePlanningRunResult(
                    icId,
                    replaceMode,
                    slotPaths,
                    build,
                    WorkbenchIssueCodes.ReplaceDpProfilePending,
                    $"DP Replace output is enabled only for {CanonicalCapabilityProjection.FormatBuiltInV2DpReplaceIcIds()} until per-IC DP source mapping and golden evidence are approved."),
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
                WorkbenchReplaceModes.General => CompositionPlanningAdapter.CreatePlanningRunResult(
                    icId,
                    replaceMode,
                    slotPaths,
                    build,
                    WorkbenchIssueCodes.ReplaceWorkflowNotSupported,
                    "General Replace requires a canonical typed mapping draft."),
                _ => CompositionPlanningAdapter.CreatePlanningRunResult(
                    icId,
                    replaceMode,
                    slotPaths,
                    build,
                    WorkbenchIssueCodes.ReplaceModeUnknown,
                    $"Unknown Replace mode '{replaceMode}'."),
            };
    }

}
