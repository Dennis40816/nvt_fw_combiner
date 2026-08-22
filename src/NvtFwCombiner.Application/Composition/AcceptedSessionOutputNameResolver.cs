using NvtFwCombiner.Application.Authoring;
using NvtFwCombiner.Application.Capabilities;
using NvtFwCombiner.Application.InputInspection;
using NvtFwCombiner.Domain.Composition;
using NvtFwCombiner.Domain.Firmware;

namespace NvtFwCombiner.Application.Composition;

/// <summary>Resolves compiled output naming from one exact immutable authoring publication.</summary>
public static class AcceptedSessionOutputNameResolver
{
    /// <summary>
    /// Resolves the profile-owned name and optional deliveries without reopening
    /// paths, decoding metadata again, executing composition, or running processors.
    /// </summary>
    public static CompositionOutputPreparation Resolve(
        ActiveSessionSnapshot session,
        ResolvedCapability currentCapability,
        DateTimeOffset resolvedAtUtc,
        TopologySelection? abMergeTopologySelection = null,
        CtrlRamFirmwareVersionDraftState? ctrlRamVersionEdit = null)
    {
        ArgumentNullException.ThrowIfNull(session);
        ArgumentNullException.ThrowIfNull(currentCapability);
        CapabilityPublicationCoherence.ValidateResolved(currentCapability);
        CompiledComposition composition = currentCapability.CompiledComposition;
        CompiledOutputNameRendererKind renderer = composition.V2Details
            .OutputNamingRequirement.RendererKind;
        ResolvedCapability acceptedCapability = renderer == CompiledOutputNameRendererKind.Static
            ? session.ExactCapability ?? throw new InvalidOperationException(
                "Static output naming requires one exact accepted capability.")
            : session.GetAcceptedCapability(
                AuthoringDerivedResultKind.Inspection) ?? throw new InvalidOperationException(
                "Output naming requires one current complete input inspection.");
        if (!ReferenceEquals(acceptedCapability, currentCapability) ||
            session.ResolutionToken != currentCapability.ResolutionToken ||
            !StringComparer.Ordinal.Equals(
                session.CompilationFingerprint,
                currentCapability.CompiledComposition.CompilationFingerprint))
        {
            throw new InvalidOperationException(
                "Output naming requires the current exact accepted capability publication.");
        }

        if (ctrlRamVersionEdit is not null &&
            (!StringComparer.Ordinal.Equals(session.WorkflowId, ExperienceIds.CtrlRamReplace) ||
             session.DraftState is not CtrlRamFirmwareVersionDraftState acceptedEdit ||
             !acceptedEdit.HasSameValue(ctrlRamVersionEdit)))
        {
            throw new InvalidOperationException(
                "CtrlRAM output naming requires the exact compiled firmware-version draft.");
        }

        if (renderer == CompiledOutputNameRendererKind.Static)
        {
            var staticPreview = new CompositionOutputNamePreview(
                composition.V2Details.OutputNamingRequirement.FileNameTemplate,
                outputNaming: null,
                issues: []);
            return new CompositionOutputPreparation(
                staticPreview,
                [
                    .. composition.V2Details.AdditionalDeliveries.Select(delivery =>
                        CompositionAdditionalDeliveryPlanner.TryCreate(
                            composition,
                            outputNaming: null,
                            delivery.Kind) ?? throw new InvalidOperationException(
                            $"Compiled additional delivery '{delivery.Kind}' could not be prepared.")),
                ]);
        }

        var statuses = session.InputSlotStatuses
            .ToDictionary(static status => status.AddressSpaceId, StringComparer.Ordinal);
        var inputBytes = new Dictionary<string, byte[]>(StringComparer.Ordinal);
        var summaries = new List<InputArtifactSummary>(statuses.Count);
        var bindings = new List<InputArtifactBinding>(statuses.Count);
        foreach (string addressSpaceId in composition.Plan.RequiredInputAddressSpaceIds
                     .Order(StringComparer.Ordinal))
        {
            AuthoringInputSlotStatus status = statuses.TryGetValue(
                    addressSpaceId,
                    out AuthoringInputSlotStatus? candidate) &&
                candidate.IsTerminal &&
                candidate.AcceptedByteArray is not null &&
                candidate.FileStamp is { } stamp
                    ? candidate
                    : throw new InvalidOperationException(
                        $"Output naming input '{addressSpaceId}' has no current immutable accepted bytes.");
            byte[] bytes = [.. status.AcceptedByteArray!];
            inputBytes.Add(addressSpaceId, bytes);
            CompiledInputSpaceBinding spaceBinding = composition.V2Details.InputContract
                .SpaceBindings.Single(binding => StringComparer.Ordinal.Equals(
                    binding.AddressSpaceId,
                    addressSpaceId));
            CompiledInputSlotRequirement slot = composition.V2Details.InputContract.Slots
                .Single(candidateSlot => StringComparer.Ordinal.Equals(
                    candidateSlot.SlotId,
                    spaceBinding.SlotId));
            string artifactId = status.SelectedPathHint ?? status.SlotId;
            bindings.Add(new InputArtifactBinding(
                addressSpaceId,
                status.SlotId,
                artifactId,
                status.SelectedPathHint is null
                    ? null
                    : Path.GetFileName(status.SelectedPathHint),
                slot.ArtifactClass,
                stamp));
            summaries.Add(new InputArtifactSummary(
                addressSpaceId,
                status.SlotId,
                bytes.LongLength,
                stamp.Sha256,
                status.SelectedPathHint is null
                    ? null
                    : Path.GetFileName(status.SelectedPathHint),
                CreateExecutionSnapshot(status.Inspection)));
        }

        AcceptedOutputNamingPublication? naming =
            AcceptedOutputNamingInspection.TryAcceptForCompiledRenderer(session);
        var request = new CompositionRunRequest(
            $"accepted-output-name-{session.AuthoringRevision.Value}",
            composition,
            bindings,
            composition.V2Details.OutputNamingRequirement.FileNameTemplate,
            icNumberSelection: composition.V2Details.CompositionKind == CompositionKind.Replace
                ? ResolveAcceptedIcNumberSelection(currentCapability)
                : null,
            abMergeTopologySelection: abMergeTopologySelection,
            outputNamingInspection: naming?.Inspection,
            outputNamingAdmission: naming?.Admission,
            resolvedCapability: currentCapability);
        OutputNameResolution resolution = CompiledOutputNameResolver.Resolve(
            request,
            inputBytes,
            summaries,
            resolvedAtUtc,
            ctrlRamVersionEdit);
        var preview = new CompositionOutputNamePreview(
            resolution.FileName,
            resolution.Summary,
            resolution.Issues);
        return !preview.CanUseAutomaticName
            ? throw new InvalidOperationException(string.Join(
                Environment.NewLine,
                preview.Issues.Select(static issue => $"{issue.Code}: {issue.Message}")))
            : new CompositionOutputPreparation(
                preview,
                [
                    .. composition.V2Details.AdditionalDeliveries.Select(delivery =>
                        CompositionAdditionalDeliveryPlanner.TryCreate(
                            composition,
                            preview.OutputNaming,
                            delivery.Kind) ?? throw new InvalidOperationException(
                            $"Compiled additional delivery '{delivery.Kind}' could not be prepared.")),
                ]);
    }

    private static IcNumberSelection ResolveAcceptedIcNumberSelection(
        ResolvedCapability capability)
    {
        return capability.DpExecutionPlan?.IcNumberSelection ??
            capability.CtrlRamExecutionPlan?.IcNumberSelection ??
            capability.GeneralExecutionPlan?.IcNumberSelection ??
            throw new InvalidOperationException(
                "Replace output naming requires the exact accepted IC-number selection.");
    }

    private static InputArtifactExecutionSnapshotSummary? CreateExecutionSnapshot(
        CompiledInputArtifactInspectionResult? inspection)
    {
        return inspection?.AcceptedSnapshotRange is { } acceptedRange &&
               inspection.AcceptedSnapshotSha256 is { } acceptedSha256
            ? new InputArtifactExecutionSnapshotSummary(
                acceptedRange,
                acceptedSha256,
                inspection.IgnoredTrailingRange)
            : null;
    }
}
