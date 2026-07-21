using NvtFwCombiner.Application.ExternalTools;
using NvtFwCombiner.Application.FlashMaps;
using NvtFwCombiner.Profiles;

namespace NvtFwCombiner.Bootstrap;

public static partial class WorkbenchCompositionService
{
    /// <summary>Reads gen_flash-backed contiguous DP main/sub version metadata from a selected DP payload.</summary>
    public static WorkbenchDpVersionMetadata? TryReadDpVersionMetadata(string icId, string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(icId);
        ArgumentException.ThrowIfNullOrWhiteSpace(path);

        byte[]? image = TryReadFirmwareImage(path);
        return image is null ? null : ReadDpVersionMetadata(icId, image);
    }

    /// <summary>Reads CMI DP Jira and major/minor facts without making unobserved DP sizes a build blocker.</summary>
    public static WorkbenchCmiDpCodeMetadata? TryReadCmiDpCodeMetadata(
        string icId,
        string path,
        string? tpPath = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(icId);
        ArgumentException.ThrowIfNullOrWhiteSpace(path);

        byte[]? image = TryReadFirmwareImage(path);
        if (image is null)
        {
            return null;
        }

        byte? chipNumber = TryReadFirmwareConfigBackupChipNumber(icId, tpPath);
        return ReadCmiDpCodeMetadata(icId, image, chipNumber);
    }

    /// <summary>Reads FWConfig display metadata from the canonical NVT-located Backup in a selected firmware image.</summary>
    public static WorkbenchFirmwareConfigMetadata? TryReadFirmwareConfigMetadata(string icId, string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(icId);
        ArgumentException.ThrowIfNullOrWhiteSpace(path);

        byte[]? image = TryReadFirmwareImage(path);
        return image is null ? null : ReadFirmwareConfigMetadata(icId, image);
    }

    /// <summary>
    /// Returns an auto-applicable IC-number selection only when the canonical NVT Backup is unique
    /// and its chip number resolves to one approved postbuild branch token.
    /// </summary>
    public static WorkbenchFirmwareContextSuggestion? TryReadFirmwareContextSuggestion(string icId, string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(icId);
        ArgumentException.ThrowIfNullOrWhiteSpace(path);

        byte[]? image = TryReadFirmwareImage(path);
        return image is null ? null : ReadFirmwareContextSuggestion(icId, image);
    }

    private static bool TryReadBaseCommonFwVersion(
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

    private static bool TryResolveNumberTokenForFirmwareChipNumber(
        string icId,
        byte chipNumber,
        out string? numberToken)
    {
        numberToken = null;
        IReadOnlyList<LegacyCombinerPostbuildProfile> profiles = GetPostbuildProfiles(icId);
        if (profiles.Count == 0)
        {
            return false;
        }

        string? stableToken = null;
        foreach (LegacyCombinerPostbuildProfile profile in profiles)
        {
            LegacyCombinerPostbuildPlanSelector[] matches =
            [
                .. profile.PlanSelectors.Where(selector => selector.MatchesReportedChipCount(chipNumber)),
            ];
            if (matches.Length != 1)
            {
                return false;
            }

            if (stableToken is not null && !string.Equals(stableToken, matches[0].Token, StringComparison.Ordinal))
            {
                return false;
            }

            stableToken = matches[0].Token;
        }

        if (stableToken is null)
        {
            return false;
        }

        numberToken = stableToken;
        return true;
    }

    private static bool TryReadFirmwareConfigBackupMetadata(
        string icId,
        string path,
        out FirmwareConfigMetadata metadata)
    {
        metadata = default;
        if (!IcSupportCatalog.TryFind(icId, out _))
        {
            return false;
        }

        byte[]? image = TryReadFirmwareImage(path);
        return image is not null && TryReadFirmwareConfigBackupMetadata(icId, image, out metadata);
    }

    private static byte? TryReadFirmwareConfigBackupChipNumber(string icId, string? tpPath)
    {
        return !string.IsNullOrWhiteSpace(tpPath) &&
            TryReadFirmwareConfigBackupMetadata(icId, tpPath, out FirmwareConfigMetadata metadata)
                ? metadata.ChipNumber
                : null;
    }

    private static byte[]? TryReadFirmwareImage(string path)
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

    private static bool TryReadFirmwareConfigBackupMetadata(
        string icId,
        ReadOnlySpan<byte> image,
        out FirmwareConfigMetadata metadata)
    {
        metadata = default;
        if (!IcSupportCatalog.TryFind(icId, out _) ||
            !FirmwareConfigMetadataReader.TryReadBackup(image, out FirmwareConfigMetadata backup))
        {
            return false;
        }

        metadata = backup;
        return true;
    }

    private static bool TryResolvePostbuildProfileFromBasePathForDisplay(
        string icId,
        string? basePath,
        out LegacyCombinerPostbuildProfile? postbuildProfile)
    {
        postbuildProfile = null;
        IReadOnlyList<LegacyCombinerPostbuildProfile> profiles = GetPostbuildProfiles(icId);
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
            TrySelectPostbuildProfileByCommonFwVersion(
                icId,
                commonFwVersion,
                out postbuildProfile,
                out _);

        return matchedBaseProfile ||
            (!hasReadableBase && TryGetDefaultPostbuildProfile(icId, out postbuildProfile));
    }
}
