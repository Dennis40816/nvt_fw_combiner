using NvtFwCombiner.Application.Authoring;
using NvtFwCombiner.Domain.Composition;

namespace NvtFwCombiner.Bootstrap;

/// <summary>Projects DP Replace workbench slots without collapsing compiler slot and address-space identities.</summary>
internal static class DpReplaceInputSlotProjection
{
    internal static IReadOnlyList<WorkbenchReplaceInputSlot> GetInputSlots(string icId)
    {
        if (!CanonicalCapabilityProjection.TryResolveBuiltInV2DpReplaceContracts(
                icId,
                out IReadOnlyList<CompiledComposition>? compositions))
        {
            return [];
        }

        var slotsByAddressSpace = new Dictionary<string, WorkbenchReplaceInputSlot>(StringComparer.Ordinal);
        foreach (CompiledComposition composition in compositions)
        {
            CompiledInputContract contract = composition.V2Details.InputContract;
            foreach (CompiledInputSlotRequirement slot in contract.Slots.Where(static slot =>
                         slot.ArtifactClass != CompiledInputArtifactClass.ReferenceImage))
            {
                WorkbenchReplaceInputSlot projected = CreateInputSlot(icId, composition, contract, slot);
                if (slotsByAddressSpace.TryGetValue(
                        projected.AddressSpaceId,
                        out WorkbenchReplaceInputSlot? existing) &&
                    existing != projected)
                {
                    throw new InvalidDataException(
                        $"DP Replace address space '{projected.AddressSpaceId}' has conflicting compiled map-variant projections.");
                }

                slotsByAddressSpace[projected.AddressSpaceId] = projected;
            }
        }

        return [.. slotsByAddressSpace.Values.OrderBy(static slot => slot.SlotId, StringComparer.Ordinal)];
    }

    internal static string[] GetSelectedCompiledSlotIds(
        IEnumerable<WorkbenchReplaceInputSlot> slots,
        IReadOnlyDictionary<string, string> slotPaths)
    {
        ArgumentNullException.ThrowIfNull(slots);
        ArgumentNullException.ThrowIfNull(slotPaths);
        return
        [
            .. slots
                .Where(slot => slot.SelectionGroupId is not null &&
                    slotPaths.TryGetValue(slot.SlotId, out string? path) &&
                    !string.IsNullOrWhiteSpace(path))
                .Select(static slot => slot.CompiledSlotId)
                .Distinct(StringComparer.Ordinal)
                .Order(StringComparer.Ordinal),
        ];
    }

    internal static string[] NormalizeCompiledSlotIds(
        string icId,
        IEnumerable<string> slotOrAddressSpaceIds)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(icId);
        ArgumentNullException.ThrowIfNull(slotOrAddressSpaceIds);
        Dictionary<string, string> aliases = CreateCompiledSlotAliases(icId);
        return
        [
            .. slotOrAddressSpaceIds.Select(id => aliases.GetValueOrDefault(id, id))
                .Distinct(StringComparer.Ordinal),
        ];
    }

    internal static IReadOnlyDictionary<string, FileStamp> NormalizeCompiledSlotFileStamps(
        string icId,
        IReadOnlyDictionary<string, FileStamp> fileStamps)
    {
        ArgumentNullException.ThrowIfNull(fileStamps);
        Dictionary<string, string> aliases = CreateCompiledSlotAliases(icId);
        return fileStamps.ToDictionary(
            pair => aliases.GetValueOrDefault(pair.Key, pair.Key),
            static pair => pair.Value,
            StringComparer.Ordinal);
    }

    internal static string ResolveCompiledSlotId(CompiledComposition composition, string addressSpaceId)
    {
        ArgumentNullException.ThrowIfNull(composition);
        ArgumentException.ThrowIfNullOrWhiteSpace(addressSpaceId);
        return composition.V2Details.InputContract.SpaceBindings.Single(binding =>
            StringComparer.Ordinal.Equals(binding.AddressSpaceId, addressSpaceId)).SlotId;
    }

    private static Dictionary<string, string> CreateCompiledSlotAliases(string icId)
    {
        return CanonicalCapabilityProjection.TryResolveBuiltInV2DpReplaceContracts(
                icId,
                out IReadOnlyList<CompiledComposition>? compositions)
            ? compositions
                .SelectMany(static composition => composition.V2Details.InputContract.SpaceBindings)
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
                    StringComparer.Ordinal)
            : new Dictionary<string, string>(StringComparer.Ordinal);
    }

    private static WorkbenchReplaceInputSlot CreateInputSlot(
        string icId,
        CompiledComposition composition,
        CompiledInputContract contract,
        CompiledInputSlotRequirement slot)
    {
        CompiledInputSpaceBinding binding = AssertSingleSpaceBinding(contract, slot.SlotId);
        CompiledInputSelectionGroup? selectionGroup = contract.SelectionGroups.SingleOrDefault(group =>
            group.MemberSlotIds.Contains(slot.SlotId, StringComparer.Ordinal));
        bool isLdc = StringComparer.Ordinal.Equals(slot.Role, "ldc") ||
            StringComparer.Ordinal.Equals(binding.AddressSpaceId, CompositionAddressSpaceIds.LdcReplacement);
        string description = isLdc
            ? $"{icId}-only LDC payload declared by the compiled DP Replace profile."
            : CompositionMemoryProjection.TryGetV2DpReplaceInputDescription(icId, out string v2Description)
                ? v2Description
                : "Replacement DP payload declared by the compiled DP Replace profile.";
        CompiledResolvedPhysicalView? sourceView = composition.V2Details.RegionAccessContract.ResolvedViews
            .FirstOrDefault(view => StringComparer.Ordinal.Equals(view.AddressSpaceId, binding.AddressSpaceId));
        return new WorkbenchReplaceInputSlot(
            isLdc ? WorkbenchSlotIds.ReplaceLdc : WorkbenchSlotIds.ReplaceDp,
            isLdc
                ? "LDC replacement BIN"
                : binding.AddressSpaceId == CompositionAddressSpaceIds.InitialCodeReplacement
                    ? "Initial Code replacement BIN"
                    : "DP replacement BIN",
            description,
            selectionGroup is not null || !slot.Required,
            binding.AddressSpaceId,
            sourceView?.GoverningRegionChain[^1].RegionId,
            slot.SlotId,
            selectionGroup?.GroupId,
            InputRole: WorkbenchReplaceInputRole.Dp);
    }

    private static CompiledInputSpaceBinding AssertSingleSpaceBinding(
        CompiledInputContract contract,
        string slotId)
    {
        CompiledInputSpaceBinding[] bindings =
        [
            .. contract.SpaceBindings.Where(binding =>
                StringComparer.Ordinal.Equals(binding.SlotId, slotId)),
        ];
        return bindings.Length == 1
            ? bindings[0]
            : throw new InvalidOperationException(
                $"DP Replace slot '{slotId}' must bind exactly one compiled input address space.");
    }
}
