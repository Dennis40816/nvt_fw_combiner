using NvtFwCombiner.Application.Capabilities;
using NvtFwCombiner.Application.Authoring;
using NvtFwCombiner.Domain.Composition;

namespace NvtFwCombiner.Presentation.Avalonia.ViewModels;

internal sealed partial class ReplacePresentationViewModel
{
    public string ReplaceMemorySummary => Text.GetReplaceMemorySummary(SelectedReplaceMode);

    public string ReplaceReadinessStatus => !HasSelectedIc
        ? Text.NotAvailableLabel
        : Inspection.IsRunning
        ? Text.FirmwareInspectionLoadingStatus
        : IsSelectedReplaceModeSupported
            ? Text.GetReplaceReadinessStatus(SelectedReplaceMode, CanRunReplace())
            : Text.GetReplaceNotSupportedStatus(SelectedIc);

    public Task BuildReplaceAsync(
        string outputPath,
        CtrlRamAuthoringDraftState? ctrlRamFirmwareVersionEdit = null)
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

    internal async Task RequestBuildOutputDeliveryAsync(
        CtrlRamAuthoringDraftState? ctrlRamFirmwareVersionEdit = null,
        ActiveSessionSnapshot? exactSession = null)
    {
        CompositionRunContext context = CaptureRunContext(SelectedReplaceMode, build: true);
        if (exactSession is not null && !ReferenceEquals(exactSession, context.AcceptedSession))
        {
            return;
        }
        ActiveSessionSnapshot session = exactSession ?? context.AcceptedSession ?? throw new InvalidOperationException(
            "Build output confirmation requires one accepted Replace session.");
        long preparation = _stateBindings.OutputDelivery.BeginPreparation();
        CompositionOutputBundleProposal proposal =
            await _compositionServices.OutputNaming.PrepareBundleProposalAsync(
                session,
                CancellationToken.None,
                exactSession is null ? ctrlRamFirmwareVersionEdit as CtrlRamFirmwareVersionDraftState : null);
        if (!IsAcceptedReplaceSessionCurrent(context) || !_stateBindings.OutputDelivery.IsPreparationCurrent(preparation))
        {
            return;
        }
        CloseSelectionForRun();
        _stateBindings.OutputDelivery.Open(new OutputDeliveryRequest(
            proposal,
            IsReplaceOutput: true,
            AdditionalDelivery: null,
            () => IsAcceptedReplaceSessionCurrent(context),
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
        (bool succeeded, CtrlRamAuthoringDraftState? edit) =
            await TryCreateCtrlRamFirmwareVersionEditAsync();
        return succeeded && await RequestCtrlRamBuildOutputDeliveryAsync(edit);
    }

    private bool IsAcceptedReplaceSessionCurrent(CompositionRunContext context)
    {
        return StringComparer.Ordinal.Equals(SelectedReplaceMode, context.Mode) && context.IsPublicationCurrent &&
            ReferenceEquals(context.AuthoringSession?.CurrentSnapshot, context.AcceptedSession);
    }

    internal async Task<bool> RequestCtrlRamBuildOutputDeliveryAsync(
        CtrlRamAuthoringDraftState? edit)
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

        CurrentCtrlRamDraft = transition.Session.DraftState as CtrlRamAuthoringDraftState;
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
        CtrlRamAuthoringDraftState? ctrlRamFirmwareVersionEdit)
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
            (SelectedReplaceMode != CtrlRamReplaceMode || !IsAbCtrlRamReference || AbCtrlRamReadiness.IsAvailable) &&
            (SelectedReplaceMode switch
            {
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

    private void ResetReplaceInputSelectionReadiness()
    {
        foreach (FirmwareSlotViewModel slot in ReplaceSlots.ToArray().Where(slot =>
                     !ReferenceEquals(slot, ReplaceBaseSlot)))
        {
            slot.IsOptional = slot.DeclaredIsOptional;
            slot.ClearSelectionReadiness();
        }
    }

    private async Task RunReplaceAsync(
        bool build,
        string? outputPath,
        CtrlRamAuthoringDraftState? ctrlRamFirmwareVersionEdit,
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
                    ctrlRamFirmwareVersionEdit ?? (IsAbCtrlRamReference ? CurrentCtrlRamDraft : null))
                : null;
        if (ctrlRamTransition?.Succeeded == true)
        {
            CurrentCtrlRamDraft = ctrlRamTransition.Session!.DraftState as CtrlRamAuthoringDraftState;
        }
        CompositionRunContext context = CaptureRunContext(replaceMode, build);
        ActiveSessionSnapshot? generalSession = replaceMode == GeneralReplaceMode
            ? context.AcceptedSession ??
                throw new InvalidOperationException(
                    "General Replace requires one accepted authoring session.")
            : null;
        var generalDraft =
            generalSession?.DraftState as GeneralMappingDraftState;
        ActiveSessionSnapshot? compiledSession = replaceMode switch
        {
            CtrlRamReplaceMode => exactPreparedSession ?? ctrlRamTransition?.Session,
            _ => null,
        };
        CapabilityActionReadinessSnapshot? actionReadiness = replaceMode switch
        {
            CtrlRamReplaceMode => _ctrlRamActionReadiness,
            GeneralReplaceMode => _generalReplaceActionReadiness,
            _ => null,
        };
        CompositionRunReport? diagnosticReport = _generalReplaceDiagnosticPreviewReport;
        IReadOnlyList<CompositionIssue> inputIssues = ctrlRamTransition?.Issues ?? [];
        if (ctrlRamTransition?.Succeeded == true && compiledSession is not null)
        {
            actionReadiness = await _compositionServices.CtrlRamAuthoring.GetActionReadinessAsync(
                icId, number, slotPaths, compiledSession, CancellationToken.None);
            if (context.IsPublicationCurrent && ReferenceEquals(compiledSession, _ctrlRamReplaceSession.CurrentSnapshot))
            {
                _ctrlRamActionReadiness = actionReadiness;
                _ctrlRamReadinessSession = compiledSession;
                _ctrlRamReadinessIc = icId;
                _ctrlRamReadinessNumber = number;
                NotifyCommandAvailabilityChanged();
            }
        }
        if (actionReadiness is { } readiness)
        {
            CapabilityActionAvailability action = build
                ? readiness.Build
                : readiness.Preview;
            if (!action.IsAvailable)
            {
                _stateBindings.ShowActionReadiness(context, readiness, build);
                return;
            }
            if (replaceMode == GeneralReplaceMode &&
                !build &&
                diagnosticReport is not null)
            {
                await _stateBindings.ShowDiagnosticPreviewAsync(context, diagnosticReport);
                return;
            }
        }
        await RunCompositionAsync(
            context,
            build,
            async (progress, cancellationToken) =>
            {
                if (replaceMode != GeneralReplaceMode &&
                    compiledSession?.GetAcceptedCapability(
                        AuthoringDerivedResultKind.Inspection) is null)
                {
                    IReadOnlyList<CompositionIssue> issues = inputIssues;
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
                        context.IsPublicationCurrent)
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
