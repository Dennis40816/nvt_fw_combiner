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

    private static CompositionPlanProvenance CreateCtrlRamReplaceProvenance()
    {
        return new CompositionPlanProvenance(
            "ctrlram-replace-profile",
            "1.0.0",
            "NT-SYNTHETIC",
            "ctrlram-replace",
            "ctrlram-replace",
            CompositionKind.Replace);
    }
}
