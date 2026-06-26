namespace NvtFwCombiner.Domain.Composition;

/// <summary>Describes one user-facing composition experience and its authoring policy.</summary>
public sealed record ExperienceDescriptor
{
    /// <summary>Creates an immutable experience descriptor.</summary>
    public ExperienceDescriptor(
        string experienceId,
        CompositionKind compositionKind,
        AudienceKind audience,
        LayoutPolicy layoutPolicy,
        InputPolicy inputPolicy)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(experienceId);
        ExperienceId = experienceId;
        CompositionKind = compositionKind;
        Audience = audience;
        LayoutPolicy = layoutPolicy;
        InputPolicy = inputPolicy;
    }

    /// <summary>Stable profile and UI identifier for the experience.</summary>
    public string ExperienceId { get; }

    /// <summary>Merge or replace execution model used by the experience.</summary>
    public CompositionKind CompositionKind { get; }

    /// <summary>Audience allowed to author this experience.</summary>
    public AudienceKind Audience { get; }

    /// <summary>Layout authority granted to this experience.</summary>
    public LayoutPolicy LayoutPolicy { get; }

    /// <summary>Input binding policy granted to this experience.</summary>
    public InputPolicy InputPolicy { get; }

    /// <summary>Image initialization required by the execution model.</summary>
    public ImageInitializationKind RequiredInitialization =>
        CompositionKind == CompositionKind.Merge
            ? ImageInitializationKind.Blank
            : ImageInitializationKind.Reference;
}
