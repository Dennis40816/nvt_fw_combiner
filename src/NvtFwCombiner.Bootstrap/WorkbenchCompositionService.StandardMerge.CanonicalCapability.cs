using NvtFwCombiner.Application.Capabilities;
using NvtFwCombiner.Domain.Composition;
using NvtFwCombiner.Profiles;

namespace NvtFwCombiner.Bootstrap;

public static partial class WorkbenchCompositionService
{
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

    internal static CapabilityResolutionResult
        ResolveCanonicalDpReplaceCapability(string icId)
    {
        return s_canonicalCapabilityCatalog.ResolveUniqueRoute(
            icId,
            IcWorkflowIds.DpReplace,
            SelectorFreeIcCountVariant);
    }

    private static bool TryCompilePublishedStandardMergeCapability(
        string icId,
        out CompiledComposition? composition,
        out IReadOnlyList<CompositionIssue> issues)
    {
        CapabilityResolutionResult resolution =
            ResolveCanonicalStandardMergeCapability(icId);
        if (StringComparer.Ordinal.Equals(
                resolution.Issue?.Code,
                CapabilityCatalogIssueCodes.RouteUnavailable))
        {
            composition = null;
            issues = [];
            return false;
        }

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

    private static bool TryCompilePublishedDpReplaceCapability(
        string icId,
        long baseCapacity,
        out CompiledComposition? composition,
        out IReadOnlyList<CompositionIssue> issues)
    {
        CapabilityResolutionResult resolution =
            ResolveCanonicalDpReplaceCapability(icId);
        if (StringComparer.Ordinal.Equals(
                resolution.Issue?.Code,
                CapabilityCatalogIssueCodes.RouteUnavailable))
        {
            composition = null;
            issues = [];
            return false;
        }

        composition = resolution.Capability?.CompiledComposition;
        long? resolvedCapacity = composition?.V2Details?.Provenance
            .ResolvedMap.CapacityBytes;
        if (composition is not null && resolvedCapacity != baseCapacity)
        {
            composition = null;
            issues =
            [
                new CompositionIssue(
                    CompositionIssueCodes.InputAddressSpaceLengthMismatch,
                    $"{icId} DP Replace base flash BIN length must be 0x{resolvedCapacity:X} (actual 0x{baseCapacity:X})."),
            ];
            return true;
        }

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
