using NvtFwCombiner.Application.Authoring;
using NvtFwCombiner.Application.Capabilities;
using NvtFwCombiner.Domain.Composition;
using NvtFwCombiner.Profiles;

namespace NvtFwCombiner.Bootstrap;

public static partial class CanonicalAuthoringAdapter
{
    private static readonly CompiledAuthoringWorkflowService s_dpReplaceAuthoring =
        new(new DpReplaceAuthoringResolver());

    /// <summary>Projects canonical DP Replace picker readiness from accepted content identities.</summary>
    public static CompiledAuthoringSelectionSnapshot GetDpReplaceAuthoringSnapshot(
        string icId,
        IReadOnlyCollection<string> selectedSlotIds,
        IReadOnlyDictionary<string, FileStamp> acceptedFileStamps,
        AuthoringRevision authoringRevision,
        ActiveSessionSnapshot? retainedSession = null)
    {
        string[] compiledSlotIds = DpReplaceInputSlotProjection.NormalizeCompiledSlotIds(
            icId,
            selectedSlotIds);
        IReadOnlyDictionary<string, FileStamp> compiledFileStamps =
            DpReplaceInputSlotProjection.NormalizeCompiledSlotFileStamps(
                icId,
                acceptedFileStamps);
        return s_dpReplaceAuthoring.ProjectSelection(
            icId, authoringRevision, compiledSlotIds, compiledFileStamps, retainedSession);
    }

    internal static WorkbenchCompiledAuthoringInspectionBatch InspectDpReplaceInputSlots(
        string icId,
        IReadOnlyList<WorkbenchFirmwareInspectionInput> inputs,
        Func<string, byte[]?> readFirmwareImage)
    {
        WorkbenchFirmwareInspectionInput[] selected = [.. inputs.Where(static input => input.DpReplaceAddressSpaceId is not null)];
        if (selected.Length == 0)
        {
            return WorkbenchCompiledAuthoringInspectionBatch.Empty;
        }

        var authoringRevision = new AuthoringRevision(selected.Select(static input => input.AuthoringRevision).Distinct().Single());
        ResolvedCapability? exactCapability = selected[0].ExactCapability;
        if (selected.Any(input => !ReferenceEquals(input.ExactCapability, exactCapability)))
        {
            throw new InvalidOperationException("DP Replace inspection leases disagree on the exact compilation.");
        }
        CompiledAuthoringInspectionBatch batch = s_dpReplaceAuthoring.InspectBatch(
            icId,
            authoringRevision,
            [.. selected.Select(input => new CompiledAuthoringSelectedInput(
                exactCapability is null
                    ? DpReplaceInputSlotProjection.NormalizeCompiledSlotIds(
                        icId,
                        [input.DpReplaceAddressSpaceId!]).Single()
                    : DpReplaceInputSlotProjection.ResolveCompiledSlotId(
                        exactCapability.CompiledComposition,
                        input.DpReplaceAddressSpaceId!),
                input.Path,
                readFirmwareImage(input.Path) is { } image
                    ? image
                    : (ReadOnlyMemory<byte>?)null))],
            exactCapability);
        return new WorkbenchCompiledAuthoringInspectionBatch(
            batch.Catalog,
            selected.ToDictionary(
                static input => input.InspectionId,
                input => batch.Statuses[input.DpReplaceAddressSpaceId!],
                StringComparer.Ordinal),
            batch.Issues);
    }

    private sealed class DpReplaceAuthoringResolver : ICompiledAuthoringWorkflowResolver
    {
        public string WorkflowId => ExperienceIds.DpReplace;

        public CompiledAuthoringWorkflowDiscovery Discover(string icId)
        {
            BuiltInV2Registration registration =
                BuiltInV2RegistrationRegistry.DpReplaceByIc.Value[
                    IcIdentifier.Normalize(icId)];
            IReadOnlyList<long> capacities = registration.GetMapCapacities(
                out IReadOnlyList<CompositionIssue> capacityIssues);
            ResolvedCapability? capability = null;
            IReadOnlyList<CompositionIssue> issues = capacityIssues;
            bool compiled = capacityIssues.Count == 0 && capacities.Count != 0 &&
                CanonicalCapabilityResolution.TryCompileDpReplace(
                    icId, capacities[0], selectedInputSlotIds: null,
                    out _, out capability, out issues);
            ResolvedCapability discovery = compiled && capability is not null
                ? capability
                : throw new InvalidOperationException(string.Join(
                    " | ", (capacityIssues.Count == 0 ? issues : capacityIssues)
                        .Select(static issue => issue.Message)));
            if (!CanonicalCapabilityProjection.TryResolveBuiltInV2DpReplaceContracts(
                    icId,
                    out IReadOnlyList<CompiledComposition>? compositions))
            {
                throw new InvalidOperationException(
                    $"DP Replace compiled-contract discovery is unavailable for '{icId}'.");
            }
            string[] available =
            [
                .. compositions.SelectMany(static composition =>
                        composition.V2Details.InputContract.Slots)
                    .Select(static slot => slot.SlotId)
                    .Distinct(StringComparer.Ordinal),
            ];
            ReviewedDiscoveryTransition transition =
                (WorkbenchHostServices.CanonicalCapabilities.Read(
                    static catalog => catalog.CurrentSnapshot) ??
                    throw new InvalidOperationException("Canonical capability publication is unavailable."))
                .ResolveReviewedDiscoveryTransition(
                    discovery,
                    CompositionAddressSpaceIds.ReferenceBase);
            return new CompiledAuthoringWorkflowDiscovery(
                discovery,
                available,
                CompositionAddressSpaceIds.ReferenceBase,
                transition);
        }

        public CompiledAuthoringWorkflowResolution ResolveExact(
            string icId,
            long? prerequisiteLength,
            IReadOnlyCollection<string> selectedSlotIds)
        {
            ResolvedCapability? capability = null;
            IReadOnlyList<CompositionIssue> issues = [];
            string[] replacements =
            [
                .. selectedSlotIds.Where(static slotId => !StringComparer.Ordinal.Equals(
                    slotId, CompositionAddressSpaceIds.ReferenceBase)),
            ];
            bool compiled = prerequisiteLength is not null &&
                CanonicalCapabilityResolution.TryCompileDpReplace(
                    icId,
                    prerequisiteLength.Value,
                    replacements.Length == 0 ? null : replacements,
                    out _, out capability, out issues);
            return new CompiledAuthoringWorkflowResolution(compiled ? capability : null, issues);
        }
    }
}
