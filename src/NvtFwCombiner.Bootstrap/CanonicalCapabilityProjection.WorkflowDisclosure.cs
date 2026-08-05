using System.Globalization;
using NvtFwCombiner.Application.Capabilities;
using NvtFwCombiner.Domain.Composition;
using NvtFwCombiner.Domain.Firmware;
using NvtFwCombiner.Profiles;

namespace NvtFwCombiner.Bootstrap;

public static partial class CanonicalCapabilityProjection
{
    /// <summary>Returns true when canonical onboarding exposes Replace authoring.</summary>
    public static bool IsReplaceWorkflowAvailable(string icId, string replaceMode)
    {
        return GetReplaceWorkflowReadiness(icId, replaceMode).IsAvailable;
    }

    /// <summary>Gets onboarding availability plus exact-route evidence from canonical owners.</summary>
    public static CapabilityWorkflowReadiness GetReplaceWorkflowReadiness(
        string icId,
        string replaceMode)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(icId);
        ArgumentException.ThrowIfNullOrWhiteSpace(replaceMode);
        string? workflowId = GetReplaceWorkflowId(replaceMode);
        bool isDpReplace = StringComparer.Ordinal.Equals(
            replaceMode,
            WorkbenchReplaceModes.Dp);
        string unsupportedReason = workflowId is null
            ? "The selected Replace mode is not declared by the canonical capability contract."
            : isDpReplace
            ? "No owner-approved DP Replace profile/map is registered for this IC."
            : "No owner-approved executable and safety contract is registered for this IC and Replace mode.";
        string openCondition = workflowId is null
            ? "Add an owner-reviewed capability definition, profile/safety contract, and full-byte evidence."
            : isDpReplace
            ? "Add the IC-specific DP map/profile, full-byte golden parity, and firmware-owner review."
            : "Owner must reactivate the scope with a safe executable contract, direct evidence, and firmware-owner review.";
        string normalizedIcId = IcIdentifier.Normalize(icId);
        CanonicalCapabilityCatalogSnapshot snapshot = WorkbenchHostServices
            .CanonicalCapabilityQuery
            .GetCurrentSnapshot();
        CanonicalSupportMatrixQueryResult publication = WorkbenchHostServices
            .CanonicalSupportMatrixQuery
            .Query();
        return CapabilityWorkflowReadinessProjector.Project(
            publication.Matrix,
            normalizedIcId,
            workflowId,
            workflowId is not null && HasAuthorableRoute(
                snapshot,
                normalizedIcId,
                workflowId),
            unsupportedReason,
            openCondition);
    }

    /// <summary>Gets the owner-defined perfect/partial IC family relation for display.</summary>
    public static CapabilityFamilySummary GetIcFamilySummary(string icId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(icId);
        string normalizedIcId = IcIdentifier.Normalize(icId);
        MapBoundV2CompilationContext[] contexts =
        [
            .. GetCanonicalFamilyCompositions(normalizedIcId)
                .Select(static composition =>
                    composition.V2Details.Provenance.Context)
                .OfType<MapBoundV2CompilationContext>(),
        ];
        (string FamilyId, FirmwareFamilyRelationship relationship)[] relationshipBindings =
        [
            .. contexts
                .SelectMany(context =>
                    context.ResolvedMap.FamilyRelationships
                        .Where(relationship => relationship.MemberIds.Contains(
                            normalizedIcId,
                            StringComparer.Ordinal))
                        .Select(relationship => (
                            context.ResolvedMap.FamilyId,
                            relationship)))
                .DistinctBy(static binding => (
                    binding.FamilyId,
                    binding.relationship.RelationshipId)),
        ];
        IEnumerable<(string FamilyId, FirmwareFamilyRelationship relationship)> selectedSource =
            relationshipBindings.Any(static binding =>
                binding.relationship is PerfectFamilyRelationship)
            ? relationshipBindings.Where(static binding =>
                binding.relationship is PerfectFamilyRelationship)
            : relationshipBindings.Where(static binding =>
                binding.relationship is SharedFactRelationship);
        (string FamilyId, FirmwareFamilyRelationship relationship)[] selected =
            [.. selectedSource];
        string[] familyIds =
        [
            .. selected.Select(static binding => binding.FamilyId)
                .Distinct(StringComparer.Ordinal),
        ];
        return selected.Length == 0
            ? StandaloneFamily()
            : familyIds.Length != 1
            ? throw new InvalidDataException(
                $"Canonical family facts for '{normalizedIcId}' resolve to multiple families: " +
                string.Join(", ", familyIds.Order(StringComparer.Ordinal)))
            : new CapabilityFamilySummary(
                familyIds[0],
                selected[0].relationship is PerfectFamilyRelationship
                    ? CapabilityFamilyRelationship.PerfectAlias
                    : CapabilityFamilyRelationship.PartialAlias,
                string.Join(" ", selected
                    .Select(static binding => binding.relationship.Reason)
                    .Distinct(StringComparer.Ordinal)
                    .Order(StringComparer.Ordinal)));
    }

    /// <summary>Returns whether two ICs are owner-declared perfect members of the same family.</summary>
    public static bool ArePerfectFamilyMembers(string firstIcId, string secondIcId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(firstIcId);
        ArgumentException.ThrowIfNullOrWhiteSpace(secondIcId);
        CapabilityFamilySummary first = GetIcFamilySummary(firstIcId);
        CapabilityFamilySummary second = GetIcFamilySummary(secondIcId);
        return !string.IsNullOrWhiteSpace(first.FamilyId) &&
            StringComparer.Ordinal.Equals(first.FamilyId, second.FamilyId) &&
            IsPerfectFamilyRelationship(first.Relationship) &&
            IsPerfectFamilyRelationship(second.Relationship);
    }

    internal static string? GetReplaceWorkflowId(string replaceMode)
    {
        return replaceMode switch
        {
            WorkbenchReplaceModes.Dp => IcWorkflowIds.DpReplace,
            WorkbenchReplaceModes.CtrlRam => IcWorkflowIds.CtrlRamReplace,
            WorkbenchReplaceModes.General => IcWorkflowIds.GeneralReplace,
            _ => null,
        };
    }

    private static bool IsPerfectFamilyRelationship(
        CapabilityFamilyRelationship relationship)
    {
        return relationship == CapabilityFamilyRelationship.PerfectAlias;
    }

    private static bool HasAuthorableRoute(
        CanonicalCapabilityCatalogSnapshot snapshot,
        string icId,
        string workflowId)
    {
        return snapshot.Capabilities.Any(capability =>
                capability.Authoring.Value ==
                    CapabilityAuthoringAvailability.Available &&
                StringComparer.Ordinal.Equals(capability.Identity.IcId, icId) &&
                StringComparer.Ordinal.Equals(
                    capability.Identity.WorkflowId,
                    workflowId)) ||
            snapshot.DynamicRoutes.Any(route =>
                route.Authoring.Value ==
                    CapabilityAuthoringAvailability.Available &&
                StringComparer.Ordinal.Equals(route.Identity.IcId, icId) &&
                StringComparer.Ordinal.Equals(route.Identity.WorkflowId, workflowId));
    }

    private static CompiledComposition[] GetCanonicalFamilyCompositions(string icId)
    {
        CanonicalCapabilityCatalogSnapshot snapshot = WorkbenchHostServices
            .CanonicalCapabilityQuery
            .GetCurrentSnapshot();
        var compositions = new List<CompiledComposition>(snapshot.Capabilities
            .Where(capability =>
                capability.Authoring.Value ==
                    CapabilityAuthoringAvailability.Available &&
                StringComparer.Ordinal.Equals(capability.Identity.IcId, icId))
            .Select(static capability => capability.CompiledComposition));
        if (HasAuthorableRoute(snapshot, icId, IcWorkflowIds.StandardMerge) &&
            BuiltInV2RegistrationRegistry.StandardMergeByIc.TryGetValue(
                icId,
                out BuiltInV2Registration? registration))
        {
            IReadOnlyList<long> capacities = registration.GetMapCapacities(out _);
            long?[] candidates = capacities.Count == 0
                ? [null]
                : [.. capacities.Select(static capacity => (long?)capacity)];
            foreach (long? capacity in candidates)
            {
                if (!CanonicalCapabilityResolution.TryCompileStandardMerge(
                        icId,
                        capacity,
                        out CompiledComposition? composition,
                        out IReadOnlyList<CompositionIssue> issues) ||
                    composition is null)
                {
                    throw new InvalidDataException(
                        $"Canonical family projection could not compile Standard Merge for " +
                        $"'{icId}' at capacity '" +
                        $"{(capacity is null ? "default" : capacity.Value.ToString(CultureInfo.InvariantCulture))}': " +
                        (issues.Count == 0
                            ? "no compiled composition"
                            : string.Join(", ", issues.Select(static issue => issue.Code))));
                }

                compositions.Add(composition);
            }
        }
        return
        [
            .. compositions.DistinctBy(static composition =>
                composition.CompilationFingerprint),
        ];
    }

    private static CapabilityFamilySummary StandaloneFamily()
    {
        return new CapabilityFamilySummary(
            null,
            CapabilityFamilyRelationship.Standalone,
            null);
    }

}
