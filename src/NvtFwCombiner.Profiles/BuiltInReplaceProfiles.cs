using NvtFwCombiner.Domain.Composition;

namespace NvtFwCombiner.Profiles;

/// <summary>Built-in Replace profiles used as executable command and contract evidence.</summary>
public static class BuiltInReplaceProfiles
{
    private const string SyntheticIc = "NT-SYNTHETIC";
    private const long Nt51950DpContainerLength = 0x100000;
    private static readonly long[] Nt51950AllowedDpReplacementLengths = [0x40000, 0x80000, Nt51950DpContainerLength];
    private static readonly ByteRange Nt51950DpContainerRange = new(0, Nt51950DpContainerLength);
    private static readonly ByteRange Nt51950TpRestoreRange = ByteRange.FromStartEndExclusive(0x0A000, 0x37000);
    private static readonly ByteRange Nt51950CustomerInfoPreserveRange = new(0x37000, 0x1000);

    /// <summary>All built-in Replace profiles in stable command display order.</summary>
    public static IReadOnlyList<CompositionProfileDefinition> All =>
    [
        SyntheticDpReplace,
        SyntheticCtrlRamReplace,
        SyntheticGeneralReplace,
        Nt51950DpReplace,
        Nt51951DpReplace,
    ];

    /// <summary>Synthetic DP Replace profile with separate DP and LD replacement payloads.</summary>
    public static CompositionProfileDefinition SyntheticDpReplace { get; } =
        new(
            "synthetic-dp-replace",
            "0.5.0",
            SyntheticIc,
            "dp-replace",
            CompositionKind.Replace,
            "dp-replace",
            "synthetic-dp-replace.bin",
            ImageInitialization.Reference("output-image", "reference-base", 8),
            [
                new AddressSpace("reference-base", 8, AddressSpaceMutability.Immutable),
                new AddressSpace("dp-replacement", 4, AddressSpaceMutability.Immutable, inputPaddingByte: 0xFF),
                new AddressSpace("ld-replacement", 2, AddressSpaceMutability.Immutable, inputPaddingByte: 0xFF),
                new AddressSpace("output-image", 8, AddressSpaceMutability.Mutable),
            ],
            [
                CompositionOperation.ReplaceRange(
                    "replace-dp",
                    100,
                    "dp-replacement",
                    new ByteRange(0, 4),
                    "output-image",
                    new ByteRange(0, 4),
                    OverlapPolicy.Reject,
                    "Replace synthetic DP declared partition."),
                CompositionOperation.ReplaceRange(
                    "replace-ld",
                    110,
                    "ld-replacement",
                    new ByteRange(0, 2),
                    "output-image",
                    new ByteRange(6, 2),
                    OverlapPolicy.Reject,
                    "Replace synthetic LD declared partition under DP Replace."),
            ],
            [
                new ProfileRegion(
                    "dp",
                    "output-image",
                    new ByteRange(0, 4),
                    RegionAtomicity.Partitioned,
                    RegionWritePolicy.DeclaredParts,
                    classificationTags: ["dp"]),
                new ProfileRegion(
                    "ld",
                    "output-image",
                    new ByteRange(6, 2),
                    RegionAtomicity.Partitioned,
                    RegionWritePolicy.DeclaredParts,
                    classificationTags: ["dp", "ld"]),
            ],
            [
                new RegionAccessRule("dp", RegionAccessKind.Parts, "Synthetic DP Replace profile operation."),
                new RegionAccessRule("ld", RegionAccessKind.Parts, "Synthetic LD Replace profile operation."),
            ],
            IcNumberInputMode.SingleSelector);

    /// <summary>Synthetic CtrlRAM Replace profile with profile-declared oversized-input truncation.</summary>
    public static CompositionProfileDefinition SyntheticCtrlRamReplace { get; } =
        new(
            "synthetic-ctrlram-replace",
            "0.5.0",
            SyntheticIc,
            "ctrlram-replace",
            CompositionKind.Replace,
            "ctrlram-replace",
            "synthetic-ctrlram-replace.bin",
            ImageInitialization.Reference("output-image", "reference-base", 8),
            [
                new AddressSpace("reference-base", 8, AddressSpaceMutability.Immutable),
                new AddressSpace(
                    "ctrlram-replacement",
                    2,
                    AddressSpaceMutability.Immutable,
                    inputOversizePolicy: InputOversizePolicy.TruncateWithWarning),
                new AddressSpace("output-image", 8, AddressSpaceMutability.Mutable),
            ],
            [
                CompositionOperation.ReplaceRange(
                    "replace-ctrlram",
                    100,
                    "ctrlram-replacement",
                    new ByteRange(0, 2),
                    "output-image",
                    new ByteRange(4, 2),
                    OverlapPolicy.Reject,
                    "Replace synthetic CtrlRAM region."),
            ],
            [
                new ProfileRegion(
                    "ctrlram",
                    "output-image",
                    new ByteRange(4, 2),
                    RegionAtomicity.Partitioned,
                    RegionWritePolicy.DeclaredParts,
                    classificationTags: ["tp-ctrlram"]),
            ],
            [
                new RegionAccessRule("ctrlram", RegionAccessKind.Parts, "Synthetic CtrlRAM Replace profile operation."),
            ],
            IcNumberInputMode.CascadeSelector);

    /// <summary>Synthetic General Replace profile that accepts runtime explicit mappings.</summary>
    public static CompositionProfileDefinition SyntheticGeneralReplace { get; } =
        new(
            "synthetic-general-replace",
            "0.5.0",
            SyntheticIc,
            "general-replace",
            CompositionKind.Replace,
            "general-replace",
            "synthetic-general-replace.bin",
            ImageInitialization.Reference("output-image", "reference-base", 8),
            [
                new AddressSpace("reference-base", 8, AddressSpaceMutability.Immutable),
                new AddressSpace("output-image", 8, AddressSpaceMutability.Mutable),
            ],
            [],
            [
                new ProfileRegion(
                    "protected-header",
                    "output-image",
                    new ByteRange(0, 1),
                    RegionAtomicity.Whole,
                    RegionWritePolicy.Forbidden,
                    classificationTags: ["header", "protected"]),
                new ProfileRegion(
                    "general-payload",
                    "output-image",
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

    /// <summary>NT51950 DP Perspective Replace profile with base TP/customer-info restoration.</summary>
    public static CompositionProfileDefinition Nt51950DpReplace { get; } = CreateNt51950DpReplaceProfile("NT51950");

    /// <summary>NT51951 DP Perspective Replace profile using the owner-confirmed NT51950 policy.</summary>
    public static CompositionProfileDefinition Nt51951DpReplace { get; } = CreateNt51950DpReplaceProfile("NT51951");

    private static CompositionProfileDefinition CreateNt51950DpReplaceProfile(string icId)
    {
        string normalizedIc = icId.ToLowerInvariant();
        return new CompositionProfileDefinition(
            $"{normalizedIc}-dp-replace-dp-perspective",
            "0.5.0",
            icId,
            "dp-replace",
            CompositionKind.Replace,
            "dp-replace",
            $"{normalizedIc}-dp-replace.bin",
            ImageInitialization.Reference("output-image", "reference-base", Nt51950DpContainerLength),
            [
                new AddressSpace("reference-base", Nt51950DpContainerLength, AddressSpaceMutability.Immutable),
                new AddressSpace(
                    "dp-replacement",
                    Nt51950DpContainerLength,
                    AddressSpaceMutability.Immutable,
                    inputPaddingByte: 0x00,
                    allowedInputLengths: Nt51950AllowedDpReplacementLengths),
                new AddressSpace("output-image", Nt51950DpContainerLength, AddressSpaceMutability.Mutable),
            ],
            [
                CompositionOperation.ReplaceRange(
                    "replace-dp-container",
                    100,
                    "dp-replacement",
                    Nt51950DpContainerRange,
                    "output-image",
                    Nt51950DpContainerRange,
                    OverlapPolicy.Reject,
                    "Replace the NT51950/NT51951 DP Perspective container after accepting only 0x40000, 0x80000, or 0x100000 DP inputs and padding to 0x100000."),
                CompositionOperation.CopyRange(
                    "restore-base-tp",
                    200,
                    "reference-base",
                    Nt51950TpRestoreRange,
                    "output-image",
                    Nt51950TpRestoreRange,
                    OverlapPolicy.ReplaceExisting,
                    "Restore original TP FW at 0x0A000-0x36FFF from the base firmware after DP replacement."),
                CompositionOperation.CopyRange(
                    "restore-base-customer-info",
                    210,
                    "reference-base",
                    Nt51950CustomerInfoPreserveRange,
                    "output-image",
                    Nt51950CustomerInfoPreserveRange,
                    OverlapPolicy.ReplaceExisting,
                    "Restore customer information at 0x37000-0x37FFF from the base firmware after DP replacement."),
            ],
            [
                new ProfileRegion(
                    "dp-perspective-container",
                    "output-image",
                    Nt51950DpContainerRange,
                    RegionAtomicity.Partitioned,
                    RegionWritePolicy.DeclaredParts,
                    classificationTags: ["dp", "tp-restore", "customer-info-preserve"]),
            ],
            [
                new RegionAccessRule(
                    "dp-perspective-container",
                    RegionAccessKind.Parts,
                    "NT51950/NT51951 DP Replace first copies replacement DP, then restores the original TP and customer-info ranges."),
            ],
            IcNumberInputMode.SingleSelector);
    }
}
