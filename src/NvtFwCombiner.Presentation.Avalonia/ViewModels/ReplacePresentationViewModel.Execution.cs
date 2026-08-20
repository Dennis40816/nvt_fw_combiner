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

    private async Task RequestBuildFromCommandAsync()
    {
        if (IsCtrlRamReplaceModeSelected)
        {
            _ = await RequestCtrlRamBuildSettingsAsync();
            return;
        }

        await RequestBuildOutputDeliveryAsync();
    }

    internal Task RequestBuildOutputDeliveryAsync(
        CtrlRamFirmwareVersionDraftState? ctrlRamFirmwareVersionEdit = null,
        ActiveSessionSnapshot? exactSession = null)
    {
        ActiveSessionSnapshot session = exactSession ?? SelectedReplaceMode switch
        {
            DpReplaceMode => _dpReplaceSession.CurrentSnapshot,
            CtrlRamReplaceMode => _ctrlRamReplaceSession.CurrentSnapshot,
            GeneralReplaceMode => _generalReplaceSession.CurrentSnapshot,
            _ => null,
        } ?? throw new InvalidOperationException(
            "Build output confirmation requires one accepted Replace session.");
        CompositionOutputBundleProposal proposal =
            _compositionServices.OutputNaming.ResolveAcceptedBundleProposal(
                session,
                exactSession is null ? ctrlRamFirmwareVersionEdit : null);
        CloseSelectionForRun();
        _stateBindings.OutputDelivery.Open(new OutputDeliveryRequest(
            proposal,
            IsReplaceOutput: true,
            AdditionalDelivery: null,
            () => IsAcceptedReplaceSessionCurrent(session),
            CtrlRamOptions: IsCtrlRamReplaceModeSelected ? this : null,
            PrepareModeSpecificAsync: IsCtrlRamReplaceModeSelected && exactSession is null
                ? PrepareCtrlRamBuildSettingsAsync
                : null,
            Cancel: IsCtrlRamReplaceModeSelected ? CloseCtrlRamFirmwareVersionModal : null,
            decision => RunReplaceAsync(
                build: true,
                decision.OutputPath,
                ctrlRamFirmwareVersionEdit,
                decision.OutputPathUsesAutomaticName,
                decision.BundleIntent,
                exactSession)),
            preserveDeliveryState: exactSession is not null);
        return Task.CompletedTask;
    }

    internal async Task<bool> RequestCtrlRamBuildSettingsAsync()
    {
        if (!await TryOpenCtrlRamFirmwareVersionModalAsync())
        {
            return false;
        }

        await RequestBuildOutputDeliveryAsync();
        return true;
    }

    private async Task<bool> PrepareCtrlRamBuildSettingsAsync()
    {
        (bool succeeded, CtrlRamFirmwareVersionDraftState? edit) =
            await TryCreateCtrlRamFirmwareVersionEditAsync();
        return succeeded && await RequestCtrlRamBuildOutputDeliveryAsync(edit);
    }

    private bool IsAcceptedReplaceSessionCurrent(ActiveSessionSnapshot acceptedSession)
    {
        ActiveSessionSnapshot? current = SelectedReplaceMode switch
        {
            DpReplaceMode => _dpReplaceSession.CurrentSnapshot,
            CtrlRamReplaceMode => _ctrlRamReplaceSession.CurrentSnapshot,
            GeneralReplaceMode => _generalReplaceSession.CurrentSnapshot,
            _ => null,
        };
        return ReferenceEquals(current, acceptedSession);
    }

    internal async Task<bool> RequestCtrlRamBuildOutputDeliveryAsync(
        CtrlRamFirmwareVersionDraftState? edit)
    {
        if (!IsCtrlRamReplaceModeSelected ||
            !await IsCtrlRamFirmwareVersionBuildConfirmationCurrentAsync())
        {
            return false;
        }

        CtrlRamAuthoringTransitionResult transition =
            _compositionServices.CtrlRamAuthoring.TransitionFirmwareVersionCompilation(
                _ctrlRamReplaceSession,
                SelectedIc,
                SelectedNumber,
                CreateReplaceSlotPaths(),
                edit);
        if (!transition.Succeeded || transition.Session is null)
        {
            return false;
        }

        await RefreshCtrlRamActionReadinessAsync(CancellationToken.None);
        if (!CanBuildReplace)
        {
            return false;
        }

        await RequestBuildOutputDeliveryAsync(edit, transition.Session);
        CloseCtrlRamFirmwareVersionModal();
        return true;
    }

    private Task PreviewReplaceAsync()
    {
        return RunReplaceAsync(
            build: false,
            outputPath: null,
            ctrlRamFirmwareVersionEdit: null,
            outputPathUsesAutomaticName: false);
    }

    private Task RunBuildReplaceAsync(
        string? outputPath,
        CtrlRamFirmwareVersionDraftState? ctrlRamFirmwareVersionEdit)
    {
        return RunReplaceAsync(
            build: true,
            outputPath,
            ctrlRamFirmwareVersionEdit,
            outputPathUsesAutomaticName: false);
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

            string label = Text.GetDpInputSelectionReadinessLabel(member);
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
        CtrlRamFirmwareVersionDraftState? ctrlRamFirmwareVersionEdit,
        bool outputPathUsesAutomaticName,
        CompositionOutputBundleIntent? outputBundle = null,
        ActiveSessionSnapshot? exactPreparedSession = null)
    {
        CloseSelectionForRun();
        string icId = SelectedIc;
        string number = SelectedNumber;
        string replaceMode = SelectedReplaceMode;
        IReadOnlyDictionary<string, string> slotPaths = CreateReplaceSlotPaths();
        CtrlRamAuthoringTransitionResult? ctrlRamTransition =
            replaceMode == CtrlRamReplaceMode && exactPreparedSession is null
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
            CtrlRamReplaceMode => exactPreparedSession ?? ctrlRamTransition?.Session,
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
                             outputPathUsesAutomaticName: outputPathUsesAutomaticName,
                             actionReadiness: actionReadiness,
                             outputBundle: outputBundle),
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
