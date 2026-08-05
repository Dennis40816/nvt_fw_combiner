using NvtFwCombiner.Application.Capabilities;
using NvtFwCombiner.Application.Authoring;
using NvtFwCombiner.Application.Metadata;
using NvtFwCombiner.Bootstrap;

namespace NvtFwCombiner.Presentation.Avalonia.ViewModels;

public sealed partial class ReplacePresentationViewModel
{
    /// <summary>Gets short Replace memory-map summary text.</summary>
    public string ReplaceMemorySummary => Text.GetReplaceMemorySummary(SelectedReplaceMode);

    /// <summary>Status shown in the replace inspector.</summary>
    public string ReplaceReadinessStatus => _stateBindings.IsFirmwareInspectionLoading()
        ? Text.FirmwareInspectionLoadingStatus
        : IsSelectedReplaceModeSupported
            ? Text.GetReplaceReadinessStatus(SelectedReplaceMode, CanRunReplace())
            : Text.GetReplaceNotSupportedStatus(SelectedIc);

    /// <summary>Builds Replace output to a user-selected path.</summary>
    public Task BuildReplaceAsync(
        string outputPath,
        WorkbenchCtrlRamFirmwareVersionEdit? ctrlRamFirmwareVersionEdit = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(outputPath);
        return RunBuildReplaceAsync(outputPath, ctrlRamFirmwareVersionEdit);
    }

    private Task PreviewReplaceAsync()
    {
        return RunReplaceAsync(
            build: false,
            outputPath: null,
            ctrlRamFirmwareVersionEdit: null,
            WorkbenchCompositionService.PreviewGeneralReplaceAcceptedSessionWithProgressAsync);
    }

    private Task RunBuildReplaceAsync(
        string? outputPath,
        WorkbenchCtrlRamFirmwareVersionEdit? ctrlRamFirmwareVersionEdit)
    {
        return RunReplaceAsync(
            build: true,
            outputPath,
            ctrlRamFirmwareVersionEdit,
            (icId, number, slotPaths, acceptedSession, progress, cancellationToken) =>
                WorkbenchCompositionService.BuildGeneralReplaceAcceptedSessionWithProgressAsync(
                    icId,
                    number,
                    slotPaths,
                    acceptedSession,
                    progress,
                    outputPath,
                    cancellationToken));
    }

    private bool CanRunReplace()
    {
        return !_stateBindings.IsGlobalBuildBlocked() &&
            !_stateBindings.IsRunInProgress() && !_stateBindings.IsFirmwareInspectionLoading() &&
            IsSelectedReplaceModeSupported &&
            (SelectedReplaceMode switch
            {
                DpReplaceMode => CanRunDpReplace(),
                CtrlRamReplaceMode =>
                    CanRunCompiledReplaceSession(_authoringSessions.CtrlRamReplace) &&
                    HasCurrentCtrlRamActionReadiness(build: false),
                GeneralReplaceMode => ReplaceBaseSlot.HasFile &&
                    _generalReplaceDraft is not null &&
                    _generalReplaceAdmission?.IsAdmitted == true &&
                    _generalReplaceActionReadiness?.Preview.IsAvailable == true,
                _ => false,
            });
    }

    private bool CanRunDpReplace()
    {
        return CanRunCompiledReplaceSession(_authoringSessions.DpReplace);
    }

    private void RefreshDpReplaceInputSelectionReadiness()
    {
        FirmwareSlotViewModel[] selected = [.. CurrentReplaceInputSlots().DistinctBy(ReplaceInputId)];
        CompiledAuthoringSelectionSnapshot? projection = SelectedReplaceMode == DpReplaceMode
            ? _dpReplaceSelection is { } current && IsCurrentDpReplaceSelection(current, selected)
                    ? current
                    : ResolveDpReplaceAuthoringSnapshot(selected)
            : null;
        foreach (FirmwareSlotViewModel slot in ReplaceSlots.Where(slot =>
                     !ReferenceEquals(slot, ReplaceBaseSlot)))
        {
            InputSelectionMemberReadiness? member = projection?.Slots.FirstOrDefault(candidate =>
                string.Equals(candidate.SlotId, slot.AddressSpaceId, StringComparison.Ordinal));
            if (member is null)
            {
                slot.IsOptional = slot.DeclaredIsOptional;
                slot.ClearSelectionReadiness();
                continue;
            }

            slot.IsOptional = member.Readiness == ResolvedChildReadiness.Ready
                ? !member.IsRequired
                : slot.DeclaredIsOptional;

            string label = Text.GetDpInputSelectionReadinessLabel(member.Readiness);
            string detail = Text.GetDpInputSelectionReadinessDetail(member);
            slot.SetSelectionReadiness(
                member.Readiness,
                label,
                detail,
                Text.GetInputSelectionReadinessAutomationText(label, detail),
                member.CanSelect);
        }
    }

    private async Task RunReplaceAsync(
        bool build,
        string? outputPath,
        WorkbenchCtrlRamFirmwareVersionEdit? ctrlRamFirmwareVersionEdit,
        WorkbenchGeneralReplaceAcceptedSessionRunner generalReplaceRun)
    {
        CloseSelectionForRun();
        string icId = SelectedIc;
        string number = SelectedNumber;
        string replaceMode = SelectedReplaceMode;
        IReadOnlyDictionary<string, string> slotPaths = CreateReplaceSlotPaths();
        WorkbenchCtrlRamAuthoringTransitionResult? ctrlRamTransition =
            replaceMode == CtrlRamReplaceMode
                ? WorkbenchCompositionService.TransitionCtrlRamFirmwareVersionCompilation(
                    _authoringSessions.CtrlRamReplace,
                    icId,
                    number,
                    slotPaths,
                    ctrlRamFirmwareVersionEdit)
                : null;
        if (ctrlRamTransition?.Succeeded == true)
        {
            await RefreshCtrlRamActionReadinessAsync(CancellationToken.None);
        }
        ActiveSessionSnapshot? generalSession = replaceMode == GeneralReplaceMode
            ? _authoringSessions.GeneralReplace.CurrentSnapshot ??
                throw new InvalidOperationException(
                    "General Replace requires one accepted authoring session.")
            : null;
        var generalDraft =
            generalSession?.DraftState as GeneralMappingDraftState;
        ActiveSessionSnapshot? compiledSession = replaceMode switch
        {
            DpReplaceMode => _authoringSessions.DpReplace.CurrentSnapshot,
            CtrlRamReplaceMode => ctrlRamTransition?.Session,
            _ => null,
        };
        await RunCompositionAsync(
            build,
            async (progress, cancellationToken) =>
            {
                WorkbenchRunResult result =
                    replaceMode == GeneralReplaceMode
                        ? await generalReplaceRun(
                                icId,
                                number,
                                slotPaths,
                                generalSession!,
                                progress,
                                cancellationToken)
                            .ConfigureAwait(false)
                        : compiledSession?.GetAcceptedCapability(
                            AuthoringDerivedResultKind.Inspection) is null
                        ? WorkbenchCompositionService.CreateRejectedReplaceAttemptResult(
                            icId,
                            number,
                            replaceMode,
                            slotPaths,
                            replaceMode == DpReplaceMode
                                ? _dpReplaceSelection?.Issues ?? []
                                : ctrlRamTransition?.Issues ?? [],
                            build)
                        : await WorkbenchCompositionService.RunReplaceAcceptedSessionWithProgressAsync(
                            icId,
                            number,
                            replaceMode,
                            slotPaths,
                            compiledSession,
                            build,
                            progress,
                            cancellationToken,
                            outputPath,
                            ctrlRamFirmwareVersionEdit).ConfigureAwait(false);

                if (replaceMode == GeneralReplaceMode)
                {
                    if (result.AcceptedGeneralMappingDraft is { } accepted &&
                        ReferenceEquals(generalDraft, _generalReplaceDraft) &&
                        _authoringSessions.GeneralReplace.CurrentSnapshot?.AuthoringRevision ==
                            generalSession!.AuthoringRevision)
                    {
                        _generalReplaceDraft = accepted;
                    }
                }
                return result;
            },
            (action, errorMessage) => Reports.LoadRunErrorReport(
                action,
                $"{icId.ToLowerInvariant()}-{replaceMode.ToLowerInvariant()}-replace",
                icId,
                number,
                errorMessage,
                slotPaths,
             compositionKind: "Replace",
             modeId: $"{replaceMode.ToLowerInvariant()}-replace",
             experienceId: $"{replaceMode.ToLowerInvariant()}-replace"));
    }

    private Dictionary<string, string> CreateReplaceSlotPaths()
    {
        Dictionary<string, string> paths = new(StringComparer.Ordinal);
        foreach (FirmwareSlotViewModel slot in ReplaceSlots)
        {
            FirmwareSlotPathProjection.Add(paths, slot.SlotId, slot);
        }

        if (!ReplaceSlots.Contains(ReplaceBaseSlot))
        {
            FirmwareSlotPathProjection.Add(paths, ReplaceBaseSlot.SlotId, ReplaceBaseSlot);
        }

        foreach (GeneralReplaceMappingViewModel mapping in GeneralReplaceMappings)
        {
            if (mapping.UsesFileSource && !string.IsNullOrWhiteSpace(mapping.FilePath))
            {
                paths[mapping.MappingId] = mapping.FilePath;
            }
        }

        return paths;
    }
}
