using NvtFwCombiner.Application.Authoring;
using NvtFwCombiner.Application.Capabilities;
using NvtFwCombiner.Domain.Composition;

namespace NvtFwCombiner.Bootstrap;

/// <summary>
/// Temporary profile-compiler adapter for the shared Application authoring
/// session use case. It owns no execution, readiness policy, or UI state.
/// </summary>
public static partial class CanonicalAuthoringAdapter
{
    private static readonly CompiledAuthoringWorkflowService
        s_standardMergeAuthoring = new(new StandardMergeAuthoringResolver());

    /// <summary>Returns true when the selected IC has a canonical Standard Merge route.</summary>
    public static bool IsStandardMergeSupported(string icId)
    {
        return CanonicalCapabilityResolution.HasCanonicalCapability(
            icId,
            ExperienceIds.StandardMerge);
    }

    /// <summary>Gets the canonical Standard Merge profile id for the selected IC, if any.</summary>
    public static string? GetStandardMergeProfileId(string icId)
    {
        return CanonicalCapabilityProjection.FindStandardMergeProfileSummary(icId)?.ProfileId;
    }

    /// <summary>Gets required Standard Merge input spaces for the selected IC.</summary>
    public static IReadOnlyList<string> GetStandardMergeRequiredAddressSpaces(string icId)
    {
        return CanonicalCapabilityProjection
            .FindStandardMergeProfileSummary(icId)?
            .RequiredInputAddressSpaceIds ?? [];
    }

    /// <summary>Gets all fixed input spaces exposed by canonical Standard Merge authoring.</summary>
    public static IReadOnlyList<string> GetStandardMergeInputAddressSpaces(string icId)
    {
        IReadOnlyList<string> required = GetStandardMergeRequiredAddressSpaces(icId);
        return !BuiltInV2RegistrationRegistry.StandardMergeByIc.TryGetValue(
                icId,
                out BuiltInV2Registration? registration)
            ? required
            : Array.AsReadOnly(
            [
                .. required
                    .Concat(registration.InputSelectionGroupMemberSlotIds)
                    .Distinct(StringComparer.Ordinal)
                    .Order(StringComparer.Ordinal),
            ]);
    }

    /// <summary>Projects canonical Standard Merge picker readiness from accepted content identities.</summary>
    public static CompiledAuthoringSelectionSnapshot GetStandardMergeAuthoringSnapshot(
        string icId,
        IReadOnlyCollection<string> selectedSlotIds,
        IReadOnlyDictionary<string, FileStamp> acceptedFileStamps,
        AuthoringRevision authoringRevision,
        ActiveSessionSnapshot? retainedSession = null)
    {
        return s_standardMergeAuthoring.ProjectSelection(
            icId,
            authoringRevision,
            selectedSlotIds,
            acceptedFileStamps,
            retainedSession);
    }

    private sealed class StandardMergeAuthoringResolver
        : ICompiledAuthoringWorkflowResolver
    {
        public string WorkflowId => ExperienceIds.StandardMerge;

        public CompiledAuthoringWorkflowDiscovery Discover(string icId)
        {
            bool compiled = CanonicalCapabilityResolution.TryCompileStandardMerge(
                icId,
                dpInputLength: null,
                selectedInputSlotIds: [],
                out _,
                out ResolvedCapability? capability,
                out IReadOnlyList<CompositionIssue> issues);
            if (!compiled &&
                CanonicalCapabilityResolution.TryGetBuiltInV2StandardMergeAuthoringDefaultCapacity(
                    icId,
                    out long capacity,
                    out IReadOnlyList<CompositionIssue> capacityIssues))
            {
                compiled = CanonicalCapabilityResolution.TryCompileStandardMerge(
                    icId,
                    capacity,
                    selectedInputSlotIds: [],
                    out _,
                    out capability,
                    out issues);
                if (!compiled && issues.Count == 0)
                {
                    issues = capacityIssues;
                }
            }

            ResolvedCapability resolvedCapability = compiled && capability is not null
                ? capability
                : throw new InvalidOperationException(
                    issues.Count == 0
                        ? $"No reviewed Standard Merge authoring route exists for '{icId}'."
                        : string.Join(" | ", issues.Select(static issue => issue.Message)));

            string? prerequisiteSlotId =
                CanonicalCapabilityResolution.IsBuiltInV2StandardMergeMapCapacityPending(icId)
                ? CompositionAddressSpaceIds.DpInput
                : null;
            ReviewedDiscoveryTransition? discoveryTransition = prerequisiteSlotId is null
                ? null
                : (WorkbenchHostServices.CanonicalCapabilities.Read(
                    static catalog => catalog.CurrentSnapshot) ??
                        throw new InvalidOperationException(
                            "Canonical capability publication is unavailable."))
                    .ResolveReviewedDiscoveryTransition(
                    resolvedCapability,
                    prerequisiteSlotId);
            return new CompiledAuthoringWorkflowDiscovery(
                resolvedCapability,
                GetStandardMergeInputAddressSpaces(icId),
                prerequisiteSlotId,
                discoveryTransition);
        }

        public CompiledAuthoringWorkflowResolution ResolveExact(
            string icId,
            long? prerequisiteLength,
            IReadOnlyCollection<string> selectedSlotIds)
        {
            bool compiled = CanonicalCapabilityResolution.TryCompileStandardMerge(
                icId,
                prerequisiteLength,
                selectedSlotIds,
                out _,
                out ResolvedCapability? capability,
                out IReadOnlyList<CompositionIssue> issues);
            return new CompiledAuthoringWorkflowResolution(
                compiled ? capability : null,
                issues);
        }
    }
}
