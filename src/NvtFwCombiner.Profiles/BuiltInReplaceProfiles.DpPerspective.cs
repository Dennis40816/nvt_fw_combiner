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

    /// <summary>Customer information range preserved from the base firmware after DP Perspective replacement.</summary>
    public static ByteRange DpPerspectiveCustomerInfoPreserveRange => DpPerspectiveCatalog.CustomerInfoPreserveRange;

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
            "0.5.0",
            normalizedIc,
            IcWorkflowIds.DpReplace,
            CompositionKind.Replace,
            IcWorkflowIds.DpReplace,
            $"{normalizedProfileIc}-dp-replace.bin",
            ImageInitialization.Reference("output-image", "reference-base", capacity),
            [
                new AddressSpace("reference-base", capacity, AddressSpaceMutability.Immutable),
                new AddressSpace(
                    "dp-replacement",
                    capacity,
                    AddressSpaceMutability.Immutable,
                    inputPaddingByte: 0x00,
                    allowedInputLengths: allowedReplacementLengths),
                new AddressSpace("output-image", capacity, AddressSpaceMutability.Mutable),
            ],
            [
                CompositionOperation.ReplaceRange(
                    "replace-dp-container",
                    100,
                    "dp-replacement",
                    dpContainerRange,
                    "output-image",
                    dpContainerRange,
                    OverlapPolicy.Reject,
                    $"Replace the {DpPerspectiveCatalog.FormatSupportedIcIds()} DP Perspective container using the selected base length 0x{capacity:X}."),
                CompositionOperation.CopyRange(
                    "restore-base-tp",
                    200,
                    "reference-base",
                    DpPerspectiveCatalog.TpOverlayRange,
                    "output-image",
                    DpPerspectiveCatalog.TpOverlayRange,
                    OverlapPolicy.ReplaceExisting,
                    $"Restore original TP FW at {DpPerspectiveCatalog.FormatRange(DpPerspectiveCatalog.TpOverlayRange)} from the base firmware after DP replacement."),
                CompositionOperation.CopyRange(
                    "restore-base-customer-info",
                    210,
                    "reference-base",
                    DpPerspectiveCatalog.CustomerInfoPreserveRange,
                    "output-image",
                    DpPerspectiveCatalog.CustomerInfoPreserveRange,
                    OverlapPolicy.ReplaceExisting,
                    $"Restore customer information at {DpPerspectiveCatalog.FormatRange(DpPerspectiveCatalog.CustomerInfoPreserveRange)} from the base firmware after DP replacement."),
            ],
            [
                new ProfileRegion(
                    "dp-perspective-container",
                    "output-image",
                    dpContainerRange,
                    RegionAtomicity.Partitioned,
                    RegionWritePolicy.DeclaredParts,
                    classificationTags: ["dp", "tp-restore", "customer-info-preserve"]),
            ],
            [
                new RegionAccessRule(
                    "dp-perspective-container",
                    RegionAccessKind.Parts,
                    $"{DpPerspectiveCatalog.FormatSupportedIcIds()} DP Replace first copies replacement DP, then restores the original TP and customer-info ranges."),
            ],
            IcNumberInputMode.SingleSelector);
    }
}
