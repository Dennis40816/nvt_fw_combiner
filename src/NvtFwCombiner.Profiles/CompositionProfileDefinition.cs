using NvtFwCombiner.Domain.Composition;

namespace NvtFwCombiner.Profiles;

/// <summary>Typed profile definition used by the 0.2 core compiler before JSON loading is introduced.</summary>
public sealed class CompositionProfileDefinition
{
    /// <summary>Creates a profile definition from already parsed typed profile data.</summary>
    public CompositionProfileDefinition(
        string profileId,
        string profileVersion,
        string icId,
        string modeId,
        CompositionKind compositionKind,
        string experienceId,
        string defaultOutputFileName,
        ImageInitialization initialization,
        IReadOnlyList<AddressSpace> addressSpaces,
        IReadOnlyList<CompositionOperation> operations)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(profileId);
        ArgumentException.ThrowIfNullOrWhiteSpace(profileVersion);
        ArgumentException.ThrowIfNullOrWhiteSpace(icId);
        ArgumentException.ThrowIfNullOrWhiteSpace(modeId);
        ArgumentException.ThrowIfNullOrWhiteSpace(experienceId);
        ArgumentException.ThrowIfNullOrWhiteSpace(defaultOutputFileName);
        ArgumentNullException.ThrowIfNull(initialization);
        ArgumentNullException.ThrowIfNull(addressSpaces);
        ArgumentNullException.ThrowIfNull(operations);

        ProfileId = profileId;
        ProfileVersion = profileVersion;
        IcId = icId;
        ModeId = modeId;
        CompositionKind = compositionKind;
        ExperienceId = experienceId;
        DefaultOutputFileName = defaultOutputFileName;
        Initialization = initialization;
        AddressSpaces = addressSpaces;
        Operations = operations;
    }

    /// <summary>Stable profile id.</summary>
    public string ProfileId { get; }

    /// <summary>Profile content version.</summary>
    public string ProfileVersion { get; }

    /// <summary>IC id declared by the profile.</summary>
    public string IcId { get; }

    /// <summary>Mode id declared by the profile.</summary>
    public string ModeId { get; }

    /// <summary>Merge or replace composition kind.</summary>
    public CompositionKind CompositionKind { get; }

    /// <summary>Approved experience id from the experience catalog.</summary>
    public string ExperienceId { get; }

    /// <summary>Default output file name rendered by the profile naming policy.</summary>
    public string DefaultOutputFileName { get; }

    /// <summary>Image initialization declared by the profile.</summary>
    public ImageInitialization Initialization { get; }

    /// <summary>Address spaces declared by the profile.</summary>
    public IReadOnlyList<AddressSpace> AddressSpaces { get; }

    /// <summary>Fixed operations declared by the profile.</summary>
    public IReadOnlyList<CompositionOperation> Operations { get; }
}
