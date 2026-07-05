namespace NvtFwCombiner.Domain.Composition;

/// <summary>Stable structured issue emitted by profile compilation or plan execution.</summary>
public sealed class CompositionIssue
{
    /// <summary>Creates a structured issue with an optional operation id.</summary>
    public CompositionIssue(
        string code,
        string message,
        string? operationId = null,
        string severity = CompositionIssueSeverity.Error)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(code);
        ArgumentException.ThrowIfNullOrWhiteSpace(message);
        ArgumentException.ThrowIfNullOrWhiteSpace(severity);
        if (!CompositionIssueSeverity.IsDefined(severity))
        {
            throw new ArgumentException($"Unsupported issue severity '{severity}'.", nameof(severity));
        }

        Code = code;
        Message = message;
        OperationId = string.IsNullOrWhiteSpace(operationId) ? null : operationId;
        Severity = severity;
    }

    /// <summary>Stable machine-readable issue code.</summary>
    public string Code { get; }

    /// <summary>Machine-readable severity: info, warning, or error.</summary>
    public string Severity { get; }

    /// <summary>Human-readable issue text.</summary>
    public string Message { get; }

    /// <summary>Operation id associated with the issue when available.</summary>
    public string? OperationId { get; }
}

/// <summary>Stable issue severity values used by run reports.</summary>
public static class CompositionIssueSeverity
{
    /// <summary>Informational diagnostic that does not block execution.</summary>
    public const string Info = "info";

    /// <summary>Warning diagnostic that does not block execution but requires review.</summary>
    public const string Warning = "warning";

    /// <summary>Error diagnostic that blocks successful execution.</summary>
    public const string Error = "error";

    /// <summary>Returns true when <paramref name="severity"/> is a supported report severity.</summary>
    public static bool IsDefined(string severity)
    {
        return severity is Info or Warning or Error;
    }
}
