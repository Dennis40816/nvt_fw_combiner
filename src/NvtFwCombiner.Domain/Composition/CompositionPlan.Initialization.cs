namespace NvtFwCombiner.Domain.Composition;

public sealed partial class CompositionPlan
{
    private void ValidateInitializations()
    {
        DomainInvariant.Reject(
            !_addressSpacesById.TryGetValue(OutputSpaceId, out AddressSpace? outputSpace),
            $"Output address space '{OutputSpaceId}' is not declared.",
            nameof(OutputSpaceId));

        DomainInvariant.Reject(
            outputSpace.Mutability != AddressSpaceMutability.Mutable,
            "Output address space must be mutable.", nameof(OutputSpaceId));

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
        DomainInvariant.Reject(
            !_addressSpacesById.TryGetValue(initialization.TargetSpaceId, out AddressSpace? targetSpace),
            $"Initialization target address space '{initialization.TargetSpaceId}' is not declared.",
            nameof(initialization));

        DomainInvariant.Reject(
            targetSpace.Mutability != AddressSpaceMutability.Mutable,
            "Initialization target address space must be mutable.", nameof(initialization));

        DomainInvariant.Reject(
            targetSpace.Length != initialization.Capacity,
            "Initialization capacity must match target address-space length.",
            nameof(initialization));

        if (initialization.Kind == ImageInitializationKind.Reference)
        {
            ValidateReferenceInitialization(initialization);
        }
    }

    private void ValidateReferenceInitialization(ImageInitialization initialization)
    {
        DomainInvariant.Reject(
            initialization.ReferenceSpaceId is null,
            "Reference initialization requires a reference address space id.",
            nameof(initialization));

        DomainInvariant.Reject(
            !_addressSpacesById.TryGetValue(initialization.ReferenceSpaceId, out AddressSpace? referenceSpace),
            $"Reference address space '{initialization.ReferenceSpaceId}' is not declared.",
            nameof(initialization));

        DomainInvariant.Reject(
            referenceSpace.Mutability != AddressSpaceMutability.Immutable,
            "Reference address space must be immutable.", nameof(initialization));

        DomainInvariant.Reject(
            referenceSpace.Length != initialization.Capacity,
            "Reference address-space length must match initialization capacity.",
            nameof(initialization));

        DomainInvariant.Reject(
            referenceSpace.InputPaddingByte is not null,
            "Reference address space cannot declare input padding.", nameof(initialization));

        bool clonesCheckedSourceIntoWorkBuffer =
            !StringComparer.Ordinal.Equals(initialization.TargetSpaceId, OutputSpaceId) &&
            referenceSpace.InputOversizePolicy == InputOversizePolicy.ExtractDeclaredRange &&
            referenceSpace.AllowedInputLengths.Count == 0;
        DomainInvariant.Reject(
            referenceSpace.InputOversizePolicy != InputOversizePolicy.Reject &&
            !clonesCheckedSourceIntoWorkBuffer,
            "Reference address space cannot declare input truncation.", nameof(initialization));

        DomainInvariant.Reject(
            referenceSpace.AllowedInputLengths.Count > 0,
            "Reference address space cannot declare alternate input lengths.",
            nameof(initialization));

        DomainInvariant.Reject(
            referenceSpace.ExpectedInputLengths.Count > 0 &&
            !clonesCheckedSourceIntoWorkBuffer,
            "Reference address space cannot declare expected input lengths.",
            nameof(initialization));
    }

    private void ValidateProcessorInputPadding()
    {
        DomainInvariant.Reject(
            AddressSpaces.Any(addressSpace => addressSpace.InputPaddingByte is not null),
            "Address spaces cannot declare input padding when an external processor operation is present.",
            nameof(AddressSpaces));
    }
}
