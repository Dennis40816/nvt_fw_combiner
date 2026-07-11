using NvtFwCombiner.Domain.Composition;
using NvtFwCombiner.Profiles;

namespace NvtFwCombiner.Bootstrap;

public static partial class WorkbenchCompositionService
{
    /// <summary>Gets readable memory-map rows for the selected Standard Merge profile.</summary>
    public static IReadOnlyList<WorkbenchMemoryMapRow> GetStandardMergeMemoryMapRows(string icId)
    {
        return GetStandardMergeMemoryMapRows(icId, dpInputLength: null);
    }

    /// <summary>Gets readable memory-map rows for the selected Standard Merge profile and DP input length.</summary>
    public static IReadOnlyList<WorkbenchMemoryMapRow> GetStandardMergeMemoryMapRows(string icId, long? dpInputLength)
    {
        if (!StandardMergeProfilesByIc.TryGetValue(icId, out CompositionProfileDefinition? profile))
        {
            return
            [
                new WorkbenchMemoryMapRow(
                    "Profile",
                    "No profile",
                    "Blocked",
                    "No output",
                    $"Standard Merge is not available for {icId}."),
            ];
        }

        if (IsDpPerspectiveLengthPending(profile, dpInputLength))
        {
            return
            [
                new WorkbenchMemoryMapRow(
                    FormatStandardMergeInitializationRangeLabel(profile, dpInputLength),
                    "No output",
                    "Initialize",
                    $"Blank output 0x{profile.Initialization.FillByte:X2}",
                    FormatStandardMergeInitializationDetail(profile, dpInputLength)),
            ];
        }

        if (!TryResolveStandardMergeProfileForDisplay(profile, dpInputLength, out profile, out string profileIssue))
        {
            return
            [
                new WorkbenchMemoryMapRow(
                    "Profile",
                    "Profile",
                    "Blocked",
                    "No output",
                    profileIssue),
            ];
        }

        ProfileCompileResult compile = CompositionProfileCompiler.Compile(profile, []);
        if (!compile.IsSuccess)
        {
            return
            [
                new WorkbenchMemoryMapRow(
                    "Profile",
                    "Profile",
                    "Blocked",
                    "No output",
                    FormatIssues(compile.Issues)),
            ];
        }

        string initializedState = profile.Initialization.Kind == ImageInitializationKind.Blank
            ? $"Blank output 0x{profile.Initialization.FillByte:X2}"
            : $"Reference {profile.Initialization.ReferenceSpaceId}";
        List<WorkbenchMemoryMapRow> rows =
        [
            new(
                FormatStandardMergeInitializationRangeLabel(profile, dpInputLength),
                "No output",
                "Initialize",
                initializedState,
                FormatStandardMergeInitializationDetail(profile, dpInputLength)),
        ];

        foreach (CompositionOperation operation in compile.CompiledComposition!.Plan.OrderedOperations)
        {
            string afterSource = operation.SourceSpaceId is null
                ? operation.Kind.ToString()
                : AddressSpaceLabel(operation.SourceSpaceId);
            string sourceRange = operation.SourceRange is null
                ? "no source range"
                : FormatDisplayRange(operation.SourceRange.Value);
            rows.Add(new WorkbenchMemoryMapRow(
                FormatDisplayRange(operation.TargetRange),
                initializedState,
                ActionLabel(operation.Kind),
                afterSource,
                $"Sequence {operation.Sequence}: {operation.Kind} {sourceRange} -> output image {FormatDisplayRange(operation.TargetRange)}. Reason: {operation.Reason}"));
        }

        return rows;
    }

}
