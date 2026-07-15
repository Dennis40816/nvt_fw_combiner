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

    /// <summary>Gets the profile-owned default Standard Merge output file name for the selected IC.</summary>
    public static string GetStandardMergeDefaultOutputFileName(string icId)
    {
        return FindStandardMergeProfileSummaryByIc(icId)?.DefaultOutputFileName ??
            StandardMergeFallbackOutputFileName;
    }

    /// <summary>Gets a compact, catalog-backed policy summary for the selected Standard Merge IC.</summary>
    public static string GetStandardMergePolicySummary(string icId)
    {
        return TryGetBuiltInV2StandardMergeContainerPolicy(icId, out V2StandardMergeContainerPolicy? policy)
            ? $"TP paste range: {FormatDisplayRange(policy.TpOverlayRange)}; {FormatDisplayRange(policy.CustomerInfoRange)} remains from the DP image."
            : "Address ranges come from the built-in Standard Merge profile.";
    }
}
