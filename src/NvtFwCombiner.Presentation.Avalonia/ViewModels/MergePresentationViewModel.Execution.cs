using NvtFwCombiner.Application.Authoring;
using NvtFwCombiner.Domain.Composition;

namespace NvtFwCombiner.Presentation.Avalonia.ViewModels;

internal sealed record MergeBuildSavePreparation(
    string SuggestedFileName,
    CompositionAdditionalDeliveryPlan? AFlashCodePlan);

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

    private Task RunMergeAsync(
        bool build,
        string? outputPath,
        string? aFlashCodeOutputPath = null,
        bool outputPathUsesAutomaticName = false,
        bool aFlashCodeOutputPathUsesAutomaticName = false)
    {
        return SelectedMergeMode switch
        {
            NormalMergeMode => RunStandardMergeAsync(build, outputPath),
            AbCodeMergeMode => RunAbMergeAsync(
                build,
                outputPath,
                aFlashCodeOutputPath,
                outputPathUsesAutomaticName,
                aFlashCodeOutputPathUsesAutomaticName),
            GeneralMergeMode => RunGeneralMergeAsync(build, outputPath),
            _ => Task.CompletedTask,
        };
    }

    internal async ValueTask<MergeBuildSavePreparation?> TryPrepareMergeBuildSaveAsync(
        CancellationToken cancellationToken)
    {
        if (!IsAbCodeMergeModeSelected)
        {
            return new MergeBuildSavePreparation(MergeOutputFileName, AFlashCodePlan: null);
        }

        try
        {
            CompositionOutputPreparation preparation = await _compositionServices.OutputNaming.PrepareAutomaticOutputAsync(
                    _abMergeSession.CurrentSnapshot ?? throw new InvalidOperationException(
                        "AB Merge Build preparation requires one accepted authoring session."),
                    cancellationToken)
                .ConfigureAwait(false);
            CompositionAdditionalDeliveryPlan? aFlashCodePlan = preparation.AdditionalDeliveries
                .SingleOrDefault(delivery => StringComparer.Ordinal.Equals(
                    delivery.DeliveryKind,
                    CompiledAdditionalDelivery.AbAFlashCodeKind));
            return new MergeBuildSavePreparation(preparation.OutputName.FileName, aFlashCodePlan);
        }
        catch (Exception exception) when (
            exception is InvalidOperationException or IOException or UnauthorizedAccessException or ArgumentException)
        {
            PublishAbMergeBuildSavePreparationFailure(exception.Message);
            return null;
        }
    }

    private Task RunStandardMergeAsync(bool build, string? outputPath)
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
                    outputPath: outputPath),
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

    private Task RunGeneralMergeAsync(bool build, string? outputPath)
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
                            outputPath: outputPath),
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

    private Task RunAbMergeAsync(
        bool build,
        string? outputPath,
        string? aFlashCodeOutputPath,
        bool outputPathUsesAutomaticName,
        bool aFlashCodeOutputPathUsesAutomaticName)
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
                        aFlashCodeOutputPathUsesAutomaticName),
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

    private void PublishAbMergeBuildSavePreparationFailure(string message)
    {
        string icId = SelectedIc;
        string number = SelectedNumber;
        IReadOnlyDictionary<string, string> slotPaths = CreateAbMergeSlotPaths();
        string profileId = _compositionServices.Capabilities.GetAbMergeProfileSummaries()
            .Single(profile => StringComparer.Ordinal.Equals(profile.IcId, icId))
            .ProfileId;
        Reports.LoadRunErrorReport(
            "Build",
            profileId,
            icId,
            number,
            message,
            slotPaths,
            compositionKind: "Merge",
            modeId: ExperienceIds.AbMerge,
            experienceId: ExperienceIds.AbMerge);
        _stateBindings.PublishRunResult(
            new UiRunResultViewModel("Build failed", message, "No output", succeeded: false));
        Reports.ShowReport();
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
