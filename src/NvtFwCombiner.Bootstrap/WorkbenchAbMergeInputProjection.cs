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
        return !AbMergeWorkbenchCompositionService.TryCompileAbMerge(
                icId,
                requestedTopology,
                out CompiledComposition? composition,
                out _)
            ? []
            : CreateInputSlots(composition);
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
