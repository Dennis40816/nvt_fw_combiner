using NvtFwCombiner.Bootstrap;

namespace NvtFwCombiner.Presentation.Avalonia.ViewModels;

public sealed partial class MainWindowViewModel
{
    /// <summary>Builds the active Merge output to a user-selected path.</summary>
    public Task BuildMergeAsync(string outputPath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(outputPath);
        return RunMergeAsync(build: true, outputPath);
    }

    /// <summary>Builds Standard Merge output to a user-selected path.</summary>
    public Task BuildStandardMergeAsync(string outputPath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(outputPath);
        return RunStandardMergeAsync(build: true, outputPath);
    }

    private void RefreshMergeSlotRequirements()
    {
        IReadOnlyList<string> required = UiCompositionRunner.GetStandardMergeRequiredAddressSpaces(SelectedIc);
        _mergeDpSlot.IsOptional = !required.Contains(WorkbenchAddressSpaceIds.DpInput, StringComparer.Ordinal);
        _mergeTpSlot.IsOptional = !required.Contains(WorkbenchAddressSpaceIds.TpInput, StringComparer.Ordinal);
        _mergeLdSlot.IsOptional = !required.Contains(WorkbenchAddressSpaceIds.LdInput, StringComparer.Ordinal);
        MergeSlots.Clear();
        if (required.Contains(WorkbenchAddressSpaceIds.DpInput, StringComparer.Ordinal))
        {
            MergeSlots.Add(_mergeDpSlot);
        }

        if (required.Contains(WorkbenchAddressSpaceIds.TpInput, StringComparer.Ordinal))
        {
            MergeSlots.Add(_mergeTpSlot);
        }

        if (required.Contains(WorkbenchAddressSpaceIds.LdInput, StringComparer.Ordinal))
        {
            MergeSlots.Add(_mergeLdSlot);
        }
    }

    private void RefreshMergeModeState()
    {
        ActiveMergeRows.Clear();
        if (IsGeneralMergeModeSelected)
        {
            AddMergeRows(
                $"{SelectedIc}: General Merge input policy is active.",
                "Output starts as reserved bytes; mapping rows copy explicit source ranges into it.",
                "No postbuild command is invoked by General Merge.",
                $"Output length: {GeneralMergeOutputLength}");
            return;
        }

        if (IsAbCodeMergeModeSelected)
        {
            AddMergeRows(
                "AB Code Merge is reserved.",
                "This mode will need a dedicated profile because it has TP CRC/start-address behavior.",
                "Use Standard Merge or General Merge for current 0.7.x workflows.");
            return;
        }

        string? profileId = UiCompositionRunner.GetStandardMergeProfileId(SelectedIc);
        if (profileId is null)
        {
            AddMergeRows(
                $"Profile: not available for {SelectedIc}",
                "Build stays disabled until a profile is added.",
                $"{SelectedIc} / {SelectedNumber} still refreshes Replace region policy.");
            return;
        }

        AddMergeRows(
            $"Profile: {profileId}",
            $"Required slots: {GetRequiredStandardMergeSlotLabels()}",
            GetStandardMergeRangeSummary());
    }

    private void AddMergeRows(params string[] rows)
    {
        foreach (string row in rows)
        {
            ActiveMergeRows.Add(row);
        }
    }

    private string GetRequiredStandardMergeSlotLabels()
    {
        IReadOnlyList<string> required = UiCompositionRunner.GetStandardMergeRequiredAddressSpaces(SelectedIc);
        return required.Count == 0
            ? "none"
            : string.Join(", ", required.Select(AddressSpaceLabel));
    }

    private string GetStandardMergeRangeSummary()
    {
        return UiCompositionRunner.GetStandardMergePolicySummary(SelectedIc);
    }

    private static string AddressSpaceLabel(string addressSpaceId)
    {
        return addressSpaceId switch
        {
            WorkbenchAddressSpaceIds.DpInput => "DP",
            WorkbenchAddressSpaceIds.TpInput => "TP",
            WorkbenchAddressSpaceIds.LdInput => "LD",
            _ => addressSpaceId,
        };
    }

    private bool CanRunStandardMerge()
    {
        IReadOnlyList<string> requiredAddressSpaces = UiCompositionRunner.GetStandardMergeRequiredAddressSpaces(SelectedIc);
        return IsNormalMergeModeSelected && requiredAddressSpaces.Count > 0 && requiredAddressSpaces.All(addressSpace =>
            MergeSlotForAddressSpace(addressSpace) is { HasFile: true });
    }

    private bool CanRunGeneralMerge()
    {
        return IsGeneralMergeModeSelected &&
            !string.IsNullOrWhiteSpace(GeneralMergeOutputLength) &&
            GeneralMergeMappings.Any(mapping => mapping.HasFile);
    }

    private bool CanRunMerge()
    {
        return !IsRunInProgress && (SelectedMergeMode switch
        {
            NormalMergeMode => CanRunStandardMerge(),
            GeneralMergeMode => CanRunGeneralMerge(),
            _ => false,
        });
    }

    private Task RunMergeAsync(bool build)
    {
        return RunMergeAsync(build, outputPath: null);
    }

    private Task RunMergeAsync(bool build, string? outputPath)
    {
        return SelectedMergeMode switch
        {
            NormalMergeMode => RunStandardMergeAsync(build, outputPath),
            GeneralMergeMode => RunGeneralMergeAsync(build, outputPath),
            _ => Task.CompletedTask,
        };
    }

    private async Task RunStandardMergeAsync(bool build, string? outputPath)
    {
        CancellationTokenSource? cancellationSource = null;
        try
        {
            cancellationSource = BeginRun();
            WorkbenchRunResult result = await UiCompositionRunner.RunStandardMergeAsync(
                SelectedIc,
                CreateStandardMergeSlotPaths(),
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
                UiCompositionRunner.GetStandardMergeProfileId(SelectedIc) ?? WorkbenchWorkflowIds.StandardMerge,
                SelectedIc,
                SelectedNumber,
                exception.Message,
                CreateStandardMergeSlotPaths());
        }
        finally
        {
            if (cancellationSource is not null)
            {
                CompleteRun(cancellationSource);
            }
        }
    }

    private async Task RunGeneralMergeAsync(bool build, string? outputPath)
    {
        CancellationTokenSource? cancellationSource = null;
        try
        {
            cancellationSource = BeginRun();
            WorkbenchRunResult result = await UiCompositionRunner.RunGeneralMergeAsync(
                SelectedIc,
                GeneralMergeOutputLength,
                CreateGeneralMergeMappingInputs(),
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
                UiCompositionRunner.GetGeneralMergeDefaultOutputFileName(SelectedIc),
                SelectedIc,
                SelectedNumber,
                exception.Message,
                CreateGeneralMergeSlotPaths(),
                compositionKind: "Merge",
                modeId: WorkbenchWorkflowIds.GeneralMerge,
                experienceId: WorkbenchWorkflowIds.GeneralMerge);
        }
        finally
        {
            if (cancellationSource is not null)
            {
                CompleteRun(cancellationSource);
            }
        }
    }

    private Dictionary<string, string> CreateStandardMergeSlotPaths()
    {
        Dictionary<string, string> paths = new(StringComparer.Ordinal);
        AddPath(paths, WorkbenchAddressSpaceIds.DpInput, _mergeDpSlot);
        AddPath(paths, WorkbenchAddressSpaceIds.TpInput, _mergeTpSlot);
        AddPath(paths, WorkbenchAddressSpaceIds.LdInput, _mergeLdSlot);
        return paths;
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

    private static void AddPath(
        Dictionary<string, string> paths,
        string addressSpaceId,
        FirmwareSlotViewModel slot)
    {
        if (!string.IsNullOrWhiteSpace(slot.FilePath))
        {
            paths[addressSpaceId] = slot.FilePath;
        }
    }

    private void ApplyRunResult(WorkbenchRunResult result, bool build)
    {
        string action = build ? "Build" : "Preview";
        var report = ReportReviewViewModel.FromJson(
            result.ReportJson,
            $"{action.ToLowerInvariant()} report",
            result.CommittedOutputId,
            Text.Language);
        string detail = result.Succeeded
            ? $"{result.ProfileId} / {result.OutputSize} bytes / {Text.RunResultReportReadyLabel}"
            : report.Issues.Count == 0 ? result.Status : report.Issues[0].Detail;
        LastRunResult = new UiRunResultViewModel(
            result.Succeeded ? $"{action} succeeded" : $"{action} blocked",
            detail,
            result.Succeeded ? result.CommittedOutputId ?? result.OutputFileName : "No output",
            result.Succeeded);
        OnPropertyChanged(nameof(LastRunResult));

        LoadedReport = report;
        LoadedReportJson = result.ReportJson;
        CaptureLoadedReportInHistory();
        SetReportToast(Text.FormatReportGeneratedToast(action));
        NotifyReportChanged();
        RefreshSettingsState();
    }

    private FirmwareSlotViewModel? MergeSlotForAddressSpace(string addressSpaceId)
    {
        return addressSpaceId switch
        {
            WorkbenchAddressSpaceIds.DpInput => _mergeDpSlot,
            WorkbenchAddressSpaceIds.TpInput => _mergeTpSlot,
            WorkbenchAddressSpaceIds.LdInput => _mergeLdSlot,
            _ => null,
        };
    }
}
