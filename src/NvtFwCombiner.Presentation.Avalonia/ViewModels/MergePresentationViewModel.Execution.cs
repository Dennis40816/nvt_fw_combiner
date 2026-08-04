using NvtFwCombiner.Application.Authoring;
using NvtFwCombiner.Bootstrap;

namespace NvtFwCombiner.Presentation.Avalonia.ViewModels;

internal sealed record MergeBuildSavePreparation(
    string SuggestedFileName,
    WorkbenchAbAFlashCodeDeliveryPlan? AFlashCodePlan);

public sealed partial class MergePresentationViewModel
{
    /// <summary>Builds the active Merge output to a user-selected path.</summary>
    public Task BuildMergeAsync(
        string outputPath,
        string? aFlashCodeOutputPath = null,
        bool outputPathUsesAutomaticName = false,
        bool aFlashCodeOutputPathUsesAutomaticName = false)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(outputPath);
        return RunMergeAsync(
            build: true,
            outputPath,
            aFlashCodeOutputPath,
            outputPathUsesAutomaticName,
            aFlashCodeOutputPathUsesAutomaticName);
    }

    private Task RunMergeAsync(
        bool build,
        string? outputPath,
        string? aFlashCodeOutputPath = null,
        bool outputPathUsesAutomaticName = false,
        bool aFlashCodeOutputPathUsesAutomaticName = false)
    {
        return SelectedMergeMode switch
        {
            NormalMergeMode => RunStandardMergeAsync(build, outputPath),
            AbCodeMergeMode => RunAbMergeAsync(
                build,
                outputPath,
                aFlashCodeOutputPath,
                outputPathUsesAutomaticName,
                aFlashCodeOutputPathUsesAutomaticName),
            GeneralMergeMode => RunGeneralMergeAsync(build, outputPath),
            _ => Task.CompletedTask,
        };
    }

    /// <summary>Resolves the active Merge filename for the native Save dialog without duplicating firmware naming rules in UI.</summary>
    internal async ValueTask<string> ResolveMergeOutputFileNameForSaveAsync(CancellationToken cancellationToken)
    {
        if (!IsAbCodeMergeModeSelected)
        {
            return MergeOutputFileName;
        }

        IReadOnlyDictionary<string, string> slotPaths = MergeSlots
            .Where(static slot => slot.HasFile)
            .ToDictionary(
                slot => AbMergeAddressSpaceBySlotId[slot.SlotId],
                slot => slot.FilePath!,
                StringComparer.Ordinal);
        return await AbMergeWorkbenchCompositionService.ResolveAutomaticOutputFileNameAsync(
                SelectedIc,
                slotPaths,
                cancellationToken,
                GetSelectedAbMergeTopologyToken(),
                _authoringSessions.AbMerge.CurrentSnapshot)
            .ConfigureAwait(false);
    }

    /// <summary>Returns the optional A FlashCode plan only for the currently compiled AB profile.</summary>
    internal async ValueTask<WorkbenchAbAFlashCodeDeliveryPlan?> TryCreateAbAFlashCodeDeliveryPlanAsync(
        CancellationToken cancellationToken)
    {
        if (!IsAbCodeMergeModeSelected)
        {
            return null;
        }

        IReadOnlyDictionary<string, string> slotPaths = MergeSlots
            .Where(static slot => slot.HasFile)
            .ToDictionary(
                slot => AbMergeAddressSpaceBySlotId[slot.SlotId],
                slot => slot.FilePath!,
                StringComparer.Ordinal);
        return await AbMergeWorkbenchCompositionService.TryCreateAFlashCodeDeliveryPlanAsync(
                SelectedIc,
                slotPaths,
                cancellationToken,
                GetSelectedAbMergeTopologyToken(),
                _authoringSessions.AbMerge.CurrentSnapshot)
            .ConfigureAwait(false);
    }

    /// <summary>Prepares all Build save-dialog data and converts admission failures into the standard run report.</summary>
    internal async ValueTask<MergeBuildSavePreparation?> TryPrepareMergeBuildSaveAsync(
        CancellationToken cancellationToken)
    {
        if (!IsAbCodeMergeModeSelected)
        {
            return new MergeBuildSavePreparation(MergeOutputFileName, AFlashCodePlan: null);
        }

        try
        {
            string suggestedFileName = await ResolveMergeOutputFileNameForSaveAsync(cancellationToken)
                .ConfigureAwait(false);
            WorkbenchAbAFlashCodeDeliveryPlan? aFlashCodePlan = await TryCreateAbAFlashCodeDeliveryPlanAsync(
                    cancellationToken)
                .ConfigureAwait(false);
            return new MergeBuildSavePreparation(suggestedFileName, aFlashCodePlan);
        }
        catch (Exception exception) when (
            exception is InvalidOperationException or IOException or UnauthorizedAccessException or ArgumentException)
        {
            PublishAbMergeBuildSavePreparationFailure(exception.Message);
            return null;
        }
    }

    private Task RunStandardMergeAsync(bool build, string? outputPath)
    {
        string icId = SelectedIc;
        string number = SelectedNumber;
        IReadOnlyDictionary<string, string> slotPaths = CreateStandardMergeSlotPaths();
        string profileId =
            WorkbenchCompositionService.GetStandardMergeProfileId(icId) ?? WorkbenchWorkflowIds.StandardMerge;
        return RunCompositionAsync(
            build,
            (progress, cancellationToken) => WorkbenchCompositionService.RunStandardMergeAcceptedSessionWithProgressAsync(
                icId,
                slotPaths,
                _authoringSessions.StandardMerge.CurrentSnapshot ?? throw new InvalidOperationException(
                    "Standard Merge requires one accepted authoring session."),
                build,
                progress,
                cancellationToken,
                outputPath),
            (action, errorMessage) => Reports.LoadRunErrorReport(
                action,
                profileId,
                icId,
                number,
                errorMessage,
                slotPaths));
    }

    private Task RunGeneralMergeAsync(bool build, string? outputPath)
    {
        string icId = SelectedIc;
        string number = SelectedNumber;
        ActiveSessionSnapshot acceptedSession =
            _authoringSessions.GeneralMerge.CurrentSnapshot ??
            throw new InvalidOperationException("General Merge requires one active authoring session.");
        GeneralMergeDraftState draft = acceptedSession.DraftState as GeneralMergeDraftState ??
            throw new InvalidOperationException("General Merge requires one admitted typed draft.");
        IReadOnlyDictionary<string, string> slotPaths = CreateGeneralMergeSlotPaths();
        string outputFileName = WorkbenchCompositionService.GetGeneralMergeDefaultOutputFileName(icId);
        return RunCompositionAsync(
            build,
            async (progress, cancellationToken) =>
            {
                WorkbenchRunResult result = await WorkbenchCompositionService
                    .RunGeneralMergeAcceptedSessionWithProgressAsync(
                        icId,
                        acceptedSession,
                        build,
                        progress,
                        cancellationToken,
                        outputPath)
                    .ConfigureAwait(false);

                if (result.AcceptedGeneralMappingDraft is { } accepted &&
                    ReferenceEquals(draft, _generalMergeDraft) &&
                    _authoringSessions.GeneralMerge.CurrentSnapshot?.AuthoringRevision ==
                        acceptedSession.AuthoringRevision)
                {
                    _generalMergeDraft = new GeneralMergeDraftState(
                        draft.OutputInitializer,
                        accepted);
                }
                return result;
            },
            (action, errorMessage) => Reports.LoadRunErrorReport(
                action,
                outputFileName,
                icId,
                number,
                errorMessage,
                slotPaths,
                compositionKind: "Merge",
                modeId: WorkbenchWorkflowIds.GeneralMerge,
                experienceId: WorkbenchWorkflowIds.GeneralMerge));
    }

    private Task RunAbMergeAsync(
        bool build,
        string? outputPath,
        string? aFlashCodeOutputPath,
        bool outputPathUsesAutomaticName,
        bool aFlashCodeOutputPathUsesAutomaticName)
    {
        string icId = SelectedIc;
        IReadOnlyDictionary<string, string> slotPaths = CreateAbMergeSlotPaths();
        string profileId = WorkbenchCompositionService.GetAbMergeProfileSummaries()
            .Single(profile => StringComparer.Ordinal.Equals(profile.IcId, icId))
            .ProfileId;
        return RunCompositionAsync(
            build,
            (progress, cancellationToken) => AbMergeWorkbenchCompositionService.RunAbMergeAcceptedSessionWithProgressAsync(
                icId,
                slotPaths,
                _authoringSessions.AbMerge.CurrentSnapshot ?? throw new InvalidOperationException(
                    "AB Merge requires one accepted authoring session."),
                build,
                progress,
                cancellationToken,
                outputPath,
                GetSelectedAbMergeTopologyToken(),
                aFlashCodeOutputPath,
                outputPathUsesAutomaticName,
                aFlashCodeOutputPathUsesAutomaticName),
            (action, errorMessage) => Reports.LoadRunErrorReport(
                action,
                profileId,
                icId,
                SelectedNumber,
                errorMessage,
                slotPaths,
                compositionKind: "Merge",
                modeId: WorkbenchWorkflowIds.AbMerge,
                experienceId: WorkbenchWorkflowIds.AbMerge));
    }

    private void PublishAbMergeBuildSavePreparationFailure(string message)
    {
        string icId = SelectedIc;
        string number = SelectedNumber;
        IReadOnlyDictionary<string, string> slotPaths = CreateAbMergeSlotPaths();
        string profileId = WorkbenchCompositionService.GetAbMergeProfileSummaries()
            .Single(profile => StringComparer.Ordinal.Equals(profile.IcId, icId))
            .ProfileId;
        Reports.LoadRunErrorReport(
            "Build",
            profileId,
            icId,
            number,
            message,
            slotPaths,
            compositionKind: "Merge",
            modeId: WorkbenchWorkflowIds.AbMerge,
            experienceId: WorkbenchWorkflowIds.AbMerge);
        _stateBindings.PublishRunResult(
            new UiRunResultViewModel("Build failed", message, "No output", succeeded: false));
        Reports.ShowReport();
    }

    private Dictionary<string, string> CreateStandardMergeSlotPaths()
    {
        Dictionary<string, string> paths = new(StringComparer.Ordinal);
        FirmwareSlotPathProjection.Add(paths, WorkbenchAddressSpaceIds.DpInput, MergeDpSlot);
        FirmwareSlotPathProjection.Add(paths, WorkbenchAddressSpaceIds.TpInput, MergeTpSlot);
        FirmwareSlotPathProjection.Add(paths, WorkbenchAddressSpaceIds.LdcInput, MergeLdcSlot);
        return paths;
    }

    private Dictionary<string, string> CreateAbMergeSlotPaths()
    {
        return MergeSlots
            .Where(static slot => slot.HasFile)
            .ToDictionary(
                slot => AbMergeAddressSpaceBySlotId[slot.SlotId],
                slot => slot.FilePath!,
                StringComparer.Ordinal);
    }

    private Dictionary<string, string> CreateGeneralMergeSlotPaths()
    {
        Dictionary<string, string> paths = new(StringComparer.Ordinal);
        foreach (GeneralMergeMappingViewModel mapping in GeneralMergeMappings)
        {
            if (!string.IsNullOrWhiteSpace(mapping.FilePath))
            {
                paths[mapping.MappingId] = mapping.FilePath;
            }
        }

        return paths;
    }

}
