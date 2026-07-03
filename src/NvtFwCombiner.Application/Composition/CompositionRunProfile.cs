using NvtFwCombiner.Domain.Composition;

namespace NvtFwCombiner.Application.Composition;

/// <summary>Profile metadata required by application run reports.</summary>
public sealed class CompositionRunProfile
{
    /// <summary>Creates profile metadata for a preview or build run.</summary>
    public CompositionRunProfile(
        string profileId,
        string profileVersion,
        string icId,
        string modeId,
        string experienceId,
        CompositionKind compositionKind,
        IcNumberInputMode? icNumberInputMode = null)
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
        IcNumberInputMode = icNumberInputMode;
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

    /// <summary>Profile-declared IC number input mode for Replace runs.</summary>
    public IcNumberInputMode? IcNumberInputMode { get; }
}
