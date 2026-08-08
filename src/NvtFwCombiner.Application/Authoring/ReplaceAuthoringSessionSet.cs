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
        DpReplace = new AuthoringSessionState(ExperienceIds.DpReplace);
        CtrlRamReplace = new AuthoringSessionState(ExperienceIds.CtrlRamReplace);
        GeneralReplace = new AuthoringSessionState(ExperienceIds.GeneralReplace);
    }

    /// <summary>DP Replace session.</summary>
    public AuthoringSessionState DpReplace { get; }

    /// <summary>CtrlRAM Replace session.</summary>
    public AuthoringSessionState CtrlRamReplace { get; }

    /// <summary>General Replace session.</summary>
    public AuthoringSessionState GeneralReplace { get; }
}
