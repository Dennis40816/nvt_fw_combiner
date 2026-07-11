using System.Globalization;
using NvtFwCombiner.Application.ExternalTools;
using NvtFwCombiner.Application.FlashMaps;
using NvtFwCombiner.Domain.Composition;

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

    /// <summary>Reads CMI DP Jira and major/minor facts without making unobserved DP sizes a build blocker.</summary>
    public static WorkbenchCmiDpCodeMetadata? TryReadCmiDpCodeMetadata(
        string icId,
        string path,
        string? tpPath = null)
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
            byte? chipNumber = TryReadNvtCopyChipNumber(icId, tpPath);
            return GenFlashVersionCatalog.TryReadCmiDpCode(
                icId,
                image,
                chipNumber,
                out CmiDpCodeMetadata metadata)
                    ? new WorkbenchCmiDpCodeMetadata(
                        metadata.IcId,
                        metadata.MajorVersionByte,
                        metadata.MinorVersionNibble,
                        metadata.JiraNumber,
                        metadata.JiraBadge,
                        metadata.PayloadLength,
                        metadata.ExpectedPayloadLengths,
                        metadata.HasPayloadLengthWarning,
                        metadata.Register16Offset,
                        metadata.Register18Offset,
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

        if (!TryReadConsistentFirmwareConfigMetadata(icId, path, out FirmwareConfigMetadata metadata))
        {
            return null;
        }

        try
        {
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
                metadata.ChipNumber,
                metadata.ProjectId,
                postbuildCategory,
                metadata.Hardware);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or ArgumentException or NotSupportedException)
        {
            return null;
        }
    }

    /// <summary>
    /// Returns an auto-applicable IC-number selection only when the NVT-copy FWConfig is unique,
    /// agrees with the selected IC flash-map location, and its chip number resolves to one
    /// approved postbuild branch token.
    /// </summary>
    public static WorkbenchFirmwareContextSuggestion? TryReadFirmwareContextSuggestion(string icId, string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(icId);
        ArgumentException.ThrowIfNullOrWhiteSpace(path);

        return TryReadConsistentFirmwareConfigMetadata(icId, path, out FirmwareConfigMetadata metadata) &&
            metadata.ChipNumber != 0 &&
            TryResolveNumberTokenForFirmwareChipNumber(icId, metadata.ChipNumber, out string? numberToken)
            ? new WorkbenchFirmwareContextSuggestion(
                icId,
                numberToken!,
                metadata.ChipNumber,
                metadata.CommonFwVersion,
                metadata.ProjectId)
            : null;
    }

    private static bool TryReadBaseCommonFwVersion(
        string icId,
        string basePath,
        out string? commonFwVersion)
    {
        commonFwVersion = null;
        if (!TryReadConsistentFirmwareConfigMetadata(icId, basePath, out FirmwareConfigMetadata metadata) ||
            !metadata.IsFirmwareVersionBarValid)
        {
            return false;
        }

        commonFwVersion = metadata.CommonFwVersion;
        return true;
    }

    private static bool TryReadBaseCommonFwVersion(
        string icId,
        WorkbenchGeneralReplaceBaseSnapshot baseSnapshot,
        out string? commonFwVersion)
    {
        ArgumentNullException.ThrowIfNull(baseSnapshot);

        commonFwVersion = null;
        if (!TryReadConsistentFirmwareConfigMetadata(icId, baseSnapshot.AsSpan(), out FirmwareConfigMetadata metadata) ||
            !metadata.IsFirmwareVersionBarValid)
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
        IReadOnlyList<LegacyCombinerPostbuildProfile> profiles = IcMetadataFacade.GetPostbuildProfiles(icId);
        if (profiles.Count == 0)
        {
            return false;
        }

        string numericToken = chipNumber.ToString(CultureInfo.InvariantCulture);
        LegacyCombinerPostbuildBranch? branch = null;
        foreach (LegacyCombinerPostbuildProfile profile in profiles)
        {
            if (!profile.BranchRules.TryGetValue(numericToken, out LegacyCombinerPostbuildBranch candidate) &&
                (chipNumber <= 1 || !profile.BranchRules.TryGetValue(IcNumberSelectionTokens.Cascade, out candidate)))
            {
                return false;
            }

            if (branch is not null && branch != candidate)
            {
                return false;
            }

            branch = candidate;
        }

        if (branch is null)
        {
            return false;
        }

        foreach (IcNumberChoice choice in IcMetadataFacade.GetNumberSelectionChoices(icId))
        {
            bool matchesEveryProfile = profiles.All(profile =>
                profile.BranchRules.TryGetValue(choice.Token, out LegacyCombinerPostbuildBranch candidate) &&
                candidate == branch);
            if (matchesEveryProfile)
            {
                numberToken = choice.Token;
                return true;
            }
        }

        return false;
    }

    private static byte? TryReadNvtCopyChipNumber(string icId, string? tpPath)
    {
        return !string.IsNullOrWhiteSpace(tpPath) &&
            TryReadConsistentFirmwareConfigMetadata(icId, tpPath, out FirmwareConfigMetadata metadata)
                ? metadata.ChipNumber
                : null;
    }

    private static bool TryReadConsistentFirmwareConfigMetadata(
        string icId,
        string path,
        out FirmwareConfigMetadata metadata)
    {
        metadata = default;
        if (!IcMetadataFacade.TryGetFirmwareConfigStart(icId, out _) ||
            !File.Exists(path))
        {
            return false;
        }

        try
        {
            byte[] image = File.ReadAllBytes(path);
            return TryReadConsistentFirmwareConfigMetadata(icId, image, out metadata);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or ArgumentException or NotSupportedException)
        {
            return false;
        }
    }

    private static bool TryReadConsistentFirmwareConfigMetadata(
        string icId,
        ReadOnlySpan<byte> image,
        out FirmwareConfigMetadata metadata)
    {
        metadata = default;
        if (!IcMetadataFacade.TryGetFirmwareConfigStart(icId, out long firmwareConfigStart) ||
            !FirmwareConfigMetadataReader.TryRead(image, firmwareConfigStart, out FirmwareConfigMetadata primary) ||
            !FirmwareConfigMetadataReader.TryReadNvtCopy(image, out FirmwareConfigMetadata nvtCopy) ||
            !HaveEquivalentFirmwareConfigValues(primary, nvtCopy))
        {
            return false;
        }

        // Preserve the flash-map address as the public traceability location. The NVT copy
        // is the verified source of the displayed values, not a second canonical map row.
        metadata = nvtCopy with { FirmwareConfigStart = firmwareConfigStart };
        return true;
    }

    private static bool HaveEquivalentFirmwareConfigValues(
        FirmwareConfigMetadata primary,
        FirmwareConfigMetadata nvtCopy)
    {
        return primary.FirmwareVersion == nvtCopy.FirmwareVersion &&
            primary.FirmwareVersionBar == nvtCopy.FirmwareVersionBar &&
            primary.IsFirmwareVersionBarValid == nvtCopy.IsFirmwareVersionBarValid &&
            primary.FirmwareSubVersion == nvtCopy.FirmwareSubVersion &&
            primary.ChipNumber == nvtCopy.ChipNumber &&
            primary.CommonFwMajorVersion == nvtCopy.CommonFwMajorVersion &&
            primary.CommonFwMinorVersion == nvtCopy.CommonFwMinorVersion &&
            primary.CommonFwAdditionalVersion == nvtCopy.CommonFwAdditionalVersion &&
            primary.ProjectId == nvtCopy.ProjectId &&
            primary.Hardware == nvtCopy.Hardware;
    }

    private static bool TryResolvePostbuildProfileForDisplay(
        string icId,
        string? basePath,
        out LegacyCombinerPostbuildProfile? postbuildProfile)
    {
        postbuildProfile = null;
        IReadOnlyList<LegacyCombinerPostbuildProfile> profiles = IcMetadataFacade.GetPostbuildProfiles(icId);
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
            IcMetadataFacade.TrySelectPostbuildProfile(
                icId,
                commonFwVersion,
                out postbuildProfile,
                out _);

        return matchedBaseProfile ||
            (!hasReadableBase && IcMetadataFacade.TryGetDefaultPostbuildProfile(icId, out postbuildProfile));
    }
}
