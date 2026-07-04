using NvtFwCombiner.Domain.Composition;

namespace NvtFwCombiner.Profiles;

/// <summary>Built-in standard merge profiles used as executable contract evidence.</summary>
public static class BuiltInStandardMergeProfiles
{
    private const long DpPerspectiveMaxContainerLength = 0x100000;
    private static readonly long[] DpPerspectiveAllowedOutputLengths = [0x40000, 0x80000, DpPerspectiveMaxContainerLength];

    /// <summary>Profiles ported from the approved gen_flash_bin_v2 standard merge reference config.</summary>
    public static IReadOnlyList<CompositionProfileDefinition> GenFlashStandardMergeProfiles { get; } =
    [
        CreateGenFlashProfile(
            "51920",
            flashSize: 0x40000,
            tpRegion: new StandardMergeRegion("tp", "tp-input", 0x00000, 0x30000),
            dpRegion: new StandardMergeRegion("dp", "dp-input", 0x3E000, 0x40000)),
        CreateGenFlashProfile(
            "51923",
            flashSize: 0x40000,
            tpRegion: new StandardMergeRegion("tp", "tp-input", 0x00000, 0x3C000),
            dpRegion: new StandardMergeRegion("dp", "dp-input", 0x3E000, 0x40000)),
        CreateGenFlashProfile(
            "51926",
            flashSize: 0x40000,
            tpRegion: new StandardMergeRegion("tp", "tp-input", 0x00000, 0x3C000),
            dpRegion: new StandardMergeRegion("dp", "dp-input", 0x3E000, 0x40000)),
        CreateGenFlashProfile(
            "51927",
            flashSize: 0x40000,
            tpRegion: new StandardMergeRegion("tp", "tp-input", 0x00000, 0x35000),
            dpRegion: new StandardMergeRegion("dp", "dp-input", 0x3C000, 0x40000, 0x200000)),
        CreateGenFlashProfile(
            "51928",
            flashSize: 0x80000,
            tpRegion: new StandardMergeRegion("tp", "tp-input", 0x00000, 0x35000),
            dpRegion: new StandardMergeRegion("dp", "dp-input", 0x3C000, 0x40000, 0x80000),
            extraRegion: new StandardMergeRegion("ld", "ld-input", 0x40000, 0x62000, 0x80000)),
        CreateGenFlashProfile(
            "51929",
            flashSize: 0x40000,
            tpRegion: new StandardMergeRegion("tp", "tp-input", 0x07000, 0x40000),
            dpRegion: new StandardMergeRegion("dp", "dp-input", 0x00000, 0x06000, 0x40000)),
        CreateGenFlashProfile(
            "51931",
            flashSize: 0x40000,
            tpRegion: new StandardMergeRegion("tp", "tp-input", 0x00000, 0x3C000),
            dpRegion: new StandardMergeRegion("dp", "dp-input", 0x3E000, 0x40000, 0x80000)),
        CreateGenFlashProfile(
            "51932",
            flashSize: 0x40000,
            tpRegion: new StandardMergeRegion("tp", "tp-input", 0x07000, 0x40000),
            dpRegion: new StandardMergeRegion("dp", "dp-input", 0x00000, 0x06000, 0x40000)),
    ];

    /// <summary>Owner-confirmed Standard Merge aliases that reuse an approved gen_flash reference layout.</summary>
    public static IReadOnlyList<CompositionProfileDefinition> OwnerConfirmedAliasStandardMergeProfiles { get; } =
    [
        CreateGenFlashProfile(
            "51917",
            flashSize: 0x40000,
            tpRegion: new StandardMergeRegion("tp", "tp-input", 0x00000, 0x35000),
            dpRegion: new StandardMergeRegion("dp", "dp-input", 0x3C000, 0x40000, 0x200000),
            profileIdSuffix: "gen-flash-alias",
            profileVersion: "0.5.0",
            evidenceSource: "owner-confirmed NT51917 follows NT51927 standard merge source."),
        CreateGenFlashProfile(
            "51919",
            flashSize: 0x40000,
            tpRegion: new StandardMergeRegion("tp", "tp-input", 0x07000, 0x40000),
            dpRegion: new StandardMergeRegion("dp", "dp-input", 0x00000, 0x06000, 0x40000),
            profileIdSuffix: "gen-flash-alias",
            profileVersion: "0.5.0",
            evidenceSource: "owner-confirmed NT51919 follows NT51929 standard merge source."),
    ];

    /// <summary>Standard merge profiles derived from owner-approved TP FlashMap evidence.</summary>
    public static IReadOnlyList<CompositionProfileDefinition> FlashMapStandardMergeProfiles { get; } =
    [
        CreateGenFlashProfile(
            "51930",
            flashSize: 0x40000,
            tpRegion: new StandardMergeRegion("tp", "tp-input", 0x07000, 0x40000, 0x40000),
            dpRegion: new StandardMergeRegion("dp", "dp-input", 0x00000, 0x06000, 0x40000),
            profileIdSuffix: "flashmap",
            profileVersion: "0.5.0",
            evidenceSource: "IC_FlashMap.xlsx 51930 TP Flashmap."),
    ];

    /// <summary>Standard merge profiles for NT51950/NT51951 DP Perspective containers.</summary>
    public static IReadOnlyList<CompositionProfileDefinition> DpPerspectiveStandardMergeProfiles { get; } =
    [
        CreateDpPerspectiveProfile("51950"),
        CreateDpPerspectiveProfile("51951"),
    ];

    /// <summary>All executable built-in standard merge profiles exposed by CLI/UI.</summary>
    public static IReadOnlyList<CompositionProfileDefinition> ExecutableStandardMergeProfiles { get; } =
    [
        .. GenFlashStandardMergeProfiles,
        .. OwnerConfirmedAliasStandardMergeProfiles,
        .. FlashMapStandardMergeProfiles,
        .. DpPerspectiveStandardMergeProfiles,
    ];

    /// <summary>All built-in standard merge profiles, including synthetic and reference-derived cases.</summary>
    public static IReadOnlyList<CompositionProfileDefinition> All =>
    [
        SyntheticStandardMerge,
        .. ExecutableStandardMergeProfiles,
    ];

    /// <summary>Creates an NT51950/NT51951 DP Perspective profile matching the selected DP input length.</summary>
    public static CompositionProfileDefinition CreateDpPerspectiveProfileForInputLength(string icId, long dpInputLength)
    {
        string icNumber = NormalizeDpPerspectiveIc(icId);
        return DpPerspectiveAllowedOutputLengths.Contains(dpInputLength)
            ? CreateDpPerspectiveProfile(icNumber, dpInputLength)
            : throw new ArgumentOutOfRangeException(
                nameof(dpInputLength),
                dpInputLength,
                "NT51950/NT51951 Standard Merge accepts DP input lengths 0x40000, 0x80000, or 0x100000.");
    }

    /// <summary>Returns true when a profile is the NT51950/NT51951 DP Perspective Standard Merge template.</summary>
    public static bool IsDpPerspectiveStandardMergeProfile(CompositionProfileDefinition profile)
    {
        ArgumentNullException.ThrowIfNull(profile);

        return profile.ProfileId.EndsWith("-standard-merge-dp-perspective", StringComparison.Ordinal) &&
            IsDpPerspectiveIc(profile.IcId);
    }

    // NT51950/NT51951 DP Perspective profiles must preserve customer info at
    // 0x37000-0x37FFF by overlaying TP only through 0x36FFF.

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
            new("output-image", flashSize, AddressSpaceMutability.Mutable),
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
            "standard-merge",
            CompositionKind.Merge,
            "standard-merge",
            $"{profileId}.bin",
            ImageInitialization.Blank("output-image", flashSize, 0x00),
            addressSpaces,
            operations,
            regions,
            accessRules);
    }

    private static CompositionProfileDefinition CreateDpPerspectiveProfile(string icNumber)
    {
        return CreateDpPerspectiveProfile(icNumber, DpPerspectiveMaxContainerLength);
    }

    private static CompositionProfileDefinition CreateDpPerspectiveProfile(string icNumber, long outputLength)
    {
        var outputRange = new ByteRange(0, outputLength);
        var tpOverlay = ByteRange.FromStartEndExclusive(0x0A000, 0x37000);
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
                new AddressSpace("tp-input", 0x37000, AddressSpaceMutability.Immutable),
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
                    "Overlay TP FW 0x0A000-0x36FFF (len 0x2D000) and leave customer information 0x37000-0x37FFF (len 0x1000) from DP."),
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

    private static string NormalizeDpPerspectiveIc(string icId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(icId);
        string icNumber = icId.StartsWith("NT", StringComparison.OrdinalIgnoreCase)
            ? icId[2..]
            : icId;
        return icNumber is "51950" or "51951"
            ? icNumber
            : throw new ArgumentException($"'{icId}' is not an NT51950/NT51951 DP Perspective IC.", nameof(icId));
    }

    private static bool IsDpPerspectiveIc(string icId)
    {
        return icId is "NT51950" or "NT51951";
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
            "output-image",
            range,
            OverlapPolicy.Reject,
            reason);
    }

    private static ProfileRegion ToProfileRegion(StandardMergeRegion region)
    {
        return new ProfileRegion(
            $"{region.RegionId}-region",
            "output-image",
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
