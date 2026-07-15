namespace NvtFwCombiner.Domain.Composition;

/// <summary>Profile identity proven by the legacy typed profile compiler.</summary>
internal sealed class LegacyCompiledCompositionIdentity
{
    internal LegacyCompiledCompositionIdentity(
        string profileId,
        string profileVersion,
        string icId,
        string modeId,
        string experienceId,
        CompositionKind compositionKind)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(profileId);
        ArgumentException.ThrowIfNullOrWhiteSpace(profileVersion);
        ArgumentException.ThrowIfNullOrWhiteSpace(icId);
        ArgumentException.ThrowIfNullOrWhiteSpace(modeId);
        ArgumentException.ThrowIfNullOrWhiteSpace(experienceId);
        if (!Enum.IsDefined(compositionKind))
        {
            throw new ArgumentOutOfRangeException(
                nameof(compositionKind),
                compositionKind,
                "Unknown composition kind.");
        }

        ProfileId = profileId;
        ProfileVersion = profileVersion;
        IcId = icId;
        ModeId = modeId;
        ExperienceId = experienceId;
        CompositionKind = compositionKind;
    }

    internal string ProfileId { get; }

    internal string ProfileVersion { get; }

    internal string IcId { get; }

    internal string ModeId { get; }

    internal string ExperienceId { get; }

    internal CompositionKind CompositionKind { get; }
}
