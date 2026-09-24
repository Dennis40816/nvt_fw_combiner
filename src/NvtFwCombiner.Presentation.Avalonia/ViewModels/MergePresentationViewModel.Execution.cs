using NvtFwCombiner.Application.Authoring;
using NvtFwCombiner.Application.Capabilities;
using NvtFwCombiner.Domain.Composition;

namespace NvtFwCombiner.Presentation.Avalonia.ViewModels;

internal sealed partial class MergePresentationViewModel
{
    public Task BuildMergeAsync(
        string outputPath,
        string? aFlashCodeOutputPath = null,
        bool outputPathUsesAutomaticName = false,
        bool aFlashCodeOutputPathUsesAutomaticName = false)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(outputPath);
        return RunMergeAsync(
            build: true,
            outputPath,
            aFlashCodeOutputPath,
            outputPathUsesAutomaticName,
            aFlashCodeOutputPathUsesAutomaticName);
    }

    internal async Task RequestBuildOutputDeliveryAsync()
    {
        CompositionRunContext context = CaptureRunContext(SelectedMergeMode, build: true);
        ActiveSessionSnapshot session = context.AcceptedSession ?? throw new InvalidOperationException(
            "Build output confirmation requires one accepted Merge session.");
        long preparation = _stateBindings.OutputDelivery.BeginPreparation();
        CompositionOutputBundleProposal proposal;
        try
        {
            proposal = await _compositionServices.OutputNaming.PrepareBundleProposalAsync(session, CancellationToken.None);
        }
        catch (CompositionPreRunRefusalException exception)
        {
            if (IsAcceptedMergeSessionCurrent(context) && _stateBindings.OutputDelivery.IsPreparationCurrent(preparation))
            {
                _stateBindings.PublishRunResult(context.Owner, new UiRunResultViewModel("Build blocked", exception.Message, "No output", succeeded: false));
                await RefreshAbMergeActionReadinessAsync(CancellationToken.None);
            }
            return;
        }
        if (!IsAcceptedMergeSessionCurrent(context) || !_stateBindings.OutputDelivery.IsPreparationCurrent(preparation))
        {
            return;
        }
        CompositionAdditionalDeliveryPlan? additional = proposal.OutputPreparation.AdditionalDeliveries
            .SingleOrDefault(delivery => StringComparer.Ordinal.Equals(
                delivery.DeliveryKind,
                CompiledAdditionalDelivery.AbAFlashCodeKind));
        _stateBindings.OutputDelivery.Open(new OutputDeliveryRequest(
            proposal,
            IsReplaceOutput: false,
            AdditionalDelivery: additional,
            () => IsAcceptedMergeSessionCurrent(context),
            CtrlRamOptions: null,
            PrepareModeSpecificAsync: null,
            Cancel: null,
            decision => RunMergeAsync(
                build: true,
                decision.OutputPath,
                decision.AdditionalOutputPath,
                decision.OutputPathUsesAutomaticName,
                decision.AdditionalOutputPathUsesAutomaticName,
                decision.BundleIntent)));
    }

    private bool IsAcceptedMergeSessionCurrent(CompositionRunContext context)
    {
        return StringComparer.Ordinal.Equals(SelectedMergeMode, context.Mode) && context.IsPublicationCurrent &&
            ReferenceEquals(context.AuthoringSession?.CurrentSnapshot, context.AcceptedSession);
    }

    private Task RunMergeAsync(
        bool build,
        string? outputPath,
        string? aFlashCodeOutputPath = null,
        bool outputPathUsesAutomaticName = false,
        bool aFlashCodeOutputPathUsesAutomaticName = false,
        CompositionOutputBundleIntent? outputBundle = null)
    {
        return SelectedMergeMode switch
        {
            NormalMergeMode => RunStandardMergeAsync(
                build,
                outputPath,
                outputPathUsesAutomaticName,
                outputBundle),
            AbCodeMergeMode => RunAbMergeAsync(
                build,
                outputPath,
                aFlashCodeOutputPath,
                outputPathUsesAutomaticName,
                aFlashCodeOutputPathUsesAutomaticName,
                outputBundle),
            GeneralMergeMode => RunGeneralMergeAsync(
                build,
                outputPath,
                outputPathUsesAutomaticName,
                outputBundle),
            _ => Task.CompletedTask,
        };
    }

    private Task RunStandardMergeAsync(
        bool build,
        string? outputPath,
        bool outputPathUsesAutomaticName,
        CompositionOutputBundleIntent? outputBundle = null)
    {
        CompositionRunContext context = CaptureRunContext(NormalMergeMode, build);
        string icId = context.Ic;
        string number = context.Number;
        ActiveSessionSnapshot? acceptedSession = context.AcceptedSession;
        IReadOnlyDictionary<string, string> slotPaths = CreateStandardMergeSlotPaths();
        string profileId =
            _compositionServices.StandardMergeAuthoring.GetProfileId(icId) ??
            ExperienceIds.StandardMerge;
        return RunCompositionAsync(
            context,
            build,
            (progress, cancellationToken) => _compositionServices.Execution.ExecuteAsync(
                new AcceptedCompositionExecutionRequest(
                    acceptedSession ?? throw new InvalidOperationException(
                        "Standard Merge requires one accepted authoring session."),
                    slotPaths,
                    build,
                    outputPath: outputPath,
                    outputPathUsesAutomaticName: outputPathUsesAutomaticName,
                    outputBundle: outputBundle),
                progress,
                cancellationToken),
            (action, errorMessage) => Reports.LoadRunErrorReport(
                action,
                profileId,
                icId,
                number,
                errorMessage,
                slotPaths));
    }

    private Task RunGeneralMergeAsync(
        bool build,
        string? outputPath,
        bool outputPathUsesAutomaticName,
        CompositionOutputBundleIntent? outputBundle = null)
    {
        CompositionRunContext context = CaptureRunContext(GeneralMergeMode, build);
        string icId = context.Ic;
        string number = context.Number;
        ActiveSessionSnapshot acceptedSession =
            context.AcceptedSession ??
            throw new InvalidOperationException("General Merge requires one active authoring session.");
        GeneralMergeDraftState draft = acceptedSession.DraftState as GeneralMergeDraftState ??
            throw new InvalidOperationException("General Merge requires one admitted typed draft.");
        IReadOnlyDictionary<string, string> slotPaths = CreateGeneralMergeSlotPaths();
        string outputFileName = GeneralMergeAuthoringUseCase.GetDefaultOutputFileName(icId);
        return RunCompositionAsync(
            context,
            build,
            async (progress, cancellationToken) =>
            {
                CompositionRunResult result = await _compositionServices.Execution.ExecuteAsync(
                        new AcceptedCompositionExecutionRequest(
                            acceptedSession,
                            slotPaths,
                            build,
                            outputPath: outputPath,
                            outputPathUsesAutomaticName: outputPathUsesAutomaticName,
                            outputBundle: outputBundle),
                        progress,
                        cancellationToken)
                    .ConfigureAwait(false);

                if (result.AcceptedGeneralMappingDraft is { } accepted &&
                    ReferenceEquals(draft, _generalMergeDraft) &&
                    context.IsPublicationCurrent)
                {
                    _generalMergeDraft = new GeneralMergeDraftState(
                        draft.OutputInitializer,
                        accepted);
                }
                return result;
            },
            (action, errorMessage) => Reports.LoadRunErrorReport(
                action,
                outputFileName,
                icId,
                number,
                errorMessage,
                slotPaths,
                compositionKind: "Merge",
                modeId: ExperienceIds.GeneralMerge,
                experienceId: ExperienceIds.GeneralMerge));
    }

    private async Task RunAbMergeAsync(
        bool build,
        string? outputPath,
        string? aFlashCodeOutputPath,
        bool outputPathUsesAutomaticName,
        bool aFlashCodeOutputPathUsesAutomaticName,
        CompositionOutputBundleIntent? outputBundle = null)
    {
        CompositionRunContext context = CaptureRunContext(AbCodeMergeMode, build);
        ActiveSessionSnapshot session = context.AcceptedSession ??
            throw new InvalidOperationException("AB Merge requires one accepted authoring session.");
        string icId = context.Ic;
        string number = context.Number;
        IReadOnlyDictionary<string, string> slotPaths = CreateAbMergeSlotPaths();
        string profileId = session.ExactCapability?.CompiledComposition.V2Details.ProfileId ??
            throw new InvalidOperationException("AB Merge requires one accepted exact profile.");
        CapabilityActionReadinessSnapshot? readiness = await _compositionServices.AbMergeAuthoring
            .GetActionReadinessAsync(session, CancellationToken.None);
        if (context.IsPublicationCurrent && ReferenceEquals(session, _abMergeSession.CurrentSnapshot))
        {
            _abMergeActionReadiness = readiness;
            _abMergeReadinessSession = session;
            RefreshCommandState();
        }
        if (readiness is null || !(build ? readiness.Build : readiness.Preview).IsAvailable)
        {
            CapabilityActionBlocker? blocker = (build ? readiness?.Build : readiness?.Preview)?.PrimaryBlocker;
            _stateBindings.PublishRunResult(context.Owner, new UiRunResultViewModel(
                build ? "Build blocked" : "Preview blocked",
                blocker?.Message ?? "The accepted AB Merge action is unavailable.", "No output", succeeded: false));
            return;
        }
        var request = new AcceptedCompositionExecutionRequest(
            session,
            slotPaths,
            build,
            outputPath: outputPath,
            additionalDeliveryOutputPath: aFlashCodeOutputPath,
            outputPathUsesAutomaticName: outputPathUsesAutomaticName,
            additionalDeliveryOutputPathUsesAutomaticName: aFlashCodeOutputPathUsesAutomaticName,
            actionReadiness: readiness,
            outputBundle: outputBundle);
        await RunCompositionAsync(
            context,
            build,
            (progress, cancellationToken) => _compositionServices.Execution.ExecuteAsync(
                request,
                progress,
                cancellationToken),
            (action, errorMessage) => Reports.LoadRunErrorReport(
                action,
                profileId,
                icId,
                number,
                errorMessage,
                slotPaths,
                compositionKind: "Merge",
                modeId: ExperienceIds.AbMerge,
                experienceId: ExperienceIds.AbMerge));
    }

    private Dictionary<string, string> CreateStandardMergeSlotPaths()
    {
        Dictionary<string, string> paths = new(StringComparer.Ordinal);
        FirmwareSlotPathProjection.Add(paths, CompositionAddressSpaceIds.DpInput, MergeDpSlot);
        FirmwareSlotPathProjection.Add(paths, CompositionAddressSpaceIds.TpInput, MergeTpSlot);
        FirmwareSlotPathProjection.Add(paths, CompositionAddressSpaceIds.LdcInput, MergeLdcSlot);
        return paths;
    }

    private Dictionary<string, string> CreateAbMergeSlotPaths()
    {
        return MergeSlots
            .Where(static slot => slot.HasFile)
            .ToDictionary(
                slot => AbMergeAddressSpaceBySlotId[slot.SlotId],
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

}
