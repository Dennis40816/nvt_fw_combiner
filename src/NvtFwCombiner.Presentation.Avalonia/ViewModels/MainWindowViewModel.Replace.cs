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
    public Task BuildReplaceAsync(string outputPath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(outputPath);
        return RunReplaceAsync(build: true, outputPath);
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

    /// <summary>Whether the Hex Editor has a selected base BIN and at least one staged patch.</summary>
    public bool CanBuildHexEditor =>
        !IsRunInProgress && HasGeneralReplaceBaseSnapshot && GeneralReplacePatches.Count > 0;

    /// <summary>Builds the staged experimental Hex Editor changes through the General Replace pipeline.</summary>
    public Task BuildHexEditorAsync(string outputPath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(outputPath);
        return RunHexEditorAsync(build: true, outputPath);
    }

    private async Task RunReplaceAsync(bool build)
    {
        await RunReplaceAsync(build, outputPath: null);
    }

    private async Task RunReplaceAsync(bool build, string? outputPath)
    {
        CloseReplaceSelectionForRun();
        CancellationTokenSource? cancellationSource = null;
        try
        {
            cancellationSource = BeginRun();
            WorkbenchRunResult result = await UiCompositionRunner.RunReplaceAsync(
                SelectedIc,
                SelectedNumber,
                SelectedReplaceMode,
                CreateReplaceSlotPaths(),
                CreateGeneralReplaceMappingInputs(),
                [],
                build,
                cancellationSource.Token,
                outputPath);
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

    private async Task RunHexEditorAsync(bool build, string? outputPath = null)
    {
        if (_generalReplaceBaseSnapshot is null)
        {
            throw new InvalidOperationException(_generalReplaceBaseSnapshotError ?? Text.HexEditorBaseRequiredDetail);
        }

        CloseReplaceSelectionForRun();
        CancellationTokenSource? cancellationSource = null;
        try
        {
            cancellationSource = BeginRun();
            WorkbenchRunResult result = await UiCompositionRunner.RunReplaceAsync(
                SelectedIc,
                SelectedNumber,
                GeneralReplaceMode,
                CreateReplaceSlotPaths(),
                [],
                CreateGeneralReplacePatchInputs(),
                build,
                cancellationSource.Token,
                outputPath,
                _generalReplaceBaseSnapshot);
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
            LastRunResult = new UiRunResultViewModel(
                "Hex Editor build failed",
                exception.Message,
                "No output",
                succeeded: false);
            OnPropertyChanged(nameof(LastRunResult));
            LoadRunErrorReport(
                "Build",
                $"{SelectedIc.ToLowerInvariant()}-hex-editor",
                SelectedIc,
                SelectedNumber,
                exception.Message,
                CreateReplaceSlotPaths(),
                compositionKind: "Replace",
                modeId: "general-replace-hex-editor",
                experienceId: "experimental-hex-editor");
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
