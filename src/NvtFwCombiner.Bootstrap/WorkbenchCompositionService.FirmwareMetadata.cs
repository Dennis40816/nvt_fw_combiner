using NvtFwCombiner.Application.ExternalTools;
using NvtFwCombiner.Application.FlashMaps;

namespace NvtFwCombiner.Bootstrap;

public static partial class WorkbenchCompositionService
{
    /// <summary>Returns true when the IC has a gen_flash-backed DP version-byte rule.</summary>
    public static bool HasDpVersionMetadataRule(string icId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(icId);
        return GenFlashVersionCatalog.TryGetDpVersionRule(icId, out _);
    }

    /// <summary>Reads gen_flash-backed DP version metadata from a selected DP payload.</summary>
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
                        metadata.InputReadOffset,
                        metadata.OutputAbsoluteAddress,
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
                    ? FormatPostbuildCategory(postbuildProfile!)
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

    private static string FormatPostbuildCategory(LegacyCombinerPostbuildProfile postbuildProfile)
    {
        string evidence = postbuildProfile.Evidence.Split(';', StringSplitOptions.TrimEntries)[0];
        string fileName = Path.GetFileName(evidence.Replace('\\', '/'));
        return string.IsNullOrWhiteSpace(fileName)
            ? postbuildProfile.ProcessorId
            : Path.GetFileNameWithoutExtension(fileName);
    }

    /// <summary>Gets TP Overview CtrlRAM regions visible for a selected IC and IC-number context.</summary>
    public static IReadOnlyList<WorkbenchCtrlRamRegion> GetCtrlRamRegions(
        string icId,
        string number,
        string? basePath = null)
    {
        LegacyCombinerPostbuildProfile? postbuildProfile = TryResolvePostbuildProfileForDisplay(
            icId,
            basePath,
            out LegacyCombinerPostbuildProfile? profile)
                ? profile
                : null;
        return
        [
            .. TpFlashMapCatalog.GetCtrlRamRegions(icId, ToIcNumberSelection(number), postbuildProfile)
                .Select(region => new WorkbenchCtrlRamRegion(
                    region.DisplayName,
                    region.Range.Start,
                    region.Range.Length,
                    region.Tags.Any(tag =>
                        string.Equals(tag, "diff", StringComparison.OrdinalIgnoreCase) ||
                        string.Equals(tag, "dlm", StringComparison.OrdinalIgnoreCase) ||
                        string.Equals(tag, "slave", StringComparison.OrdinalIgnoreCase)))),
        ];
    }
}
