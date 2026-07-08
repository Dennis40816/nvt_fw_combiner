using NvtFwCombiner.Domain.Composition;

namespace NvtFwCombiner.Profiles;

public static partial class BuiltInStandardMergeProfiles
{
    /// <summary>Standard merge profiles for NT51950/NT51951 DP Perspective containers.</summary>
    public static IReadOnlyList<CompositionProfileDefinition> DpPerspectiveStandardMergeProfiles { get; } =
    [
        .. DpPerspectiveCatalog.SupportedIcIds.Select(icId => CreateDpPerspectiveProfile(icId)),
    ];

    /// <summary>Creates an NT51950/NT51951 DP Perspective profile matching the selected DP input length.</summary>
    public static CompositionProfileDefinition CreateDpPerspectiveProfileForInputLength(string icId, long dpInputLength)
    {
        string normalizedIc = DpPerspectiveCatalog.NormalizeIcId(icId);
        return DpPerspectiveCatalog.IsSupportedContainerLength(dpInputLength)
            ? CreateDpPerspectiveProfile(normalizedIc, dpInputLength)
            : throw new ArgumentOutOfRangeException(
                nameof(dpInputLength),
                dpInputLength,
                $"{DpPerspectiveCatalog.FormatSupportedIcIds()} Standard Merge accepts DP input lengths {DpPerspectiveCatalog.FormatSupportedLengths()}.");
    }

    /// <summary>Returns true when a profile is the NT51950/NT51951 DP Perspective Standard Merge template.</summary>
    public static bool IsDpPerspectiveStandardMergeProfile(CompositionProfileDefinition profile)
    {
        ArgumentNullException.ThrowIfNull(profile);

        return profile.ProfileId.EndsWith("-standard-merge-dp-perspective", StringComparison.Ordinal) &&
            DpPerspectiveCatalog.IsSupportedIc(profile.IcId);
    }

    private static CompositionProfileDefinition CreateDpPerspectiveProfile(string icId)
    {
        return CreateDpPerspectiveProfile(icId, DpPerspectiveCatalog.MaxContainerLength);
    }

    private static CompositionProfileDefinition CreateDpPerspectiveProfile(string icId, long outputLength)
    {
        string normalizedIc = DpPerspectiveCatalog.NormalizeIcId(icId);
        string icNumber = normalizedIc[2..];
        var outputRange = new ByteRange(0, outputLength);
        ByteRange tpOverlay = DpPerspectiveCatalog.TpOverlayRange;
        return new CompositionProfileDefinition(
            $"nt{icNumber}-standard-merge-dp-perspective",
            "0.5.1",
            normalizedIc,
            IcWorkflowIds.StandardMerge,
            CompositionKind.Merge,
            IcWorkflowIds.StandardMerge,
            $"nt{icNumber}-standard-merge-dp-perspective.bin",
            ImageInitialization.Blank(CompositionAddressSpaceIds.OutputImage, outputLength, 0x00),
            [
                new AddressSpace(
                    CompositionAddressSpaceIds.DpInput,
                    outputLength,
                    AddressSpaceMutability.Immutable,
                    allowedInputLengths: [outputLength]),
                new AddressSpace(CompositionAddressSpaceIds.TpInput, DpPerspectiveCatalog.TpInputLength, AddressSpaceMutability.Immutable),
                new AddressSpace(CompositionAddressSpaceIds.OutputImage, outputLength, AddressSpaceMutability.Mutable),
            ],
            [
                CompositionOperation.CopyRange(
                    DpPerspectiveCatalog.CopyDpContainerOperationId,
                    DpPerspectiveCatalog.CopyDpContainerSequence,
                    CompositionAddressSpaceIds.DpInput,
                    outputRange,
                    CompositionAddressSpaceIds.OutputImage,
                    outputRange,
                    OverlapPolicy.Reject,
                    $"Copy the {DpPerspectiveCatalog.FormatSupportedIcIds()} DP Perspective bytes using the selected DP input length as the output length."),
                CompositionOperation.CopyRange(
                    DpPerspectiveCatalog.OverlayTpOperationId,
                    DpPerspectiveCatalog.OverlayTpSequence,
                    CompositionAddressSpaceIds.TpInput,
                    tpOverlay,
                    CompositionAddressSpaceIds.OutputImage,
                    tpOverlay,
                    OverlapPolicy.ReplaceExisting,
                    $"Overlay TP FW {DpPerspectiveCatalog.FormatRange(DpPerspectiveCatalog.TpOverlayRange)} and leave customer information {DpPerspectiveCatalog.FormatRange(DpPerspectiveCatalog.CustomerInfoPreserveRange)} from DP."),
            ],
            [
                new ProfileRegion(
                    DpPerspectiveCatalog.ContainerRegionId,
                    CompositionAddressSpaceIds.OutputImage,
                    outputRange,
                    RegionAtomicity.Partitioned,
                    RegionWritePolicy.DeclaredParts,
                    classificationTags: ["dp", "tp-overlay"]),
            ],
            [
                new RegionAccessRule(
                    DpPerspectiveCatalog.ContainerRegionId,
                    RegionAccessKind.Parts,
                    $"{DpPerspectiveCatalog.FormatSupportedIcIds()} Standard Merge first copies DP, then overlays the declared TP range."),
            ]);
    }
}
