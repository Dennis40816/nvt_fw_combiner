namespace NvtFwCombiner.Domain.Composition;

/// <summary>Stable structured issue emitted by profile compilation or plan execution.</summary>
public sealed class CompositionIssue
{
    /// <summary>Creates a structured issue with an optional operation id.</summary>
    public CompositionIssue(string code, string message, string? operationId = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(code);
        ArgumentException.ThrowIfNullOrWhiteSpace(message);

        Code = code;
        Message = message;
        OperationId = string.IsNullOrWhiteSpace(operationId) ? null : operationId;
    }

    /// <summary>Stable machine-readable issue code.</summary>
    public string Code { get; }

    /// <summary>Human-readable issue text.</summary>
    public string Message { get; }

    /// <summary>Operation id associated with the issue when available.</summary>
    public string? OperationId { get; }
}
