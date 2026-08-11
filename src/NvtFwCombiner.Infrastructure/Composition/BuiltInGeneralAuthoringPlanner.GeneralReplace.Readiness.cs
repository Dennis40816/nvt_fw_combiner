using NvtFwCombiner.Application.Authoring;
using NvtFwCombiner.Application.Capabilities;
using NvtFwCombiner.Application.ExternalTools;
using NvtFwCombiner.Application.FlashMaps;
using NvtFwCombiner.Domain.Composition;

namespace NvtFwCombiner.Infrastructure.Composition;

/// <summary>Resolves profile-owned General Replace compilation and postbuild planning.</summary>
internal sealed partial class BuiltInGeneralAuthoringPlanner
{
    internal static LegacyCombinerPostbuildCommandPlan? TryPlanGeneralReplacePostbuild(
        string icId,
        IcNumberSelection selection,
        long capacity,
        IReadOnlyList<TpFlashMapRegion> regions,
        IReadOnlyList<ExplicitMapping> mappings,
        bool profileResolved,
        LegacyCombinerPostbuildProfile? profile,
        CompositionIssue? profileIssue,
        out bool touchesTp,
        out CompositionIssue? issue)
    {
        touchesTp = GeneralReplaceTouchesTpRegion(regions, mappings);
        issue = null;
        if (!touchesTp)
        {
            return null;
        }

        if (!profileResolved)
        {
            issue = profileIssue;
            return null;
        }

        LegacyCombinerPostbuildCommandPlan plan;
        try
        {
            plan = profile!.ResolvePlan(selection);
        }
        catch (ArgumentException exception)
        {
            issue = new CompositionIssue(
                CompositionPlanningIssueCodes.ReplaceGeneralIcNumberUnsupported,
                exception.Message,
                "number");
            return null;
        }

        long requiredCapacity = LegacyCombinerPostbuildPlanCompiler.CalculateRequiredCapacity(plan, []);
        if (capacity < requiredCapacity)
        {
            issue = new CompositionIssue(
                CompositionIssueCodes.InputAddressSpaceLengthMismatch,
                $"Base flash BIN is too short for {icId} General Replace postbuild (actual {capacity} bytes, required at least {requiredCapacity} bytes).",
                CompositionSlotIds.ReplaceBase);
            return null;
        }

        if (LegacyCombinerPostbuildPlanCompiler.GetAllowedWriteRangeSectionsForInPlaceRefresh(
                plan,
                capacity).Count == 0)
        {
            issue = new CompositionIssue(
                CompositionPlanningIssueCodes.ReplaceGeneralPostbuildWriteRangeMissing,
                "No approved postbuild write range could be derived for TP-touching General Replace.",
                "postbuild");
            return null;
        }

        return plan;
    }

    private static bool GeneralReplaceTouchesTpRegion(
        IReadOnlyList<TpFlashMapRegion> regions,
        IReadOnlyList<ExplicitMapping> mappings)
    {
        return mappings.Any(mapping => regions.Any(region =>
            (region.Kind == TpFlashMapRegionKind.CtrlRam ||
             region.Tags.Any(tag =>
                 string.Equals(tag, "tp", StringComparison.OrdinalIgnoreCase) ||
                 tag.StartsWith("tp-", StringComparison.OrdinalIgnoreCase))) &&
            region.Range.Overlaps(mapping.TargetRange)));
    }

    internal static bool TryResolveGeneralReplaceCompiledRoute(
        ICanonicalCapabilityQuery catalog,
        GeneralReplaceV2Registration? registration,
        long capacity,
        IcNumberSelection selection,
        GeneralMappingDraftState mappingDraft,
        IReadOnlyList<TpFlashMapRegion> regions,
        IReadOnlyList<ExplicitMapping> mappings,
        IReadOnlyList<AddressSpace> addressSpaces,
        out ResolvedCapability? resolvedCapability,
        out IReadOnlyList<CompositionIssue> issues)
    {
        resolvedCapability = null;
        if (registration is null ||
            !IsGeneralReplaceDpV2Route(selection, mappingDraft, regions, mappings))
        {
            issues = [new CompositionIssue(
                CompositionPlanningIssueCodes.ReplaceWorkflowNotSupported,
                "The selected General Replace shape has no exact evidence-backed V2 route.",
                "mapping")];
            return false;
        }

        if (!TryResolveGeneralReplaceRoute(
                catalog,
                registration,
                capacity,
                out ResolvedCapabilityRoute? route,
                out issues))
        {
            return false;
        }

        Profiles.V2.V2CompositionPlanCompileResult compile = registration.Compile(
            capacity,
            addressSpaces,
            mappings);
        if (compile.CompiledComposition is null)
        {
            issues = compile.Issues;
            return false;
        }

        resolvedCapability = route!.BindCompilation(
            compile.CompiledComposition,
            registration.CreateMetadataPlan(compile.CompiledComposition));
        issues = [];
        return true;
    }

    private static bool TryResolveGeneralReplaceRoute(
        ICanonicalCapabilityQuery catalog,
        GeneralReplaceV2Registration registration,
        long capacity,
        out ResolvedCapabilityRoute? route,
        out IReadOnlyList<CompositionIssue> issues)
    {
        IReadOnlyList<Domain.Firmware.FirmwareImageMap> maps = registration.GetMapVariants(
            out _,
            out IReadOnlyList<CompositionIssue> mapIssues);
        Domain.Firmware.FirmwareImageMap? map = mapIssues.Count == 0
            ? maps.SingleOrDefault(candidate => candidate.CapacityBytes == capacity)
            : null;
        if (map is null)
        {
            route = null;
            issues = mapIssues.Count == 0
                ? [new CompositionIssue(
                    CapabilityCatalogIssueCodes.RouteUnavailable,
                    "The selected General Replace capacity has no canonical map route.")]
                : mapIssues;
            return false;
        }

        var identity = new CapabilityRouteIdentity(
            registration.IcId,
            ExperienceIds.GeneralReplace,
            "1-ic",
            map.MapId);
        CapabilityRouteResolutionResult resolution =
            catalog.ResolveDynamicRoute(identity.RouteId);
        route = resolution.Route;
        issues = resolution.Succeeded
            ? []
            : [new CompositionIssue(resolution.Issue!.Code, resolution.Issue.Message)];
        return resolution.Succeeded;
    }
}
