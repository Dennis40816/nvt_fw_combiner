using NvtFwCombiner.Domain.Composition;

namespace NvtFwCombiner.Domain.Firmware;

/// <summary>One checked half-open byte range in a named firmware address space.</summary>
public sealed record FirmwareAddressedRange
{
    /// <summary>Creates an addressed range without changing <paramref name="range"/> semantics.</summary>
    public FirmwareAddressedRange(string addressSpaceId, ByteRange range)
    {
        AddressSpaceId = RequiredValue.NotBlank(addressSpaceId);
        if (range.Length <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(range), "Addressed firmware ranges must be non-empty.");
        }

        Range = range;
    }

    /// <summary>Stable address-space identifier.</summary>
    public string AddressSpaceId { get; }

    /// <summary>Checked half-open byte range.</summary>
    public ByteRange Range { get; }
}
