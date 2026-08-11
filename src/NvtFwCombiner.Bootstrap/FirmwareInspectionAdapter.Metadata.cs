using NvtFwCombiner.Application.ExternalTools;
using NvtFwCombiner.Application.FlashMaps;

namespace NvtFwCombiner.Bootstrap;

public static partial class FirmwareInspectionAdapter
{
    /// <summary>Reads FWConfig display metadata from the canonical NVT-located Backup in a selected firmware image.</summary>
    public static WorkbenchFirmwareConfigMetadata? TryReadFirmwareConfigMetadata(string icId, string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(icId);
        ArgumentException.ThrowIfNullOrWhiteSpace(path);

        byte[]? image = TryReadFirmwareImage(path);
        return image is null ? null : ReadFirmwareConfigMetadata(icId, image);
    }

    internal static bool TryReadBaseCommonFwVersion(
        string icId,
        string basePath,
        out string? commonFwVersion)
    {
        commonFwVersion = null;
        if (!TryReadFirmwareConfigBackupMetadata(icId, basePath, out FirmwareConfigMetadata metadata))
        {
            return false;
        }

        commonFwVersion = metadata.CommonFwVersion;
        return true;
    }

    private static bool TryResolveNumberTokenForFirmwareConfig(
        string icId,
        FirmwareConfigMetadata firmwareConfig,
        out string? numberToken)
    {
        numberToken = null;
        if (!TryResolvePostbuildProfileForDisplay(
                icId,
                firmwareConfig,
                out LegacyCombinerPostbuildProfile? profile) ||
            profile is null)
        {
            return false;
        }

        LegacyCombinerPostbuildPlanSelector[] matches =
        [
            .. profile.PlanSelectors.Where(selector =>
                selector.MatchesReportedChipCount(firmwareConfig.ChipNumber)),
        ];
        if (matches.Length != 1 ||
            !CtrlRamV2RouteRegistry.TryResolve(profile, matches[0].Branch, out _))
        {
            return false;
        }

        numberToken = matches[0].Token;
        return true;
    }

    internal static bool TryReadFirmwareConfigBackupMetadata(
        string icId,
        string path,
        out FirmwareConfigMetadata metadata)
    {
        metadata = default;
        if (!CanonicalCapabilityProjection.IsKnownIcId(icId))
        {
            return false;
        }

        byte[]? image = TryReadFirmwareImage(path);
        return image is not null && TryReadFirmwareConfigBackupMetadata(icId, image, out metadata);
    }

    internal static byte[]? TryReadFirmwareImage(string path)
    {
        try
        {
            return File.ReadAllBytes(path);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or ArgumentException or NotSupportedException)
        {
            return null;
        }
    }

    internal static bool TryReadFirmwareConfigBackupMetadata(
        string icId,
        ReadOnlySpan<byte> image,
        out FirmwareConfigMetadata metadata)
    {
        metadata = default;
        if (!CanonicalCapabilityProjection.IsKnownIcId(icId) ||
            !FirmwareConfigMetadataReader.TryReadBackup(image, out FirmwareConfigMetadata backup))
        {
            return false;
        }

        metadata = backup;
        return true;
    }

    internal static bool TryResolvePostbuildProfileFromBasePathForDisplay(
        string icId,
        string? basePath,
        out LegacyCombinerPostbuildProfile? postbuildProfile)
    {
        postbuildProfile = null;
        IReadOnlyList<LegacyCombinerPostbuildProfile> profiles =
            CanonicalCapabilityProjection.GetPostbuildProfiles(icId);
        if (profiles.Count == 0)
        {
            return false;
        }

        if (profiles.Count == 1)
        {
            postbuildProfile = profiles[0];
            return true;
        }

        bool hasReadableBase = !string.IsNullOrWhiteSpace(basePath) && File.Exists(basePath);
        bool matchedBaseProfile = hasReadableBase &&
            TryReadBaseCommonFwVersion(icId, basePath!, out string? commonFwVersion) &&
            CanonicalCapabilityProjection.TrySelectPostbuildProfileByCommonFwVersion(
                icId,
                commonFwVersion,
                out postbuildProfile,
                out _);

        return matchedBaseProfile ||
            (!hasReadableBase && CanonicalCapabilityProjection.TryGetDefaultPostbuildProfile(
                icId,
                out postbuildProfile));
    }
}
