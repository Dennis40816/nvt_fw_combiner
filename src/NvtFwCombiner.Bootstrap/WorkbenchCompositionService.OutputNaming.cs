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
        return CreateOutputFileNameSuggestion(normalizedIc, dpVersion, tpVersion, date);
    }

    /// <summary>Creates the same suggestion from immutable inspection DTOs without reading firmware files.</summary>
    public static WorkbenchOutputFileNameSuggestion CreateFlashCodeOutputFileNameFromInspections(
        string icId,
        IReadOnlyList<WorkbenchOutputNameInspectionCandidate> candidates,
        DateOnly? date = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(icId);
        ArgumentNullException.ThrowIfNull(candidates);

        string normalizedIc = IcSupportCatalog.NormalizeIcId(icId);
        string dpVersion = FindInspectedDpVersionToken(candidates) ?? UnknownVersionToken;
        string tpVersion = FindInspectedTpVersionToken(candidates) ?? UnknownVersionToken;
        return CreateOutputFileNameSuggestion(normalizedIc, dpVersion, tpVersion, date);
    }

    private static WorkbenchOutputFileNameSuggestion CreateOutputFileNameSuggestion(
        string normalizedIc,
        string dpVersion,
        string tpVersion,
        DateOnly? date)
    {
        string dateToken = (date ?? DateOnly.FromDateTime(DateTime.Now)).ToString("yyyyMMdd", CultureInfo.InvariantCulture);
        return new WorkbenchOutputFileNameSuggestion(
            FormattableString.Invariant($"{normalizedIc}_FlashCode_D{dpVersion}T{tpVersion}_{dateToken}.bin"),
            dpVersion,
            dpVersion != UnknownVersionToken,
            tpVersion,
            tpVersion != UnknownVersionToken,
            dateToken);
    }

    private static string? FindInspectedDpVersionToken(
        IReadOnlyList<WorkbenchOutputNameInspectionCandidate> candidates)
    {
        bool hasDpInput = candidates.Any(static candidate =>
            candidate.Kind == WorkbenchOutputNameCandidateKind.Dp);
        return candidates
            .Where(candidate => candidate.Kind == (hasDpInput
                ? WorkbenchOutputNameCandidateKind.Dp
                : WorkbenchOutputNameCandidateKind.Base))
            .Select(static candidate => candidate.Inspection?.DpVersion?.VersionToken)
            .FirstOrDefault(static versionToken => !string.IsNullOrWhiteSpace(versionToken));
    }

    private static string? FindInspectedTpVersionToken(
        IReadOnlyList<WorkbenchOutputNameInspectionCandidate> candidates)
    {
        WorkbenchFirmwareConfigMetadata? metadata = candidates
            .OrderBy(static candidate => OutputNameCandidateOrder(candidate.Kind))
            .Select(static candidate => candidate.Inspection?.FirmwareConfig)
            .FirstOrDefault(static candidateMetadata => candidateMetadata?.IsFirmwareVersionBarValid == true);
        return metadata is null
            ? null
            : FormattableString.Invariant($"{metadata.FirmwareVersion:X2}{metadata.FirmwareSubVersion:X2}");
    }

    private static int OutputNameCandidateOrder(WorkbenchOutputNameCandidateKind kind)
    {
        return kind switch
        {
            WorkbenchOutputNameCandidateKind.Tp => 0,
            WorkbenchOutputNameCandidateKind.CtrlRam => 1,
            WorkbenchOutputNameCandidateKind.Base => 2,
            WorkbenchOutputNameCandidateKind.Dp => 3,
            WorkbenchOutputNameCandidateKind.Unknown => 4,
            _ => 4,
        };
    }

    private static string? FindDpVersionToken(
        string icId,
        IReadOnlyList<WorkbenchOutputNameCandidate> candidates)
    {
        bool hasDpInput = candidates.Any(static candidate =>
            candidate.Kind == WorkbenchOutputNameCandidateKind.Dp);
        foreach (WorkbenchOutputNameCandidate candidate in candidates.Where(candidate =>
                     candidate.Kind == (hasDpInput
                         ? WorkbenchOutputNameCandidateKind.Dp
                         : WorkbenchOutputNameCandidateKind.Base)))
        {
            if (string.IsNullOrWhiteSpace(candidate.Path))
            {
                continue;
            }

            WorkbenchDpVersionMetadata? metadata = TryReadDpVersionMetadata(icId, candidate.Path);
            if (metadata is WorkbenchDpVersionMetadata value &&
                !string.IsNullOrWhiteSpace(value.VersionToken))
            {
                return value.VersionToken;
            }
        }

        return null;
    }

    private static string? FindTpVersionToken(
        string icId,
        IReadOnlyList<WorkbenchOutputNameCandidate> candidates)
    {
        foreach (WorkbenchOutputNameCandidate candidate in candidates.OrderBy(static candidate =>
                     OutputNameCandidateOrder(candidate.Kind)))
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
