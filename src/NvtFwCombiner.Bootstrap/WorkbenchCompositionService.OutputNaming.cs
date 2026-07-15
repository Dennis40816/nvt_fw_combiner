using System.Globalization;
using NvtFwCombiner.Profiles;

namespace NvtFwCombiner.Bootstrap;

public static partial class WorkbenchCompositionService
{
    private const string UnknownVersionToken = "xxxx";

    /// <summary>Creates the suggested FlashCode output file name from selected firmware metadata.</summary>
    public static WorkbenchOutputFileNameSuggestion CreateFlashCodeOutputFileName(
        string icId,
        IReadOnlyList<WorkbenchOutputNameCandidate> candidates,
        DateOnly? date = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(icId);
        ArgumentNullException.ThrowIfNull(candidates);

        string normalizedIc = IcSupportCatalog.NormalizeIcId(icId);
        string dpVersion = FindDpVersionToken(normalizedIc, candidates) ?? UnknownVersionToken;
        string tpVersion = FindTpVersionToken(normalizedIc, candidates) ?? UnknownVersionToken;
        string dateToken = (date ?? DateOnly.FromDateTime(DateTime.Now)).ToString("yyyyMMdd", CultureInfo.InvariantCulture);
        return new WorkbenchOutputFileNameSuggestion(
            FormattableString.Invariant($"{normalizedIc}_FlashCode_D{dpVersion}T{tpVersion}_{dateToken}.bin"),
            dpVersion,
            dpVersion != UnknownVersionToken,
            tpVersion,
            tpVersion != UnknownVersionToken,
            dateToken);
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
}
