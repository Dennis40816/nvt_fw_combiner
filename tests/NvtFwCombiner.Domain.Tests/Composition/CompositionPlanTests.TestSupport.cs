using NvtFwCombiner.Domain.Composition;

namespace NvtFwCombiner.Domain.Tests.Composition;

public sealed partial class CompositionPlanTests
{
    private static CompositionPlan CreatePlan(params CompositionOperation[] operations)
    {
        AddressSpace[] addressSpaces =
        [
            new("output-image", 4, AddressSpaceMutability.Mutable),
        ];
        return new CompositionPlan(
            ImageInitialization.Blank("output-image", 4, 0),
            addressSpaces,
            operations);
    }
}
