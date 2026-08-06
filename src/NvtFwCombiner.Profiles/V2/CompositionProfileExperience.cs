using NvtFwCombiner.Domain.Composition;

namespace NvtFwCombiner.Profiles.V2;

/// <summary>Immutable orthogonal profile experience policy.</summary>
internal sealed record CompositionProfileExperience
{
    internal CompositionProfileExperience(
        string experienceId,
        LayoutPolicy layoutPolicy,
        InputPolicy inputPolicy,
        string displayNameKey)
    {
        ExperienceId = CompositionProfileValueRules.RequireId(experienceId, nameof(experienceId));
        if (!Enum.IsDefined(layoutPolicy))
        {
            throw new ArgumentOutOfRangeException(nameof(layoutPolicy), layoutPolicy, "Unknown layout policy.");
        }

        if (!Enum.IsDefined(inputPolicy))
        {
            throw new ArgumentOutOfRangeException(nameof(inputPolicy), inputPolicy, "Unknown input policy.");
        }

        ArgumentException.ThrowIfNullOrWhiteSpace(displayNameKey);
        LayoutPolicy = layoutPolicy;
        InputPolicy = inputPolicy;
    }

    internal string ExperienceId { get; }

    internal LayoutPolicy LayoutPolicy { get; }

    internal InputPolicy InputPolicy { get; }
}
