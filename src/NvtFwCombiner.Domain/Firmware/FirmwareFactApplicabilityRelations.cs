using System.Numerics;

namespace NvtFwCombiner.Domain.Firmware;

/// <summary>Closed semantic relations for typed map-bound fact applicability values.</summary>
internal static class FirmwareFactApplicabilityRelations
{
    internal static bool IsSatisfiable(
        FirmwareFactApplicability applicability,
        IReadOnlyDictionary<string, FirmwareMetadataStructure> structuresById)
    {
        ArgumentNullException.ThrowIfNull(applicability);
        return CreateConstraints(applicability, structuresById).Values.All(static constraint => constraint.IsSatisfiable);
    }

    internal static bool IsContainedBy(
        FirmwareFactApplicability candidate,
        FirmwareFactApplicability container,
        IReadOnlyDictionary<string, FirmwareMetadataStructure> structuresById)
    {
        ArgumentNullException.ThrowIfNull(candidate);
        ArgumentNullException.ThrowIfNull(container);
        if (candidate.CapacityBytes != container.CapacityBytes ||
            !IsSubset(candidate.ModeIds, container.ModeIds) ||
            !TopologyContains(container.TopologyRequirement, candidate.TopologyRequirement) ||
            !CategoriesContain(container.CommonFirmwareCategoryIds, candidate.CommonFirmwareCategoryIds))
        {
            return false;
        }

        Dictionary<FieldKey, FieldConstraint> candidateConstraints = CreateConstraints(candidate, structuresById);
        Dictionary<FieldKey, FieldConstraint> containerConstraints = CreateConstraints(container, structuresById);
        if (candidateConstraints.Values.Any(static constraint => !constraint.IsSatisfiable) ||
            containerConstraints.Values.Any(static constraint => !constraint.IsSatisfiable))
        {
            return false;
        }

        foreach ((FieldKey key, FieldConstraint containerConstraint) in containerConstraints)
        {
            FieldConstraint? candidateConstraint = candidateConstraints.GetValueOrDefault(key);
            if (!FieldConstraint.IsContainedBy(candidateConstraint, containerConstraint))
            {
                return false;
            }
        }

        return true;
    }

    internal static bool HasSameScope(
        FirmwareFactApplicability left,
        FirmwareFactApplicability right,
        IReadOnlyDictionary<string, FirmwareMetadataStructure> structuresById)
    {
        return IsContainedBy(left, right, structuresById) &&
            IsContainedBy(right, left, structuresById);
    }

    internal static bool Overlaps(
        FirmwareFactApplicability left,
        FirmwareFactApplicability right,
        IReadOnlyDictionary<string, FirmwareMetadataStructure> structuresById)
    {
        ArgumentNullException.ThrowIfNull(left);
        ArgumentNullException.ThrowIfNull(right);
        if (left.CapacityBytes != right.CapacityBytes ||
            !HaveIntersection(left.ModeIds, right.ModeIds) ||
            !TopologyOverlaps(left.TopologyRequirement, right.TopologyRequirement) ||
            !CategoriesOverlap(left.CommonFirmwareCategoryIds, right.CommonFirmwareCategoryIds))
        {
            return false;
        }

        Dictionary<FieldKey, FieldConstraint> leftConstraints = CreateConstraints(left, structuresById);
        Dictionary<FieldKey, FieldConstraint> rightConstraints = CreateConstraints(right, structuresById);
        foreach (FieldKey key in leftConstraints.Keys.Union(rightConstraints.Keys))
        {
            if (!FieldConstraint.Overlaps(leftConstraints.GetValueOrDefault(key), rightConstraints.GetValueOrDefault(key)))
            {
                return false;
            }
        }

        return true;
    }

    private static Dictionary<FieldKey, FieldConstraint> CreateConstraints(
        FirmwareFactApplicability applicability,
        IReadOnlyDictionary<string, FirmwareMetadataStructure> structuresById)
    {
        ArgumentNullException.ThrowIfNull(structuresById);
        var constraints = new Dictionary<FieldKey, FieldConstraint>();
        foreach (IGrouping<(string StructureId, string FieldId), FirmwareMetadataPredicate> group in applicability.MetadataPredicates
                     .GroupBy(static predicate => (predicate.MetadataStructureId, predicate.FieldId)))
        {
            if (!structuresById.TryGetValue(group.Key.StructureId, out FirmwareMetadataStructure? structure))
            {
                throw new ArgumentException($"Unknown metadata structure '{group.Key.StructureId}'.", nameof(structuresById));
            }

            FirmwareMetadataField field = structure.Fields.FirstOrDefault(candidate =>
                StringComparer.Ordinal.Equals(candidate.FieldId, group.Key.FieldId)) ?? throw new ArgumentException(
                $"Unknown metadata field '{group.Key.StructureId}.{group.Key.FieldId}'.",
                nameof(structuresById));
            constraints.Add(new FieldKey(group.Key.StructureId, group.Key.FieldId), new FieldConstraint(field, group));
        }

        return constraints;
    }

    private static bool IsSubset(IReadOnlyList<string> candidate, IReadOnlyList<string> container)
    {
        return candidate.All(value => container.Contains(value, StringComparer.Ordinal));
    }

    private static bool HaveIntersection(IReadOnlyList<string> left, IReadOnlyList<string> right)
    {
        return left.Any(value => right.Contains(value, StringComparer.Ordinal));
    }

    private static bool CategoriesContain(IReadOnlyList<string> container, IReadOnlyList<string> candidate)
    {
        return container.Count == 0 || (candidate.Count != 0 && IsSubset(candidate, container));
    }

    private static bool CategoriesOverlap(IReadOnlyList<string> left, IReadOnlyList<string> right)
    {
        return left.Count == 0 || right.Count == 0 || HaveIntersection(left, right);
    }

    private static bool TopologyContains(TopologyRequirement container, TopologyRequirement candidate)
    {
        var containerInterval = ChipInterval.From(container);
        var candidateInterval = ChipInterval.From(candidate);
        return containerInterval.Minimum <= candidateInterval.Minimum &&
            (containerInterval.Maximum is null ||
            (candidateInterval.Maximum is { } candidateMaximum && candidateMaximum <= containerInterval.Maximum));
    }

    private static bool TopologyOverlaps(TopologyRequirement left, TopologyRequirement right)
    {
        var leftInterval = ChipInterval.From(left);
        var rightInterval = ChipInterval.From(right);
        int minimum = Math.Max(leftInterval.Minimum, rightInterval.Minimum);
        int? maximum = Min(leftInterval.Maximum, rightInterval.Maximum);
        return maximum is null || minimum <= maximum;
    }

    private static int? Min(int? left, int? right)
    {
        return left switch
        {
            null => right,
            _ when right is null => left,
            _ => Math.Min(left.Value, right.Value),
        };
    }

    private readonly record struct FieldKey(string StructureId, string FieldId);

    private readonly record struct ChipInterval(int Minimum, int? Maximum)
    {
        internal static ChipInterval From(TopologyRequirement requirement)
        {
            ArgumentNullException.ThrowIfNull(requirement);
            return requirement.Kind switch
            {
                TopologyRequirementKind.None => new ChipInterval(1, null),
                TopologyRequirementKind.SingleChip => new ChipInterval(1, 1),
                TopologyRequirementKind.Cascade => new ChipInterval(
                    requirement.MinimumChipCount!.Value,
                    requirement.MaximumChipCount),
                TopologyRequirementKind.ExactCount => new ChipInterval(
                    requirement.ExactChipCount!.Value,
                    requirement.ExactChipCount),
                _ => throw new ArgumentOutOfRangeException(nameof(requirement), "Unknown topology requirement."),
            };
        }
    }

    private sealed class FieldConstraint
    {
        private HashSet<FirmwareMetadataValue>? _positive;
        private readonly HashSet<FirmwareMetadataValue> _excluded;

        internal FieldConstraint(
            FirmwareMetadataField field,
            IEnumerable<FirmwareMetadataPredicate> predicates)
        {
            ArgumentNullException.ThrowIfNull(field);
            ArgumentNullException.ThrowIfNull(predicates);
            DomainCardinality = GetDomainCardinality(field);
            _excluded = [];
            foreach (FirmwareMetadataPredicate predicate in predicates)
            {
                if (predicate.ExpectedValues.Any(value => !field.CanRepresent(value)))
                {
                    throw new ArgumentException(
                        $"Predicate value is not representable by '{field.FieldId}'.",
                        nameof(predicates));
                }

                switch (predicate.Comparison)
                {
                    case FirmwareMetadataPredicateOperator.Equal:
                        IntersectPositive([predicate.ExpectedValues[0]]);
                        break;
                    case FirmwareMetadataPredicateOperator.OneOf:
                        IntersectPositive(predicate.ExpectedValues);
                        break;
                    case FirmwareMetadataPredicateOperator.NotEqual:
                        _ = _excluded.Add(predicate.ExpectedValues[0]);
                        break;
                    default:
                        throw new ArgumentOutOfRangeException(nameof(predicates), "Unknown metadata predicate operator.");
                }
            }
        }

        internal BigInteger DomainCardinality { get; }

        internal bool IsSatisfiable => _positive is { } positive
            ? positive.Any(value => !_excluded.Contains(value))
            : new BigInteger(_excluded.Count) < DomainCardinality;

        internal static bool IsContainedBy(FieldConstraint? candidate, FieldConstraint container)
        {
            ArgumentNullException.ThrowIfNull(container);
            return container.IsSatisfiable &&
                (candidate is null
                    ? container.IsTautology
                    : candidate.IsSatisfiable &&
                        candidate.DomainCardinality == container.DomainCardinality &&
                        (candidate._positive is { } candidatePositive
                            ? candidatePositive
                                .Where(value => !candidate._excluded.Contains(value))
                                .All(container.Allows)
                            : container._positive is null &&
                                container._excluded.IsSubsetOf(candidate._excluded)));
        }

        internal static bool Overlaps(FieldConstraint? left, FieldConstraint? right)
        {
            if (left is null || right is null)
            {
                return (left ?? right)?.IsSatisfiable ?? true;
            }

            if (left.DomainCardinality != right.DomainCardinality || !left.IsSatisfiable || !right.IsSatisfiable)
            {
                return false;
            }

            if (left._positive is { } leftPositive)
            {
                return leftPositive.Any(value => !left._excluded.Contains(value) && right.Allows(value));
            }

            if (right._positive is { } rightPositive)
            {
                return rightPositive.Any(value => !right._excluded.Contains(value) && left.Allows(value));
            }

            var exclusions = new HashSet<FirmwareMetadataValue>(left._excluded);
            exclusions.UnionWith(right._excluded);
            return new BigInteger(exclusions.Count) < left.DomainCardinality;
        }

        private bool IsTautology => _positive is null && _excluded.Count == 0;

        private bool Allows(FirmwareMetadataValue value)
        {
            return (_positive is null || _positive.Contains(value)) && !_excluded.Contains(value);
        }

        private void IntersectPositive(IEnumerable<FirmwareMetadataValue> values)
        {
            var next = new HashSet<FirmwareMetadataValue>(values);
            if (_positive is null)
            {
                _positive = next;
                return;
            }

            _positive.IntersectWith(next);
        }

        private static BigInteger GetDomainCardinality(FirmwareMetadataField field)
        {
            return field.Encoding switch
            {
                FirmwareMetadataEncoding.UnsignedInteger => BigInteger.One << field.EffectiveBitCount!.Value,
                FirmwareMetadataEncoding.SignedInteger => BigInteger.One << checked(field.WidthBytes * 8),
                FirmwareMetadataEncoding.Bytes => BigInteger.Pow(256, field.WidthBytes),
                FirmwareMetadataEncoding.PrintableAscii => BigInteger.Pow(95, field.WidthBytes),
                _ => throw new ArgumentOutOfRangeException(nameof(field), "Unknown metadata field encoding."),
            };
        }
    }
}
