using NvtFwCombiner.Application.Authoring;
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

    internal Task RequestBuildOutputDeliveryAsync()
    {
        ActiveSessionSnapshot session = SelectedMergeMode switch
        {
            NormalMergeMode => _standardMergeSession.CurrentSnapshot,
            AbCodeMergeMode => _abMergeSession.CurrentSnapshot,
            GeneralMergeMode => _generalMergeSession.CurrentSnapshot,
            _ => null,
        } ?? throw new InvalidOperationException(
            "Build output confirmation requires one accepted Merge session.");
        CompositionOutputBundleProposal proposal =
            _compositionServices.OutputNaming.ResolveAcceptedBundleProposal(session);
        CompositionAdditionalDeliveryPlan? additional = proposal.OutputPreparation.AdditionalDeliveries
            .SingleOrDefault(delivery => StringComparer.Ordinal.Equals(
                delivery.DeliveryKind,
                CompiledAdditionalDelivery.AbAFlashCodeKind));
        _stateBindings.OutputDelivery.Open(new OutputDeliveryRequest(
            proposal,
            IsReplaceOutput: false,
            AdditionalDelivery: additional,
            () => IsAcceptedMergeSessionCurrent(session),
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
        return Task.CompletedTask;
    }

    private bool IsAcceptedMergeSessionCurrent(ActiveSessionSnapshot acceptedSession)
    {
        ActiveSessionSnapshot? current = SelectedMergeMode switch
        {
            NormalMergeMode => _standardMergeSession.CurrentSnapshot,
            AbCodeMergeMode => _abMergeSession.CurrentSnapshot,
            GeneralMergeMode => _generalMergeSession.CurrentSnapshot,
            _ => null,
        };
        return ReferenceEquals(current, acceptedSession);
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
        string icId = SelectedIc;
        string number = SelectedNumber;
        IReadOnlyDictionary<string, string> slotPaths = CreateStandardMergeSlotPaths();
        string profileId =
            _compositionServices.StandardMergeAuthoring.GetProfileId(icId) ??
            ExperienceIds.StandardMerge;
        return RunCompositionAsync(
            build,
            (progress, cancellationToken) => _compositionServices.Execution.ExecuteAsync(
                new AcceptedCompositionExecutionRequest(
                    _standardMergeSession.CurrentSnapshot ?? throw new InvalidOperationException(
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
        string icId = SelectedIc;
        string number = SelectedNumber;
        ActiveSessionSnapshot acceptedSession =
            _generalMergeSession.CurrentSnapshot ??
            throw new InvalidOperationException("General Merge requires one active authoring session.");
        GeneralMergeDraftState draft = acceptedSession.DraftState as GeneralMergeDraftState ??
            throw new InvalidOperationException("General Merge requires one admitted typed draft.");
        IReadOnlyDictionary<string, string> slotPaths = CreateGeneralMergeSlotPaths();
        string outputFileName = GeneralMergeAuthoringUseCase.GetDefaultOutputFileName(icId);
        return RunCompositionAsync(
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
                    _generalMergeSession.CurrentSnapshot?.AuthoringRevision ==
                        acceptedSession.AuthoringRevision)
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
        await RefreshAbMergeActionReadinessAsync(CancellationToken.None);
        if (!HasCurrentAbMergeActionReadiness(build))
        {
            if (_abMergeActionReadiness is { } readiness)
            {
                _stateBindings.ShowActionReadiness(readiness, build);
            }
            return;
        }

        await RunAbMergeWithCurrentReadinessAsync(
            build,
            outputPath,
            aFlashCodeOutputPath,
            outputPathUsesAutomaticName,
            aFlashCodeOutputPathUsesAutomaticName,
            outputBundle);
    }

    private Task RunAbMergeWithCurrentReadinessAsync(
        bool build,
        string? outputPath,
        string? aFlashCodeOutputPath,
        bool outputPathUsesAutomaticName,
        bool aFlashCodeOutputPathUsesAutomaticName,
        CompositionOutputBundleIntent? outputBundle)
    {
        string icId = SelectedIc;
        IReadOnlyDictionary<string, string> slotPaths = CreateAbMergeSlotPaths();
        string profileId = _compositionServices.Capabilities.GetAbMergeProfileSummaries()
            .Single(profile => StringComparer.Ordinal.Equals(profile.IcId, icId))
            .ProfileId;
        return RunCompositionAsync(
            build,
            (progress, cancellationToken) => _compositionServices.Execution.ExecuteAsync(
                new AcceptedCompositionExecutionRequest(
                    _abMergeSession.CurrentSnapshot ?? throw new InvalidOperationException(
                        "AB Merge requires one accepted authoring session."),
                    slotPaths,
                    build,
                    outputPath: outputPath,
                    additionalDeliveryOutputPath: aFlashCodeOutputPath,
                    outputPathUsesAutomaticName: outputPathUsesAutomaticName,
                    additionalDeliveryOutputPathUsesAutomaticName:
                        aFlashCodeOutputPathUsesAutomaticName,
                    actionReadiness: _abMergeActionReadiness,
                    outputBundle: outputBundle),
                progress,
                cancellationToken),
            (action, errorMessage) => Reports.LoadRunErrorReport(
                action,
                profileId,
                icId,
                SelectedNumber,
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
