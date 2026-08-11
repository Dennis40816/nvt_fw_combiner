using NvtFwCombiner.Domain.Composition;
using NvtFwCombiner.Domain.Firmware;

namespace NvtFwCombiner.Profiles.V2;

internal static class TrustedProfileBundleCatalogTestExtensions
{
    internal static V2CompositionPlanCompileResult Compile(
        this TrustedProfileBundleCatalog catalog,
        string profileId,
        string profileVersion,
        string memberId,
        string modeId,
        long? requestedMapCapacity = null)
    {
        return catalog.Compile(
            profileId,
            profileVersion,
            memberId,
            modeId,
            requestedMapCapacity,
            requestedTopology: null,
            []);
    }

    internal static V2CompositionPlanCompileResult Compile(
        this TrustedProfileBundleCatalog catalog,
        string profileId,
        string profileVersion,
        string memberId,
        string modeId,
        long? requestedMapCapacity,
        IReadOnlyList<FirmwareArtifactPayload> resolutionArtifacts,
        IReadOnlyCollection<string>? selectedInputSlotIds = null)
    {
        return catalog.Compile(
            profileId,
            profileVersion,
            memberId,
            modeId,
            requestedMapCapacity,
            requestedTopology: null,
            resolutionArtifacts,
            selectedInputSlotIds);
    }

    internal static V2CompositionPlanCompileResult CompileRuntimeReferenceReplace(
        this TrustedProfileBundleCatalog catalog,
        string profileId,
        string profileVersion,
        string memberId,
        V2RuntimeReferenceReplaceCompileRequest request)
    {
        return catalog.CompileRuntimeReferenceReplace(
            profileId,
            profileVersion,
            memberId,
            ExperienceIds.GeneralReplace,
            requestedTopology: null,
            [],
            request);
    }

    internal static V2CompositionPlanCompileResult CompileRuntimeReferenceReplace(
        this TrustedProfileBundleCatalog catalog,
        string profileId,
        string profileVersion,
        string memberId,
        string experienceId,
        TopologySelection? requestedTopology,
        V2RuntimeReferenceReplaceCompileRequest request)
    {
        return catalog.CompileRuntimeReferenceReplace(
            profileId,
            profileVersion,
            memberId,
            experienceId,
            requestedTopology,
            [],
            request);
    }
}
