using NvtFwCombiner.Domain.Composition;
using NvtFwCombiner.Domain.Firmware;
using NvtFwCombiner.Profiles.V2;

namespace NvtFwCombiner.Bootstrap;

public static partial class WorkbenchCompositionService
{
    private const int Nt51926CtrlRamCandidateCapacity = 0x40000;

    private static readonly BuiltInV2Bundle s_nt51926CtrlRamReplaceCandidateV2Bundle = new(
        "nt51926-ctrlram-replace-candidate",
        "83f22d939a257046a6b7357c98c34b1e953687b28545612d749a16a9323c0736");

    /// <summary>
    /// Compiles the isolated NT51926 CtrlRAM V2 candidate with the immutable reference image needed to resolve
    /// the canonical FWConfig Backup locator. This boundary does not route the candidate into Preview or Build.
    /// </summary>
    internal static V2CompositionPlanCompileResult CompileNt51926CtrlRamReplaceV2Candidate(
        ReadOnlyMemory<byte> referenceBaseBytes)
    {
        var referenceBase = new FirmwareArtifactPayload(
            CompositionAddressSpaceIds.ReferenceBase,
            referenceBaseBytes.Span);
        return s_nt51926CtrlRamReplaceCandidateV2Bundle.Compile(
            "nt51926-ctrlram-replace-fw141-cascade",
            "0.2.0",
            "NT51926",
            ExperienceIds.CtrlRamReplace,
            Nt51926CtrlRamCandidateCapacity,
            [referenceBase]);
    }
}
