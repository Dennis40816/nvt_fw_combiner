using NvtFwCombiner.Domain.Composition;
using NvtFwCombiner.Profiles;

namespace NvtFwCombiner.TestSupport;

/// <summary>Synthetic Replace definitions for compiler and application tests only.</summary>
public static class SyntheticReplaceProfiles
{
    private const string SyntheticIc = "NT-SYNTHETIC";

    /// <inheritdoc/>
    public static CompositionProfileDefinition General { get; } =
        new(
            "synthetic-general-replace",
            "0.5.0",
            SyntheticIc,
            IcWorkflowIds.GeneralReplace,
            CompositionKind.Replace,
            IcWorkflowIds.GeneralReplace,
            "synthetic-general-replace.bin",
            ImageInitialization.Reference(CompositionAddressSpaceIds.OutputImage, CompositionAddressSpaceIds.ReferenceBase, 8),
            [
                new AddressSpace(CompositionAddressSpaceIds.ReferenceBase, 8, AddressSpaceMutability.Immutable),
                new AddressSpace(CompositionAddressSpaceIds.OutputImage, 8, AddressSpaceMutability.Mutable),
            ],
            [],
            [
                new ProfileRegion(
                    "protected-header",
                    CompositionAddressSpaceIds.OutputImage,
                    new ByteRange(0, 1),
                    RegionAtomicity.Whole,
                    RegionWritePolicy.Forbidden,
                    classificationTags: ["header", "protected"]),
                new ProfileRegion(
                    "general-payload",
                    CompositionAddressSpaceIds.OutputImage,
                    new ByteRange(1, 7),
                    RegionAtomicity.ExplicitMapping,
                    RegionWritePolicy.GeneralExplicit,
                    classificationTags: ["general"]),
            ],
            [
                new RegionAccessRule("protected-header", RegionAccessKind.Hidden, "Synthetic protected header."),
                new RegionAccessRule("general-payload", RegionAccessKind.ExplicitRange, "Synthetic general mapping area."),
            ],
            IcNumberInputMode.SingleSelector);
}
