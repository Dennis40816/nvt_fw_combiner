using NvtFwCombiner.Profiles;

namespace NvtFwCombiner.Bootstrap;

public static partial class WorkbenchCompositionService
{
    /// <summary>Returns true when the selected IC has a built-in standard merge profile.</summary>
    public static bool IsStandardMergeSupported(string icId)
    {
        return HasCanonicalCapability(icId, IcWorkflowIds.StandardMerge) ||
            StringComparer.Ordinal.Equals(icId, "NT51928");
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

    /// <summary>
    /// Gets every fixed Standard Merge input address space exposed for authoring,
    /// including optional members of a profile-owned selection group.
    /// </summary>
    public static IReadOnlyList<string> GetStandardMergeInputAddressSpaces(string icId)
    {
        IReadOnlyList<string> required = GetStandardMergeRequiredAddressSpaces(icId);
        return !BuiltInV2RegistrationRegistry.StandardMergeByIc.TryGetValue(
                icId,
                out BuiltInV2Registration? registration)
            ? required
            : Array.AsReadOnly(
            [
                .. required
                    .Concat(registration.InputSelectionGroupMemberSlotIds)
                    .Distinct(StringComparer.Ordinal)
                    .Order(StringComparer.Ordinal),
            ]);
    }
}
