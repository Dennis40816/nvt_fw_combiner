namespace NvtFwCombiner.Domain.Composition;

/// <summary>Execution status for a composition plan.</summary>
public enum CompositionExecutionStatus
{
    /// <summary>The plan executed and produced output bytes.</summary>
    Succeeded,

    /// <summary>The plan could not execute because validation or input binding failed.</summary>
    Failed,
}
