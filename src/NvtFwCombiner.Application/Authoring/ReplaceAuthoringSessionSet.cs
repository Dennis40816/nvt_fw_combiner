using NvtFwCombiner.Domain.Composition;

namespace NvtFwCombiner.Application.Authoring;

/// <summary>
/// Caller-owned fixed set of isolated Replace authoring sessions. Desktop keeps
/// one set; CLI creates one ephemeral session per invocation.
/// </summary>
public sealed class ReplaceAuthoringSessionSet
{
    /// <summary>Creates exactly one session for every Replace workflow.</summary>
    public ReplaceAuthoringSessionSet()
    {
        DpReplace = CreateEphemeral(ExperienceIds.DpReplace);
        CtrlRamReplace = CreateEphemeral(ExperienceIds.CtrlRamReplace);
        GeneralReplace = CreateEphemeral(ExperienceIds.GeneralReplace);
    }

    /// <summary>DP Replace session.</summary>
    public AuthoringSessionState DpReplace { get; }

    /// <summary>CtrlRAM Replace session.</summary>
    public AuthoringSessionState CtrlRamReplace { get; }

    /// <summary>General Replace session.</summary>
    public AuthoringSessionState GeneralReplace { get; }

    /// <summary>Returns the stable session owned by one exact Replace workflow.</summary>
    public AuthoringSessionState ForWorkflow(string workflowId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(workflowId);
        return workflowId switch
        {
            ExperienceIds.DpReplace => DpReplace,
            ExperienceIds.CtrlRamReplace => CtrlRamReplace,
            ExperienceIds.GeneralReplace => GeneralReplace,
            _ => throw new ArgumentOutOfRangeException(
                nameof(workflowId),
                workflowId,
                "The fixed Replace session set accepts only Replace workflows."),
        };
    }

    /// <summary>
    /// Creates one ephemeral Replace session over the same transition policy.
    /// </summary>
    public static AuthoringSessionState CreateEphemeral(string workflowId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(workflowId);
        return workflowId switch
        {
            ExperienceIds.DpReplace or
            ExperienceIds.CtrlRamReplace or
            ExperienceIds.GeneralReplace => new AuthoringSessionState(workflowId),
            _ => throw new ArgumentOutOfRangeException(
                nameof(workflowId),
                workflowId,
                "Ephemeral authoring sessions are limited to Replace workflows."),
        };
    }
}
