using NvtFwCombiner.Domain.Composition;
using NvtFwCombiner.Profiles;

namespace NvtFwCombiner.TestSupport;

/// <summary>Small, non-production composition definitions shared by compiler and runner tests.</summary>
public static class SyntheticCompositionProfiles
{
    /// <summary>Creates a fixed Standard Merge profile with two non-overlapping input copies.</summary>
    public static CompositionProfileDefinition CreateStandardMerge()
    {
        return new CompositionProfileDefinition(
            "synthetic-standard-merge",
            "0.3.0",
            "NT-SYNTHETIC",
            IcWorkflowIds.StandardMerge,
            CompositionKind.Merge,
            IcWorkflowIds.StandardMerge,
            "synthetic-standard-merge.bin",
            ImageInitialization.Blank(CompositionAddressSpaceIds.OutputImage, 8, 0xFF),
            [
                new AddressSpace(CompositionAddressSpaceIds.DpInput, 4, AddressSpaceMutability.Immutable),
                new AddressSpace(CompositionAddressSpaceIds.TpInput, 4, AddressSpaceMutability.Immutable),
                new AddressSpace(CompositionAddressSpaceIds.OutputImage, 8, AddressSpaceMutability.Mutable),
            ],
            [
                CompositionOperation.CopyRange(
                    "copy-dp",
                    100,
                    CompositionAddressSpaceIds.DpInput,
                    new ByteRange(0, 4),
                    CompositionAddressSpaceIds.OutputImage,
                    new ByteRange(0, 4),
                    OverlapPolicy.Reject,
                    "Copy synthetic DP input into the output DP range."),
                CompositionOperation.CopyRange(
                    "copy-tp",
                    200,
                    CompositionAddressSpaceIds.TpInput,
                    new ByteRange(0, 4),
                    CompositionAddressSpaceIds.OutputImage,
                    new ByteRange(4, 4),
                    OverlapPolicy.Reject,
                    "Copy synthetic TP input into the output TP range."),
            ]);
    }
}
