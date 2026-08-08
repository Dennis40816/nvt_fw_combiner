using NvtFwCombiner.Domain.Composition;

namespace NvtFwCombiner.Application.Authoring;

/// <summary>
/// Caller-owned fixed set of isolated Merge authoring sessions. Desktop keeps
/// one set; CLI creates one ephemeral session per invocation.
/// </summary>
public sealed class MergeAuthoringSessionSet
{
    /// <summary>Creates exactly one session for every Merge workflow.</summary>
    public MergeAuthoringSessionSet()
    {
        StandardMerge = new AuthoringSessionState(ExperienceIds.StandardMerge);
        AbMerge = new AuthoringSessionState(ExperienceIds.AbMerge);
        GeneralMerge = new AuthoringSessionState(ExperienceIds.GeneralMerge);
    }

    /// <summary>Standard Merge session.</summary>
    public AuthoringSessionState StandardMerge { get; }

    /// <summary>AB Merge session.</summary>
    public AuthoringSessionState AbMerge { get; }

    /// <summary>General Merge session.</summary>
    public AuthoringSessionState GeneralMerge { get; }
}
