using System.Collections.ObjectModel;

namespace NvtFwCombiner.Domain.Composition;

public sealed partial class CompiledComposition
{
    private static ReadOnlyCollection<CompiledValidationRequirement> CopyValidationRequirements(
        IReadOnlyList<CompiledValidationRequirement>? requirements)
    {
        CompiledValidationRequirement[] copy = ImmutableReferenceSnapshot.CreateUnique(
            requirements ?? [],
            static requirement => requirement.RuleId,
            "Compiled validation requirements must be non-null with ordinally unique rule ids.",
            "Compiled validation requirements must be non-null with ordinally unique rule ids.",
            StringComparer.Ordinal,
            parameterName: nameof(requirements));
        Array.Sort(copy, static (left, right) =>
        {
            int stage = left.Stage.CompareTo(right.Stage);
            return stage != 0
                ? stage
                : StringComparer.Ordinal.Compare(left.RuleId, right.RuleId);
        });
        return Array.AsReadOnly(copy);
    }

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
