using NvtFwCombiner.Domain.Composition;

namespace NvtFwCombiner.Profiles;

public static partial class BuiltInReplaceProfiles
{
    private const string SyntheticIc = "NT-SYNTHETIC";

    /// <summary>Synthetic DP Replace profile with separate DP and LD replacement payloads.</summary>
    public static CompositionProfileDefinition SyntheticDpReplace { get; } =
        new(
            "synthetic-dp-replace",
            "0.5.0",
            SyntheticIc,
            IcWorkflowIds.DpReplace,
            CompositionKind.Replace,
            IcWorkflowIds.DpReplace,
            "synthetic-dp-replace.bin",
            ImageInitialization.Reference(CompositionAddressSpaceIds.OutputImage, CompositionAddressSpaceIds.ReferenceBase, 8),
            [
                new AddressSpace(CompositionAddressSpaceIds.ReferenceBase, 8, AddressSpaceMutability.Immutable),
                new AddressSpace(CompositionAddressSpaceIds.DpReplacement, 4, AddressSpaceMutability.Immutable, inputPaddingByte: 0xFF),
                new AddressSpace(CompositionAddressSpaceIds.LdReplacement, 2, AddressSpaceMutability.Immutable, inputPaddingByte: 0xFF),
                new AddressSpace(CompositionAddressSpaceIds.OutputImage, 8, AddressSpaceMutability.Mutable),
            ],
            [
                CompositionOperation.ReplaceRange(
                    "replace-dp",
                    100,
                    CompositionAddressSpaceIds.DpReplacement,
                    new ByteRange(0, 4),
                    CompositionAddressSpaceIds.OutputImage,
                    new ByteRange(0, 4),
                    OverlapPolicy.Reject,
                    "Replace synthetic DP declared partition."),
                CompositionOperation.ReplaceRange(
                    "replace-ld",
                    110,
                    CompositionAddressSpaceIds.LdReplacement,
                    new ByteRange(0, 2),
                    CompositionAddressSpaceIds.OutputImage,
                    new ByteRange(6, 2),
                    OverlapPolicy.Reject,
                    "Replace synthetic LD declared partition under DP Replace."),
            ],
            [
                new ProfileRegion(
                    "dp",
                    CompositionAddressSpaceIds.OutputImage,
                    new ByteRange(0, 4),
                    RegionAtomicity.Partitioned,
                    RegionWritePolicy.DeclaredParts,
                    classificationTags: ["dp"]),
                new ProfileRegion(
                    "ld",
                    CompositionAddressSpaceIds.OutputImage,
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
            IcWorkflowIds.CtrlRamReplace,
            CompositionKind.Replace,
            IcWorkflowIds.CtrlRamReplace,
            "synthetic-ctrlram-replace.bin",
            ImageInitialization.Reference(CompositionAddressSpaceIds.OutputImage, CompositionAddressSpaceIds.ReferenceBase, 8),
            [
                new AddressSpace(CompositionAddressSpaceIds.ReferenceBase, 8, AddressSpaceMutability.Immutable),
                new AddressSpace(
                    CompositionAddressSpaceIds.CtrlRamReplacement,
                    2,
                    AddressSpaceMutability.Immutable,
                    inputOversizePolicy: InputOversizePolicy.TruncateWithWarning),
                new AddressSpace(CompositionAddressSpaceIds.OutputImage, 8, AddressSpaceMutability.Mutable),
            ],
            [
                CompositionOperation.ReplaceRange(
                    "replace-ctrlram",
                    100,
                    CompositionAddressSpaceIds.CtrlRamReplacement,
                    new ByteRange(0, 2),
                    CompositionAddressSpaceIds.OutputImage,
                    new ByteRange(4, 2),
                    OverlapPolicy.Reject,
                    "Replace synthetic CtrlRAM region."),
            ],
            [
                new ProfileRegion(
                    "ctrlram",
                    CompositionAddressSpaceIds.OutputImage,
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
