namespace NvtFwCombiner.Bootstrap;

public static partial class WorkbenchCompositionService
{
    private const string Nt51928IcId = "NT51928";

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

    /// <summary>Gets every Standard Merge input exposed for the selected IC.</summary>
    public static IReadOnlyList<WorkbenchStandardMergeInputSlot> GetStandardMergeInputSlots(string icId)
    {
        WorkbenchProfileSummary? summary =
            BuiltInV2RegistrationRegistry.StandardMergeByIc.GetValueOrDefault(icId)?.CreateProfileSummary();
        return summary is null
            ? []
            : Array.AsReadOnly(summary.RequiredInputAddressSpaceIds
                .Select(addressSpaceId => new WorkbenchStandardMergeInputSlot(
                    addressSpaceId,
                    !StringComparer.Ordinal.Equals(icId, Nt51928IcId) ||
                    !StringComparer.Ordinal.Equals(addressSpaceId, WorkbenchAddressSpaceIds.LdInput)))
                .ToArray());
    }

    /// <summary>Gets required standard merge input address spaces for the selected IC.</summary>
    public static IReadOnlyList<string> GetStandardMergeRequiredAddressSpaces(string icId)
    {
        return Array.AsReadOnly(GetStandardMergeInputSlots(icId)
            .Where(static slot => slot.Required)
            .Select(static slot => slot.AddressSpaceId)
            .ToArray());
    }
}
