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

    private void RefreshMergeSlotRequirements()
    {
        if (IsAbCodeMergeModeSelected)
        {
            RefreshAbMergeSlots();
            return;
        }

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

    private void RefreshAbMergeSlots()
    {
        RefreshAbMergeTopologyChoices();
        RefreshAbMergeInputSlots();
    }

    private void RefreshAbMergeInputSlots()
    {
        MergeSlots.Clear();
        _abMergeAddressSpaceBySlotId.Clear();
        foreach (WorkbenchAbMergeInputSlot input in WorkbenchCompositionService.GetAbMergeInputSlots(
                     SelectedIc,
                     GetSelectedAbMergeTopologyToken()))
        {
            if (!_abMergeSlotsByAddressSpace.TryGetValue(input.AddressSpaceId, out FirmwareSlotViewModel? slot))
            {
                slot = new FirmwareSlotViewModel(
                    input.SlotId,
                    ShellTextResources.GetAbSlotTitle(input.Role),
                    Text.GetAbSlotDescription(input),
                    input.Role == WorkbenchAbMergeInputRole.DpAb ? FirmwareSlotKind.Dp : FirmwareSlotKind.Tp);
                _abMergeSlotsByAddressSpace.Add(input.AddressSpaceId, slot);
            }

            slot.ApplyDisplayText(
                ShellTextResources.GetAbSlotTitle(input.Role),
                Text.GetAbSlotDescription(input),
                Text.RequiredLabel,
                Text.OptionalLabel,
                Text.NoBinSelectedLabel);
            _abMergeAddressSpaceBySlotId[input.SlotId] = input.AddressSpaceId;
            MergeSlots.Add(slot);
        }
    }

    private void RefreshAbMergeTopologyChoices()
    {
        string? selectedToken = SelectedAbMergeTopologyChoice?.Token;
        IReadOnlyList<WorkbenchAbMergeTopologyChoice> choices =
            AbMergeWorkbenchCompositionService.GetTopologyChoices(SelectedIc);
        _isRefreshingAbMergeTopology = true;
        try
        {
            AbMergeTopologyChoices.Clear();
            foreach (WorkbenchAbMergeTopologyChoice choice in choices)
            {
                AbMergeTopologyChoices.Add(choice);
            }

            WorkbenchAbMergeTopologyChoice? retainedChoice = null;
            foreach (WorkbenchAbMergeTopologyChoice choice in choices)
            {
                if (StringComparer.Ordinal.Equals(choice.Token, selectedToken))
                {
                    retainedChoice = choice;
                    break;
                }
            }

            SelectedAbMergeTopologyChoice = retainedChoice ??
                (choices.Count == 0 ? null : choices[0]);
        }
        finally
        {
            _isRefreshingAbMergeTopology = false;
        }

        OnPropertyChanged(nameof(HasAbMergeTopologyChoices));
    }

    private string? GetSelectedAbMergeTopologyToken()
    {
        return SelectedAbMergeTopologyChoice?.Token;
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

    private bool CanRunAbMerge()
    {
        return IsAbCodeMergeModeSelected &&
            IsAbMergeSupported &&
            (!HasAbMergeTopologyChoices || SelectedAbMergeTopologyChoice is not null) &&
            MergeSlots.Count > 0 &&
            MergeSlots.All(static slot =>
                slot.HasFile &&
                slot.InputInspectionSeverity is not null &&
                !slot.BlocksBuild &&
                !slot.IsInputInspectionPending);
    }

    private bool CanRunMerge()
    {
        return !IsRunInProgress && !IsFirmwareInspectionLoading && (SelectedMergeMode switch
        {
            NormalMergeMode => CanRunStandardMerge(),
            AbCodeMergeMode => CanRunAbMerge(),
            GeneralMergeMode => CanRunGeneralMerge(),
            _ => false,
        });
    }

    private Task RunMergeAsync(bool build, string? outputPath)
    {
        return SelectedMergeMode switch
        {
            NormalMergeMode => RunStandardMergeAsync(build, outputPath),
            AbCodeMergeMode => RunAbMergeAsync(build, outputPath),
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
                slot => _abMergeAddressSpaceBySlotId[slot.SlotId],
                slot => slot.FilePath!,
                StringComparer.Ordinal);
        return await AbMergeWorkbenchCompositionService.ResolveAutomaticOutputFileNameAsync(
                SelectedIc,
                slotPaths,
                cancellationToken,
                GetSelectedAbMergeTopologyToken())
            .ConfigureAwait(false);
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
            (progress, cancellationToken) => WorkbenchCompositionService.RunStandardMergeWithProgressAsync(
                icId,
                slotPaths,
                build,
                progress,
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
            (progress, cancellationToken) => WorkbenchCompositionService.RunGeneralMergeWithProgressAsync(
                icId,
                outputLength,
                mappingInputs,
                build,
                progress,
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

    private Task RunAbMergeAsync(bool build, string? outputPath)
    {
        string icId = SelectedIc;
        IReadOnlyDictionary<string, string> slotPaths = MergeSlots
            .Where(static slot => slot.HasFile)
            .ToDictionary(
                slot => _abMergeAddressSpaceBySlotId[slot.SlotId],
                slot => slot.FilePath!,
                StringComparer.Ordinal);
        string profileId = WorkbenchCompositionService.GetAbMergeProfileSummaries()
            .Single(profile => StringComparer.Ordinal.Equals(profile.IcId, icId))
            .ProfileId;
        return RunCompositionAsync(
            build,
            (progress, cancellationToken) => AbMergeWorkbenchCompositionService.RunAbMergeWithProgressAsync(
                icId,
                slotPaths,
                build,
                progress,
                cancellationToken,
                outputPath,
                GetSelectedAbMergeTopologyToken()),
            (action, errorMessage) => LoadRunErrorReport(
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

    private void ApplyRunResult(
        WorkbenchRunResult result,
        bool build,
        ReportReviewViewModel report,
        bool publishReport)
    {
        string action = build ? "Build" : "Preview";
        string detail = result.Succeeded
            ? $"{result.ProfileId} / {result.OutputSize} bytes / {Text.RunResultReportReadyLabel}"
            : report.Issues.Count == 0 ? result.Status : report.Issues[0].Detail;
        LastRunResult = new UiRunResultViewModel(
            result.Succeeded ? $"{action} succeeded" : $"{action} blocked",
            detail,
            result.Succeeded ? result.CommittedOutputId ?? result.OutputFileName : "No output",
            result.Succeeded);
        OnPropertyChanged(nameof(LastRunResult));
        _ = TryShowBuildCompleted(result, build);

        if (!publishReport)
        {
            return;
        }

        LoadedReport = report;
        LoadedReportJson = result.ReportJson;
        CaptureLoadedReportInHistory();
        SetReportToast(Text.FormatReportGeneratedToast(action));
        NotifyReportChanged();
        if (build && (!result.Succeeded || string.IsNullOrWhiteSpace(result.CommittedOutputId)))
        {
            ShowReport();
        }
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
