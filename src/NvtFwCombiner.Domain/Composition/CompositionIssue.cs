namespace NvtFwCombiner.Domain.Composition;

/// <summary>Stable structured issue emitted by profile compilation or plan execution.</summary>
public sealed class CompositionIssue(
    string code,
    string message,
    string? operationId = null,
    string severity = CompositionIssueSeverity.Error)
{
    /// <summary>Stable machine-readable issue code.</summary>
    public string Code { get; } = RequiredValue.NotBlank(code);

    /// <summary>Human-readable issue text.</summary>
    public string Message { get; } = RequiredValue.NotBlank(message);

    /// <summary>Machine-readable severity: info, warning, or error.</summary>
    public string Severity { get; } = RequireSeverity(severity);

    /// <summary>Operation id associated with the issue when available.</summary>
    public string? OperationId { get; } = string.IsNullOrWhiteSpace(operationId) ? null : operationId;

    private static string RequireSeverity(string severity)
    {
        _ = RequiredValue.NotBlank(severity);
        return CompositionIssueSeverity.IsDefined(severity)
            ? severity
            : throw new ArgumentException($"Unsupported issue severity '{severity}'.", nameof(severity));
    }
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

    /// <summary>Maps the closed compiled severity without string inference.</summary>
    public static string FromCompiled(CompiledValidationSeverity severity)
    {
        return severity switch
        {
            CompiledValidationSeverity.Info => Info,
            CompiledValidationSeverity.Warning => Warning,
            CompiledValidationSeverity.Error => Error,
            _ => throw new ArgumentOutOfRangeException(nameof(severity), severity, null),
        };
    }

    /// <summary>Returns true when <paramref name="severity"/> is a supported report severity.</summary>
    public static bool IsDefined(string severity)
    {
        return severity is Info or Warning or Error;
    }
}
