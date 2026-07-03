namespace NvtFwCombiner.Domain.Composition;

/// <summary>Declares a named byte address space used by a composition plan.</summary>
public sealed class AddressSpace
{
    private readonly long[] _allowedInputLengths;

    /// <summary>Creates an address space with a checked non-empty byte length.</summary>
    public AddressSpace(
        string addressSpaceId,
        long length,
        AddressSpaceMutability mutability,
        byte? inputPaddingByte = null,
        InputOversizePolicy inputOversizePolicy = InputOversizePolicy.Reject,
        IReadOnlyList<long>? allowedInputLengths = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(addressSpaceId);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(length);
        if (!Enum.IsDefined(inputOversizePolicy))
        {
            throw new ArgumentOutOfRangeException(nameof(inputOversizePolicy), inputOversizePolicy, "Unknown input oversize policy.");
        }

        _allowedInputLengths = NormalizeAllowedInputLengths(allowedInputLengths, length);
        AddressSpaceId = addressSpaceId;
        Length = length;
        Mutability = mutability;
        InputPaddingByte = inputPaddingByte;
        InputOversizePolicy = inputOversizePolicy;
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

        if (allowedInputLengths.Count == 0)
        {
            throw new ArgumentException("Allowed input lengths cannot be empty when supplied.", nameof(allowedInputLengths));
        }

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
}
