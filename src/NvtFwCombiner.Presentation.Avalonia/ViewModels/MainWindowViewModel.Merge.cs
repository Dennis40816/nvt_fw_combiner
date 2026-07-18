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
        IReadOnlyList<string> required = WorkbenchCompositionService.GetStandardMergeRequiredAddressSpaces(SelectedIc);
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

    private string GetRequiredStandardMergeSlotLabels()
    {
        IReadOnlyList<string> required = WorkbenchCompositionService.GetStandardMergeRequiredAddressSpaces(SelectedIc);
        return required.Count == 0
            ? "none"
            : string.Join(", ", required.Select(AddressSpaceLabel));
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
        IReadOnlyList<string> requiredAddressSpaces =
            WorkbenchCompositionService.GetStandardMergeRequiredAddressSpaces(SelectedIc);
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

    private Task RunStandardMergeAsync(bool build, string? outputPath)
    {
        string icId = SelectedIc;
        string number = SelectedNumber;
        IReadOnlyDictionary<string, string> slotPaths = CreateStandardMergeSlotPaths();
        string profileId =
            WorkbenchCompositionService.GetStandardMergeProfileId(icId) ?? WorkbenchWorkflowIds.StandardMerge;
        return RunCompositionAsync(
            build,
            cancellationToken => WorkbenchCompositionService.RunStandardMergeAsync(
                icId,
                slotPaths,
                build,
                cancellationToken,
                outputPath),
            (action, errorMessage) => LoadRunErrorReport(
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
        string outputLength = GeneralMergeOutputLength;
        IReadOnlyList<WorkbenchGeneralMergeMappingInput> mappingInputs = CreateGeneralMergeMappingInputs();
        IReadOnlyDictionary<string, string> slotPaths = CreateGeneralMergeSlotPaths();
        string outputFileName = WorkbenchCompositionService.GetGeneralMergeDefaultOutputFileName(icId);
        return RunCompositionAsync(
            build,
            cancellationToken => WorkbenchCompositionService.RunGeneralMergeAsync(
                icId,
                outputLength,
                mappingInputs,
                build,
                cancellationToken,
                outputPath),
            (action, errorMessage) => LoadRunErrorReport(
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
