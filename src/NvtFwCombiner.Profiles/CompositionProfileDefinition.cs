using NvtFwCombiner.Domain.Composition;

namespace NvtFwCombiner.Profiles;

/// <summary>Typed profile definition used by the 0.2 core compiler before JSON loading is introduced.</summary>
public sealed class CompositionProfileDefinition
{
    /// <summary>Creates a profile definition from already parsed typed profile data.</summary>
    public CompositionProfileDefinition(
        string profileId,
        string profileVersion,
        CompositionKind compositionKind,
        string experienceId,
        ImageInitialization initialization,
        IReadOnlyList<AddressSpace> addressSpaces,
        IReadOnlyList<CompositionOperation> operations)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(profileId);
        ArgumentException.ThrowIfNullOrWhiteSpace(profileVersion);
        ArgumentException.ThrowIfNullOrWhiteSpace(experienceId);
        ArgumentNullException.ThrowIfNull(initialization);
        ArgumentNullException.ThrowIfNull(addressSpaces);
        ArgumentNullException.ThrowIfNull(operations);

        ProfileId = profileId;
        ProfileVersion = profileVersion;
        CompositionKind = compositionKind;
        ExperienceId = experienceId;
        Initialization = initialization;
        AddressSpaces = addressSpaces;
        Operations = operations;
    }

    /// <summary>Stable profile id.</summary>
    public string ProfileId { get; }

    /// <summary>Profile content version.</summary>
    public string ProfileVersion { get; }

    /// <summary>Merge or replace composition kind.</summary>
    public CompositionKind CompositionKind { get; }

    /// <summary>Approved experience id from the experience catalog.</summary>
    public string ExperienceId { get; }

    /// <summary>Image initialization declared by the profile.</summary>
    public ImageInitialization Initialization { get; }

    /// <summary>Address spaces declared by the profile.</summary>
    public IReadOnlyList<AddressSpace> AddressSpaces { get; }

    /// <summary>Fixed operations declared by the profile.</summary>
    public IReadOnlyList<CompositionOperation> Operations { get; }
}
