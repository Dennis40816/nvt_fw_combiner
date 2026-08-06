using NvtFwCombiner.Application.Authoring;
using NvtFwCombiner.Application.Capabilities;
using NvtFwCombiner.Domain.Composition;
using NvtFwCombiner.Domain.Firmware;

namespace NvtFwCombiner.Bootstrap;

internal static class WorkbenchAbMergeInputProjection
{
    private const string AbDpRole = "dp-ab";
    private const string AbTpARole = "tp-a";
    private const string AbTpBRole = "tp-b";
    internal static IReadOnlyList<WorkbenchAbMergeInputSlot> GetInputSlots(
        string icId,
        TopologySelection? requestedTopology = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(icId);
        return !CanonicalCapabilityResolution.TryCompileAbMerge(
                icId,
                requestedTopology,
                out CompiledComposition? composition,
                out _)
            ? []
            : CreateInputSlots(composition);
    }

    internal static CompiledComposition ResolveComposition(
        string icId,
        TopologySelection? topology,
        ActiveSessionSnapshot? acceptedSession = null)
    {
        string normalizedIcId = Profiles.IcIdentifier.Normalize(icId);
        if (acceptedSession is not null)
        {
            return AcceptedAuthoringSessionBinding.RequireCapability(
                acceptedSession,
                ExperienceIds.AbMerge,
                normalizedIcId,
                AuthoringDerivedResultKind.Inspection).CompiledComposition;
        }

        if (!CanonicalCapabilityResolution.IsAbMergeSupported(normalizedIcId))
        {
            throw new InvalidOperationException($"AB Merge is not available for '{normalizedIcId}'.");
        }

        ValidateTopologySelection(
            CanonicalCapabilityResolution.GetAbMergeTopologyChoices(normalizedIcId),
            topology);
        return !CanonicalCapabilityResolution.TryCompileAbMerge(
                normalizedIcId,
                topology,
                out CompiledComposition? composition,
                out IReadOnlyList<CompositionIssue> issues)
            ? throw new InvalidOperationException(string.Join(
                Environment.NewLine,
                issues.Select(issue => $"{issue.Code}: {issue.Message}")))
            : composition;
    }

    internal static InputArtifactBinding[] CreateInputBindings(
        CompiledComposition composition,
        IReadOnlyDictionary<string, string> slotPaths,
        ActiveSessionSnapshot? acceptedSession = null)
    {
        ArgumentNullException.ThrowIfNull(composition);
        ArgumentNullException.ThrowIfNull(slotPaths);
        return
        [
            .. composition.Plan.RequiredInputAddressSpaceIds
                .Order(StringComparer.Ordinal)
                .Select(addressSpaceId => slotPaths.TryGetValue(addressSpaceId, out string? path) &&
                    !string.IsNullOrWhiteSpace(path)
                        ? acceptedSession is null
                            ? CompiledCompositionInputBindingFactory.Create(
                                composition,
                                addressSpaceId,
                                Path.GetFullPath(path))
                            : AcceptedAuthoringSessionBinding.Create(
                                composition,
                                addressSpaceId,
                                path,
                                acceptedSession)
                        : throw new InvalidOperationException($"Input slot '{addressSpaceId}' is required.")),
        ];
    }

    private static IReadOnlyList<WorkbenchAbMergeInputSlot> CreateInputSlots(
        CompiledComposition composition)
    {
        V2CompiledCompositionDetails details = composition.V2Details;
        return
        [
            .. composition.Plan.RequiredInputAddressSpaceIds.Select(addressSpaceId =>
            {
                CompiledInputSpaceBinding binding = details.InputContract.SpaceBindings.Single(candidate =>
                    StringComparer.Ordinal.Equals(candidate.AddressSpaceId, addressSpaceId));
                CompiledInputSlotRequirement slot = details.InputContract.Slots.Single(candidate =>
                    StringComparer.Ordinal.Equals(candidate.SlotId, binding.SlotId));
                (long requiredEndExclusive, IReadOnlyList<long> expectedOuterLengths) =
                    ProjectLengthRequirement(
                        composition,
                        slot.LengthRequirement,
                        addressSpaceId);
                return new WorkbenchAbMergeInputSlot(
                    slot.SlotId,
                    binding.AddressSpaceId,
                    MapRole(slot.Role),
                    requiredEndExclusive,
                    expectedOuterLengths);
            }),
        ];
    }

    private static WorkbenchAbMergeInputRole MapRole(string role)
    {
        return role switch
        {
            AbDpRole => WorkbenchAbMergeInputRole.DpAb,
            AbTpARole => WorkbenchAbMergeInputRole.TpA,
            AbTpBRole => WorkbenchAbMergeInputRole.TpB,
            _ => throw new InvalidOperationException($"Supported AB profile declares unknown input role '{role}'."),
        };
    }

    private static void ValidateTopologySelection(
        IReadOnlyList<CapabilityTopologyChoice> topologyChoices,
        TopologySelection? topology)
    {
        if (topologyChoices.Count > 0 && topology is null)
        {
            throw new InvalidOperationException("AB Merge requires one explicit topology choice: 1 IC or Cascade.");
        }

        if (topologyChoices.Count == 0 && topology is not null)
        {
            throw new InvalidOperationException("The selected AB Merge profile does not expose an IC topology choice.");
        }
    }

    private static (long RequiredEndExclusive, IReadOnlyList<long> ExpectedOuterLengths) ProjectLengthRequirement(
        CompiledComposition composition,
        CompiledInputLengthRequirement requirement,
        string addressSpaceId)
    {
        return requirement switch
        {
            CompiledDeclaredPrefixWithWarningInputLengthRequirement declaredPrefix =>
                (declaredPrefix.RequiredEndExclusive, declaredPrefix.ExpectedOuterLengths),
            CompiledExactBytesInputLengthRequirement exact =>
                (exact.Bytes, [exact.Bytes]),
            CompiledExactResolvedMapCapacityInputLengthRequirement exact =>
                (exact.Bytes, [exact.Bytes]),
            CompiledSourceViewCoverageInputLengthRequirement sourceView =>
                (composition.Plan.AddressSpaces.Single(candidate =>
                        StringComparer.Ordinal.Equals(candidate.AddressSpaceId, addressSpaceId)).Length,
                    sourceView.ExpectedOuterLengths),
            _ => throw new InvalidOperationException(
                $"Supported AB input '{addressSpaceId}' has no displayable length contract."),
        };
    }

}
