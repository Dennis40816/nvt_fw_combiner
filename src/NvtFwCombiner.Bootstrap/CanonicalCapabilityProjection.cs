using NvtFwCombiner.Application.Capabilities;
using NvtFwCombiner.Application.ExternalTools;
using NvtFwCombiner.Application.FlashMaps;
using NvtFwCombiner.Domain.Composition;
using NvtFwCombiner.Infrastructure.ExternalTools;
using NvtFwCombiner.Profiles;

namespace NvtFwCombiner.Bootstrap;

/// <summary>
/// Projects immutable discovery summaries from the host's canonical
/// publication. Dynamic routes use only their registered compiler adapter to
/// materialize display fields absent from the definition-level contract.
/// </summary>
public static partial class CanonicalCapabilityProjection
{
    /// <summary>Default IC selected from the broadest authorable canonical route set.</summary>
    public static string DefaultIcId
    {
        get
        {
            CanonicalCapabilityCatalogSnapshot snapshot = WorkbenchHostServices
                .CanonicalCapabilityQuery
                .GetCurrentSnapshot();
            return snapshot.Capabilities
                .Where(capability => IsAuthorable(capability.Authoring))
                .Select(static capability => capability.Identity)
                .Concat(snapshot.DynamicRoutes
                    .Where(route => IsAuthorable(route.Authoring))
                    .Select(static route => route.Identity))
                .DistinctBy(static identity => identity.RouteId, StringComparer.Ordinal)
                .GroupBy(static identity => identity.IcId, StringComparer.Ordinal)
                .OrderByDescending(static group => group.Count())
                .ThenByDescending(static group => group
                    .Select(identity => identity.WorkflowId)
                    .Distinct(StringComparer.Ordinal)
                    .Count())
                .ThenBy(static group => group.Key, StringComparer.Ordinal)
                .Select(static group => group.Key)
                .FirstOrDefault() ?? throw new InvalidOperationException(
                    "The canonical capability publication has no authorable IC route.");
        }
    }

    /// <summary>Gets canonical Standard Merge profile summaries.</summary>
    public static IReadOnlyList<CapabilityProfileSummary>
        GetStandardMergeProfileSummaries()
    {
        return GetProfileSummaries(IcWorkflowIds.StandardMerge);
    }

    /// <summary>Gets canonical AB Merge profile summaries.</summary>
    public static IReadOnlyList<CapabilityProfileSummary> GetAbMergeProfileSummaries()
    {
        return GetProfileSummaries(IcWorkflowIds.AbMerge);
    }

    /// <summary>Gets canonical DP Replace profile summaries.</summary>
    public static IReadOnlyList<CapabilityProfileSummary> GetDpReplaceProfileSummaries()
    {
        return GetProfileSummaries(IcWorkflowIds.DpReplace);
    }

    /// <summary>Gets stable authorable profile summaries for one workflow.</summary>
    public static IReadOnlyList<CapabilityProfileSummary> GetProfileSummaries(
        string workflowId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(workflowId);
        CanonicalCapabilityCatalogSnapshot snapshot = WorkbenchHostServices
            .CanonicalCapabilityQuery
            .GetCurrentSnapshot();
        var fixedByKey = snapshot.Capabilities
            .Where(capability => IsAuthorable(capability.Authoring) &&
                StringComparer.Ordinal.Equals(
                    capability.Identity.WorkflowId,
                    workflowId))
            .GroupBy(
                static capability => (
                    capability.Identity.IcId,
                    capability.CompiledComposition.ProfileId))
            .ToDictionary(
                static group => group.Key,
                static group => group
                    .OrderBy(capability => capability.Identity.RouteId, StringComparer.Ordinal)
                    .First());
        var dynamicByKey = snapshot.DynamicRoutes
            .Where(route => IsAuthorable(route.Authoring) &&
                StringComparer.Ordinal.Equals(route.Identity.WorkflowId, workflowId))
            .GroupBy(
                static route => (
                    route.Identity.IcId,
                    route.CompilationContract.ProfileId))
            .ToDictionary(
                static group => group.Key,
                static group => group.First());

        return Array.AsReadOnly(
            fixedByKey
                .Select(static pair => FromCompiled(pair.Value.CompiledComposition))
                .Concat(dynamicByKey.Keys
                    .Where(key => !fixedByKey.ContainsKey(key))
                    .Select(key => CreateDynamicSummary(workflowId, key.IcId)))
                .OrderBy(static summary => summary.IcId, StringComparer.Ordinal)
                .ThenBy(static summary => summary.ProfileId, StringComparer.Ordinal)
                .ToArray());
    }

    /// <summary>Gets all IC ids present in the current authorable publication.</summary>
    public static IReadOnlyList<string> GetIcIds()
    {
        CanonicalCapabilityCatalogSnapshot snapshot = WorkbenchHostServices
            .CanonicalCapabilityQuery
            .GetCurrentSnapshot();
        return Array.AsReadOnly(
            snapshot.Capabilities
                .Where(capability => IsAuthorable(capability.Authoring))
                .Select(static capability => capability.Identity.IcId)
                .Concat(snapshot.DynamicRoutes
                    .Where(route => IsAuthorable(route.Authoring))
                    .Select(static route => route.Identity.IcId))
                .Distinct(StringComparer.Ordinal)
                .Order(StringComparer.Ordinal)
                .ToArray());
    }

    internal static bool IsKnownIcId(string icId)
    {
        return GetIcIds().Contains(
            IcIdentifier.Normalize(icId),
            StringComparer.Ordinal);
    }

    /// <summary>Gets profile-owned IC-number choices for one IC.</summary>
    public static IReadOnlyList<CapabilityNumberChoice> GetNumberSelectionChoices(
        string icId)
    {
        return Array.AsReadOnly(IcNumberChoicePolicy.GetNumberSelectionChoices(
            GetPostbuildProfiles(icId))
            .Select(static choice => new CapabilityNumberChoice(
                choice.Token,
                choice.DisplayLabel))
            .ToArray());
    }

    /// <summary>Gets focused catalog counts for Settings warmup and summary.</summary>
    public static CapabilityCatalogSummary GetCatalogSummary()
    {
        IReadOnlyList<string> icIds = GetIcIds();
        CanonicalCapabilityCatalogSnapshot snapshot = WorkbenchHostServices
            .CanonicalCapabilityQuery
            .GetCurrentSnapshot();
        int ctrlRamCount = snapshot.Capabilities
            .Where(capability => IsAuthorable(capability.Authoring) &&
                StringComparer.Ordinal.Equals(
                    capability.Identity.WorkflowId,
                    IcWorkflowIds.CtrlRamReplace))
            .Select(static capability => capability.Identity.IcId)
            .Concat(snapshot.DynamicRoutes
                .Where(route => IsAuthorable(route.Authoring) &&
                    StringComparer.Ordinal.Equals(
                        route.Identity.WorkflowId,
                        IcWorkflowIds.CtrlRamReplace))
                .Select(static route => route.Identity.IcId))
            .Distinct(StringComparer.Ordinal)
            .Count();
        return new CapabilityCatalogSummary(
            icIds.Count,
            GetStandardMergeProfileSummaries().Count,
            GetDpReplaceProfileSummaries().Count,
            ctrlRamCount);
    }

    internal static CapabilityProfileSummary? FindStandardMergeProfileSummary(
        string icId)
    {
        ArgumentNullException.ThrowIfNull(icId);
        string normalizedIcId = IcIdentifier.Normalize(icId);
        return GetStandardMergeProfileSummaries().FirstOrDefault(profile =>
            StringComparer.Ordinal.Equals(profile.IcId, normalizedIcId));
    }

    internal static IReadOnlyList<LegacyCombinerPostbuildProfile>
        GetPostbuildProfiles(string icId)
    {
        return GetIcIds().Contains(
                IcIdentifier.Normalize(icId),
                StringComparer.Ordinal)
            ? BuiltInPostbuildProfileCatalog.GetProfiles(
                IcIdentifier.Normalize(icId))
            : [];
    }

    internal static bool TryGetDefaultPostbuildProfile(
        string icId,
        out LegacyCombinerPostbuildProfile? postbuildProfile)
    {
        IReadOnlyList<LegacyCombinerPostbuildProfile> profiles =
            GetPostbuildProfiles(icId);
        postbuildProfile = profiles.Count == 0 ? null : profiles[0];
        return postbuildProfile is not null;
    }

    internal static bool TrySelectPostbuildProfileByCommonFwVersion(
        string icId,
        string? commonFwVersion,
        out LegacyCombinerPostbuildProfile? postbuildProfile,
        out string? issue)
    {
        return BuiltInPostbuildProfileCatalog.TrySelectProfileForCommonFwVersion(
            IcIdentifier.Normalize(icId),
            commonFwVersion,
            out postbuildProfile,
            out issue);
    }

    internal static CapabilityProfileSummary FromCompiled(
        CompiledComposition composition)
    {
        ArgumentNullException.ThrowIfNull(composition);
        return new CapabilityProfileSummary(
            composition.ProfileId,
            composition.IcId,
            composition.CompositionKind,
            Array.AsReadOnly(composition.Plan.RequiredInputAddressSpaceIds.ToArray()),
            composition.DefaultOutputFileName,
            composition.IcNumberPolicy,
            CompileSucceeded: true,
            []);
    }

    private static bool IsAuthorable(
        PinnedCapabilityDecision<CapabilityAuthoringAvailability> decision)
    {
        return decision.Value == CapabilityAuthoringAvailability.Available;
    }

    private static CapabilityProfileSummary CreateDynamicSummary(
        string workflowId,
        string icId)
    {
        BuiltInV2Registration registration = workflowId switch
        {
            IcWorkflowIds.StandardMerge =>
                BuiltInV2RegistrationRegistry.StandardMergeByIc[icId],
            IcWorkflowIds.AbMerge => BuiltInV2RegistrationRegistry.AbMergeByIc[icId],
            IcWorkflowIds.DpReplace =>
                BuiltInV2RegistrationRegistry.DpReplaceByIc.Value[icId],
            _ => throw new InvalidOperationException(
                $"Workflow '{workflowId}' has no registered dynamic summary adapter."),
        };
        return registration.CreateProfileSummary();
    }
}
