using System.Collections.ObjectModel;

namespace NvtFwCombiner.Domain.Composition;

public sealed partial class CompiledComposition
{
    private static ReadOnlyCollection<CompiledValidationRequirement> CopyValidationRequirements(
        IReadOnlyList<CompiledValidationRequirement>? requirements)
    {
        CompiledValidationRequirement[] copy = requirements is null ? [] : [.. requirements];
        bool invalid = copy.Any(static requirement => requirement is null) ||
            copy.Select(static requirement => requirement.RuleId).Distinct(StringComparer.Ordinal).Count() != copy.Length;
        if (!invalid)
        {
            Array.Sort(copy, static (left, right) =>
            {
                int stage = left.Stage.CompareTo(right.Stage);
                return stage != 0
                    ? stage
                    : StringComparer.Ordinal.Compare(left.RuleId, right.RuleId);
            });
            return Array.AsReadOnly(copy);
        }

        throw new ArgumentException(
            "Compiled validation requirements must be non-null with ordinally unique rule ids.",
            nameof(requirements));
    }
}
