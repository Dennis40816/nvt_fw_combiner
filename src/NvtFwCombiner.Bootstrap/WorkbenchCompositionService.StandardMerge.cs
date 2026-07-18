namespace NvtFwCombiner.Bootstrap;

public static partial class WorkbenchCompositionService
{
    /// <summary>Returns true when the selected IC has a built-in standard merge profile.</summary>
    public static bool IsStandardMergeSupported(string icId)
    {
        return FindStandardMergeProfileSummaryByIc(icId) is { CompileSucceeded: true };
    }

    /// <summary>Gets the built-in standard merge profile id for the selected IC, if any.</summary>
    public static string? GetStandardMergeProfileId(string icId)
    {
        return FindStandardMergeProfileSummaryByIc(icId)?.ProfileId;
    }

    /// <summary>Gets required standard merge input address spaces for the selected IC.</summary>
    public static IReadOnlyList<string> GetStandardMergeRequiredAddressSpaces(string icId)
    {
        return FindStandardMergeProfileSummaryByIc(icId)?.RequiredInputAddressSpaceIds ?? [];
    }
}
