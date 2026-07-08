using System.Globalization;
using NvtFwCombiner.Application.ExternalTools;
using NvtFwCombiner.Application.FlashMaps;

namespace NvtFwCombiner.Bootstrap;

public static partial class WorkbenchCompositionService
{
    /// <summary>Creates the suggested FlashCode output file name from selected firmware metadata.</summary>
    public static WorkbenchOutputFileNameSuggestion CreateFlashCodeOutputFileName(
        string icId,
        IReadOnlyList<WorkbenchOutputNameCandidate> candidates,
        DateOnly? date = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(icId);
        ArgumentNullException.ThrowIfNull(candidates);

        string normalizedIc = NormalizeOutputIcId(icId);
        string dpVersion = FindDpVersionToken(normalizedIc, candidates) ?? "xx";
        string tpVersion = FindTpVersionToken(normalizedIc, candidates) ?? "xx";
        string dateToken = (date ?? DateOnly.FromDateTime(DateTime.Now)).ToString("yyyyMMdd", CultureInfo.InvariantCulture);
        return new WorkbenchOutputFileNameSuggestion(
            FormattableString.Invariant($"{normalizedIc}_FlashCode_D{dpVersion}T{tpVersion}_{dateToken}.bin"),
            dpVersion,
            dpVersion != "xx",
            tpVersion,
            tpVersion != "xx",
            dateToken);
    }

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

    private static string? FindDpVersionToken(
        string icId,
        IReadOnlyList<WorkbenchOutputNameCandidate> candidates)
    {
        foreach (WorkbenchOutputNameCandidate candidate in candidates.Where(candidate =>
                     candidate.Kind == WorkbenchOutputNameCandidateKind.Dp))
        {
            if (string.IsNullOrWhiteSpace(candidate.Path))
            {
                continue;
            }

            WorkbenchDpVersionMetadata? metadata = TryReadDpVersionMetadata(icId, candidate.Path);
            if (!string.IsNullOrWhiteSpace(metadata?.VersionToken))
            {
                return metadata.VersionToken;
            }
        }

        return null;
    }

    private static string? FindTpVersionToken(
        string icId,
        IReadOnlyList<WorkbenchOutputNameCandidate> candidates)
    {
        foreach (WorkbenchOutputNameCandidate candidate in candidates.OrderBy(candidate => candidate.Kind switch
                 {
                     WorkbenchOutputNameCandidateKind.Tp => 0,
                     WorkbenchOutputNameCandidateKind.CtrlRam => 1,
                     WorkbenchOutputNameCandidateKind.Base => 2,
                     WorkbenchOutputNameCandidateKind.Dp => 3,
                     WorkbenchOutputNameCandidateKind.Unknown => 4,
                     _ => 4,
                 }))
        {
            if (string.IsNullOrWhiteSpace(candidate.Path))
            {
                continue;
            }

            WorkbenchFirmwareConfigMetadata? metadata = TryReadFirmwareConfigMetadata(icId, candidate.Path);
            if (metadata is { IsFirmwareVersionBarValid: true })
            {
                return FormattableString.Invariant($"{metadata.FirmwareVersion:X2}{metadata.FirmwareSubVersion:X2}");
            }
        }

        return null;
    }

    private static string NormalizeOutputIcId(string icId)
    {
        string trimmed = icId.Trim();
        return trimmed.StartsWith("NT", StringComparison.OrdinalIgnoreCase)
            ? $"NT{trimmed[2..]}"
            : $"NT{trimmed}";
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

/// <summary>One selected firmware path candidate used by output naming metadata policy.</summary>
public sealed record WorkbenchOutputNameCandidate(
    WorkbenchOutputNameCandidateKind Kind,
    string? Path);

/// <summary>Firmware candidate role used by FlashCode output naming.</summary>
public enum WorkbenchOutputNameCandidateKind
{
    /// <summary>Unknown or generic BIN path.</summary>
    Unknown,

    /// <summary>Display/DP-family payload candidate.</summary>
    Dp,

    /// <summary>Touch-panel payload candidate.</summary>
    Tp,

    /// <summary>CtrlRAM payload candidate.</summary>
    CtrlRam,

    /// <summary>Base/reference firmware image candidate.</summary>
    Base,
}

/// <summary>Suggested FlashCode output name and the metadata tokens used to create it.</summary>
public sealed record WorkbenchOutputFileNameSuggestion(
    string FileName,
    string DpVersionToken,
    bool HasDpVersion,
    string TpVersionToken,
    bool HasTpVersion,
    string DateToken);
