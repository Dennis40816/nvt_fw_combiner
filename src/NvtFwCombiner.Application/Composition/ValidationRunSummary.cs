using NvtFwCombiner.Domain.Composition;

namespace NvtFwCombiner.Application.Composition;

/// <summary>Report-safe outcome for one compiled validation requirement.</summary>
public sealed class ValidationRunSummary
{
    /// <summary>Creates one immutable validation outcome.</summary>
    public ValidationRunSummary(
        string ruleId,
        CompiledValidationStage stage,
        ValidationRunStatus status,
        CompiledValidationSeverity severity,
        string issueCode)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(ruleId);
        ArgumentException.ThrowIfNullOrWhiteSpace(issueCode);
        if (!Enum.IsDefined(stage) || !Enum.IsDefined(status) || !Enum.IsDefined(severity))
        {
            throw new ArgumentOutOfRangeException(nameof(status), "Unknown validation outcome discriminator.");
        }

        RuleId = ruleId;
        Stage = stage;
        Status = status;
        Severity = severity;
        IssueCode = issueCode;
    }

    /// <summary>Stable compiled validation rule identifier.</summary>
    public string RuleId { get; }

    /// <summary>Lifecycle stage at which the rule was evaluated or skipped.</summary>
    public CompiledValidationStage Stage { get; }

    /// <summary>Observed outcome for the rule.</summary>
    public ValidationRunStatus Status { get; }

    /// <summary>Compiled severity that controls whether a failed rule blocks publication.</summary>
    public CompiledValidationSeverity Severity { get; }

    /// <summary>Stable issue code declared by the requirement or emitted by its failure branch.</summary>
    public string IssueCode { get; }
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
