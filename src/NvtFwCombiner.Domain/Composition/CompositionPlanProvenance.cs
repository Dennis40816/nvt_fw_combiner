namespace NvtFwCombiner.Domain.Composition;

/// <summary>Profile identity carried by a compiled composition plan.</summary>
public sealed class CompositionPlanProvenance
{
    /// <summary>Creates profile identity for a compiled plan.</summary>
    public CompositionPlanProvenance(
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

        ProfileId = profileId;
        ProfileVersion = profileVersion;
        IcId = icId;
        ModeId = modeId;
        ExperienceId = experienceId;
        CompositionKind = compositionKind;
    }

    /// <summary>Stable profile id.</summary>
    public string ProfileId { get; }

    /// <summary>Profile content version.</summary>
    public string ProfileVersion { get; }

    /// <summary>IC id declared by the profile.</summary>
    public string IcId { get; }

    /// <summary>Mode id declared by the profile.</summary>
    public string ModeId { get; }

    /// <summary>Experience id declared by the profile.</summary>
    public string ExperienceId { get; }

    /// <summary>Merge or replace composition kind.</summary>
    public CompositionKind CompositionKind { get; }
}
