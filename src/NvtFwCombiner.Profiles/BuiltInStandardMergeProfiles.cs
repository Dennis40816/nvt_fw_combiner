using NvtFwCombiner.Domain.Composition;

namespace NvtFwCombiner.Profiles;

/// <summary>Built-in synthetic standard merge profiles used as executable contract evidence.</summary>
public static class BuiltInStandardMergeProfiles
{
    /// <summary>
    /// Synthetic standard merge profile that copies DP and TP inputs into non-overlapping output ranges.
    /// </summary>
    public static CompositionProfileDefinition SyntheticStandardMerge { get; } =
        new(
            "synthetic-standard-merge",
            "0.3.0",
            "NT-SYNTHETIC",
            "standard-merge",
            CompositionKind.Merge,
            "standard-merge",
            "synthetic-standard-merge.bin",
            ImageInitialization.Blank("output-image", 8, 0xFF),
            [
                new AddressSpace("dp-input", 4, AddressSpaceMutability.Immutable),
                new AddressSpace("tp-input", 4, AddressSpaceMutability.Immutable),
                new AddressSpace("output-image", 8, AddressSpaceMutability.Mutable),
            ],
            [
                CompositionOperation.CopyRange(
                    "copy-dp",
                    100,
                    "dp-input",
                    new ByteRange(0, 4),
                    "output-image",
                    new ByteRange(0, 4),
                    OverlapPolicy.Reject,
                    "Copy synthetic DP input into the output DP range."),
                CompositionOperation.CopyRange(
                    "copy-tp",
                    200,
                    "tp-input",
                    new ByteRange(0, 4),
                    "output-image",
                    new ByteRange(4, 4),
                    OverlapPolicy.Reject,
                    "Copy synthetic TP input into the output TP range."),
            ]);
}
