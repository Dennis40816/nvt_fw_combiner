namespace NvtFwCombiner.Domain.Composition;

public sealed partial class CompositionPlan
{
    private void ValidateInitialization()
    {
        if (!_addressSpacesById.TryGetValue(Initialization.TargetSpaceId, out AddressSpace? targetSpace))
        {
            throw new ArgumentException(
                $"Initialization target address space '{Initialization.TargetSpaceId}' is not declared.",
                nameof(Initialization));
        }

        if (targetSpace.Mutability != AddressSpaceMutability.Mutable)
        {
            throw new ArgumentException("Initialization target address space must be mutable.", nameof(Initialization));
        }

        if (targetSpace.Length != Initialization.Capacity)
        {
            throw new ArgumentException("Initialization capacity must match target address-space length.", nameof(Initialization));
        }

        if (Initialization.Kind == ImageInitializationKind.Reference)
        {
            ValidateReferenceInitialization();
        }

        if (OrderedOperations.Any(operation => operation.Kind == CompositionOperationKind.RunExternalProcessor))
        {
            ValidateProcessorInputPadding();
        }
    }

    private void ValidateReferenceInitialization()
    {
        if (Initialization.ReferenceSpaceId is null)
        {
            throw new ArgumentException("Reference initialization requires a reference address space id.", nameof(Initialization));
        }

        if (!_addressSpacesById.TryGetValue(Initialization.ReferenceSpaceId, out AddressSpace? referenceSpace))
        {
            throw new ArgumentException(
                $"Reference address space '{Initialization.ReferenceSpaceId}' is not declared.",
                nameof(Initialization));
        }

        if (referenceSpace.Mutability != AddressSpaceMutability.Immutable)
        {
            throw new ArgumentException("Reference address space must be immutable.", nameof(Initialization));
        }

        if (referenceSpace.Length != Initialization.Capacity)
        {
            throw new ArgumentException("Reference address-space length must match initialization capacity.", nameof(Initialization));
        }

        if (referenceSpace.InputPaddingByte is not null)
        {
            throw new ArgumentException("Reference address space cannot declare input padding.", nameof(Initialization));
        }

        if (referenceSpace.InputOversizePolicy != InputOversizePolicy.Reject)
        {
            throw new ArgumentException("Reference address space cannot declare input truncation.", nameof(Initialization));
        }

        if (referenceSpace.AllowedInputLengths.Count > 0)
        {
            throw new ArgumentException("Reference address space cannot declare alternate input lengths.", nameof(Initialization));
        }
    }

    private void ValidateProcessorInputPadding()
    {
        if (AddressSpaces.Any(addressSpace => addressSpace.InputPaddingByte is not null))
        {
            throw new ArgumentException(
                "Address spaces cannot declare input padding when an external processor operation is present.",
                nameof(AddressSpaces));
        }
    }
}
