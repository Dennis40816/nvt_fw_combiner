namespace NvtFwCombiner.Domain.Composition;

/// <summary>Declares a named byte address space used by a composition plan.</summary>
public sealed class AddressSpace
{
    private readonly long[] _allowedInputLengths;
    private readonly long[] _expectedInputLengths;

    /// <summary>Creates an address space with a checked non-empty byte length.</summary>
    public AddressSpace(
        string addressSpaceId,
        long length,
        AddressSpaceMutability mutability,
        byte? inputPaddingByte = null,
        InputOversizePolicy inputOversizePolicy = InputOversizePolicy.Reject,
        IReadOnlyList<long>? allowedInputLengths = null,
        IReadOnlyList<long>? expectedInputLengths = null,
        string? unexpectedInputLengthIssueCode = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(addressSpaceId);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(length);
        ClosedEnum.ThrowIfUndefined(inputOversizePolicy, "Unknown input oversize policy.");

        _allowedInputLengths = NormalizeAllowedInputLengths(allowedInputLengths, length);
        _expectedInputLengths = NormalizeExpectedInputLengths(expectedInputLengths, length);
        if (unexpectedInputLengthIssueCode is not null)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(unexpectedInputLengthIssueCode);
            DomainInvariant.Reject(
                mutability != AddressSpaceMutability.Immutable ||
                inputOversizePolicy != InputOversizePolicy.ExtractDeclaredRange ||
                _expectedInputLengths.Length == 0,
                "An unexpected-length warning code requires declared-range extraction with expected input lengths.",
                nameof(unexpectedInputLengthIssueCode));
        }

        AddressSpaceId = addressSpaceId;
        Length = length;
        Mutability = mutability;
        InputPaddingByte = inputPaddingByte;
        InputOversizePolicy = inputOversizePolicy;
        UnexpectedInputLengthIssueCode = unexpectedInputLengthIssueCode;
    }

    /// <summary>Stable identifier used by operations and reports.</summary>
    public string AddressSpaceId { get; }

    /// <summary>Total byte length of this address space.</summary>
    public long Length { get; }

    /// <summary>Whether operations may write to this address space.</summary>
    public AddressSpaceMutability Mutability { get; }

    /// <summary>Byte used to extend a shorter supplied input to the declared length; null keeps exact-size validation.</summary>
    public byte? InputPaddingByte { get; }

    /// <summary>Policy for supplied input bytes longer than the declared length.</summary>
    public InputOversizePolicy InputOversizePolicy { get; }

    /// <summary>Exact source artifact lengths accepted for this address space; empty means any length allowed by padding/truncation policy.</summary>
    public IReadOnlyList<long> AllowedInputLengths => _allowedInputLengths;

    /// <summary>Known source artifact lengths that remain non-blocking expectations for diagnostics and traceability.</summary>
    public IReadOnlyList<long> ExpectedInputLengths => _expectedInputLengths;

    /// <summary>Optional profile-owned warning code emitted when a declared-range extraction input has an unexpected outer length.</summary>
    public string? UnexpectedInputLengthIssueCode { get; }

    /// <summary>Returns true when <paramref name="range"/> is fully inside this address space.</summary>
    public bool Contains(ByteRange range)
    {
        return range.EndExclusive <= Length;
    }

    private static long[] NormalizeAllowedInputLengths(IReadOnlyList<long>? allowedInputLengths, long declaredLength)
    {
        if (allowedInputLengths is null)
        {
            return [];
        }

        DomainInvariant.Reject(
            allowedInputLengths.Count == 0,
            "Allowed input lengths cannot be empty when supplied.", nameof(allowedInputLengths));

        long[] normalized = [.. allowedInputLengths.Order().Distinct()];
        foreach (long allowedLength in normalized)
        {
            ArgumentOutOfRangeException.ThrowIfNegativeOrZero(allowedLength, nameof(allowedInputLengths));
            if (allowedLength > declaredLength)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(allowedInputLengths),
                    "Allowed input lengths cannot exceed the declared address-space length.");
            }
        }

        return normalized;
    }

    private static long[] NormalizeExpectedInputLengths(IReadOnlyList<long>? expectedInputLengths, long declaredLength)
    {
        if (expectedInputLengths is null)
        {
            return [];
        }

        DomainInvariant.Reject(
            expectedInputLengths.Count == 0,
            "Expected input lengths cannot be empty when supplied.", nameof(expectedInputLengths));

        long[] normalized = [.. expectedInputLengths.Order().Distinct()];
        foreach (long expectedLength in normalized)
        {
            ArgumentOutOfRangeException.ThrowIfNegativeOrZero(expectedLength, nameof(expectedInputLengths));
            if (expectedLength < declaredLength)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(expectedInputLengths),
                    "Expected input lengths cannot be shorter than the declared address-space length.");
            }
        }

        return normalized;
    }
}
