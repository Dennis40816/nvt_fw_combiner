using NvtFwCombiner.Domain.Composition;

namespace NvtFwCombiner.Profiles;

public static partial class BuiltInStandardMergeProfiles
{
    /// <summary>Profiles ported from the approved gen_flash_bin_v2 standard merge reference config.</summary>
    public static IReadOnlyList<CompositionProfileDefinition> GenFlashStandardMergeProfiles { get; } =
    [
        CreateGenFlashProfile(
            "51920",
            flashSize: 0x40000,
            tpRegion: new StandardMergeRegion("tp", CompositionAddressSpaceIds.TpInput, 0x00000, 0x30000),
            dpRegion: new StandardMergeRegion("dp", CompositionAddressSpaceIds.DpInput, 0x3E000, 0x40000)),
        CreateGenFlashProfile(
            "51923",
            flashSize: 0x40000,
            tpRegion: new StandardMergeRegion("tp", CompositionAddressSpaceIds.TpInput, 0x00000, 0x3C000),
            dpRegion: new StandardMergeRegion("dp", CompositionAddressSpaceIds.DpInput, 0x3E000, 0x40000)),
        CreateGenFlashProfile(
            "51926",
            flashSize: 0x40000,
            tpRegion: new StandardMergeRegion("tp", CompositionAddressSpaceIds.TpInput, 0x00000, 0x3C000),
            dpRegion: new StandardMergeRegion("dp", CompositionAddressSpaceIds.DpInput, 0x3E000, 0x40000)),
        CreateGenFlashProfile(
            "51927",
            flashSize: 0x40000,
            tpRegion: new StandardMergeRegion("tp", CompositionAddressSpaceIds.TpInput, 0x00000, 0x35000),
            dpRegion: new StandardMergeRegion("dp", CompositionAddressSpaceIds.DpInput, 0x3C000, 0x40000, 0x200000)),
        CreateGenFlashProfile(
            "51928",
            flashSize: 0x80000,
            tpRegion: new StandardMergeRegion("tp", CompositionAddressSpaceIds.TpInput, 0x00000, 0x35000),
            dpRegion: new StandardMergeRegion("dp", CompositionAddressSpaceIds.DpInput, 0x3C000, 0x40000, 0x80000),
            extraRegion: new StandardMergeRegion("ld", CompositionAddressSpaceIds.LdInput, 0x40000, 0x62000, 0x80000)),
        CreateGenFlashProfile(
            "51929",
            flashSize: 0x40000,
            tpRegion: new StandardMergeRegion("tp", CompositionAddressSpaceIds.TpInput, 0x07000, 0x40000),
            dpRegion: new StandardMergeRegion("dp", CompositionAddressSpaceIds.DpInput, 0x00000, 0x06000, 0x40000)),
        CreateGenFlashProfile(
            "51931",
            flashSize: 0x40000,
            tpRegion: new StandardMergeRegion("tp", CompositionAddressSpaceIds.TpInput, 0x00000, 0x3C000),
            dpRegion: new StandardMergeRegion("dp", CompositionAddressSpaceIds.DpInput, 0x3E000, 0x40000, 0x80000)),
        CreateGenFlashProfile(
            "51932",
            flashSize: 0x40000,
            tpRegion: new StandardMergeRegion("tp", CompositionAddressSpaceIds.TpInput, 0x07000, 0x40000),
            dpRegion: new StandardMergeRegion("dp", CompositionAddressSpaceIds.DpInput, 0x00000, 0x06000, 0x40000)),
    ];

    /// <summary>Owner-confirmed Standard Merge aliases that reuse an approved gen_flash reference layout.</summary>
    public static IReadOnlyList<CompositionProfileDefinition> OwnerConfirmedAliasStandardMergeProfiles { get; } =
    [
        .. CreateOwnerConfirmedGenFlashAliasProfiles(),
    ];

    /// <summary>Standard merge profiles derived from owner-approved TP FlashMap evidence.</summary>
    public static IReadOnlyList<CompositionProfileDefinition> FlashMapStandardMergeProfiles { get; } =
    [
        CreateGenFlashProfile(
            "51930",
            flashSize: 0x40000,
            tpRegion: new StandardMergeRegion("tp", CompositionAddressSpaceIds.TpInput, 0x07000, 0x40000, 0x40000),
            dpRegion: new StandardMergeRegion("dp", CompositionAddressSpaceIds.DpInput, 0x00000, 0x06000, 0x40000),
            profileIdSuffix: "flashmap",
            profileVersion: "0.5.0",
            evidenceSource: "IC_FlashMap.xlsx 51930 TP Flashmap."),
    ];

    private static IReadOnlyList<CompositionProfileDefinition> CreateOwnerConfirmedGenFlashAliasProfiles()
    {
        var genFlashProfilesByIc =
            GenFlashStandardMergeProfiles.ToDictionary(profile => profile.IcId, StringComparer.Ordinal);
        return
        [
            .. IcSupportCatalog.All
                .Where(entry => entry.StandardMergeSourceIcId is not null)
                .Where(entry => genFlashProfilesByIc.ContainsKey(entry.StandardMergeSourceIcId!))
                .Select(entry => CreateGenFlashAliasProfile(
                    entry.IcId,
                    genFlashProfilesByIc[entry.StandardMergeSourceIcId!]))
        ];
    }

    private static CompositionProfileDefinition CreateGenFlashAliasProfile(
        string aliasIcId,
        CompositionProfileDefinition reference)
    {
        string aliasIcNumber = aliasIcId[2..];
        string profileId = $"nt{aliasIcNumber}-standard-merge-gen-flash-alias";
        return new CompositionProfileDefinition(
            profileId,
            "0.5.0",
            aliasIcId,
            reference.ModeId,
            reference.CompositionKind,
            reference.ExperienceId,
            $"{profileId}.bin",
            reference.Initialization,
            reference.AddressSpaces,
            reference.Operations,
            reference.Regions,
            reference.RegionAccessRules,
            reference.IcNumberInputMode);
    }

    private static CompositionProfileDefinition CreateGenFlashProfile(
        string icNumber,
        long flashSize,
        StandardMergeRegion tpRegion,
        StandardMergeRegion dpRegion,
        StandardMergeRegion? extraRegion = null,
        string profileIdSuffix = "gen-flash",
        string profileVersion = "0.4.0",
        string evidenceSource = "gen_flash standard merge source.")
    {
        string profileId = $"nt{icNumber}-standard-merge-{profileIdSuffix}";
        List<AddressSpace> addressSpaces =
        [
            new(tpRegion.SourceSpaceId, tpRegion.SourceLength, AddressSpaceMutability.Immutable),
            new(dpRegion.SourceSpaceId, dpRegion.SourceLength, AddressSpaceMutability.Immutable),
            new(CompositionAddressSpaceIds.OutputImage, flashSize, AddressSpaceMutability.Mutable),
        ];
        List<CompositionOperation> operations =
        [
            CopyRegion("copy-tp", 100, tpRegion, $"Copy TP range from {evidenceSource}"),
            CopyRegion("copy-dp", 200, dpRegion, $"Copy DP range from {evidenceSource}"),
        ];
        List<ProfileRegion> regions =
        [
            ToProfileRegion(tpRegion),
            ToProfileRegion(dpRegion),
        ];
        List<RegionAccessRule> accessRules =
        [
            ToAccessRule(tpRegion),
            ToAccessRule(dpRegion),
        ];

        if (extraRegion is not null)
        {
            addressSpaces.Add(new AddressSpace(
                extraRegion.SourceSpaceId,
                extraRegion.SourceLength,
                AddressSpaceMutability.Immutable));
            operations.Add(CopyRegion(
                "copy-ld",
                300,
                extraRegion,
                $"Copy LD range from {evidenceSource}"));
            regions.Add(ToProfileRegion(extraRegion));
            accessRules.Add(ToAccessRule(extraRegion));
        }

        return new CompositionProfileDefinition(
            profileId,
            profileVersion,
            $"NT{icNumber}",
            IcWorkflowIds.StandardMerge,
            CompositionKind.Merge,
            IcWorkflowIds.StandardMerge,
            $"{profileId}.bin",
            ImageInitialization.Blank(CompositionAddressSpaceIds.OutputImage, flashSize, 0x00),
            addressSpaces,
            operations,
            regions,
            accessRules);
    }

    private static CompositionOperation CopyRegion(
        string operationId,
        int sequence,
        StandardMergeRegion region,
        string reason)
    {
        ByteRange range = region.Range;
        return CompositionOperation.CopyRange(
            operationId,
            sequence,
            region.SourceSpaceId,
            range,
            CompositionAddressSpaceIds.OutputImage,
            range,
            OverlapPolicy.Reject,
            reason);
    }

    private static ProfileRegion ToProfileRegion(StandardMergeRegion region)
    {
        return new ProfileRegion(
            $"{region.RegionId}-region",
            CompositionAddressSpaceIds.OutputImage,
            region.Range,
            RegionAtomicity.Whole,
            RegionWritePolicy.WholeOnly);
    }

    private static RegionAccessRule ToAccessRule(StandardMergeRegion region)
    {
        return new RegionAccessRule(
            $"{region.RegionId}-region",
            RegionAccessKind.Whole,
            "Fixed standard-merge profile operation.");
    }

    private sealed record StandardMergeRegion(
        string RegionId,
        string SourceSpaceId,
        long Start,
        long EndExclusive,
        long? DeclaredSourceLength = null)
    {
        internal long SourceLength => DeclaredSourceLength ?? EndExclusive;

        internal ByteRange Range => ByteRange.FromStartEndExclusive(Start, EndExclusive);
    }
}
