using NvtFwCombiner.Bootstrap;

namespace NvtFwCombiner.Presentation.Avalonia.ViewModels;

internal sealed record MergeBuildSavePreparation(
    string SuggestedFileName,
    WorkbenchAbAFlashCodeDeliveryPlan? AFlashCodePlan);

public sealed partial class MainWindowViewModel
{
    /// <summary>Builds the active Merge output to a user-selected path.</summary>
    public Task BuildMergeAsync(string outputPath, string? aFlashCodeOutputPath = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(outputPath);
        return RunMergeAsync(build: true, outputPath, aFlashCodeOutputPath);
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
        IReadOnlyList<WorkbenchAbMergeTopologyChoice> choices =
            AbMergeWorkbenchCompositionService.GetTopologyChoices(SelectedIc);
        AbMergeTopologyChoices.Clear();
        foreach (WorkbenchAbMergeTopologyChoice choice in choices)
        {
            AbMergeTopologyChoices.Add(choice);
        }

        OnPropertyChanged(nameof(HasAbMergeTopologyChoices));
    }

    private string? GetSelectedAbMergeTopologyToken()
    {
        return AbMergeTopologyChoices.Any(choice =>
            StringComparer.Ordinal.Equals(choice.Token, SelectedNumber))
            ? SelectedNumber
            : null;
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
            (!HasAbMergeTopologyChoices || GetSelectedAbMergeTopologyToken() is not null) &&
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

    private Task RunMergeAsync(bool build, string? outputPath, string? aFlashCodeOutputPath = null)
    {
        return SelectedMergeMode switch
        {
            NormalMergeMode => RunStandardMergeAsync(build, outputPath),
            AbCodeMergeMode => RunAbMergeAsync(build, outputPath, aFlashCodeOutputPath),
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
                slot => _abMergeAddressSpaceBySlotId[slot.SlotId],
                slot => slot.FilePath!,
                StringComparer.Ordinal);
        return await AbMergeWorkbenchCompositionService.TryCreateAFlashCodeDeliveryPlanAsync(
                SelectedIc,
                slotPaths,
                cancellationToken,
                GetSelectedAbMergeTopologyToken())
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

    /// <summary>
    /// Refreshes an AB automatic save name after the native dialog closes without replacing a user-entered name.
    /// </summary>
    internal async ValueTask<string?> TryResolveAbMergeBuildOutputPathAsync(
        string selectedOutputPath,
        string initialAutomaticFileName,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(selectedOutputPath);
        ArgumentException.ThrowIfNullOrWhiteSpace(initialAutomaticFileName);

        if (!IsAbCodeMergeModeSelected ||
            !string.Equals(
                Path.GetFileName(selectedOutputPath),
                initialAutomaticFileName,
                StringComparison.Ordinal))
        {
            return selectedOutputPath;
        }

        MergeBuildSavePreparation? refreshedPreparation = await TryPrepareMergeBuildSaveAsync(cancellationToken)
            .ConfigureAwait(false);
        if (refreshedPreparation is null)
        {
            return null;
        }

        string outputDirectory = Path.GetDirectoryName(Path.GetFullPath(selectedOutputPath))!;
        return Path.Combine(outputDirectory, refreshedPreparation.SuggestedFileName);
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

    private Task RunAbMergeAsync(bool build, string? outputPath, string? aFlashCodeOutputPath)
    {
        string icId = SelectedIc;
        IReadOnlyDictionary<string, string> slotPaths = CreateAbMergeSlotPaths();
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
                GetSelectedAbMergeTopologyToken(),
                aFlashCodeOutputPath),
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

    private void PublishAbMergeBuildSavePreparationFailure(string message)
    {
        string icId = SelectedIc;
        string number = SelectedNumber;
        IReadOnlyDictionary<string, string> slotPaths = CreateAbMergeSlotPaths();
        string profileId = WorkbenchCompositionService.GetAbMergeProfileSummaries()
            .Single(profile => StringComparer.Ordinal.Equals(profile.IcId, icId))
            .ProfileId;
        LoadRunErrorReport(
            "Build",
            profileId,
            icId,
            number,
            message,
            slotPaths,
            compositionKind: "Merge",
            modeId: WorkbenchWorkflowIds.AbMerge,
            experienceId: WorkbenchWorkflowIds.AbMerge);
        LastRunResult = new UiRunResultViewModel("Build failed", message, "No output", succeeded: false);
        OnPropertyChanged(nameof(LastRunResult));
        ShowReport();
    }

    private Dictionary<string, string> CreateStandardMergeSlotPaths()
    {
        Dictionary<string, string> paths = new(StringComparer.Ordinal);
        AddPath(paths, WorkbenchAddressSpaceIds.DpInput, _mergeDpSlot);
        AddPath(paths, WorkbenchAddressSpaceIds.TpInput, _mergeTpSlot);
        AddPath(paths, WorkbenchAddressSpaceIds.LdInput, _mergeLdSlot);
        return paths;
    }

    private Dictionary<string, string> CreateAbMergeSlotPaths()
    {
        return MergeSlots
            .Where(static slot => slot.HasFile)
            .ToDictionary(
                slot => _abMergeAddressSpaceBySlotId[slot.SlotId],
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
        bool deliveryComplete = result.Succeeded && result.IsDeliveryComplete;
        string detail = !result.IsDeliveryComplete && !string.IsNullOrWhiteSpace(result.DeliveryFailureMessage)
            ? result.DeliveryFailureMessage
            : result.Succeeded
            ? $"{result.ProfileId} / {result.OutputSize} bytes / {Text.RunResultReportReadyLabel}"
            : report.Issues.Count == 0 ? result.Status : report.Issues[0].Detail;
        LastRunResult = new UiRunResultViewModel(
            result.Succeeded
                ? deliveryComplete ? $"{action} succeeded" : $"{action} partially delivered"
                : $"{action} blocked",
            detail,
            result.Succeeded ? result.CommittedOutputId ?? result.OutputFileName : "No output",
            deliveryComplete);
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
        if (build && (!deliveryComplete || string.IsNullOrWhiteSpace(result.CommittedOutputId)))
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
