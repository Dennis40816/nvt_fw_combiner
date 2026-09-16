using NvtFwCombiner.Application.Capabilities;
using NvtFwCombiner.Application.Composition;
using NvtFwCombiner.Application.Configuration;
using NvtFwCombiner.Application.Ports;
using NvtFwCombiner.Domain.Composition;
using NvtFwCombiner.Domain.Firmware;

namespace NvtFwCombiner.Application.Authoring;

internal sealed partial class AbMergeAuthoringExperience
{
    private sealed record AbInspectionContext(FirmwareInspectionSnapshotInput[] Selected,
        CompiledAuthoringSelectedInput[] Captured, AuthoringRevision Revision,
        TopologySelection? Topology, AbMergeDpMode DpMode);

    public async ValueTask<AbMergeInspectionBatch> InspectInputSlotsAsync(string icId,
        IReadOnlyList<FirmwareInspectionSnapshotInput> inputs, Func<string, byte[]?> readFirmwareImage,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        AbInspectionContext? context = CaptureAbInspection(icId, inputs, readFirmwareImage);
        if (context is null) { return AbMergeInspectionBatch.Empty; }
        FormatResolution resolution = await ResolveAbFormatAsync(icId, context.Topology, context.Captured,
            context.DpMode, cancellationToken).ConfigureAwait(false);
        cancellationToken.ThrowIfCancellationRequested();
        return ProjectAbInspection(icId, context, resolution);
    }

    // Internal deterministic seam: the caller must supply an explicit configuration capture.
    internal AbMergeInspectionBatch InspectInputSlotsCaptured(string icId,
        IReadOnlyList<FirmwareInspectionSnapshotInput> inputs, Func<string, byte[]?> readFirmwareImage,
        EventBufferFormatConfigurationState? configuration)
    {
        AbInspectionContext? context = CaptureAbInspection(icId, inputs, readFirmwareImage);
        if (context is null) { return AbMergeInspectionBatch.Empty; }
        (ResolvedCapabilityRoute route, CanonicalAbAuthoringDefinition definition) = GetAbDeclarations(icId, context.Topology);
        return ProjectAbInspection(icId, context, ResolveCapturedFormat(route, definition, context.Topology,
            context.Captured, context.DpMode, configuration));
    }

    private AbInspectionContext? CaptureAbInspection(string icId,
        IReadOnlyList<FirmwareInspectionSnapshotInput> inputs, Func<string, byte[]?> readFirmwareImage)
    {
        ArgumentNullException.ThrowIfNull(inputs);
        ArgumentNullException.ThrowIfNull(readFirmwareImage);
        FirmwareInspectionSnapshotInput[] selected = [.. inputs.Where(static input => input.AbMergeAddressSpaceId is not null)];
        if (selected.Length == 0) { return null; }
        var revision = new AuthoringRevision(selected.Select(static input => input.AuthoringRevision).Distinct().Single());
        string? topologyToken = selected.Select(static input => input.AbMergeTopologyToken).Distinct(StringComparer.Ordinal).Single();
        TopologySelection? topology = _compiler.ResolveAbMergeTopologySelection(icId, topologyToken);
        AbMergeDpMode mode = selected.Select(static input => input.AbMergeDpMode).Distinct().Single();
        ResolvedCapability? exact = selected[0].ExactCapability;
        if (selected.Any(input => !ReferenceEquals(input.ExactCapability, exact)) ||
            (exact is not null && (exact.Identity.IcId != IcIdentifier.Normalize(icId) ||
                exact.Identity.WorkflowId != ExperienceIds.AbMerge ||
                !exact.CompiledComposition.V2Details.Provenance.ResolvedMap.ImageMap.Applicability.TopologyRequirement.Matches(topology) ||
                _catalog.ResolveCurrentCompilation(exact.CompiledComposition, exact) is null)))
        {
            throw new InvalidOperationException("AB inspection requires coherent current leases for the selected IC and topology.");
        }
        var images = new Dictionary<string, byte[]?>(StringComparer.Ordinal);
        foreach (string path in selected.Select(static input => input.Path).Distinct(StringComparer.Ordinal))
        {
            images.Add(path, readFirmwareImage(path));
        }
        return new(selected, CaptureAbInputs([.. selected.Select(input => new CompiledAuthoringSelectedInput(
            input.AbMergeAddressSpaceId!, input.Path, images[input.Path] is { } image ? new ReadOnlyMemory<byte>(image) : (ReadOnlyMemory<byte>?)null))]), revision, topology, mode);
    }

    private CompiledAuthoringInspectionBatch InspectResolvedAbInputs(string icId, AbInspectionContext context,
        FormatResolution resolution)
    {
        if (resolution.Capability is { } capability)
        {
            if (context.Selected[0].ExactCapability is { } retained &&
                CompiledAuthoringWorkflowService.IsEquivalentExactCapability(retained, capability))
            {
                capability = retained;
            }
            return new CompiledAuthoringWorkflowService(new AbMergeAuthoringResolver(
                context.Topology, _compiler, context.DpMode, capability))
                .InspectBatch(icId, context.Revision, context.Captured, capability);
        }
        CompiledAuthoringSelectedInput[] normalized = CompiledAuthoringWorkflowService.NormalizeSelectedInputs(
            context.Captured, resolution.Definition.InputBindings);
        CompositionIssue primary = resolution.Issues[0];
        CompiledAuthoringSelectionSnapshot declaration = DeclarationSelection(resolution.DiscoveryRoute,
            resolution.Definition, [.. normalized.Select(static input => input.SlotId)], context.DpMode, resolution.Issues);
        if (primary.Code == "AB_FORMAT_INPUT_INVALID")
        {
            // An invalid membership has no unambiguous slot/space to receive a status.
            return new(declaration.Catalog, new Dictionary<string, AuthoringInputSlotStatus>(StringComparer.Ordinal), resolution.Issues);
        }
        return new(declaration.Catalog, normalized.ToDictionary(static input => input.SlotId, input =>
        {
            FileStamp? stamp = input.Bytes is { } bytes ? FileStamp.FromBytes(bytes.Span) : null;
            SelectedFileContentInspection? capture = stamp is { } identity
                ? new SelectedFileContentInspection(identity, acceptedBytes: input.Bytes) : null;
            return AuthoringInputSlotInspectionService.BlockBeforeCompilation(resolution.DiscoveryRoute, context.Revision,
                input.SlotId, resolution.Definition.InputBindings.Single(binding => binding.SlotId == input.SlotId).AddressSpaceId,
                primary.Code, primary.Message, stamp, input.SelectedPathHint, capture);
        }, StringComparer.Ordinal), resolution.Issues);
    }

    private AbMergeInspectionBatch ProjectAbInspection(string icId, AbInspectionContext context, FormatResolution resolution)
    {
        CompiledAuthoringInspectionBatch batch = InspectResolvedAbInputs(icId, context, resolution);
        var statuses = new Dictionary<string, AuthoringInputSlotStatus>(StringComparer.Ordinal);
        var facts = new Dictionary<string, AbMergeInputFacts>(StringComparer.Ordinal);
        if (batch.Statuses.Count == 0) { return new(batch.Catalog, statuses, facts, batch.Issues); }
        foreach (FirmwareInspectionSnapshotInput input in context.Selected)
        {
            CompiledAuthoringInputBinding binding = resolution.Definition.InputBindings.Single(candidate =>
                candidate.SlotId == input.AbMergeAddressSpaceId || candidate.AddressSpaceId == input.AbMergeAddressSpaceId);
            AuthoringInputSlotStatus status = batch.Statuses[binding.SlotId];
            statuses.Add(input.InspectionId, status);
            facts.Add(input.InspectionId, ProjectAbInputFacts(status, resolution));
        }
        return new(batch.Catalog, statuses, facts, batch.Issues);
    }

    private static AbMergeInputFacts ProjectAbInputFacts(AuthoringInputSlotStatus status, FormatResolution resolution)
    {
        var facts = new AbMergeInputFacts(status.AddressSpaceId, status.Observation.Versions);
        return resolution.Capability is null || resolution.Issues.Count != 0 || resolution.Format is not { } format
            ? facts : facts with { EventBufferFormat = ProjectEventBufferFormat(status.AddressSpaceId, format) };
    }

    internal static EventBufferFormatObservation? ProjectEventBufferFormat(string addressSpaceId, AbMergeFormatSelection? format)
    {
        if (format is null) { return null; }
        bool isA = addressSpaceId == CompositionAddressSpaceIds.TpAInput;
        if (!isA && addressSpaceId != CompositionAddressSpaceIds.TpBInput) { return null; }
        FirmwareMetadataStructureResolution primary = isA ? format.TpAPrimary : format.TpBPrimary;
        FirmwareResolvedMetadataStructure resolved = primary.Resolved ??
            throw new InvalidOperationException("Admitted AB format requires resolved primary evidence.");
        return new(isA ? format.TpAFormatByte : format.TpBFormatByte,
                format.FormatId, format.DisplayName, format.ConfigurationGeneration,
                format.ConfigurationSourceSha256, primary.MetadataStructureId,
                resolved.LocatorOutcome.ResolvedRange, resolved.ArtifactIdentity);
    }
}
