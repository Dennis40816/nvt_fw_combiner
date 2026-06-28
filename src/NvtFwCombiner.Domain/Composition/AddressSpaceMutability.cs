namespace NvtFwCombiner.Domain.Composition;

/// <summary>Declares whether an address space can be mutated during execution.</summary>
public enum AddressSpaceMutability
{
    /// <summary>The address space is an immutable input or reference image.</summary>
    Immutable,

    /// <summary>The address space is owned by the current execution run and may be mutated.</summary>
    Mutable,
}
