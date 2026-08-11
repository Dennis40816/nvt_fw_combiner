using NvtFwCombiner.Domain.Composition;

namespace NvtFwCombiner.Application.Composition;

/// <summary>Report-safe outcome for one compiled validation requirement.</summary>
public sealed class ValidationRunSummary(
    string ruleId,
    CompiledValidationStage stage,
    ValidationRunStatus status,
    CompiledValidationSeverity severity,
    string issueCode)
{
    /// <summary>Stable compiled validation rule identifier.</summary>
    public string RuleId { get; } = CompositionSummaryValue.NotBlank(ruleId, nameof(ruleId));

    /// <summary>Lifecycle stage at which the rule was evaluated or skipped.</summary>
    public CompiledValidationStage Stage { get; } = Enum.IsDefined(stage)
        ? stage
        : throw new ArgumentOutOfRangeException(nameof(stage), stage, "Unknown validation stage.");

    /// <summary>Observed outcome for the rule.</summary>
    public ValidationRunStatus Status { get; } = Enum.IsDefined(status)
        ? status
        : throw new ArgumentOutOfRangeException(nameof(status), status, "Unknown validation status.");

    /// <summary>Compiled severity that controls whether a failed rule blocks publication.</summary>
    public CompiledValidationSeverity Severity { get; } = Enum.IsDefined(severity)
        ? severity
        : throw new ArgumentOutOfRangeException(nameof(severity), severity, "Unknown validation severity.");

    /// <summary>Stable issue code declared by the requirement or emitted by its failure branch.</summary>
    public string IssueCode { get; } = CompositionSummaryValue.NotBlank(issueCode, nameof(issueCode));
}

/// <summary>Closed outcome of one runtime validation requirement.</summary>
public enum ValidationRunStatus
{
    /// <summary>The requirement was evaluated and passed.</summary>
    Passed,

    /// <summary>The requirement was evaluated and failed.</summary>
    Failed,

    /// <summary>The requirement could not run because an earlier run stage failed.</summary>
    Skipped,
}
