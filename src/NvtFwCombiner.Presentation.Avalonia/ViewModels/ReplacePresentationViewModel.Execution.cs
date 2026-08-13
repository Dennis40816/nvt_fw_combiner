using NvtFwCombiner.Application.Capabilities;
using NvtFwCombiner.Application.Authoring;
using NvtFwCombiner.Application.Metadata;
using NvtFwCombiner.Domain.Composition;

namespace NvtFwCombiner.Presentation.Avalonia.ViewModels;

internal sealed partial class ReplacePresentationViewModel
{
    public string ReplaceMemorySummary => Text.GetReplaceMemorySummary(SelectedReplaceMode);

    public string ReplaceReadinessStatus => Inspection.IsRunning
        ? Text.FirmwareInspectionLoadingStatus
        : IsSelectedReplaceModeSupported
            ? Text.GetReplaceReadinessStatus(SelectedReplaceMode, CanRunReplace())
            : Text.GetReplaceNotSupportedStatus(SelectedIc);

    public Task BuildReplaceAsync(
        string outputPath,
        CtrlRamFirmwareVersionDraftState? ctrlRamFirmwareVersionEdit = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(outputPath);
        return RunBuildReplaceAsync(outputPath, ctrlRamFirmwareVersionEdit);
    }

    private Task PreviewReplaceAsync()
    {
        return RunReplaceAsync(
            build: false,
            outputPath: null,
            ctrlRamFirmwareVersionEdit: null);
    }

    private Task RunBuildReplaceAsync(
        string? outputPath,
        CtrlRamFirmwareVersionDraftState? ctrlRamFirmwareVersionEdit)
    {
        return RunReplaceAsync(
            build: true,
            outputPath,
            ctrlRamFirmwareVersionEdit);
    }

    private bool CanRunReplace()
    {
        return !_stateBindings.IsGlobalBuildBlocked() &&
            !_stateBindings.IsRunInProgress() && !Inspection.IsRunning &&
            IsSelectedReplaceModeSupported &&
            (SelectedReplaceMode switch
            {
                DpReplaceMode => CanRunDpReplace(),
                CtrlRamReplaceMode =>
                    CanRunCompiledReplaceSession(_ctrlRamReplaceSession) &&
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
        return CanRunCompiledReplaceSession(_dpReplaceSession);
    }

    private void RefreshDpReplaceInputSelectionReadiness()
    {
        FirmwareSlotViewModel[] selected = [.. CurrentReplaceInputSlots().DistinctBy(ReplaceInputId)];
        ActiveSessionSnapshot? session = _dpReplaceSession.CurrentSnapshot;
        bool currentSelection = session is not null &&
            session.Slots.Count(static slot => slot.SelectedPath is not null) == selected.Length &&
            selected.All(slot => session.Slots.Any(current =>
                StringComparer.Ordinal.Equals(current.SelectedPath, slot.FilePath)));
        IReadOnlyList<InputSelectionMemberReadiness>? readiness =
            SelectedReplaceMode != DpReplaceMode
                ? null
                : currentSelection && session!.InputSelectionReadiness.Count != 0
                    ? session.InputSelectionReadiness
                    : ResolveDpReplaceAuthoringSnapshot(selected).Slots;
        foreach (FirmwareSlotViewModel slot in ReplaceSlots.ToArray().Where(slot =>
                     !ReferenceEquals(slot, ReplaceBaseSlot)))
        {
            InputSelectionMemberReadiness? member = readiness?.FirstOrDefault(candidate =>
                string.Equals(candidate.SlotId, slot.CompiledSlotId, StringComparison.Ordinal));
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
        CtrlRamFirmwareVersionDraftState? ctrlRamFirmwareVersionEdit)
    {
        CloseSelectionForRun();
        string icId = SelectedIc;
        string number = SelectedNumber;
        string replaceMode = SelectedReplaceMode;
        IReadOnlyDictionary<string, string> slotPaths = CreateReplaceSlotPaths();
        CtrlRamAuthoringTransitionResult? ctrlRamTransition =
            replaceMode == CtrlRamReplaceMode
                ? _compositionServices.CtrlRamAuthoring.TransitionFirmwareVersionCompilation(
                    _ctrlRamReplaceSession,
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
            ? _generalReplaceSession.CurrentSnapshot ??
                throw new InvalidOperationException(
                    "General Replace requires one accepted authoring session.")
            : null;
        var generalDraft =
            generalSession?.DraftState as GeneralMappingDraftState;
        ActiveSessionSnapshot? compiledSession = replaceMode switch
        {
            DpReplaceMode => _dpReplaceSession.CurrentSnapshot,
            CtrlRamReplaceMode => ctrlRamTransition?.Session,
            _ => null,
        };
        CapabilityActionReadinessSnapshot? actionReadiness = replaceMode switch
        {
            CtrlRamReplaceMode => _ctrlRamActionReadiness,
            GeneralReplaceMode => _generalReplaceActionReadiness,
            _ => null,
        };
        if (actionReadiness is { } readiness)
        {
            CapabilityActionAvailability action = build
                ? readiness.Build
                : readiness.Preview;
            if (!action.IsAvailable)
            {
                _stateBindings.ShowActionReadiness(readiness, build);
                return;
            }
            if (replaceMode == GeneralReplaceMode &&
                !build &&
                _generalReplaceDiagnosticPreviewReport is { } diagnosticReport)
            {
                await _stateBindings.ShowDiagnosticPreviewAsync(diagnosticReport);
                return;
            }
        }
        await RunCompositionAsync(
            build,
            async (progress, cancellationToken) =>
            {
                if (replaceMode != GeneralReplaceMode &&
                    compiledSession?.GetAcceptedCapability(
                        AuthoringDerivedResultKind.Inspection) is null)
                {
                    IReadOnlyList<CompositionIssue> issues = replaceMode == DpReplaceMode
                        ? ResolveDpReplaceAuthoringSnapshot(
                            [.. CurrentReplaceInputSlots()]).Issues
                        : ctrlRamTransition?.Issues ?? [];
                    throw new InvalidOperationException(issues.Count == 0
                        ? "Replace requires one accepted selected-input inspection."
                        : string.Join(
                            Environment.NewLine,
                            issues.Select(static issue => $"{issue.Code}: {issue.Message}")));
                }

                CompositionRunResult result = await _compositionServices.Execution.ExecuteAsync(
                        new AcceptedCompositionExecutionRequest(
                            generalSession ?? compiledSession!,
                            slotPaths,
                             build,
                             outputPath: outputPath,
                             actionReadiness: actionReadiness),
                        progress,
                        cancellationToken)
                    .ConfigureAwait(false);

                if (replaceMode == GeneralReplaceMode)
                {
                    if (result.AcceptedGeneralMappingDraft is { } accepted &&
                        ReferenceEquals(generalDraft, _generalReplaceDraft) &&
                        _generalReplaceSession.CurrentSnapshot?.AuthoringRevision ==
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
