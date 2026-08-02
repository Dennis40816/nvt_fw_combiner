using NvtFwCombiner.Application.Authoring;
using NvtFwCombiner.Application.Capabilities;
using NvtFwCombiner.Application.InputInspection;
using NvtFwCombiner.Domain.Composition;
using NvtFwCombiner.Profiles;

namespace NvtFwCombiner.Bootstrap;

public static partial class WorkbenchCompositionService
{
    private static Dictionary<string, AuthoringInputSlotStatus> InspectDpReplaceInputSlots(
        string icId,
        IReadOnlyList<WorkbenchFirmwareInspectionInput> inputs,
        Func<string, byte[]?> readFirmwareImage)
    {
        WorkbenchFirmwareInspectionInput[] selected = [.. inputs.Where(static input => input.DpReplaceAddressSpaceId is not null)];
        if (selected.Length == 0)
        {
            return [];
        }

        var authoringRevision = new AuthoringRevision(selected.Select(static input => input.AuthoringRevision).Distinct().Single());
        Dictionary<string, ReadOnlyMemory<byte>?> sourcesByAddressSpace = selected.ToDictionary(
            static input => input.DpReplaceAddressSpaceId!,
            input => readFirmwareImage(input.Path) is { } image ? image : (ReadOnlyMemory<byte>?)null,
            StringComparer.Ordinal);
        ReadOnlyMemory<byte>? reference = sourcesByAddressSpace.GetValueOrDefault(CompositionAddressSpaceIds.ReferenceBase);
        if (reference is null)
        {
            return ProjectDpReplaceCompilationFailure(
                icId, selected, authoringRevision,
                InputArtifactInspectionIssueCodes.SourceUnreadable,
                "The selected Reference BIN could not be read.");
        }

        string[] selectedSlotIds = [.. sourcesByAddressSpace.Keys.Where(static id => !StringComparer.Ordinal.Equals(id, CompositionAddressSpaceIds.ReferenceBase))];
        if (!TryCompileBuiltInV2DpReplace(
                icId, reference.Value.Length,
                selectedSlotIds.Length == 0 ? null : selectedSlotIds,
                out _, out ResolvedCapability? capability,
                out IReadOnlyList<CompositionIssue> issues) || capability is null)
        {
            CompositionIssue issue = issues.Count == 0
                ? new CompositionIssue(CapabilityCatalogIssueCodes.RouteUnavailable,
                    "The selected inputs did not produce one exact DP Replace compilation.")
                : issues[0];
            return ProjectDpReplaceCompilationFailure(
                icId, selected, authoringRevision, issue.Code, issue.Message);
        }

        IReadOnlyDictionary<string, AuthoringInputSlotStatus> statusesByAddressSpace = AuthoringInputSlotInspectionService.InspectBatch(
                capability, authoringRevision, sourcesByAddressSpace,
                selected.ToDictionary(
                    static input => input.DpReplaceAddressSpaceId!,
                    static input => input.Path,
                    StringComparer.Ordinal));
        return selected.ToDictionary(static input => input.InspectionId,
            input => statusesByAddressSpace[input.DpReplaceAddressSpaceId!],
            StringComparer.Ordinal);
    }

    private static Dictionary<string, AuthoringInputSlotStatus> ProjectDpReplaceCompilationFailure(
        string icId, IReadOnlyList<WorkbenchFirmwareInspectionInput> selected,
        AuthoringRevision authoringRevision, string issueCode, string reason)
    {
        BuiltInV2Registration registration = BuiltInV2RegistrationRegistry.DpReplaceByIc.Value[IcSupportCatalog.NormalizeIcId(icId)];
        IReadOnlyList<long> capacities = registration.GetMapCapacities(out _);
        _ = TryCompileBuiltInV2DpReplace(icId, capacities[^1], registration.InputSelectionGroupMemberSlotIds,
            out CompiledComposition? discovery, out ResolvedCapability? capability, out _);
        CompiledInputContract contract = discovery!.V2Details.InputContract;
        return selected.ToDictionary(static input => input.InspectionId,
            input => AuthoringInputSlotInspectionService.BlockBeforeCompilation(
                capability!, authoringRevision,
                contract.SpaceBindings.Single(binding => StringComparer.Ordinal.Equals(
                    binding.AddressSpaceId, input.DpReplaceAddressSpaceId)),
                issueCode, reason),
            StringComparer.Ordinal);
    }
}
