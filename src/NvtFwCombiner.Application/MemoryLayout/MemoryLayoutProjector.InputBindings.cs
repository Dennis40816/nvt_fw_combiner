using NvtFwCombiner.Application.Authoring;
using NvtFwCombiner.Domain.Composition;

namespace NvtFwCombiner.Application.MemoryLayout;

public static partial class MemoryLayoutProjector
{
    private static (
        Dictionary<string, string> SlotsBySpace,
        Dictionary<string, CompiledInputSlotRequirement> RequirementsByStateId)
        ResolveAuthoringInputBindings(
            CompiledInputContract inputContract,
            Dictionary<string, CompiledInputSlotRequirement> slotsById,
            CompositionPlan plan,
            AuthoringDraftState? draftState,
            Dictionary<string, AuthoringSlotState> statesById)
    {
        Dictionary<string, GeneralMappingDraftRow>? generalRows =
            ResolveGeneralRows(draftState);
        var operationsBySourceSpace =
            plan.OrderedOperations
                .Where(static operation => operation.SourceSpaceId is not null)
                .GroupBy(static operation => operation.SourceSpaceId!, StringComparer.Ordinal)
                .ToDictionary(
                    static group => group.Key,
                    static group => group.ToArray(),
                    StringComparer.Ordinal);
        Dictionary<string, string> slotsBySpace = new(StringComparer.Ordinal);
        Dictionary<string, CompiledInputSlotRequirement> requirementsByStateId =
            new(StringComparer.Ordinal);

        foreach (CompiledInputSpaceBinding binding in inputContract.SpaceBindings)
        {
            string? stateId = binding.InstancePolicy == CompiledInputInstancePolicy.PerBinding
                ? ResolvePerBindingStateId(
                    binding.AddressSpaceId,
                    generalRows,
                    operationsBySourceSpace,
                    statesById)
                : statesById.ContainsKey(binding.SlotId) ||
                    !statesById.ContainsKey(binding.AddressSpaceId)
                        ? binding.SlotId
                        : binding.AddressSpaceId;
            if (stateId is null)
            {
                continue;
            }

            slotsBySpace.Add(binding.AddressSpaceId, stateId);
            CompiledInputSlotRequirement requirement = slotsById[binding.SlotId];
            if (!requirementsByStateId.TryAdd(stateId, requirement) &&
                !ReferenceEquals(requirementsByStateId[stateId], requirement))
            {
                throw new ArgumentException(
                    "One authoring slot cannot represent different compiled input requirements.",
                    nameof(inputContract));
            }
        }

        return (slotsBySpace, requirementsByStateId);
    }

    private static Dictionary<string, GeneralMappingDraftRow>? ResolveGeneralRows(
        AuthoringDraftState? draftState)
    {
        GeneralMappingDraftState? mappings = draftState switch
        {
            GeneralMappingDraftState replace => replace,
            GeneralMergeDraftState merge => merge.Mappings,
            _ => null,
        };
        return mappings?.Rows.ToDictionary(
            static row => row.MappingId,
            StringComparer.Ordinal);
    }

    private static string? ResolvePerBindingStateId(
        string addressSpaceId,
        Dictionary<string, GeneralMappingDraftRow>? generalRows,
        Dictionary<string, CompositionOperation[]> operationsBySourceSpace,
        Dictionary<string, AuthoringSlotState> statesById)
    {
        if (statesById.ContainsKey(addressSpaceId) || generalRows is null)
        {
            return addressSpaceId;
        }

        if (!operationsBySourceSpace.TryGetValue(
                addressSpaceId,
                out CompositionOperation[]? operations))
        {
            return addressSpaceId;
        }

        GeneralMappingDraftRow[] rows =
        [
            .. operations.Select(operation => generalRows.TryGetValue(
                    operation.OperationId,
                    out GeneralMappingDraftRow? row)
                    ? row
                    : null)
                .Where(static row => row is not null)
                .Cast<GeneralMappingDraftRow>()
                .DistinctBy(static row => row.MappingId, StringComparer.Ordinal),
        ];
        if (rows.Length == 0)
        {
            return addressSpaceId;
        }

        GeneralMappingDraftRow[] fileRows =
        [
            .. rows.Where(static row =>
                row.Source.Kind == GeneralMappingSourceKind.FileArtifact),
        ];
        return fileRows.Length == 0
            ? null
            : fileRows.Length == 1 && fileRows.Length == rows.Length
                ? fileRows[0].MappingId
                : throw new ArgumentException(
                    "One compiled General input space must represent exactly one typed source row.",
                    nameof(addressSpaceId));
    }
}
