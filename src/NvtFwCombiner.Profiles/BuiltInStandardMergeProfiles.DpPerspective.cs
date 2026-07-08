using NvtFwCombiner.Domain.Composition;

namespace NvtFwCombiner.Profiles;

public static partial class BuiltInStandardMergeProfiles
{
    /// <summary>Creates an NT51950/NT51951 DP Perspective profile matching the selected DP input length.</summary>
    public static CompositionProfileDefinition CreateDpPerspectiveProfileForInputLength(string icId, long dpInputLength)
    {
        string icNumber = DpPerspectiveCatalog.NormalizeIcNumber(icId);
        return DpPerspectiveCatalog.IsSupportedContainerLength(dpInputLength)
            ? CreateDpPerspectiveProfile(icNumber, dpInputLength)
            : throw new ArgumentOutOfRangeException(
                nameof(dpInputLength),
                dpInputLength,
                $"NT51950/NT51951 Standard Merge accepts DP input lengths {DpPerspectiveCatalog.FormatSupportedLengths()}.");
    }

    /// <summary>Returns true when a profile is the NT51950/NT51951 DP Perspective Standard Merge template.</summary>
    public static bool IsDpPerspectiveStandardMergeProfile(CompositionProfileDefinition profile)
    {
        ArgumentNullException.ThrowIfNull(profile);

        return profile.ProfileId.EndsWith("-standard-merge-dp-perspective", StringComparison.Ordinal) &&
            DpPerspectiveCatalog.IsSupportedIc(profile.IcId);
    }

    private static CompositionProfileDefinition CreateDpPerspectiveProfile(string icNumber)
    {
        return CreateDpPerspectiveProfile(icNumber, DpPerspectiveCatalog.MaxContainerLength);
    }

    private static CompositionProfileDefinition CreateDpPerspectiveProfile(string icNumber, long outputLength)
    {
        var outputRange = new ByteRange(0, outputLength);
        ByteRange tpOverlay = DpPerspectiveCatalog.TpOverlayRange;
        return new CompositionProfileDefinition(
            $"nt{icNumber}-standard-merge-dp-perspective",
            "0.5.1",
            $"NT{icNumber}",
            "standard-merge",
            CompositionKind.Merge,
            "standard-merge",
            $"nt{icNumber}-standard-merge-dp-perspective.bin",
            ImageInitialization.Blank("output-image", outputLength, 0x00),
            [
                new AddressSpace(
                    "dp-input",
                    outputLength,
                    AddressSpaceMutability.Immutable,
                    allowedInputLengths: [outputLength]),
                new AddressSpace("tp-input", DpPerspectiveCatalog.TpInputLength, AddressSpaceMutability.Immutable),
                new AddressSpace("output-image", outputLength, AddressSpaceMutability.Mutable),
            ],
            [
                CompositionOperation.CopyRange(
                    "copy-dp-container",
                    100,
                    "dp-input",
                    outputRange,
                    "output-image",
                    outputRange,
                    OverlapPolicy.Reject,
                    "Copy the NT51950/NT51951 DP Perspective bytes using the selected DP input length as the output length."),
                CompositionOperation.CopyRange(
                    "overlay-tp",
                    200,
                    "tp-input",
                    tpOverlay,
                    "output-image",
                    tpOverlay,
                    OverlapPolicy.ReplaceExisting,
                    $"Overlay TP FW {DpPerspectiveCatalog.FormatRange(DpPerspectiveCatalog.TpOverlayRange)} and leave customer information {DpPerspectiveCatalog.FormatRange(DpPerspectiveCatalog.CustomerInfoPreserveRange)} from DP."),
            ],
            [
                new ProfileRegion(
                    "dp-perspective-container",
                    "output-image",
                    outputRange,
                    RegionAtomicity.Partitioned,
                    RegionWritePolicy.DeclaredParts,
                    classificationTags: ["dp", "tp-overlay"]),
            ],
            [
                new RegionAccessRule(
                    "dp-perspective-container",
                    RegionAccessKind.Parts,
                    "NT51950/NT51951 Standard Merge first copies DP, then overlays the declared TP range."),
            ]);
    }
}
