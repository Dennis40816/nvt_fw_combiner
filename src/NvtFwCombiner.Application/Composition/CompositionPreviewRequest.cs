using NvtFwCombiner.Domain.Composition;

namespace NvtFwCombiner.Application.Composition;

/// <summary>Application request for previewing a compiled composition plan.</summary>
public sealed class CompositionPreviewRequest
{
    /// <summary>Creates a preview request with address-space to artifact bindings.</summary>
    public CompositionPreviewRequest(
        CompositionPlan plan,
        IReadOnlyDictionary<string, string> artifactBindings)
    {
        ArgumentNullException.ThrowIfNull(plan);
        ArgumentNullException.ThrowIfNull(artifactBindings);

        Plan = plan;
        ArtifactBindings = artifactBindings;
    }

    /// <summary>Compiled plan to execute for preview.</summary>
    public CompositionPlan Plan { get; }

    /// <summary>Maps required input address-space ids to application artifact ids.</summary>
    public IReadOnlyDictionary<string, string> ArtifactBindings { get; }
}
