namespace NvtFwCombiner.Domain.Composition;

/// <summary>Declares a named byte address space used by a composition plan.</summary>
public sealed class AddressSpace
{
    /// <summary>Creates an address space with a checked non-empty byte length.</summary>
    public AddressSpace(string addressSpaceId, long length, AddressSpaceMutability mutability)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(addressSpaceId);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(length);

        AddressSpaceId = addressSpaceId;
        Length = length;
        Mutability = mutability;
    }

    /// <summary>Stable identifier used by operations and reports.</summary>
    public string AddressSpaceId { get; }

    /// <summary>Total byte length of this address space.</summary>
    public long Length { get; }

    /// <summary>Whether operations may write to this address space.</summary>
    public AddressSpaceMutability Mutability { get; }

    /// <summary>Returns true when <paramref name="range"/> is fully inside this address space.</summary>
    public bool Contains(ByteRange range)
    {
        return range.EndExclusive <= Length;
    }
}
