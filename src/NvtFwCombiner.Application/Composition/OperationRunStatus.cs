namespace NvtFwCombiner.Application.Composition;

/// <summary>Application report status for one operation.</summary>
public enum OperationRunStatus
{
    /// <summary>The operation succeeded.</summary>
    Succeeded,

    /// <summary>The operation did not run because the composition failed before execution.</summary>
    Skipped,

    /// <summary>The operation ran but failed before producing an accepted mutation.</summary>
    Failed,
}
