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
        StandardMerge = CreateEphemeral(ExperienceIds.StandardMerge);
        AbMerge = CreateEphemeral(ExperienceIds.AbMerge);
        GeneralMerge = CreateEphemeral(ExperienceIds.GeneralMerge);
    }

    /// <summary>Standard Merge session.</summary>
    public AuthoringSessionState StandardMerge { get; }

    /// <summary>AB Merge session.</summary>
    public AuthoringSessionState AbMerge { get; }

    /// <summary>General Merge session.</summary>
    public AuthoringSessionState GeneralMerge { get; }

    /// <summary>Returns the stable session owned by one exact Merge workflow.</summary>
    public AuthoringSessionState ForWorkflow(string workflowId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(workflowId);
        return workflowId switch
        {
            ExperienceIds.StandardMerge => StandardMerge,
            ExperienceIds.AbMerge => AbMerge,
            ExperienceIds.GeneralMerge => GeneralMerge,
            _ => throw new ArgumentOutOfRangeException(
                nameof(workflowId),
                workflowId,
                "The fixed Merge session set accepts only Merge workflows."),
        };
    }

    /// <summary>
    /// Creates one ephemeral Merge session over the same transition policy.
    /// </summary>
    public static AuthoringSessionState CreateEphemeral(string workflowId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(workflowId);
        return workflowId switch
        {
            ExperienceIds.StandardMerge or
            ExperienceIds.AbMerge or
            ExperienceIds.GeneralMerge => new AuthoringSessionState(workflowId),
            _ => throw new ArgumentOutOfRangeException(
                nameof(workflowId),
                workflowId,
                "Ephemeral authoring sessions are limited to Merge workflows."),
        };
    }
}
