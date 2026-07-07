using NvtFwCombiner.Bootstrap;

namespace NvtFwCombiner.Presentation.Avalonia.ViewModels;

public sealed partial class MainWindowViewModel
{
    /// <summary>Gets short Replace memory-map summary text.</summary>
    public string ReplaceMemorySummary => SelectedReplaceMode switch
    {
        DpReplaceMode => SelectedIc is "NT51950" or "NT51951"
            ? "Blue shows new DP bytes; gray shows TP restored from the base firmware."
            : "Base flash stays unchanged except approved DP replacement ranges.",
        CtrlRamReplaceMode => "Colored blocks show replaceable CtrlRAM positions; gray stays from the base firmware.",
        GeneralReplaceMode => "Base flash stays unchanged except approved explicit replacement ranges.",
        _ => "Select a replace mode to inspect its target ranges.",
    };

    /// <summary>Status shown in the replace inspector.</summary>
    public string ReplaceReadinessStatus => SelectedReplaceMode switch
    {
        DpReplaceMode => CanRunReplace()
            ? "Ready: Build will validate DP Replace inputs, then write output and report."
            : "Build blocked: base BIN and required DP replacement inputs are required.",
        CtrlRamReplaceMode => CanRunReplace()
            ? "Ready: Build will replace selected CtrlRAM regions and run postbuild."
            : "Build blocked: base BIN and at least one CtrlRAM region BIN are required.",
        GeneralReplaceMode => CanRunReplace()
            ? "Ready: Build will compile explicit mappings and run postbuild when TP ranges are touched."
            : "Build blocked: base BIN and at least one explicit replacement mapping are required.",
        _ => "Build blocked: select a Replace mode.",
    };

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
        return SelectedReplaceMode switch
        {
            DpReplaceMode => ReplaceSlots.Count > 0 &&
                ReplaceSlots.Where(slot => !slot.IsOptional).All(slot => slot.HasFile),
            CtrlRamReplaceMode => ReplaceBaseSlot.HasFile &&
                ReplaceSlots.Any(slot => !ReferenceEquals(slot, ReplaceBaseSlot) && slot.HasFile),
            GeneralReplaceMode => ReplaceBaseSlot.HasFile &&
                GeneralReplaceMappings.Any(mapping => mapping.HasFile),
            _ => false,
        };
    }

    private async Task RunReplaceAsync(bool build)
    {
        await RunReplaceAsync(build, outputPath: null);
    }

    private async Task RunReplaceAsync(bool build, string? outputPath)
    {
        CloseReplaceSelectionForRun();
        try
        {
            WorkbenchRunResult result = await UiCompositionRunner.RunReplaceAsync(
                SelectedIc,
                SelectedNumber,
                SelectedReplaceMode,
                CreateReplaceSlotPaths(),
                CreateGeneralReplaceMappingInputs(),
                build,
                CancellationToken.None,
                outputPath);
            ApplyRunResult(result, build);
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
