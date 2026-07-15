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

    private async Task RunReplaceAsync(bool build)
    {
        await RunReplaceAsync(build, outputPath: null, ctrlRamFirmwareVersionEdit: null);
    }

    private async Task RunReplaceAsync(
        bool build,
        string? outputPath,
        WorkbenchCtrlRamFirmwareVersionEdit? ctrlRamFirmwareVersionEdit)
    {
        CloseReplaceSelectionForRun();
        CancellationTokenSource? cancellationSource = null;
        try
        {
            cancellationSource = BeginRun();
            WorkbenchRunResult result = await WorkbenchCompositionService.RunReplaceAsync(
                SelectedIc,
                SelectedNumber,
                SelectedReplaceMode,
                CreateReplaceSlotPaths(),
                CreateGeneralReplaceMappingInputs(),
                build,
                cancellationSource.Token,
                outputPath,
                ctrlRamFirmwareVersionEdit);
            ApplyRunResult(result, build);
            RefreshCommandState();
        }
        catch (OperationCanceledException) when (cancellationSource is { IsCancellationRequested: true })
        {
            RefreshCommandState();
        }
        catch (Exception exception) when (exception is InvalidOperationException or IOException or UnauthorizedAccessException or ArgumentException)
        {
            RefreshCommandState();
            string action = build ? "Build" : "Preview";
            LastRunResult = new UiRunResultViewModel(
                $"{action} failed",
                exception.Message,
                "No output",
                succeeded: false);
            OnPropertyChanged(nameof(LastRunResult));
            LoadRunErrorReport(
                action,
                $"{SelectedIc.ToLowerInvariant()}-{SelectedReplaceMode.ToLowerInvariant()}-replace",
                SelectedIc,
                SelectedNumber,
                exception.Message,
                CreateReplaceSlotPaths(),
                compositionKind: "Replace",
                modeId: $"{SelectedReplaceMode.ToLowerInvariant()}-replace",
                experienceId: $"{SelectedReplaceMode.ToLowerInvariant()}-replace");
        }
        finally
        {
            if (cancellationSource is not null)
            {
                CompleteRun(cancellationSource);
            }
        }
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
