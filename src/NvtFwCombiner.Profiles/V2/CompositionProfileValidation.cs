using System.Numerics;
using NvtFwCombiner.Domain.Composition;

namespace NvtFwCombiner.Profiles.V2;

/// <summary>One metadata field reached through a profile metadata binding.</summary>
internal sealed record CompositionProfileMetadataFieldReference
{
    internal CompositionProfileMetadataFieldReference(string bindingId, string fieldId)
    {
        BindingId = CompositionProfileValueRules.RequireId(bindingId, nameof(bindingId));
        FieldId = CompositionProfileValueRules.RequireId(fieldId, nameof(fieldId));
    }

    internal string BindingId { get; }

    internal string FieldId { get; }
}

/// <summary>Base value for one schema scalar before exact family-field conversion.</summary>
internal abstract record CompositionProfileScalarLiteral;

/// <summary>One arbitrary-precision JSON integer literal.</summary>
internal sealed record CompositionProfileIntegerLiteral : CompositionProfileScalarLiteral
{
    internal CompositionProfileIntegerLiteral(BigInteger value)
    {
        Value = value;
    }

    internal BigInteger Value { get; }
}

/// <summary>One non-empty JSON string literal pending exact field conversion.</summary>
internal sealed record CompositionProfileTextLiteral : CompositionProfileScalarLiteral
{
    internal CompositionProfileTextLiteral(string value)
    {
        ArgumentException.ThrowIfNullOrEmpty(value);
        Value = value;
    }

    internal string Value { get; }
}

/// <summary>Base value for one normalized profile validation.</summary>
internal abstract record CompositionProfileValidation
{
    protected CompositionProfileValidation(
        string ruleId,
        CompiledValidationStage stage,
        CompiledValidationSeverity severity,
        string issueCode)
    {
        RuleId = CompositionProfileValueRules.RequireId(ruleId, nameof(ruleId));
        if (!Enum.IsDefined(stage))
        {
            throw new ArgumentOutOfRangeException(nameof(stage), stage, "Unknown validation stage.");
        }

        if (!Enum.IsDefined(severity))
        {
            throw new ArgumentOutOfRangeException(nameof(severity), severity, "Unknown validation severity.");
        }

        IssueCode = CompositionProfileValueRules.RequireIssueCode(issueCode, nameof(issueCode));
        Stage = stage;
        Severity = severity;
    }

    internal string RuleId { get; }

    internal CompiledValidationStage Stage { get; }

    internal CompiledValidationSeverity Severity { get; }

    internal string IssueCode { get; }

}

/// <summary>Compares one bound metadata field to exact pending field-context literals.</summary>
internal sealed record MetadataValueProfileValidation : CompositionProfileValidation
{
    private readonly CompositionProfileScalarLiteral[] _expectedValues;

    internal MetadataValueProfileValidation(
        string ruleId,
        CompiledValidationStage stage,
        CompiledValidationSeverity severity,
        string issueCode,
        CompositionProfileMetadataFieldReference field,
        CompiledValidationMetadataComparison comparison,
        IEnumerable<CompositionProfileScalarLiteral> expectedValues)
        : base(ruleId, stage, severity, issueCode)
    {
        ArgumentNullException.ThrowIfNull(field);
        if (!Enum.IsDefined(comparison))
        {
            throw new ArgumentOutOfRangeException(nameof(comparison), comparison, "Unknown metadata comparison.");
        }

        _expectedValues = ImmutableReferenceSnapshot.Create(
            expectedValues,
            "Metadata comparisons require non-null expected values.",
            requireValue: true);
        if (_expectedValues.Distinct().Count() != _expectedValues.Length)
        {
            throw new ArgumentException("Metadata comparison values must be unique.", nameof(expectedValues));
        }

        if (comparison is CompiledValidationMetadataComparison.Equal or CompiledValidationMetadataComparison.NotEqual &&
            _expectedValues.Length != 1)
        {
            throw new ArgumentException("Equal and not-equal comparisons require one value.", nameof(expectedValues));
        }

        Field = field;
        Comparison = comparison;
        ExpectedValues = Array.AsReadOnly(_expectedValues);
    }

    internal CompositionProfileMetadataFieldReference Field { get; }

    internal CompiledValidationMetadataComparison Comparison { get; }

    internal IReadOnlyList<CompositionProfileScalarLiteral> ExpectedValues { get; }
}

/// <summary>Rejects all-zero and all-FF PID field values.</summary>
internal sealed record PidSanityProfileValidation : CompositionProfileValidation
{
    internal PidSanityProfileValidation(
        string ruleId,
        CompiledValidationStage stage,
        CompiledValidationSeverity severity,
        string issueCode,
        CompositionProfileMetadataFieldReference field)
        : base(ruleId, stage, severity, issueCode)
    {
        ArgumentNullException.ThrowIfNull(field);
        Field = field;
    }

    internal CompositionProfileMetadataFieldReference Field { get; }
}

/// <summary>Compares two independently bound metadata fields for exact typed equality.</summary>
internal sealed record MetadataEqualityProfileValidation : CompositionProfileValidation
{
    internal MetadataEqualityProfileValidation(
        string ruleId,
        CompiledValidationStage stage,
        CompiledValidationSeverity severity,
        string issueCode,
        CompositionProfileMetadataFieldReference left,
        CompositionProfileMetadataFieldReference right)
        : base(ruleId, stage, severity, issueCode)
    {
        ArgumentNullException.ThrowIfNull(left);
        ArgumentNullException.ThrowIfNull(right);
        Left = left;
        Right = right;
    }

    internal CompositionProfileMetadataFieldReference Left { get; }

    internal CompositionProfileMetadataFieldReference Right { get; }
}

/// <summary>Rejects declared generic byte patterns from one bound metadata field.</summary>
internal sealed record RejectMetadataBytePatternProfileValidation : CompositionProfileValidation
{
    private readonly CompiledValidationRejectedBytePattern[] _rejectedPatterns;

    internal RejectMetadataBytePatternProfileValidation(
        string ruleId,
        CompiledValidationStage stage,
        CompiledValidationSeverity severity,
        string issueCode,
        CompositionProfileMetadataFieldReference field,
        IEnumerable<CompiledValidationRejectedBytePattern> rejectedPatterns)
        : base(ruleId, stage, severity, issueCode)
    {
        ArgumentNullException.ThrowIfNull(field);
        ArgumentNullException.ThrowIfNull(rejectedPatterns);
        _rejectedPatterns = [.. rejectedPatterns];
        if (_rejectedPatterns.Length == 0 || _rejectedPatterns.Any(static pattern => !Enum.IsDefined(pattern)))
        {
            throw new ArgumentException("At least one known rejected byte pattern is required.", nameof(rejectedPatterns));
        }

        if (_rejectedPatterns.Distinct().Count() != _rejectedPatterns.Length)
        {
            throw new ArgumentException("Rejected byte patterns must be unique.", nameof(rejectedPatterns));
        }

        Array.Sort(_rejectedPatterns);
        Field = field;
        RejectedPatterns = Array.AsReadOnly(_rejectedPatterns);
    }

    internal CompositionProfileMetadataFieldReference Field { get; }

    internal IReadOnlyList<CompiledValidationRejectedBytePattern> RejectedPatterns { get; }
}

/// <summary>Asserts exact or masked bytes in one logical view.</summary>
internal sealed record ViewByteAssertionProfileValidation : CompositionProfileValidation
{
    internal ViewByteAssertionProfileValidation(
        string ruleId,
        CompiledValidationStage stage,
        CompiledValidationSeverity severity,
        string issueCode,
        string viewId,
        CompositionProfileByteValue expected,
        CompositionProfileByteValue? mask = null)
        : base(ruleId, stage, severity, issueCode)
    {
        ViewId = CompositionProfileValueRules.RequireId(viewId, nameof(viewId));
        ArgumentNullException.ThrowIfNull(expected);
        if (mask is not null && mask.Length != expected.Length)
        {
            throw new ArgumentException("Assertion mask length must equal expected bytes.", nameof(mask));
        }

        if (mask is not null)
        {
            ValidatePartialMask(expected, mask);
        }

        Expected = expected;
        Mask = mask;
    }

    internal string ViewId { get; }

    internal CompositionProfileByteValue Expected { get; }

    internal CompositionProfileByteValue? Mask { get; }

    private static void ValidatePartialMask(
        CompositionProfileByteValue expected,
        CompositionProfileByteValue mask)
    {
        if (mask.Bytes.IndexOfAnyExcept((byte)0) < 0)
        {
            throw new ArgumentException("Assertion mask must contain a set bit.", nameof(mask));
        }

        if (mask.Bytes.IndexOfAnyExcept(byte.MaxValue) < 0)
        {
            throw new ArgumentException("An all-FF assertion mask must use exact-match form.", nameof(mask));
        }

        for (int index = 0; index < mask.Length; index++)
        {
            if ((expected.Bytes[index] & ~mask.Bytes[index]) != 0)
            {
                throw new ArgumentException("Assertion expected bits outside the mask must be zero.", nameof(mask));
            }
        }
    }
}

/// <summary>Emits a warning when one declared source view contains only one repeated byte.</summary>
internal sealed record NonUniformRegionProfileValidation : CompositionProfileValidation
{
    internal NonUniformRegionProfileValidation(
        string ruleId,
        CompiledValidationStage stage,
        CompiledValidationSeverity severity,
        string issueCode,
        string viewId)
        : base(ruleId, stage, severity, issueCode)
    {
        if (stage != CompiledValidationStage.InputLoad ||
            severity != CompiledValidationSeverity.Warning)
        {
            throw new ArgumentException(
                "Non-uniform region validation is restricted to warning-only input-load checks.");
        }

        ViewId = CompositionProfileValueRules.RequireId(viewId, nameof(viewId));
    }

    internal string ViewId { get; }
}
