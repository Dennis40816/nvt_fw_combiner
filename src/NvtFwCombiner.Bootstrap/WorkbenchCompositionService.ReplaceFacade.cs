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

    /// <summary>Gets current profile-derived DP Replace Reference FlashCode capacities.</summary>
    public static string? GetDpReplaceReferenceCapacityLabel(string icId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(icId);
        return TryResolveBuiltInV2DpReplaceDisplay(icId, baseCapacity: null, out BuiltInV2DpReplaceDisplay? display) &&
            display.Issues.Count == 0
                ? FormatV2DpReplaceCapacities(display)
                : null;
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
        return RunReplaceAsync(
            icId,
            number,
            replaceMode,
            slotPaths,
            [],
            [],
            build,
            cancellationToken,
            outputPath,
            ctrlRamFirmwareVersionEdit: ctrlRamFirmwareVersionEdit);
    }

    /// <summary>Runs a Replace preview or build through the workbench Replace facade.</summary>
    public static ValueTask<WorkbenchRunResult> RunReplaceAsync(
        string icId,
        string number,
        string replaceMode,
        IReadOnlyDictionary<string, string> slotPaths,
        IReadOnlyList<WorkbenchGeneralReplaceMappingInput> generalReplaceMappings,
        bool build,
        CancellationToken cancellationToken,
        string? outputPath = null,
        WorkbenchCtrlRamFirmwareVersionEdit? ctrlRamFirmwareVersionEdit = null)
    {
        return RunReplaceAsync(
            icId,
            number,
            replaceMode,
            slotPaths,
            generalReplaceMappings,
            [],
            build,
            cancellationToken,
            outputPath,
            ctrlRamFirmwareVersionEdit: ctrlRamFirmwareVersionEdit);
    }

    /// <summary>Runs Replace preview or build with file-backed and virtual General Replace authoring inputs.</summary>
    public static async ValueTask<WorkbenchRunResult> RunReplaceAsync(
        string icId,
        string number,
        string replaceMode,
        IReadOnlyDictionary<string, string> slotPaths,
        IReadOnlyList<WorkbenchGeneralReplaceMappingInput> generalReplaceMappings,
        IReadOnlyList<WorkbenchGeneralReplacePatchInput> generalReplacePatches,
        bool build,
        CancellationToken cancellationToken,
        string? outputPath = null,
        WorkbenchCtrlRamFirmwareVersionEdit? ctrlRamFirmwareVersionEdit = null)
    {
        return await RunReplaceCoreAsync(
            icId,
            number,
            replaceMode,
            slotPaths,
            generalReplaceMappings,
            generalReplacePatches,
            build,
            outputPath,
            ctrlRamFirmwareVersionEdit,
            progress: null,
            cancellationToken).ConfigureAwait(false);
    }

    /// <summary>Runs Replace and publishes bounded Application-owned lifecycle phases.</summary>
    public static async ValueTask<WorkbenchRunResult> RunReplaceWithProgressAsync(
        string icId,
        string number,
        string replaceMode,
        IReadOnlyDictionary<string, string> slotPaths,
        IReadOnlyList<WorkbenchGeneralReplaceMappingInput> generalReplaceMappings,
        IReadOnlyList<WorkbenchGeneralReplacePatchInput> generalReplacePatches,
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
            generalReplaceMappings,
            generalReplacePatches,
            build,
            outputPath,
            ctrlRamFirmwareVersionEdit,
            progress,
            cancellationToken).ConfigureAwait(false);
    }

    private static async ValueTask<WorkbenchRunResult> RunReplaceCoreAsync(
        string icId,
        string number,
        string replaceMode,
        IReadOnlyDictionary<string, string> slotPaths,
        IReadOnlyList<WorkbenchGeneralReplaceMappingInput> generalReplaceMappings,
        IReadOnlyList<WorkbenchGeneralReplacePatchInput> generalReplacePatches,
        bool build,
        string? outputPath,
        WorkbenchCtrlRamFirmwareVersionEdit? ctrlRamFirmwareVersionEdit,
        CompositionRunProgressFeed? progress,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(icId);
        ArgumentException.ThrowIfNullOrWhiteSpace(number);
        ArgumentException.ThrowIfNullOrWhiteSpace(replaceMode);
        ArgumentNullException.ThrowIfNull(slotPaths);
        ArgumentNullException.ThrowIfNull(generalReplaceMappings);
        ArgumentNullException.ThrowIfNull(generalReplacePatches);
        string? workflowId = GetReplaceWorkflowId(replaceMode);
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
                    cancellationToken).ConfigureAwait(false),
                WorkbenchReplaceModes.General => await RunGeneralReplaceAsync(
                    icId,
                    number,
                    slotPaths,
                    generalReplaceMappings,
                    generalReplacePatches,
                    build,
                    outputPath,
                    progress,
                    cancellationToken).ConfigureAwait(false),
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
