using System.Diagnostics.CodeAnalysis;

namespace NvtFwCombiner.Domain.Firmware;

/// <summary>
/// Immutable locator-independent metadata shape shared by every declared
/// artifact binding of the same logical structure.
/// </summary>
public sealed class FirmwareMetadataStructureDefinition
{
    private readonly FirmwareMetadataField[] _fields;
    private readonly FirmwareMetadataByteAssertion[] _assertions;
    private readonly FirmwareMetadataFieldRelation[] _relations;

    /// <summary>Creates one checked logical metadata definition.</summary>
    public FirmwareMetadataStructureDefinition(
        string definitionId,
        long lengthBytes,
        IEnumerable<FirmwareMetadataField> fields,
        IEnumerable<FirmwareMetadataByteAssertion> assertions,
        IEnumerable<FirmwareMetadataFieldRelation>? relations = null,
        FirmwareMetadataTypedDefinition? typedDefinition = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(definitionId);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(lengthBytes);
        _fields = Composition.ImmutableReferenceSnapshot.Create(
            fields,
            "Metadata definitions cannot contain null fields.");
        if (_fields.Select(static field => field.FieldId)
                .Distinct(StringComparer.Ordinal).Count() != _fields.Length)
        {
            throw new ArgumentException(
                "Metadata field ids must be ordinally unique within a definition.",
                nameof(fields));
        }

        foreach (FirmwareMetadataField field in _fields)
        {
            if (field.Range.EndExclusive > lengthBytes)
            {
                throw new ArgumentException(
                    $"Metadata field '{field.FieldId}' exceeds definition '{definitionId}'.",
                    nameof(fields));
            }
        }

        Array.Sort(_fields, CompareFields);
        _assertions = Composition.ImmutableReferenceSnapshot.Create(
            assertions,
            "Metadata definitions cannot contain null assertions.");
        foreach (FirmwareMetadataByteAssertion assertion in _assertions)
        {
            if (assertion.Range.EndExclusive > lengthBytes)
            {
                throw new ArgumentException(
                    $"Metadata assertion {assertion.Range} exceeds definition '{definitionId}'.",
                    nameof(assertions));
            }
        }

        Array.Sort(_assertions, CompareAssertions);
        _relations = Composition.ImmutableReferenceSnapshot.Create(
            relations ?? [],
            "Metadata definitions cannot contain null relations.");
        if (_relations.Select(static relation => relation.RelationId)
                .Distinct(StringComparer.Ordinal).Count() != _relations.Length)
        {
            throw new ArgumentException(
                "Metadata relation ids must be ordinally unique within a definition.",
                nameof(relations));
        }

        ValidateRelations(_fields, _relations);
        Array.Sort(_relations, static (left, right) =>
            StringComparer.Ordinal.Compare(left.RelationId, right.RelationId));
        typedDefinition?.Validate(_fields, lengthBytes);
        DefinitionId = definitionId;
        LengthBytes = lengthBytes;
        Fields = Array.AsReadOnly(_fields);
        Assertions = Array.AsReadOnly(_assertions);
        Relations = Array.AsReadOnly(_relations);
        TypedDefinition = typedDefinition;
        StructureKind = typedDefinition?.StructureKind ??
                        FirmwareMetadataStructureKind.Generic;
    }

    /// <summary>Stable logical structure identity.</summary>
    public string DefinitionId { get; }

    /// <summary>Exact positive structure length.</summary>
    public long LengthBytes { get; }

    /// <summary>Fields in deterministic structure-relative range order.</summary>
    public IReadOnlyList<FirmwareMetadataField> Fields { get; }

    /// <summary>Assertions in deterministic structure-relative range order.</summary>
    public IReadOnlyList<FirmwareMetadataByteAssertion> Assertions { get; }

    /// <summary>Typed validation relations in deterministic relation-id order.</summary>
    public IReadOnlyList<FirmwareMetadataFieldRelation> Relations { get; }

    /// <summary>Closed common-metadata specialization discriminator.</summary>
    public FirmwareMetadataStructureKind StructureKind { get; }

    /// <summary>
    /// Optional typed specialization. Null is the explicit legacy/common
    /// metadata shape and never implies TP Header semantics.
    /// </summary>
    public FirmwareMetadataTypedDefinition? TypedDefinition { get; }

    /// <summary>
    /// Resolves every physical field without granting mutation authority.
    /// Generic fields are topology-invariant and therefore Active.
    /// </summary>
    public IReadOnlyList<FirmwareResolvedMetadataField> ResolveFields(
        TopologySelection? topology)
    {
        return TypedDefinition?.ResolveFields(_fields, topology) ??
               Array.AsReadOnly(
                   _fields.Select(static field =>
                       new FirmwareResolvedMetadataField(
                           field,
                           FirmwareMetadataFieldApplicabilityState.Active))
                       .ToArray());
    }

    /// <summary>Returns whether one typed reference closes over this exact definition.</summary>
    public bool ContainsReferenceTarget(FirmwareMetadataReferenceTarget target)
    {
        ArgumentNullException.ThrowIfNull(target);
        return target.Kind switch
        {
            FirmwareMetadataReferenceTargetKind.Field =>
                _fields.Any(field =>
                    StringComparer.Ordinal.Equals(field.FieldId, target.TargetId)),
            FirmwareMetadataReferenceTargetKind.Span
                when TypedDefinition is FirmwareTpFlashHeaderDefinition header =>
                header.Spans.Any(span =>
                    StringComparer.Ordinal.Equals(span.SpanId, target.TargetId)),
            FirmwareMetadataReferenceTargetKind.Series
                when TypedDefinition is FirmwareTpFlashHeaderDefinition header =>
                header.FieldSeries.Any(series =>
                    StringComparer.Ordinal.Equals(series.SeriesId, target.TargetId)),
            FirmwareMetadataReferenceTargetKind.Group
                when TypedDefinition is FirmwareTpFlashHeaderDefinition header =>
                header.FieldGroups.Any(group =>
                    StringComparer.Ordinal.Equals(group.GroupId, target.TargetId)),
            _ => false,
        };
    }

    /// <summary>
    /// Returns whether one canonical target includes the exact physical field,
    /// expanding typed spans, series, and groups without copying their members.
    /// </summary>
    public bool ReferenceTargetContainsField(
        FirmwareMetadataReferenceTarget target,
        FirmwareMetadataField field)
    {
        ArgumentNullException.ThrowIfNull(target);
        ArgumentNullException.ThrowIfNull(field);
        if (!_fields.Any(candidate => ReferenceEquals(candidate, field)))
        {
            return false;
        }

        if (target.Kind == FirmwareMetadataReferenceTargetKind.Field)
        {
            return StringComparer.Ordinal.Equals(target.TargetId, field.FieldId);
        }

        if (TypedDefinition is not FirmwareTpFlashHeaderDefinition header)
        {
            return false;
        }

        switch (target.Kind)
        {
            case FirmwareMetadataReferenceTargetKind.Field:
                return false;
            case FirmwareMetadataReferenceTargetKind.Span:
                return header.Spans.Any(span =>
                    StringComparer.Ordinal.Equals(span.SpanId, target.TargetId) &&
                    span.Range.Contains(field.Range));
            case FirmwareMetadataReferenceTargetKind.Series:
                return header.FieldSeries.Any(series =>
                    StringComparer.Ordinal.Equals(series.SeriesId, target.TargetId) &&
                    series.Members.Any(member => StringComparer.Ordinal.Equals(
                        member.FieldId,
                        field.FieldId)));
            case FirmwareMetadataReferenceTargetKind.Group:
                FirmwareMetadataFieldGroup? group = header.FieldGroups.FirstOrDefault(
                    candidate => StringComparer.Ordinal.Equals(
                        candidate.GroupId,
                        target.TargetId));
                return group is not null &&
                       (group.FieldIds.Contains(field.FieldId, StringComparer.Ordinal) ||
                        group.SeriesIds.Any(seriesId => header.FieldSeries.Any(series =>
                            StringComparer.Ordinal.Equals(series.SeriesId, seriesId) &&
                            series.Members.Any(member => StringComparer.Ordinal.Equals(
                                member.FieldId,
                                field.FieldId)))));
            default:
                return false;
        }
    }

    internal bool TryDecode(
        string artifactBindingId,
        string structureBindingId,
        ReadOnlySpan<byte> bytes,
        [NotNullWhen(true)] out FirmwareDecodedMetadataStructure? result)
    {
        result = null;
        if (bytes.Length != LengthBytes)
        {
            return false;
        }

        foreach (FirmwareMetadataByteAssertion assertion in _assertions)
        {
            int start = checked((int)assertion.Range.Start);
            int length = checked((int)assertion.Range.Length);
            if (!assertion.Matches(bytes.Slice(start, length)))
            {
                return false;
            }
        }

        List<FirmwareDecodedMetadataFact> facts = [];
        var values = new Dictionary<string, FirmwareMetadataValue>(StringComparer.Ordinal);
        foreach (FirmwareMetadataField field in _fields)
        {
            int start = checked((int)field.Range.Start);
            if (!field.TryDecode(
                bytes.Slice(start, field.WidthBytes),
                out FirmwareMetadataValue? value))
            {
                return false;
            }

            facts.Add(new FirmwareDecodedMetadataFact(
                artifactBindingId,
                structureBindingId,
                field.FieldId,
                value));
            values.Add(field.FieldId, value);
        }

        FirmwareDecodedMetadataRelation[] relations =
        [
            .. _relations.Select(relation =>
            {
                FirmwareMetadataField source = _fields.Single(field =>
                    StringComparer.Ordinal.Equals(field.FieldId, relation.SourceFieldId));
                return new FirmwareDecodedMetadataRelation(
                    relation.RelationId,
                    relation.Kind,
                    relation.SourceFieldId,
                    relation.RelatedFieldId,
                    relation.Evaluate(values, source.WidthBytes));
            }),
        ];
        result = new FirmwareDecodedMetadataStructure(
            artifactBindingId,
            structureBindingId,
            facts,
            relations);
        return true;
    }

    private static void ValidateRelations(
        IReadOnlyList<FirmwareMetadataField> fields,
        IReadOnlyList<FirmwareMetadataFieldRelation> relations)
    {
        var fieldsById =
            fields.ToDictionary(static field => field.FieldId, StringComparer.Ordinal);
        foreach (FirmwareMetadataFieldRelation relation in relations)
        {
            if (!fieldsById.TryGetValue(relation.SourceFieldId, out FirmwareMetadataField? source) ||
                !fieldsById.TryGetValue(relation.RelatedFieldId, out FirmwareMetadataField? related))
            {
                throw new ArgumentException(
                    $"Metadata relation '{relation.RelationId}' references an unknown field.",
                    nameof(relations));
            }

            if (relation.Kind == FirmwareMetadataFieldRelationKind.BitwiseComplement &&
                (source.Encoding != FirmwareMetadataEncoding.UnsignedInteger ||
                 related.Encoding != FirmwareMetadataEncoding.UnsignedInteger ||
                 source.BitSlice is not null ||
                 related.BitSlice is not null ||
                 source.WidthBytes != related.WidthBytes))
            {
                throw new ArgumentException(
                    $"Metadata relation '{relation.RelationId}' requires unsliced equal-width unsigned fields.",
                    nameof(relations));
            }
        }
    }

    private static int CompareFields(FirmwareMetadataField left, FirmwareMetadataField right)
    {
        int rangeComparison = FirmwareRangeOrdering.Compare(left.Range, right.Range);
        return rangeComparison != 0
            ? rangeComparison
            : StringComparer.Ordinal.Compare(left.FieldId, right.FieldId);
    }

    private static int CompareAssertions(
        FirmwareMetadataByteAssertion left,
        FirmwareMetadataByteAssertion right)
    {
        int rangeComparison = FirmwareRangeOrdering.Compare(left.Range, right.Range);
        if (rangeComparison != 0)
        {
            return rangeComparison;
        }

        int expectedComparison = StringComparer.Ordinal.Compare(
            left.ExpectedBytes.Hex,
            right.ExpectedBytes.Hex);
        return expectedComparison != 0
            ? expectedComparison
            : StringComparer.Ordinal.Compare(left.MaskBytes.Hex, right.MaskBytes.Hex);
    }
}
