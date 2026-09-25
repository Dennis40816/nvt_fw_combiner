using NvtFwCombiner.Contracts.Profiles;
using NvtFwCombiner.Domain.Composition;

namespace NvtFwCombiner.Profiles.V2;

internal static partial class CompositionProfileNormalizer
{
    private static AbCodeDefinition? NormalizeAb(CompositionProfileDocument document)
    {
        return document.Ab is null ? null :
            document.SchemaVersion == "2.17" && document.Experience.ExperienceId == ExperienceIds.AbMerge &&
            document.Ab.IsFullySymmetric is { } symmetric
            ? new AbCodeDefinition(symmetric)
            : throw Error("ab", "AB characteristics require schema 2.17, AB Merge and an explicit symmetry boolean.");
    }
}
