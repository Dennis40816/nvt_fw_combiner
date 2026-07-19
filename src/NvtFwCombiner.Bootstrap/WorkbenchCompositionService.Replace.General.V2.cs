using NvtFwCombiner.Application.FlashMaps;
using NvtFwCombiner.Domain.Composition;
using NvtFwCombiner.Profiles.V2;

namespace NvtFwCombiner.Bootstrap;

public static partial class WorkbenchCompositionService
{
    private const string Nt51926GeneralReplaceDpProfileId = "nt51926-general-replace-dp-single-candidate";
    private const string Nt51926GeneralReplaceReferenceSpaceId = "reference-image";

    private static bool IsNt51926GeneralReplaceDpV2Route(
        string icId,
        GeneralReplaceRunContext context,
        IReadOnlyList<TpFlashMapRegion> regions,
        IReadOnlyList<ExplicitMapping> mappings)
    {
        if (!StringComparer.Ordinal.Equals(icId, "NT51926") ||
            context.Selection.Mode != IcNumberInputMode.SingleSelector ||
            context.SelectedPatches.Length != 0)
        {
            return false;
        }

        TpFlashMapRegion[] dpRegions = [.. regions.Where(static region => region.Kind == TpFlashMapRegionKind.Dp)];
        return mappings.Count > 0 && mappings.All(mapping =>
            dpRegions.Any(region => region.Range.Contains(mapping.TargetRange)));
    }

    private static V2CompositionPlanCompileResult CompileNt51926GeneralReplaceDpV2(
        GeneralReplaceRunContext context,
        IReadOnlyList<AddressSpace> sourceSpaces,
        IReadOnlyList<ExplicitMapping> mappings)
    {
        V2RuntimeReferenceReplaceInputBinding[] bindings =
        [
            new(Nt51926GeneralReplaceReferenceSpaceId, "reference", context.Capacity),
            .. sourceSpaces.Select(static source =>
                new V2RuntimeReferenceReplaceInputBinding(source.AddressSpaceId, "source", source.Length)),
        ];
        return BuiltInV2BundleRegistry.All["nt51926-ctrlram-replace-candidate"].CompileRuntimeReferenceReplace(
            Nt51926GeneralReplaceDpProfileId,
            "0.1.0",
            "NT51926",
            ExperienceIds.GeneralReplace,
            requestedTopology: null,
            new V2RuntimeReferenceReplaceCompileRequest(bindings, mappings));
    }
}
