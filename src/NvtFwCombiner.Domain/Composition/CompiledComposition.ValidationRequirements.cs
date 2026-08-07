namespace NvtFwCombiner.Domain.Composition;

public sealed partial class CompiledComposition
{
    private static void ValidateValidationRequirements(
        CompositionPlan plan,
        IReadOnlyList<CompiledValidationRequirement> requirements)
    {
        var addressSpaces = plan.AddressSpaces.ToDictionary(
            static space => space.AddressSpaceId,
            StringComparer.Ordinal);
        foreach (CompiledUniformInputRangeValidation validation in requirements.OfType<
                     CompiledUniformInputRangeValidation>())
        {
            if (!addressSpaces.TryGetValue(
                    validation.AddressSpaceId,
                    out AddressSpace? addressSpace) ||
                addressSpace.Mutability != AddressSpaceMutability.Immutable ||
                validation.Ranges.Any(range =>
                    range.EndExclusive > addressSpace.Length))
            {
                throw new ArgumentException(
                    $"Compiled validation rule '{validation.RuleId}' must inspect complete ranges inside one immutable input address space.",
                    nameof(requirements));
            }
        }
    }
}
