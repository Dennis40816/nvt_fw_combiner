using NvtFwCombiner.Application.Capabilities;
using NvtFwCombiner.Domain.Composition;
using NvtFwCombiner.Profiles;

namespace NvtFwCombiner.Bootstrap;

public static partial class WorkbenchCompositionService
{
    private const string CanonicalStandardMergePilotIcId = "NT51929";
    private const string SelectorFreeIcCountVariant = "selector-free";

    private static readonly CanonicalCapabilityCatalog
        s_canonicalCapabilityCatalog = CreateCanonicalCapabilityCatalog();

    internal static CapabilityCatalogReloadResult
        ReloadCanonicalCapabilityCatalog(CancellationToken cancellationToken)
    {
        return s_canonicalCapabilityCatalog.Reload(cancellationToken);
    }

    internal static CapabilityResolutionResult
        ResolveCanonicalStandardMergeCapability(string icId)
    {
        return s_canonicalCapabilityCatalog.ResolveUniqueRoute(
            icId,
            IcWorkflowIds.StandardMerge,
            SelectorFreeIcCountVariant);
    }

    private static bool IsCanonicalStandardMergePilot(string icId)
    {
        return StringComparer.Ordinal.Equals(
            icId,
            CanonicalStandardMergePilotIcId);
    }

    private static bool TryCompileCanonicalStandardMerge(
        string icId,
        out CompiledComposition? composition,
        out IReadOnlyList<CompositionIssue> issues)
    {
        CapabilityResolutionResult resolution =
            ResolveCanonicalStandardMergeCapability(icId);
        composition = resolution.Capability?.CompiledComposition;
        issues = resolution.Issue is null
            ? []
            :
            [
                new CompositionIssue(
                    resolution.Issue.Code,
                    resolution.Issue.Message),
            ];
        return true;
    }

    private static CanonicalCapabilityCatalog CreateCanonicalCapabilityCatalog()
    {
        var catalog = new CanonicalCapabilityCatalog(
            new CanonicalCapabilityCatalogMigrationSource());
        _ = catalog.Reload();
        return catalog;
    }
}
