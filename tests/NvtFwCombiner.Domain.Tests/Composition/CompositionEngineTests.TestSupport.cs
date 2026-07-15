using NvtFwCombiner.Domain.Composition;

namespace NvtFwCombiner.Domain.Tests.Composition;

public sealed partial class CompositionEngineTests
{
    private static CompositionExecutionInput EmptyInput()
    {
        return new CompositionExecutionInput(new Dictionary<string, byte[]>());
    }

    private static CompositionPlan CreateBlankPlan(
        long capacity,
        params object[] declarations)
    {
        List<AddressSpace> addressSpaces =
        [
            new("output-image", capacity, AddressSpaceMutability.Mutable),
        ];
        List<CompositionOperation> operations = [];
        foreach (object declaration in declarations)
        {
            if (declaration is AddressSpace addressSpace)
            {
                addressSpaces.Add(addressSpace);
            }
            else if (declaration is CompositionOperation operation)
            {
                operations.Add(operation);
            }
        }

        return new CompositionPlan(ImageInitialization.Blank("output-image", capacity, 0xFF), addressSpaces, operations);
    }

    private static CompositionPlan CreateReferencePlan(
        long capacity,
        AddressSpace sourceSpace,
        CompositionOperation operation)
    {
        AddressSpace[] addressSpaces =
        [
            new("reference-base", capacity, AddressSpaceMutability.Immutable),
            new("output-image", capacity, AddressSpaceMutability.Mutable),
            sourceSpace,
        ];
        return new CompositionPlan(
            ImageInitialization.Reference("output-image", "reference-base", capacity),
            addressSpaces,
            [operation]);
    }
}
