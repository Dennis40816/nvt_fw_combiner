using System.Numerics;

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

/// <summary>Closed executable validation kind.</summary>
public enum CompiledValidationKind
{
    /// <inheritdoc/>
    MetadataValue,
    /// <inheritdoc/>
    PidSanity,
    /// <inheritdoc/>
    MetadataEquality,
    /// <inheritdoc/>
    RejectMetadataBytePattern,
    /// <inheritdoc/>
    ViewByteAssertion,
    /// <inheritdoc/>
    FirmwareConfigBackupVersion,
    /// <inheritdoc/>
    FirmwareConfigBackupPlacementAuthority,
    /// <inheritdoc/>
    FirmwareConfigBackupExpectedAddress,
    /// <inheritdoc/>
    RejectUniformInputRanges,
}

/// <summary>One metadata field reached through a profile metadata binding.</summary>
public sealed record CompiledValidationFieldReference
{
    internal CompiledValidationFieldReference(string bindingId, string fieldId)
    {
        BindingId = RequiredValue.NotBlank(bindingId);
        FieldId = RequiredValue.NotBlank(fieldId);
    }

    /// <summary>Stable profile metadata binding identifier.</summary>
    public string BindingId { get; }

    /// <summary>Stable field identifier inside the bound metadata structure.</summary>
    public string FieldId { get; }
}

/// <summary>Closed scalar literal kind retained until field-specific runtime evaluation.</summary>
public enum CompiledValidationScalarLiteralKind
{
    /// <inheritdoc/>
    Integral,
    /// <inheritdoc/>
    Text,
}

/// <summary>Base value for one exact compiled validation literal.</summary>
public abstract record CompiledValidationScalarLiteral
{
    private protected CompiledValidationScalarLiteral(CompiledValidationScalarLiteralKind kind)
    {
        ClosedEnum.ThrowIfUndefined(kind, "Unknown validation scalar literal kind.");

        Kind = kind;
    }

    /// <summary>Closed literal carrier kind.</summary>
    public CompiledValidationScalarLiteralKind Kind { get; }
}

/// <summary>One arbitrary-precision exact integer literal.</summary>
public sealed record CompiledValidationIntegerLiteral : CompiledValidationScalarLiteral
{
    internal CompiledValidationIntegerLiteral(BigInteger value)
        : base(CompiledValidationScalarLiteralKind.Integral)
    {
        Value = value;
    }

    /// <summary>Exact signed integer value.</summary>
    public BigInteger Value { get; }
}

/// <summary>One non-empty exact text literal.</summary>
public sealed record CompiledValidationTextLiteral : CompiledValidationScalarLiteral
{
    internal CompiledValidationTextLiteral(string value)
        : base(CompiledValidationScalarLiteralKind.Text)
    {
        ArgumentException.ThrowIfNullOrEmpty(value);
        Value = value;
    }

    /// <summary>Exact text value.</summary>
    public string Value { get; }
}

/// <summary>Closed metadata comparison operator.</summary>
public enum CompiledValidationMetadataComparison
{
    /// <inheritdoc/>
    Equal,
    /// <inheritdoc/>
    NotEqual,
    /// <inheritdoc/>
    OneOf,
}

/// <summary>Closed metadata byte pattern rejected by one validation.</summary>
public enum CompiledValidationRejectedBytePattern
{
    /// <inheritdoc/>
    AllZero,
    /// <inheritdoc/>
    AllFF,
}

/// <summary>Immutable exact byte value used by a view assertion.</summary>
public sealed class CompiledValidationBytes : IEquatable<CompiledValidationBytes>
{
    private readonly byte[] _bytes;

    internal CompiledValidationBytes(ReadOnlySpan<byte> bytes)
    {
        DomainInvariant.Reject(bytes.IsEmpty, "Validation bytes cannot be empty.", nameof(bytes));

        _bytes = bytes.ToArray();
        Hex = Convert.ToHexString(_bytes).ToLowerInvariant();
    }

    /// <summary>Exact byte length.</summary>
    public int Length => _bytes.Length;

    /// <summary>Canonical lowercase hexadecimal bytes.</summary>
    public string Hex { get; }

    internal ReadOnlySpan<byte> Bytes => _bytes;

    /// <inheritdoc />
    public bool Equals(CompiledValidationBytes? other)
    {
        return other is not null && _bytes.AsSpan().SequenceEqual(other._bytes);
    }

    /// <inheritdoc />
    public override bool Equals(object? obj)
    {
        return Equals(obj as CompiledValidationBytes);
    }

    /// <inheritdoc />
    public override int GetHashCode()
    {
        HashCode hash = new();
        foreach (byte value in _bytes)
        {
            hash.Add(value);
        }

        return hash.ToHashCode();
    }
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
        string issueCode,
        CompiledValidationKind kind)
        : base(ruleId, stage, severity, issueCode)
    {
        if (!ClosedEnum.IsDefined(kind))
        {
            throw new ArgumentOutOfRangeException(nameof(kind), "Unknown compiled validation discriminator.");
        }

        Kind = kind;
    }

    /// <summary>Closed validation kind.</summary>
    public CompiledValidationKind Kind { get; }
}

/// <summary>Compares one bound metadata field to exact typed literals.</summary>
public sealed record CompiledMetadataValueValidation : CompiledValidationRequirement
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
        : base(ruleId, stage, severity, issueCode, CompiledValidationKind.MetadataValue)
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

    /// <summary>Bound metadata field.</summary>
    public CompiledValidationFieldReference Field { get; }

    /// <summary>Closed comparison operator.</summary>
    public CompiledValidationMetadataComparison Comparison { get; }

    /// <summary>Canonical expected literals.</summary>
    public IReadOnlyList<CompiledValidationScalarLiteral> ExpectedValues { get; }

    private static int CompareLiterals(CompiledValidationScalarLiteral left, CompiledValidationScalarLiteral right)
    {
        int kind = left.Kind.CompareTo(right.Kind);
        return kind != 0
            ? kind
            : left switch
            {
                CompiledValidationIntegerLiteral integer => integer.Value.CompareTo(((CompiledValidationIntegerLiteral)right).Value),
                CompiledValidationTextLiteral text => StringComparer.Ordinal.Compare(text.Value, ((CompiledValidationTextLiteral)right).Value),
                _ => throw new InvalidOperationException("Unknown compiled validation literal kind."),
            };
    }
}

/// <summary>Rejects all-zero and all-FF values from one PID field.</summary>
public sealed record CompiledPidSanityValidation : CompiledValidationRequirement
{
    internal CompiledPidSanityValidation(string ruleId, CompiledValidationStage stage, CompiledValidationSeverity severity, string issueCode, CompiledValidationFieldReference field)
        : base(ruleId, stage, severity, issueCode, CompiledValidationKind.PidSanity)
    {
        Field = RequiredValue.NotNull(field);
    }

    /// <summary>PID metadata field.</summary>
    public CompiledValidationFieldReference Field { get; }
}

/// <summary>Compares two bound metadata fields for exact typed equality.</summary>
public sealed record CompiledMetadataEqualityValidation : CompiledValidationRequirement
{
    internal CompiledMetadataEqualityValidation(string ruleId, CompiledValidationStage stage, CompiledValidationSeverity severity, string issueCode, CompiledValidationFieldReference left, CompiledValidationFieldReference right)
        : base(ruleId, stage, severity, issueCode, CompiledValidationKind.MetadataEquality)
    {
        Left = RequiredValue.NotNull(left);
        Right = RequiredValue.NotNull(right);
    }

    /// <summary>First bound metadata field.</summary>
    public CompiledValidationFieldReference Left { get; }

    /// <summary>Second bound metadata field.</summary>
    public CompiledValidationFieldReference Right { get; }
}

/// <summary>Rejects declared byte patterns from one metadata field.</summary>
public sealed record CompiledRejectMetadataBytePatternValidation : CompiledValidationRequirement
{
    private readonly CompiledValidationRejectedBytePattern[] _rejectedPatterns;

    internal CompiledRejectMetadataBytePatternValidation(string ruleId, CompiledValidationStage stage, CompiledValidationSeverity severity, string issueCode, CompiledValidationFieldReference field, IEnumerable<CompiledValidationRejectedBytePattern> rejectedPatterns)
        : base(ruleId, stage, severity, issueCode, CompiledValidationKind.RejectMetadataBytePattern)
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

    /// <summary>Bound metadata field.</summary>
    public CompiledValidationFieldReference Field { get; }

    /// <summary>Canonical rejected patterns.</summary>
    public IReadOnlyList<CompiledValidationRejectedBytePattern> RejectedPatterns { get; }
}

/// <summary>Asserts exact or masked bytes in one compiled logical view.</summary>
public sealed record CompiledViewByteAssertionValidation : CompiledValidationRequirement
{
    internal CompiledViewByteAssertionValidation(string ruleId, CompiledValidationStage stage, CompiledValidationSeverity severity, string issueCode, string viewId, CompiledValidationBytes expected, CompiledValidationBytes? mask = null)
        : base(ruleId, stage, severity, issueCode, CompiledValidationKind.ViewByteAssertion)
    {
        ViewId = RequiredValue.NotBlank(viewId);
        Expected = RequiredValue.NotNull(expected);
        DomainInvariant.Reject(
            mask is not null && mask.Length != expected.Length,
            "Assertion masks must match expected byte length.", nameof(mask));

        Mask = mask;
    }

    /// <summary>Compiled logical view id.</summary>
    public string ViewId { get; }

    /// <summary>Expected bytes.</summary>
    public CompiledValidationBytes Expected { get; }

    /// <summary>Optional partial mask.</summary>
    public CompiledValidationBytes? Mask { get; }
}
