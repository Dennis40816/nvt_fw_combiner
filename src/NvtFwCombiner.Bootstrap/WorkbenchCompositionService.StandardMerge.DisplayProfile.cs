using NvtFwCombiner.Domain.Composition;

namespace NvtFwCombiner.Bootstrap;

public static partial class WorkbenchCompositionService
{
    /// <summary>Gets output address coverage text for the selected Standard Merge profile.</summary>
    public static string GetStandardMergeMemoryRangeLabel(string icId)
    {
        return GetStandardMergeMemoryRangeLabel(icId, dpInputLength: null);
    }

    /// <summary>Gets output address coverage text for the selected Standard Merge profile and DP input length.</summary>
    public static string GetStandardMergeMemoryRangeLabel(string icId, long? dpInputLength)
    {
        if (FindStandardMergeProfileSummaryByIc(icId) is null)
        {
            return "No Standard Merge profile";
        }

        bool lengthPending = IsStandardMergeDpLengthPending(icId, dpInputLength);
        return lengthPending
            ? "Selected DP BIN length pending"
            : !TryCompileStandardMerge(
                icId,
                dpInputLength,
                out CompiledComposition? composition,
                out IReadOnlyList<CompositionIssue> issues)
            ? FormatIssues(issues)
            : FormatFullRange(composition.Plan.OutputInitialization.Capacity);
    }

    private static bool IsStandardMergeDpLengthPending(string icId, long? dpInputLength)
    {
        return dpInputLength is null &&
            (IsBuiltInV2StandardMergeMapCapacityPending(icId) ||
             (!IsBuiltInV2StandardMerge(icId) && IcMetadataFacade.IsDpPerspectiveIc(icId)));
    }

    private static string FormatStandardMergeInitializationRangeLabel(
        CompiledComposition composition,
        bool lengthPending)
    {
        return lengthPending
            ? "Selected DP BIN length pending"
            : FormatFullRange(composition.Plan.OutputInitialization.Capacity);
    }

    private static string FormatStandardMergeInitializationState(ImageInitialization initialization)
    {
        return initialization.Kind == ImageInitializationKind.Blank
            ? $"Blank output 0x{initialization.FillByte:X2}"
            : $"Reference {initialization.ReferenceSpaceId}";
    }

    private static string FormatStandardMergeInitializationDetail(string icId, bool lengthPending)
    {
        return !IcMetadataFacade.IsDpPerspectiveIc(icId)
            ? "Start with the initialized image. Unlisted ranges keep this value until a later operation writes them."
            : lengthPending
                ? $"Start with the initialized image after selecting a DP BIN. Supported DP lengths are {FormatStandardMergeSupportedDpLengths()}."
                : "Start with the initialized image using the selected DP BIN length. Unlisted ranges keep this value until a later operation writes them.";
    }
}
