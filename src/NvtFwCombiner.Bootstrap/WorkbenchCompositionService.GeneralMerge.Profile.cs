using NvtFwCombiner.Application.FlashMaps;
using NvtFwCombiner.Domain.Composition;

namespace NvtFwCombiner.Bootstrap;

public static partial class WorkbenchCompositionService
{
    private const byte GeneralMergeFillByte = 0x00;

    /// <summary>Gets the default General Merge output length text for the selected IC.</summary>
    public static string GetGeneralMergeDefaultOutputLength(string icId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(icId);

        WorkbenchProfileSummary? standardMergeProfile = FindStandardMergeProfileSummaryByIc(icId);
        if (standardMergeProfile is null)
        {
            return BootstrapRangeText.FormatHex(GetGeneralMergeCatalogFallbackCapacity(icId));
        }

        if (TryGetBuiltInV2StandardMergeAuthoringDefaultCapacity(
                icId,
                out long capacitySelectionDefault,
                out IReadOnlyList<CompositionIssue> capacityIssues))
        {
            return BootstrapRangeText.FormatHex(capacitySelectionDefault);
        }

        if (capacityIssues.Count != 0)
        {
            throw new InvalidOperationException(FormatIssues(capacityIssues));
        }

        if (!TryCompileStandardMerge(
                icId,
                dpInputLength: null,
                out CompiledComposition? composition,
                out IReadOnlyList<CompositionIssue> issues))
        {
            throw new InvalidOperationException(FormatIssues(issues));
        }

        long capacity = composition.Plan.OutputInitialization.Capacity;
        return BootstrapRangeText.FormatHex(capacity);
    }

    /// <summary>Gets the profile-owned default General Merge output file name for the selected IC.</summary>
    public static string GetGeneralMergeDefaultOutputFileName(string icId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(icId);

        return $"{icId.ToLowerInvariant()}-general-merge.bin";
    }

    /// <summary>Gets the legacy profile-id alias retained for persisted General Merge saved-rule compatibility.</summary>
    public static string GetGeneralMergeWorkbenchProfileId(string icId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(icId);

        return $"{icId.ToLowerInvariant()}-general-merge-workbench";
    }

    private static bool TryParseGeneralMergeCapacity(
        string outputLength,
        out long capacity,
        out CompositionIssue? issue)
    {
        if (!BootstrapRangeText.TryParseNonNegativeLong(outputLength, out capacity) || capacity <= 0)
        {
            issue = new CompositionIssue(
                WorkbenchIssueCodes.GeneralMergeCapacityInvalid,
                "General Merge output length must be a positive byte count.",
                "output-length");
            return false;
        }

        if (capacity > int.MaxValue)
        {
            issue = new CompositionIssue(
                WorkbenchIssueCodes.GeneralMergeCapacityUnsupported,
                "General Merge output length exceeds the supported in-memory composition size.",
                "output-length");
            return false;
        }

        issue = null;
        return true;
    }

    private static long GetGeneralMergeCatalogFallbackCapacity(string icId)
    {
        return TpFlashMapCatalog.TryFind(icId, out TpFlashMapProfile? profile) && profile is not null
            ? profile.Regions.Max(region => region.Range.EndExclusive)
            : throw new InvalidOperationException($"No Standard Merge profile or TP flash-map profile is available for '{icId}'.");
    }
}
