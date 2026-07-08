using NvtFwCombiner.Application.ExternalTools;
using NvtFwCombiner.Application.FlashMaps;

namespace NvtFwCombiner.Bootstrap;

public static partial class WorkbenchCompositionService
{
    /// <summary>Returns true when the IC has a gen_flash-backed DP main/sub version-byte rule.</summary>
    public static bool HasDpVersionMetadataRule(string icId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(icId);
        return GenFlashVersionCatalog.TryGetDpVersionRule(icId, out _);
    }

    /// <summary>Reads gen_flash-backed contiguous DP main/sub version metadata from a selected DP payload.</summary>
    public static WorkbenchDpVersionMetadata? TryReadDpVersionMetadata(string icId, string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(icId);
        ArgumentException.ThrowIfNullOrWhiteSpace(path);

        if (!File.Exists(path))
        {
            return null;
        }

        try
        {
            byte[] image = File.ReadAllBytes(path);
            return GenFlashVersionCatalog.TryReadDpVersion(
                icId,
                image,
                out GenFlashDpVersionMetadata metadata)
                    ? new WorkbenchDpVersionMetadata(
                        metadata.IcId,
                        metadata.Prefix,
                        metadata.VersionToken,
                        metadata.DisplayVersion,
                        metadata.MainInputReadOffset,
                        metadata.SubInputReadOffset,
                        metadata.OutputMainAbsoluteAddress,
                        metadata.OutputSubAbsoluteAddress,
                        metadata.EvidenceSource)
                    : null;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or ArgumentException or NotSupportedException)
        {
            return null;
        }
    }

    /// <summary>Reads FWConfig display metadata from a selected firmware image when the IC flash-map defines it.</summary>
    public static WorkbenchFirmwareConfigMetadata? TryReadFirmwareConfigMetadata(string icId, string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(icId);
        ArgumentException.ThrowIfNullOrWhiteSpace(path);

        if (!TpFlashMapCatalog.TryGetFirmwareConfigStart(icId, out long firmwareConfigStart) ||
            !File.Exists(path))
        {
            return null;
        }

        try
        {
            byte[] image = File.ReadAllBytes(path);
            if (!FirmwareConfigMetadataReader.TryRead(image, firmwareConfigStart, out FirmwareConfigMetadata metadata))
            {
                return null;
            }

            string? postbuildCategory = TryResolvePostbuildProfileForDisplay(
                icId,
                path,
                out LegacyCombinerPostbuildProfile? postbuildProfile)
                    ? postbuildProfile!.DisplayCategory
                    : null;
            return new WorkbenchFirmwareConfigMetadata(
                metadata.FirmwareConfigStart,
                metadata.CommonFwVersion,
                metadata.FirmwareVersion,
                metadata.FirmwareVersionBar,
                metadata.IsFirmwareVersionBarValid,
                metadata.FirmwareSubVersion,
                metadata.ProjectId,
                postbuildCategory);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or ArgumentException or NotSupportedException)
        {
            return null;
        }
    }

    private static bool TryReadBaseCommonFwVersion(
        string icId,
        string basePath,
        out string? commonFwVersion)
    {
        commonFwVersion = null;
        if (!TpFlashMapCatalog.TryGetFirmwareConfigStart(icId, out long firmwareConfigStart))
        {
            return false;
        }

        try
        {
            byte[] image = File.ReadAllBytes(basePath);
            if (!FirmwareConfigMetadataReader.TryRead(image, firmwareConfigStart, out FirmwareConfigMetadata metadata))
            {
                return false;
            }

            if (!metadata.IsFirmwareVersionBarValid)
            {
                return false;
            }

            commonFwVersion = metadata.CommonFwVersion;
            return true;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or ArgumentException or NotSupportedException)
        {
            return false;
        }
    }

    private static bool TryResolvePostbuildProfileForDisplay(
        string icId,
        string? basePath,
        out LegacyCombinerPostbuildProfile? postbuildProfile)
    {
        postbuildProfile = null;
        IReadOnlyList<LegacyCombinerPostbuildProfile> profiles = LegacyCombinerPostbuildCatalog.GetProfiles(icId);
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
            LegacyCombinerPostbuildCatalog.TrySelectProfileForCommonFwVersion(
                icId,
                commonFwVersion,
                out postbuildProfile,
                out _);

        return matchedBaseProfile ||
            (!hasReadableBase && LegacyCombinerPostbuildCatalog.TryGetDefaultProfile(icId, out postbuildProfile));
    }
}
