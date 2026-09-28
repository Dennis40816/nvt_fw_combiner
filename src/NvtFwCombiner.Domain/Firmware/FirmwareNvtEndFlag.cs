namespace NvtFwCombiner.Domain.Firmware;

/// <summary>
/// The layout-declared NVT end-flag position (<c>00 4E 56 54</c>) right after the FWConfig Backup region, the TP
/// section end minus 4 (NVT-END-FLAG-1113-01, owner decisions 30/35/37, ADR 0076). A map declares it when every
/// canonical FWConfig Backup NVT marker-relative locator it selects has a search range exactly as long as the
/// marker at the same position, unique selection and the <c>-0xFFC</c> Backup offset. Only that position counts;
/// a complete marker anywhere else is neither counted nor rejected. The position is in the coordinates of the
/// artifact the locators bind (a Base, a TP input, or one bank of an AB image). It is projected only from profile
/// declarations, never from filenames or Golden observations.
/// </summary>
public sealed record FirmwareNvtEndFlag
{
    /// <summary>Signed distance from the marker start to the FWConfig Backup start (terminal byte minus 0xFFF).</summary>
    public const long BackupStartOffsetFromMarker = -0xFFC;

    private static readonly byte[] Marker = [0x00, 0x4E, 0x56, 0x54];

    private FirmwareNvtEndFlag(FirmwareAddressedRange position)
    {
        Position = position;
    }

    /// <summary>Complete NVT end-flag bytes.</summary>
    public static ReadOnlySpan<byte> MarkerBytes => Marker;

    /// <summary>Declared half-open end-flag range, exactly the marker length, in a named address space.</summary>
    public FirmwareAddressedRange Position { get; }

    /// <summary>
    /// Projects the end flag declared by one map's selected structures. Null when the map selects no NVT locator or
    /// declares only bounded search ranges (layouts without a declared end flag keep the compatibility read).
    /// </summary>
    internal static FirmwareNvtEndFlag? FromStructures(IEnumerable<FirmwareMetadataStructure> structures)
    {
        FirmwareMarkerRelativeLocator[] locators = NvtLocators(structures);
        return locators.Length != 0 && locators.All(IsEndFlagLocator) &&
            locators.All(locator => locator.SearchRange == locators[0].SearchRange)
            ? new FirmwareNvtEndFlag(locators[0].SearchRange)
            : null;
    }

    /// <summary>Rejects a map that declares an end flag for only some NVT locators or at different positions.</summary>
    internal static void RequireConsistentDeclaration(string mapId, IEnumerable<FirmwareMetadataStructure> structures)
    {
        FirmwareMarkerRelativeLocator[] locators = NvtLocators(structures);
        DomainInvariant.Reject(
            locators.Any(IsEndFlagLocator) && FromStructures(structures) is null,
            $"Image map '{mapId}' must declare one NVT end flag for every canonical FWConfig Backup locator or none.",
            nameof(structures));
    }

    private static FirmwareMarkerRelativeLocator[] NvtLocators(IEnumerable<FirmwareMetadataStructure> structures)
    {
        ArgumentNullException.ThrowIfNull(structures);
        return
        [
            .. structures
                .Select(static structure => structure.Locator)
                .OfType<FirmwareMarkerRelativeLocator>()
                .Where(static locator => locator.MarkerBytes.Bytes.SequenceEqual(Marker)),
        ];
    }

    private static bool IsEndFlagLocator(FirmwareMarkerRelativeLocator locator)
    {
        return locator.SearchRange.Range.Length == Marker.Length &&
            locator.ResultOffset == BackupStartOffsetFromMarker &&
            locator.Selection is FirmwareUniqueMarkerSelection;
    }
}

/// <summary>
/// Outcome of resolving one layout's NVT end-flag declaration (NVT-END-FLAG-1113-01). A successful resolution carries
/// the declared end flag, or, only for a family in <see cref="FirmwareNvtEndFlagMigration"/>, no declaration and the
/// existing compatibility read. A failed resolution (missing, inconsistent or unavailable declaration) never falls
/// back to a whole-image search: readers treat the Backup as unreadable.
/// </summary>
public sealed record FirmwareNvtEndFlagResolution
{
    private FirmwareNvtEndFlagResolution(bool succeeded, FirmwareNvtEndFlag? endFlag)
    {
        Succeeded = succeeded;
        EndFlag = endFlag;
    }

    /// <summary>The declaration is missing, inconsistent or unavailable; the Backup is unreadable.</summary>
    public static FirmwareNvtEndFlagResolution Unresolved { get; } = new(false, null);

    /// <summary>A migration-inventory layout without a declaration: the existing compatibility read applies.</summary>
    internal static FirmwareNvtEndFlagResolution LegacyCompatibility { get; } = new(true, null);

    /// <summary>Whether the declaration resolved (declared, or explicitly allowed to be absent).</summary>
    public bool Succeeded { get; }

    /// <summary>The declared end flag, or null.</summary>
    public FirmwareNvtEndFlag? EndFlag { get; }

    /// <summary>Whether the existing compatibility read applies; deleted with the empty migration inventory.</summary>
    public bool UsesLegacyCompatibilityRead => Succeeded && EndFlag is null;

    internal static FirmwareNvtEndFlagResolution Declared(FirmwareNvtEndFlag endFlag)
    {
        ArgumentNullException.ThrowIfNull(endFlag);
        return new FirmwareNvtEndFlagResolution(true, endFlag);
    }

    /// <summary>The one resolution shared by every candidate, or <see cref="Unresolved"/>.</summary>
    public static FirmwareNvtEndFlagResolution Common(IEnumerable<FirmwareNvtEndFlagResolution> candidates)
    {
        ArgumentNullException.ThrowIfNull(candidates);
        FirmwareNvtEndFlagResolution[] resolved = [.. candidates];
        return resolved.Length != 0 && resolved[0].Succeeded && resolved.All(candidate => candidate == resolved[0])
            ? resolved[0]
            : Unresolved;
    }
}
