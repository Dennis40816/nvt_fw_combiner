using System.Diagnostics.CodeAnalysis;
using NvtFwCombiner.Domain.Composition;
using NvtFwCombiner.Profiles;

namespace NvtFwCombiner.Bootstrap;

public static partial class WorkbenchCompositionService
{
    private static readonly Dictionary<string, CompositionProfileDefinition> StandardMergeProfilesByIc =
        BuiltInStandardMergeProfiles.ExecutableStandardMergeProfiles
            .Where(static profile => !IsNt51920V2StandardMerge(profile.IcId))
            .ToDictionary(
            static profile => profile.IcId,
            StringComparer.Ordinal);

    internal static bool TryCompileStandardMerge(
        string icId,
        long? dpInputLength,
        [NotNullWhen(true)] out CompiledComposition? composition,
        out IReadOnlyList<CompositionIssue> issues)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(icId);
        composition = null;
        issues = [];
        if (IsNt51920V2StandardMerge(icId))
        {
            return TryCompileNt51920V2StandardMerge(out composition, out issues);
        }

        if (!StandardMergeProfilesByIc.TryGetValue(icId, out CompositionProfileDefinition? profile))
        {
            return false;
        }

        if (dpInputLength is long length &&
            BuiltInStandardMergeProfiles.IsDpPerspectiveStandardMergeProfile(profile))
        {
            try
            {
                profile = BuiltInStandardMergeProfiles.CreateDpPerspectiveProfileForInputLength(icId, length);
            }
            catch (ArgumentOutOfRangeException)
            {
                issues =
                [
                    new CompositionIssue(
                        WorkbenchIssueCodes.StandardMergeDpLengthUnsupported,
                        FormattableString.Invariant(
                            $"Selected DP BIN length 0x{length:X} is unsupported; {DpPerspectiveCatalog.FormatSupportedIcIds()} Standard Merge accepts DP input lengths {DpPerspectiveCatalog.FormatSupportedLengths()}.")),
                ];
                return false;
            }
        }

        ProfileCompileResult compile = CompositionProfileCompiler.Compile(profile, []);
        composition = compile.CompiledComposition;
        issues = compile.Issues;
        return compile.IsSuccess;
    }

    private static string FormatStandardMergeSupportedDpLengths()
    {
        return DpPerspectiveCatalog.FormatSupportedLengths();
    }
}
