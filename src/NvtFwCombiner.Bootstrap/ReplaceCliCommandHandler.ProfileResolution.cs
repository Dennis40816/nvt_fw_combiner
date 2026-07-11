using System.Diagnostics.CodeAnalysis;
using NvtFwCombiner.Profiles;

namespace NvtFwCombiner.Bootstrap;

internal static partial class ReplaceCliCommandHandler
{
    private static bool TryFindReplaceProfile(
        string command,
        string selector,
        [NotNullWhen(true)] out CompositionProfileDefinition? profile)
    {
        string normalized = selector.Trim();
        profile = BuiltInReplaceProfiles.All.FirstOrDefault(candidate =>
            string.Equals(candidate.ExperienceId, command, StringComparison.Ordinal) &&
            (string.Equals(candidate.ProfileId, normalized, StringComparison.OrdinalIgnoreCase) ||
                string.Equals(candidate.IcId, normalized, StringComparison.OrdinalIgnoreCase) ||
                string.Equals(CliCompositionRunSupport.GetIcNumber(candidate.IcId), normalized, StringComparison.OrdinalIgnoreCase)));
        return profile is not null;
    }
}
