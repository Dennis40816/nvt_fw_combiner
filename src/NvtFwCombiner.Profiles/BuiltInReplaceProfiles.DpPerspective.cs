using NvtFwCombiner.Domain.Composition;

namespace NvtFwCombiner.Profiles;

public static partial class BuiltInReplaceProfiles
{
    /// <summary>DP Perspective Replace profiles generated from the shared owner-approved IC list.</summary>
    public static IReadOnlyList<CompositionProfileDefinition> DpPerspectiveDpReplaceProfiles { get; } =
    [
        .. DpPerspectiveCatalog.SupportedIcIds.Select(icId => CreateDpPerspectiveDpReplaceProfileCore(
            icId,
            DpPerspectiveCatalog.MaxContainerLength,
            DpPerspectiveCatalog.SupportedContainerLengths)),
    ];

    /// <summary>Supported exact base firmware lengths for DP Perspective Replace.</summary>
    public static IReadOnlyList<long> DpPerspectiveSupportedDpBaseLengths => DpPerspectiveCatalog.SupportedContainerLengths;

    /// <summary>TP range restored from the base firmware after DP Perspective replacement.</summary>
    public static ByteRange DpPerspectiveTpRestoreRange => DpPerspectiveCatalog.TpOverlayRange;

    /// <summary>Returns true for ICs that use the DP Perspective Replace policy.</summary>
    public static bool IsDpPerspectiveDpReplaceIc(string icId)
    {
        return DpPerspectiveCatalog.IsSupportedIc(icId);
    }

    /// <summary>Returns true when <paramref name="length" /> is an approved DP Perspective base length.</summary>
    public static bool IsSupportedDpPerspectiveDpBaseLength(long length)
    {
        return DpPerspectiveCatalog.IsSupportedContainerLength(length);
    }

    /// <summary>Creates a DP Perspective Replace profile for the selected exact base length.</summary>
    public static CompositionProfileDefinition CreateDpPerspectiveDpReplaceProfile(string icId, long capacity)
    {
        return CreateDpPerspectiveDpReplaceProfileCore(icId, capacity, null);
    }

    private static CompositionProfileDefinition CreateDpPerspectiveDpReplaceProfileCore(
        string icId,
        long capacity,
        IReadOnlyList<long>? allowedReplacementLengths)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(icId);

        string normalizedIc = DpPerspectiveCatalog.NormalizeIcId(icId);

        if (!IsSupportedDpPerspectiveDpBaseLength(capacity))
        {
            throw new ArgumentOutOfRangeException(
                nameof(capacity),
                $"{DpPerspectiveCatalog.FormatSupportedIcIds()} DP Replace capacity must be {DpPerspectiveCatalog.FormatSupportedLengths()}.");
        }

        var dpContainerRange = new ByteRange(0, capacity);
        string normalizedProfileIc = normalizedIc.ToLowerInvariant();
        return new CompositionProfileDefinition(
            $"{normalizedProfileIc}-dp-replace-dp-perspective",
            "0.6.0",
            normalizedIc,
            IcWorkflowIds.DpReplace,
            CompositionKind.Replace,
            IcWorkflowIds.DpReplace,
            $"{normalizedProfileIc}-dp-replace.bin",
            ImageInitialization.Reference(CompositionAddressSpaceIds.OutputImage, CompositionAddressSpaceIds.ReferenceBase, capacity),
            [
                new AddressSpace(CompositionAddressSpaceIds.ReferenceBase, capacity, AddressSpaceMutability.Immutable),
                new AddressSpace(
                    CompositionAddressSpaceIds.DpReplacement,
                    capacity,
                    AddressSpaceMutability.Immutable,
                    inputPaddingByte: 0x00,
                    allowedInputLengths: allowedReplacementLengths),
                new AddressSpace(CompositionAddressSpaceIds.OutputImage, capacity, AddressSpaceMutability.Mutable),
            ],
            [
                CompositionOperation.ReplaceRange(
                    DpPerspectiveCatalog.ReplaceDpContainerOperationId,
                    DpPerspectiveCatalog.ReplaceDpContainerSequence,
                    CompositionAddressSpaceIds.DpReplacement,
                    dpContainerRange,
                    CompositionAddressSpaceIds.OutputImage,
                    dpContainerRange,
                    OverlapPolicy.Reject,
                    $"Replace the {DpPerspectiveCatalog.FormatSupportedIcIds()} DP Perspective container using the selected base length 0x{capacity:X}."),
                CompositionOperation.CopyRange(
                    DpPerspectiveCatalog.RestoreBaseTpOperationId,
                    DpPerspectiveCatalog.RestoreBaseTpSequence,
                    CompositionAddressSpaceIds.ReferenceBase,
                    DpPerspectiveCatalog.TpOverlayRange,
                    CompositionAddressSpaceIds.OutputImage,
                    DpPerspectiveCatalog.TpOverlayRange,
                    OverlapPolicy.ReplaceExisting,
                    $"Restore original TP FW at {DpPerspectiveCatalog.FormatRange(DpPerspectiveCatalog.TpOverlayRange)} from the base firmware after DP replacement."),
            ],
            [
                new ProfileRegion(
                    DpPerspectiveCatalog.ContainerRegionId,
                    CompositionAddressSpaceIds.OutputImage,
                    dpContainerRange,
                    RegionAtomicity.Partitioned,
                    RegionWritePolicy.DeclaredParts,
                    classificationTags: ["dp", "tp-restore"]),
            ],
            [
                new RegionAccessRule(
                    DpPerspectiveCatalog.ContainerRegionId,
                    RegionAccessKind.Parts,
                    $"{DpPerspectiveCatalog.FormatSupportedIcIds()} DP Replace first copies replacement DP, then restores the original TP range."),
            ],
            IcNumberInputMode.SingleSelector);
    }
}
