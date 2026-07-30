namespace NvtFwCombiner.Domain.Composition;

public sealed partial class CompositionPlan
{
    private void ValidateInitializations()
    {
        if (!_addressSpacesById.TryGetValue(OutputSpaceId, out AddressSpace? outputSpace))
        {
            throw new ArgumentException(
                $"Output address space '{OutputSpaceId}' is not declared.",
                nameof(OutputSpaceId));
        }

        if (outputSpace.Mutability != AddressSpaceMutability.Mutable)
        {
            throw new ArgumentException("Output address space must be mutable.", nameof(OutputSpaceId));
        }

        foreach (ImageInitialization initialization in _initializations)
        {
            ValidateInitialization(initialization);
        }

        AddressSpace? missingInitializer = AddressSpaces.FirstOrDefault(addressSpace =>
            addressSpace.Mutability == AddressSpaceMutability.Mutable &&
            !_initializationsByTargetSpaceId.ContainsKey(addressSpace.AddressSpaceId));
        if (missingInitializer is not null)
        {
            throw new ArgumentException(
                $"Mutable address space '{missingInitializer.AddressSpaceId}' has no initializer.",
                nameof(Initializations));
        }

        if (OrderedOperations.Any(operation => operation.Kind == CompositionOperationKind.RunExternalProcessor))
        {
            ValidateProcessorInputPadding();
        }
    }

    private void ValidateInitialization(ImageInitialization initialization)
    {
        if (!_addressSpacesById.TryGetValue(initialization.TargetSpaceId, out AddressSpace? targetSpace))
        {
            throw new ArgumentException(
                $"Initialization target address space '{initialization.TargetSpaceId}' is not declared.",
                nameof(initialization));
        }

        if (targetSpace.Mutability != AddressSpaceMutability.Mutable)
        {
            throw new ArgumentException("Initialization target address space must be mutable.", nameof(initialization));
        }

        if (targetSpace.Length != initialization.Capacity)
        {
            throw new ArgumentException(
                "Initialization capacity must match target address-space length.",
                nameof(initialization));
        }

        if (initialization.Kind == ImageInitializationKind.Reference)
        {
            ValidateReferenceInitialization(initialization);
        }
    }

    private void ValidateReferenceInitialization(ImageInitialization initialization)
    {
        if (initialization.ReferenceSpaceId is null)
        {
            throw new ArgumentException(
                "Reference initialization requires a reference address space id.",
                nameof(initialization));
        }

        if (!_addressSpacesById.TryGetValue(initialization.ReferenceSpaceId, out AddressSpace? referenceSpace))
        {
            throw new ArgumentException(
                $"Reference address space '{initialization.ReferenceSpaceId}' is not declared.",
                nameof(initialization));
        }

        if (referenceSpace.Mutability != AddressSpaceMutability.Immutable)
        {
            throw new ArgumentException("Reference address space must be immutable.", nameof(initialization));
        }

        if (referenceSpace.Length != initialization.Capacity)
        {
            throw new ArgumentException(
                "Reference address-space length must match initialization capacity.",
                nameof(initialization));
        }

        if (referenceSpace.InputPaddingByte is not null)
        {
            throw new ArgumentException("Reference address space cannot declare input padding.", nameof(initialization));
        }

        bool clonesCheckedSourceIntoWorkBuffer =
            !StringComparer.Ordinal.Equals(initialization.TargetSpaceId, OutputSpaceId) &&
            referenceSpace.InputOversizePolicy == InputOversizePolicy.ExtractDeclaredRange &&
            referenceSpace.AllowedInputLengths.Count == 0;
        if (referenceSpace.InputOversizePolicy != InputOversizePolicy.Reject &&
            !clonesCheckedSourceIntoWorkBuffer)
        {
            throw new ArgumentException("Reference address space cannot declare input truncation.", nameof(initialization));
        }

        if (referenceSpace.AllowedInputLengths.Count > 0)
        {
            throw new ArgumentException(
                "Reference address space cannot declare alternate input lengths.",
                nameof(initialization));
        }

        if (referenceSpace.ExpectedInputLengths.Count > 0 &&
            !clonesCheckedSourceIntoWorkBuffer)
        {
            throw new ArgumentException(
                "Reference address space cannot declare expected input lengths.",
                nameof(initialization));
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
