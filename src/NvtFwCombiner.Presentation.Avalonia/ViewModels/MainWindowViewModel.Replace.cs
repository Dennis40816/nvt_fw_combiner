using NvtFwCombiner.Bootstrap;

namespace NvtFwCombiner.Presentation.Avalonia.ViewModels;

public sealed partial class MainWindowViewModel
{
    /// <summary>Gets short Replace memory-map summary text.</summary>
    public string ReplaceMemorySummary => Text.GetReplaceMemorySummary(SelectedReplaceMode);

    /// <summary>Status shown in the replace inspector.</summary>
    public string ReplaceReadinessStatus => Text.GetReplaceReadinessStatus(SelectedReplaceMode, CanRunReplace());

    /// <summary>Gets the compact reason shown on disabled Replace preview.</summary>
    public string ReplacePreviewUnavailableReason => ReplaceReadinessStatus;

    /// <summary>Gets the compact reason shown on disabled Replace build.</summary>
    public string ReplaceBuildUnavailableReason => ReplaceReadinessStatus;

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
        return !IsRunInProgress && (SelectedReplaceMode switch
        {
            DpReplaceMode => ReplaceSlots.Count > 0 &&
                ReplaceSlots.Where(slot => !slot.IsOptional).All(slot => slot.HasFile),
            CtrlRamReplaceMode => ReplaceBaseSlot.HasFile &&
                ReplaceSlots.Any(slot => !ReferenceEquals(slot, ReplaceBaseSlot) && slot.HasFile),
            GeneralReplaceMode => ReplaceBaseSlot.HasFile &&
                GeneralReplaceMappings.Any(mapping => mapping.HasFile),
            _ => false,
        });
    }

    private Task RunReplaceAsync(bool build)
    {
        return RunReplaceAsync(build, outputPath: null, ctrlRamFirmwareVersionEdit: null);
    }

    private Task RunReplaceAsync(
        bool build,
        string? outputPath,
        WorkbenchCtrlRamFirmwareVersionEdit? ctrlRamFirmwareVersionEdit)
    {
        CloseReplaceSelectionForRun();
        return RunCompositionAsync(
            build,
            cancellationToken => WorkbenchCompositionService.RunReplaceAsync(
                SelectedIc,
                SelectedNumber,
                SelectedReplaceMode,
                CreateReplaceSlotPaths(),
                CreateGeneralReplaceMappingInputs(),
                build,
                cancellationToken,
                outputPath,
                ctrlRamFirmwareVersionEdit),
            (action, errorMessage) => LoadRunErrorReport(
                action,
                $"{SelectedIc.ToLowerInvariant()}-{SelectedReplaceMode.ToLowerInvariant()}-replace",
                SelectedIc,
                SelectedNumber,
                errorMessage,
                CreateReplaceSlotPaths(),
                compositionKind: "Replace",
                modeId: $"{SelectedReplaceMode.ToLowerInvariant()}-replace",
                experienceId: $"{SelectedReplaceMode.ToLowerInvariant()}-replace"));
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
