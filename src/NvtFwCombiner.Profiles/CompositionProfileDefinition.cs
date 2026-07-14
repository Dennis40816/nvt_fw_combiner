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
        IReadOnlyList<CompositionOperation> operations,
        IReadOnlyList<ProfileRegion>? regions = null,
        IReadOnlyList<RegionAccessRule>? regionAccessRules = null,
        IcNumberInputMode? icNumberInputMode = null,
        IReadOnlyList<CompiledValidationRequirement>? validationRequirements = null)
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
        Regions = regions ?? [];
        RegionAccessRules = regionAccessRules ?? [];
        IcNumberInputMode = icNumberInputMode;
        ValidationRequirements = validationRequirements is null
            ? []
            : Array.AsReadOnly([.. validationRequirements]);
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

    /// <summary>Canonical memory regions declared by the profile.</summary>
    public IReadOnlyList<ProfileRegion> Regions { get; }

    /// <summary>Experience access rules resolved for this profile.</summary>
    public IReadOnlyList<RegionAccessRule> RegionAccessRules { get; }

    /// <summary>Profile-declared IC number input mode used by Replace UI/request binding.</summary>
    public IcNumberInputMode? IcNumberInputMode { get; }

    /// <summary>Closed final or staged validation requirements bound into the compiled artifact.</summary>
    public IReadOnlyList<CompiledValidationRequirement> ValidationRequirements { get; }
}

/// <summary>Canonical profile memory region used by compiler policy checks.</summary>
public sealed record ProfileRegion
{
    /// <summary>Creates a canonical region declaration.</summary>
    public ProfileRegion(
        string regionId,
        string addressSpaceId,
        ByteRange range,
        RegionAtomicity atomicity,
        RegionWritePolicy writePolicy,
        int alignment = 1,
        IReadOnlyList<string>? processorDependencyIds = null,
        IReadOnlyList<string>? classificationTags = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(regionId);
        ArgumentException.ThrowIfNullOrWhiteSpace(addressSpaceId);
        if (alignment <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(alignment), alignment, "Alignment must be positive.");
        }

        RegionId = regionId;
        AddressSpaceId = addressSpaceId;
        Range = range;
        Atomicity = atomicity;
        WritePolicy = writePolicy;
        Alignment = alignment;
        ProcessorDependencyIds = processorDependencyIds ?? [];
        ClassificationTags = classificationTags ?? [];
    }

    /// <summary>Stable region identifier.</summary>
    public string RegionId { get; }

    /// <summary>Address space that owns this region range.</summary>
    public string AddressSpaceId { get; }

    /// <summary>Half-open byte range in address-space coordinates.</summary>
    public ByteRange Range { get; }

    /// <summary>Smallest safe write unit for this region.</summary>
    public RegionAtomicity Atomicity { get; }

    /// <summary>Profile-declared write authority for this region.</summary>
    public RegionWritePolicy WritePolicy { get; }

    /// <summary>Required target alignment for explicit writes.</summary>
    public int Alignment { get; }

    /// <summary>Processors that own additional semantics for this region.</summary>
    public IReadOnlyList<string> ProcessorDependencyIds { get; }

    /// <summary>Profile-owned semantic tags such as dp, tp, tp-ctrlram, header, or protected.</summary>
    public IReadOnlyList<string> ClassificationTags { get; }
}

/// <summary>Resolved experience access rule for a profile region.</summary>
public sealed record RegionAccessRule
{
    /// <summary>Creates a region access rule.</summary>
    public RegionAccessRule(string regionId, RegionAccessKind access, string reason)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(regionId);
        ArgumentException.ThrowIfNullOrWhiteSpace(reason);

        RegionId = regionId;
        Access = access;
        Reason = reason;
    }

    /// <summary>Region governed by this access rule.</summary>
    public string RegionId { get; }

    /// <summary>Authoring access granted to this region.</summary>
    public RegionAccessKind Access { get; }

    /// <summary>Human-readable policy reason.</summary>
    public string Reason { get; }
}
