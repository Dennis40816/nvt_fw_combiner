using System.Numerics;
using NvtFwCombiner.Domain.Firmware;

namespace NvtFwCombiner.Domain.Composition;

/// <summary>Closed runtime stage for a compiled v2 validation requirement.</summary>
public enum CompiledValidationStage
{
    /// <inheritdoc/>
    ProfileCompile,
    /// <inheritdoc/>
    InputLoad,
    /// <inheritdoc/>
    PreOperation,
    /// <inheritdoc/>
    PostOperation,
    /// <inheritdoc/>
    FinalOutput,
}

/// <summary>Closed report severity for a compiled validation requirement.</summary>
public enum CompiledValidationSeverity
{
    /// <inheritdoc/>
    Info,
    /// <inheritdoc/>
    Warning,
    /// <inheritdoc/>
    Error,
}

/// <summary>One metadata field reached through a profile metadata binding.</summary>
internal sealed record CompiledValidationFieldReference
{
    internal CompiledValidationFieldReference(string bindingId, string fieldId)
    {
        BindingId = RequiredValue.NotBlank(bindingId);
        FieldId = RequiredValue.NotBlank(fieldId);
    }

    internal string BindingId { get; }

    internal string FieldId { get; }
}

/// <summary>Base value for one exact compiled validation literal.</summary>
internal abstract record CompiledValidationScalarLiteral;

/// <summary>One arbitrary-precision exact integer literal.</summary>
internal sealed record CompiledValidationIntegerLiteral(BigInteger Value) :
    CompiledValidationScalarLiteral;

/// <summary>One non-empty exact text literal.</summary>
internal sealed record CompiledValidationTextLiteral : CompiledValidationScalarLiteral
{
    internal CompiledValidationTextLiteral(string value)
    {
        ArgumentException.ThrowIfNullOrEmpty(value);
        Value = value;
    }

    internal string Value { get; }
}

internal enum CompiledValidationMetadataComparison
{
    Equal,
    NotEqual,
    OneOf,
}

internal enum CompiledValidationRejectedBytePattern
{
    AllZero,
    AllFF,
}

/// <summary>Base value for one immutable canonical validation before or after range resolution.</summary>
public abstract record ValidationRequirementDefinition
{
    private protected ValidationRequirementDefinition(
        string ruleId,
        CompiledValidationStage stage,
        CompiledValidationSeverity severity,
        string issueCode)
    {
        RuleId = RequiredValue.NotBlank(ruleId);
        IssueCode = RequiredValue.NotBlank(issueCode);
        ClosedEnum.ThrowIfUndefined(stage, "Unknown validation stage.");
        ClosedEnum.ThrowIfUndefined(severity, "Unknown validation severity.");

        Stage = stage;
        Severity = severity;
    }

    /// <summary>Stable validation rule id.</summary>
    public string RuleId { get; }

    /// <summary>Closed execution stage.</summary>
    public CompiledValidationStage Stage { get; }

    /// <summary>Closed report severity.</summary>
    public CompiledValidationSeverity Severity { get; }

    /// <summary>Stable issue code emitted on failure.</summary>
    public string IssueCode { get; }
}

/// <summary>Base value for one resolved validation retained by a compiled composition.</summary>
public abstract record CompiledValidationRequirement : ValidationRequirementDefinition
{
    private protected CompiledValidationRequirement(
        string ruleId,
        CompiledValidationStage stage,
        CompiledValidationSeverity severity,
        string issueCode)
        : base(ruleId, stage, severity, issueCode)
    {
    }
}

/// <summary>Compares one bound metadata field to exact typed literals.</summary>
internal sealed record CompiledMetadataValueValidation : CompiledValidationRequirement
{
    private readonly CompiledValidationScalarLiteral[] _expectedValues;

    internal CompiledMetadataValueValidation(
        string ruleId,
        CompiledValidationStage stage,
        CompiledValidationSeverity severity,
        string issueCode,
        CompiledValidationFieldReference field,
        CompiledValidationMetadataComparison comparison,
        IEnumerable<CompiledValidationScalarLiteral> expectedValues)
        : base(ruleId, stage, severity, issueCode)
    {
        Field = RequiredValue.NotNull(field);
        ClosedEnum.ThrowIfUndefined(comparison, "Unknown metadata comparison.");

        _expectedValues = ImmutableReferenceSnapshot.Create(
            expectedValues,
            "Metadata validation expected values are invalid.",
            requireValue: true);
        DomainInvariant.Reject(
            _expectedValues.Distinct().Count() != _expectedValues.Length ||
            (comparison is CompiledValidationMetadataComparison.Equal or CompiledValidationMetadataComparison.NotEqual &&
             _expectedValues.Length != 1),
            "Metadata validation expected values are invalid.", nameof(expectedValues));

        Array.Sort(_expectedValues, CompareLiterals);
        Comparison = comparison;
        ExpectedValues = Array.AsReadOnly(_expectedValues);
    }

    internal CompiledValidationFieldReference Field { get; }

    internal CompiledValidationMetadataComparison Comparison { get; }

    internal IReadOnlyList<CompiledValidationScalarLiteral> ExpectedValues { get; }

    private static int CompareLiterals(CompiledValidationScalarLiteral left, CompiledValidationScalarLiteral right)
    {
        return (left, right) switch
        {
            (CompiledValidationIntegerLiteral first, CompiledValidationIntegerLiteral second) =>
                first.Value.CompareTo(second.Value),
            (CompiledValidationIntegerLiteral, CompiledValidationTextLiteral) => -1,
            (CompiledValidationTextLiteral, CompiledValidationIntegerLiteral) => 1,
            (CompiledValidationTextLiteral first, CompiledValidationTextLiteral second) =>
                StringComparer.Ordinal.Compare(first.Value, second.Value),
            _ => throw new InvalidOperationException("Unknown compiled validation literal kind."),
        };
    }
}

/// <summary>Rejects all-zero and all-FF values from one PID field.</summary>
internal sealed record CompiledPidSanityValidation : CompiledValidationRequirement
{
    internal CompiledPidSanityValidation(string ruleId, CompiledValidationStage stage, CompiledValidationSeverity severity, string issueCode, CompiledValidationFieldReference field)
        : base(ruleId, stage, severity, issueCode)
    {
        Field = RequiredValue.NotNull(field);
    }

    internal CompiledValidationFieldReference Field { get; }
}

/// <summary>Compares two bound metadata fields for exact typed equality.</summary>
internal sealed record CompiledMetadataEqualityValidation : CompiledValidationRequirement
{
    internal CompiledMetadataEqualityValidation(string ruleId, CompiledValidationStage stage, CompiledValidationSeverity severity, string issueCode, CompiledValidationFieldReference left, CompiledValidationFieldReference right)
        : base(ruleId, stage, severity, issueCode)
    {
        Left = RequiredValue.NotNull(left);
        Right = RequiredValue.NotNull(right);
    }

    internal CompiledValidationFieldReference Left { get; }

    internal CompiledValidationFieldReference Right { get; }
}

/// <summary>Rejects declared byte patterns from one metadata field.</summary>
internal sealed record CompiledRejectMetadataBytePatternValidation : CompiledValidationRequirement
{
    private readonly CompiledValidationRejectedBytePattern[] _rejectedPatterns;

    internal CompiledRejectMetadataBytePatternValidation(string ruleId, CompiledValidationStage stage, CompiledValidationSeverity severity, string issueCode, CompiledValidationFieldReference field, IEnumerable<CompiledValidationRejectedBytePattern> rejectedPatterns)
        : base(ruleId, stage, severity, issueCode)
    {
        Field = RequiredValue.NotNull(field);
        ArgumentNullException.ThrowIfNull(rejectedPatterns);
        _rejectedPatterns = [.. rejectedPatterns];
        DomainInvariant.Reject(
            _rejectedPatterns.Length == 0 || _rejectedPatterns.Any(static value => !ClosedEnum.IsDefined(value)) ||
            _rejectedPatterns.Distinct().Count() != _rejectedPatterns.Length,
            "Rejected byte patterns are invalid.", nameof(rejectedPatterns));

        Array.Sort(_rejectedPatterns);
        RejectedPatterns = Array.AsReadOnly(_rejectedPatterns);
    }

    internal CompiledValidationFieldReference Field { get; }

    internal IReadOnlyList<CompiledValidationRejectedBytePattern> RejectedPatterns { get; }
}

/// <summary>Asserts exact or masked bytes in one compiled logical view.</summary>
internal sealed record CompiledViewByteAssertionValidation : CompiledValidationRequirement
{
    internal CompiledViewByteAssertionValidation(string ruleId, CompiledValidationStage stage, CompiledValidationSeverity severity, string issueCode, string viewId, FirmwareMetadataBytes expected, FirmwareMetadataBytes? mask = null)
        : base(ruleId, stage, severity, issueCode)
    {
        ViewId = RequiredValue.NotBlank(viewId);
        Expected = RequiredValue.NotNull(expected);
        DomainInvariant.Reject(
            mask is not null && mask.Length != expected.Length,
            "Assertion masks must match expected byte length.", nameof(mask));

        Mask = mask;
    }

    internal string ViewId { get; }

    internal FirmwareMetadataBytes Expected { get; }

    internal FirmwareMetadataBytes? Mask { get; }
}
