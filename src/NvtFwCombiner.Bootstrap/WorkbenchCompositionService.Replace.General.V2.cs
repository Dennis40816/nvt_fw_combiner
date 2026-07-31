using NvtFwCombiner.Application.FlashMaps;
using NvtFwCombiner.Application.Authoring;
using NvtFwCombiner.Domain.Composition;
using NvtFwCombiner.Domain.Firmware;
using NvtFwCombiner.Profiles.V2;

namespace NvtFwCombiner.Bootstrap;

public static partial class WorkbenchCompositionService
{
    private const string Nt51926GeneralReplaceBundleId =
        "nt51926-ctrlram-replace-candidate";
    internal const string Nt51926GeneralReplaceDpProfileId =
        "nt51926-general-replace-dp-single-candidate";
    internal const string Nt51926GeneralReplaceDpProfileVersion = "0.1.0";
    internal const string Nt51926GeneralReplaceIcId = "NT51926";
    private const string Nt51926GeneralReplaceReferenceSpaceId = "reference-image";

    private static bool IsNt51926GeneralReplaceDpV2Route(
        string icId,
        GeneralReplaceRunContext context,
        IReadOnlyList<TpFlashMapRegion> regions,
        IReadOnlyList<ExplicitMapping> mappings)
    {
        if (!StringComparer.Ordinal.Equals(icId, "NT51926") ||
            context.Selection.Mode != IcNumberInputMode.SingleSelector ||
            context.MappingDraft.Rows.Any(static row =>
                row.Source.Kind != GeneralMappingSourceKind.FileArtifact))
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
        return BuiltInV2BundleRegistry.All[Nt51926GeneralReplaceBundleId].CompileRuntimeReferenceReplace(
            Nt51926GeneralReplaceDpProfileId,
            Nt51926GeneralReplaceDpProfileVersion,
            Nt51926GeneralReplaceIcId,
            ExperienceIds.GeneralReplace,
            requestedTopology: null,
            new V2RuntimeReferenceReplaceCompileRequest(bindings, mappings));
    }

    internal static IReadOnlyList<FirmwareImageMap>
        GetNt51926GeneralReplaceSupportMaps(
            out IcNumberInputMode? icNumberInputMode,
            out IReadOnlyList<CompositionIssue> issues)
    {
        return BuiltInV2BundleRegistry.All[Nt51926GeneralReplaceBundleId]
            .GetMapVariants(
                Nt51926GeneralReplaceDpProfileId,
                Nt51926GeneralReplaceDpProfileVersion,
                Nt51926GeneralReplaceIcId,
                ExperienceIds.GeneralReplace,
                out icNumberInputMode,
                out issues);
    }

    internal static SavedRuleV2GeneralReplaceAdmissionContext
        GetNt51926GeneralReplaceSavedRuleAdmissionContext()
    {
        return BuiltInV2BundleRegistry.All[Nt51926GeneralReplaceBundleId]
            .GetGeneralReplaceSavedRuleAdmissionContext(
                Nt51926GeneralReplaceDpProfileId);
    }

    private static SavedRuleV2GeneralReplaceRuntimeAuthority
        GetNt51926GeneralReplaceRuntimeAuthority()
    {
        return BuiltInV2BundleRegistry.All[Nt51926GeneralReplaceBundleId]
            .GetGeneralReplaceRuntimeAuthority(
                Nt51926GeneralReplaceDpProfileId);
    }
}
