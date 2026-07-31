using NvtFwCombiner.Application.Authoring;
using NvtFwCombiner.Application.Capabilities;
using NvtFwCombiner.Application.ExternalTools;
using NvtFwCombiner.Application.Metadata;

namespace NvtFwCombiner.Bootstrap;

public static partial class WorkbenchCompositionService
{
    private static CapabilityActionReadinessSnapshot
        CreateGeneralReplaceMissingPostbuildStageReadiness(
            GeneralAuthoringAdmissionResult admission)
    {
        SavedRuleV2ParentBinding parent =
            GetNt51926GeneralReplaceSavedRuleAdmissionContext().ParentBinding;
        var executionBlocker = new CapabilityActionBlocker(
            CapabilityActionReadinessIssueCodes
                .PostbuildStageAuthorityMissing,
            CapabilityReadinessDimension.Execution,
            parent.ProfileId,
            "Selected General Replace targets require POSTBUILD, but the exact Parent does not declare the required stage.",
            CapabilityReadinessNextAction.ReviewCompilation);
        var capability = new CapabilityAdmissionSnapshot(
            $"general-replace:{parent.ProfileId}",
            parent.ProfileContentHash,
            new ResolutionToken(
                $"bundle:{parent.BundleId}:{parent.BundleVersion}:{parent.BundleContentHash}"),
            new AuthoringRevision(1),
            CapabilityAuthoringAvailability.Available,
            executionAdmitted: false,
            CapabilityEvidenceStatus.Missing,
            CapabilityPublicationStatus.Internal,
            executionBlocker);
        var runtime = new RuntimeDependencyReadinessSnapshot(
            capability.RouteId,
            capability.CapabilityFingerprint,
            capability.ResolutionToken,
            capability.AuthoringRevision,
            generation: 1,
            DateTimeOffset.UnixEpoch,
            []);
        return CapabilityActionReadinessResolver.Resolve(
            capability,
            [
                new CapabilityChildReadiness(
                    WorkbenchSlotIds.ReplaceBase,
                    ResolvedChildReadiness.Ready),
                .. admission.InputResources.Select(static input =>
                    new CapabilityChildReadiness(
                        input.SlotId,
                        ResolvedChildReadiness.Ready)),
            ],
            runtime,
            currentRuntimeDependencyGeneration: 1);
    }
}
