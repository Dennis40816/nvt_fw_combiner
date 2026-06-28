using NvtFwCombiner.Domain.Composition;

namespace NvtFwCombiner.Application.Composition;

/// <summary>Application request for previewing or building a compiled composition profile.</summary>
public sealed class CompositionRunRequest
{
    /// <summary>Creates a run request with typed profile, plan, input bindings, and output name.</summary>
    public CompositionRunRequest(
        string runId,
        CompositionRunProfile profile,
        CompositionPlan plan,
        IReadOnlyDictionary<string, string> artifactBindings,
        string outputFileName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(runId);
        ArgumentNullException.ThrowIfNull(profile);
        ArgumentNullException.ThrowIfNull(plan);
        ArgumentNullException.ThrowIfNull(artifactBindings);
        ArgumentException.ThrowIfNullOrWhiteSpace(outputFileName);

        RunId = runId;
        Profile = profile;
        Plan = plan;
        ArtifactBindings = artifactBindings;
        OutputFileName = outputFileName;
    }

    /// <summary>Stable run id for reports and diagnostics.</summary>
    public string RunId { get; }

    /// <summary>Profile metadata used for report generation.</summary>
    public CompositionRunProfile Profile { get; }

    /// <summary>Compiled plan to execute.</summary>
    public CompositionPlan Plan { get; }

    /// <summary>Maps required address-space ids to application artifact ids.</summary>
    public IReadOnlyDictionary<string, string> ArtifactBindings { get; }

    /// <summary>Output file name proposed by profile naming policy or caller override.</summary>
    public string OutputFileName { get; }
}
