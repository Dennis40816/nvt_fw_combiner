using System.Collections.ObjectModel;
using NvtFwCombiner.Application.Capabilities;
using NvtFwCombiner.Domain.Composition;

namespace NvtFwCombiner.Application.Authoring;

public sealed partial class CompiledAuthoringWorkflowService
{
    /// <summary>Projects current picker readiness from accepted content identities only.</summary>
    public CompiledAuthoringSelectionSnapshot ProjectSelection(
        string icId,
        AuthoringRevision authoringRevision,
        IReadOnlyCollection<string> selectedSlotIds,
        IReadOnlyDictionary<string, FileStamp> acceptedFileStamps,
        ActiveSessionSnapshot? retainedSession = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(icId);
        ArgumentNullException.ThrowIfNull(selectedSlotIds);
        ArgumentNullException.ThrowIfNull(acceptedFileStamps);
        CompiledAuthoringWorkflowDiscovery discovery = _resolver.Discover(icId);
        ValidateDiscovery(discovery);
        ReadOnlyCollection<CompiledAuthoringInputBinding> inputBindings =
            ProjectInputBindings(discovery);
        selectedSlotIds = NormalizeSlotIds(selectedSlotIds, inputBindings);
        acceptedFileStamps = NormalizeFileStamps(acceptedFileStamps, inputBindings);
        FileStamp? prerequisiteStamp = null;
        if (discovery.CompilationPrerequisiteSlotId is { } prerequisite)
        {
            if (!acceptedFileStamps.TryGetValue(prerequisite, out FileStamp acceptedPrerequisite))
            {
                return new CompiledAuthoringSelectionSnapshot(
                    DiscoveryCatalog(discovery),
                    ProjectPendingPrerequisite(discovery, selectedSlotIds, prerequisite),
                    inputBindings,
                    []);
            }

            prerequisiteStamp = acceptedPrerequisite;
        }

        ResolvedCapability? retained = TryRetainExactCapability(
            retainedSession,
            icId,
            selectedSlotIds,
            acceptedFileStamps,
            discovery.CompilationPrerequisiteSlotId is null
                ? discovery.DiscoveryCapability
                : null);
        long? prerequisiteLength = prerequisiteStamp?.AcceptedLength;
        CompiledAuthoringWorkflowResolution exact = retained is null
            ? _resolver.ResolveExact(icId, authoringRevision, prerequisiteLength, selectedSlotIds)
            : new CompiledAuthoringWorkflowResolution(retained, []);
        if (!exact.Succeeded)
        {
            ResolvedCapability? nearestCapability = selectedSlotIds
                .Where(slotId => !StringComparer.Ordinal.Equals(
                    slotId, discovery.CompilationPrerequisiteSlotId))
                .Select(removed => _resolver.ResolveExact(
                    icId,
                    authoringRevision,
                    prerequisiteLength,
                    [.. selectedSlotIds.Where(slotId => !StringComparer.Ordinal.Equals(slotId, removed))]))
                .FirstOrDefault(static candidate => candidate.Succeeded)?.Capability;
            return new CompiledAuthoringSelectionSnapshot(
                DiscoveryCatalog(discovery),
                nearestCapability is null
                    ? ProjectRejectedSelection(
                        discovery,
                        authoringRevision,
                        selectedSlotIds,
                        prerequisiteLength,
                        exact.Issues,
                        exact.SelectionReadiness)
                    : ProjectExactSelection(
                        discovery, nearestCapability, authoringRevision,
                        selectedSlotIds, prerequisiteLength),
                inputBindings,
                exact.Issues);
        }

        ResolvedCapability capability = RetainEquivalentExactCapability(
            retainedSession?.ExactCapability,
            exact.Capability!);
        return new CompiledAuthoringSelectionSnapshot(
            AuthoringCapabilityCatalogSnapshot.FromResolvedCapability(
                capability,
                discovery.DiscoveryTransition),
            ProjectExactSelection(
                discovery,
                capability,
                authoringRevision,
                selectedSlotIds,
                prerequisiteLength),
            inputBindings,
            []);
    }

    private static ReadOnlyCollection<CompiledAuthoringInputBinding> ProjectInputBindings(
        CompiledAuthoringWorkflowDiscovery discovery)
    {
        if (discovery.AvailableInputBindings is { } available)
        {
            return Array.AsReadOnly([.. available]);
        }

        CompiledComposition composition = discovery.DiscoveryCapability.CompiledComposition;
        return Array.AsReadOnly(
        [
            .. composition.V2Details.InputContract.SpaceBindings.Select(binding =>
            {
                CompiledInputSlotRequirement slot = composition.V2Details.InputContract.Slots
                    .Single(candidate => StringComparer.Ordinal.Equals(
                        candidate.SlotId,
                        binding.SlotId));
                (long requiredEndExclusive, IReadOnlyList<long> expectedOuterLengths) =
                    slot.LengthRequirement switch
                    {
                        CompiledExactBytesInputLengthRequirement exact =>
                            (exact.Bytes, [exact.Bytes]),
                        CompiledExactResolvedMapCapacityInputLengthRequirement exact =>
                            (exact.Bytes, [exact.Bytes]),
                        CompiledSourceViewCoverageInputLengthRequirement sourceView =>
                            (sourceView.RequiredEndExclusive ?? composition.Plan.AddressSpaces
                                .Single(space => StringComparer.Ordinal.Equals(
                                    space.AddressSpaceId,
                                    binding.AddressSpaceId)).Length,
                                sourceView.ExpectedOuterLengths),
                        _ => throw new InvalidOperationException(
                            $"Compiled input '{binding.AddressSpaceId}' has no authoring length contract."),
                    };
                return new CompiledAuthoringInputBinding(
                    binding.SlotId,
                    binding.AddressSpaceId,
                    slot.Role,
                    requiredEndExclusive,
                    expectedOuterLengths);
            }),
        ]);
    }

    private static string[] NormalizeSlotIds(
        IEnumerable<string> slotOrAddressSpaceIds,
        IReadOnlyCollection<CompiledAuthoringInputBinding> inputBindings)
    {
        Dictionary<string, string> aliases = CreateInputAliases(inputBindings);
        return
        [
            .. slotOrAddressSpaceIds
                .Select(id => aliases.GetValueOrDefault(id, id))
                .Distinct(StringComparer.Ordinal),
        ];
    }

    private static Dictionary<string, FileStamp> NormalizeFileStamps(
        IReadOnlyDictionary<string, FileStamp> fileStamps,
        IReadOnlyCollection<CompiledAuthoringInputBinding> inputBindings)
    {
        Dictionary<string, string> aliases = CreateInputAliases(inputBindings);
        return fileStamps.ToDictionary(
            pair => aliases.GetValueOrDefault(pair.Key, pair.Key),
            static pair => pair.Value,
            StringComparer.Ordinal);
    }

    private static CompiledAuthoringSelectedInput[] NormalizeSelectedInputs(
        IEnumerable<CompiledAuthoringSelectedInput> inputs,
        IReadOnlyCollection<CompiledAuthoringInputBinding> inputBindings)
    {
        Dictionary<string, string> aliases = CreateInputAliases(inputBindings);
        return
        [
            .. inputs.Select(input => input with
            {
                SlotId = aliases.GetValueOrDefault(input.SlotId, input.SlotId),
            }),
        ];
    }

    private static Dictionary<string, string> CreateInputAliases(
        IReadOnlyCollection<CompiledAuthoringInputBinding> inputBindings)
    {
        return inputBindings
            .SelectMany(static binding => new KeyValuePair<string, string>[]
            {
                new(binding.SlotId, binding.SlotId),
                new(binding.AddressSpaceId, binding.SlotId),
            })
            .GroupBy(static alias => alias.Key, StringComparer.Ordinal)
            .ToDictionary(
                static group => group.Key,
                static group => group.Select(static alias => alias.Value)
                    .Distinct(StringComparer.Ordinal)
                    .Single(),
                StringComparer.Ordinal);
    }
}
