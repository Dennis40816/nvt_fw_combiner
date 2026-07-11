using NvtFwCombiner.Domain.Composition;

namespace NvtFwCombiner.Profiles.V2;

/// <summary>Closed topology authoring surface exposed by an experience.</summary>
internal enum CompositionProfileTopologyAuthoring
{
    Hidden,
    SingleOrCascade,
    ExactCount,
}

/// <summary>Immutable orthogonal profile experience policy.</summary>
internal sealed record CompositionProfileExperience
{
    internal CompositionProfileExperience(
        string experienceId,
        AudienceKind audience,
        LayoutPolicy layoutPolicy,
        InputPolicy inputPolicy,
        CompositionProfileTopologyAuthoring topologyAuthoring,
        string displayNameKey)
    {
        ExperienceId = CompositionProfileValueRules.RequireId(experienceId, nameof(experienceId));
        if (!Enum.IsDefined(audience))
        {
            throw new ArgumentOutOfRangeException(nameof(audience), audience, "Unknown experience audience.");
        }

        if (!Enum.IsDefined(layoutPolicy))
        {
            throw new ArgumentOutOfRangeException(nameof(layoutPolicy), layoutPolicy, "Unknown layout policy.");
        }

        if (!Enum.IsDefined(inputPolicy))
        {
            throw new ArgumentOutOfRangeException(nameof(inputPolicy), inputPolicy, "Unknown input policy.");
        }

        if (!Enum.IsDefined(topologyAuthoring))
        {
            throw new ArgumentOutOfRangeException(
                nameof(topologyAuthoring),
                topologyAuthoring,
                "Unknown topology authoring policy.");
        }

        ArgumentException.ThrowIfNullOrWhiteSpace(displayNameKey);
        Audience = audience;
        LayoutPolicy = layoutPolicy;
        InputPolicy = inputPolicy;
        TopologyAuthoring = topologyAuthoring;
        DisplayNameKey = displayNameKey;
    }

    internal string ExperienceId { get; }

    internal AudienceKind Audience { get; }

    internal LayoutPolicy LayoutPolicy { get; }

    internal InputPolicy InputPolicy { get; }

    internal CompositionProfileTopologyAuthoring TopologyAuthoring { get; }

    internal string DisplayNameKey { get; }
}
