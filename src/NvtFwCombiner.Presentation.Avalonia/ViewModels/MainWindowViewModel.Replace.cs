using NvtFwCombiner.Application.Capabilities;
using NvtFwCombiner.Bootstrap;

namespace NvtFwCombiner.Presentation.Avalonia.ViewModels;

public sealed partial class MainWindowViewModel
{
    private bool IsSelectedReplaceModeSupported => WorkbenchCompositionService.IsReplaceWorkflowSupported(SelectedIc, SelectedReplaceMode);

    /// <summary>Gets short Replace memory-map summary text.</summary>
    public string ReplaceMemorySummary => Text.GetReplaceMemorySummary(SelectedReplaceMode);

    /// <summary>Status shown in the replace inspector.</summary>
    public string ReplaceReadinessStatus => IsFirmwareInspectionLoading
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
        return RunReplaceAsync(build: true, outputPath, ctrlRamFirmwareVersionEdit);
    }

    private bool CanRunReplace()
    {
        return !IsRunInProgress && !IsFirmwareInspectionLoading && IsSelectedReplaceModeSupported &&
            (SelectedReplaceMode switch
            {
                DpReplaceMode => CanRunDpReplace(),
                CtrlRamReplaceMode => ReplaceBaseSlot.HasFile &&
                    ReplaceSlots.Any(slot => !ReferenceEquals(slot, ReplaceBaseSlot) && slot.HasFile),
                GeneralReplaceMode => ReplaceBaseSlot.HasFile &&
                    GeneralReplaceMappings.Any(mapping => mapping.HasFile),
                _ => false,
            });
    }

    private bool CanRunDpReplace()
    {
        return WorkbenchCompositionService.HasBuiltInV2DpReplaceSelectionGroup(SelectedIc)
            ? ReplaceBaseSlot.HasFile &&
                TryGetDpReplaceInputSelectionReadiness(
                    out InputSelectionReadinessSnapshot? readiness) &&
                readiness.CanBuild
            : ReplaceBaseSlot.HasFile &&
                ReplaceSlots.Count > 0 &&
                ReplaceSlots.Where(slot => !slot.IsOptional).All(slot => slot.HasFile);
    }

    private bool TryGetDpReplaceInputSelectionReadiness(
        [System.Diagnostics.CodeAnalysis.NotNullWhen(true)]
        out InputSelectionReadinessSnapshot? readiness)
    {
        string[] selectedInputAddressSpaceIds =
        [
            .. ReplaceSlots
                .Where(slot => !ReferenceEquals(slot, ReplaceBaseSlot) && slot.HasFile)
                .Select(static slot => slot.AddressSpaceId ??
                    throw new InvalidOperationException(
                        $"Replace input slot '{slot.SlotId}' has no canonical address-space id.")),
        ];
        return WorkbenchCompositionService.TryGetDpReplaceInputSelectionReadiness(
            SelectedIc,
            GetSelectedReplaceBaseLength(),
            selectedInputAddressSpaceIds,
            out readiness);
    }

    private void RefreshDpReplaceInputSelectionReadiness()
    {
        InputSelectionReadinessSnapshot? readiness = null;
        bool hasReadiness = SelectedReplaceMode == DpReplaceMode &&
            TryGetDpReplaceInputSelectionReadiness(out readiness);
        foreach (FirmwareSlotViewModel slot in ReplaceSlots.Where(slot =>
                     !ReferenceEquals(slot, ReplaceBaseSlot)))
        {
            InputSelectionMemberReadiness? member = hasReadiness
                ? readiness!.Groups
                    .SelectMany(static group => group.Members)
                    .FirstOrDefault(candidate =>
                        string.Equals(
                            candidate.SlotId,
                            slot.AddressSpaceId,
                            StringComparison.Ordinal))
                : null;
            if (member is null)
            {
                slot.ClearSelectionReadiness();
                continue;
            }

            string label = Text.GetDpInputSelectionReadinessLabel(member.Readiness);
            string detail = Text.GetDpInputSelectionReadinessDetail(member);
            slot.SetSelectionReadiness(
                member.Readiness,
                label,
                detail,
                Text.GetInputSelectionReadinessAutomationText(label, detail));
        }
    }

    private Task RunReplaceAsync(
        bool build,
        string? outputPath,
        WorkbenchCtrlRamFirmwareVersionEdit? ctrlRamFirmwareVersionEdit)
    {
        CloseReplaceSelectionForRun();
        string icId = SelectedIc;
        string number = SelectedNumber;
        string replaceMode = SelectedReplaceMode;
        IReadOnlyDictionary<string, string> slotPaths = CreateReplaceSlotPaths();
        IReadOnlyList<WorkbenchGeneralReplaceMappingInput> mappingInputs = CreateGeneralReplaceMappingInputs();
        return RunCompositionAsync(
            build,
            async (progress, cancellationToken) =>
            {
                WorkbenchRunResult result =
                    replaceMode == GeneralReplaceMode &&
                    _acceptedGeneralReplaceDraft is not null
                        ? await WorkbenchCompositionService
                            .RunGeneralReplaceAcceptedDraftWithProgressAsync(
                                icId,
                                number,
                                slotPaths,
                                _acceptedGeneralReplaceDraft,
                                build,
                                progress,
                                cancellationToken,
                                outputPath)
                            .ConfigureAwait(false)
                        : await WorkbenchCompositionService.RunReplaceWithProgressAsync(
                            icId,
                            number,
                            replaceMode,
                            slotPaths,
                            mappingInputs,
                            [],
                            build,
                            progress,
                            cancellationToken,
                            outputPath,
                            ctrlRamFirmwareVersionEdit).ConfigureAwait(false);
                if (replaceMode == GeneralReplaceMode)
                {
                    _acceptedGeneralReplaceDraft =
                        result.AcceptedGeneralMappingDraft ??
                        _acceptedGeneralReplaceDraft;
                }
                return result;
            },
            (action, errorMessage) => LoadRunErrorReport(
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
            AddPath(paths, slot.SlotId, slot);
        }

        if (!ReplaceSlots.Contains(ReplaceBaseSlot))
        {
            AddPath(paths, ReplaceBaseSlot.SlotId, ReplaceBaseSlot);
        }

        foreach (GeneralReplaceMappingViewModel mapping in GeneralReplaceMappings)
        {
            if (!string.IsNullOrWhiteSpace(mapping.FilePath))
            {
                paths[mapping.MappingId] = mapping.FilePath;
            }
        }

        return paths;
    }
}
