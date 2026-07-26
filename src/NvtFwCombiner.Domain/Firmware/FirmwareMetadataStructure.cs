using System.Diagnostics.CodeAnalysis;

namespace NvtFwCombiner.Domain.Firmware;

/// <summary>Immutable declaration of one located firmware metadata structure.</summary>
public sealed class FirmwareMetadataStructure
{
    private readonly FirmwareMetadataField[] _fields;
    private readonly FirmwareMetadataByteAssertion[] _assertions;
    private readonly FirmwareMetadataFieldRelation[] _relations;

    /// <summary>Creates a checked structure declaration without reading artifact bytes.</summary>
    public FirmwareMetadataStructure(
        string structureId,
        string artifactBindingId,
        long lengthBytes,
        FirmwareMetadataLocator locator,
        IEnumerable<FirmwareMetadataField> fields,
        IEnumerable<FirmwareMetadataByteAssertion> assertions,
        IEnumerable<FirmwareMetadataFieldRelation>? relations = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(structureId);
        ArgumentException.ThrowIfNullOrWhiteSpace(artifactBindingId);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(lengthBytes);
        ArgumentNullException.ThrowIfNull(locator);

        _fields = Composition.ImmutableReferenceSnapshot.Create(
            fields,
            "Metadata structures cannot contain null fields.");

        if (_fields.Select(static field => field.FieldId).Distinct(StringComparer.Ordinal).Count() !=
            _fields.Length)
        {
            throw new ArgumentException("Metadata field ids must be ordinally unique within a structure.", nameof(fields));
        }

        foreach (FirmwareMetadataField field in _fields)
        {
            if (field.Range.EndExclusive > lengthBytes)
            {
                throw new ArgumentException(
                    $"Metadata field '{field.FieldId}' exceeds structure '{structureId}'.",
                    nameof(fields));
            }
        }

        Array.Sort(_fields, CompareFields);

        _assertions = Composition.ImmutableReferenceSnapshot.Create(
            assertions,
            "Metadata structures cannot contain null assertions.");

        foreach (FirmwareMetadataByteAssertion assertion in _assertions)
        {
            if (assertion.Range.EndExclusive > lengthBytes)
            {
                throw new ArgumentException(
                    $"Metadata assertion {assertion.Range} exceeds structure '{structureId}'.",
                    nameof(assertions));
            }
        }

        Array.Sort(_assertions, CompareAssertions);
        _relations = Composition.ImmutableReferenceSnapshot.Create(
            relations ?? [],
            "Metadata structures cannot contain null relations.");
        if (_relations.Select(static relation => relation.RelationId)
            .Distinct(StringComparer.Ordinal).Count() != _relations.Length)
        {
            throw new ArgumentException(
                "Metadata relation ids must be ordinally unique within a structure.",
                nameof(relations));
        }

        ValidateRelations(_fields, _relations);
        Array.Sort(_relations, static (left, right) =>
            StringComparer.Ordinal.Compare(left.RelationId, right.RelationId));
        ValidateLocatorShape(locator, lengthBytes, _assertions.Length);

        StructureId = structureId;
        ArtifactBindingId = artifactBindingId;
        LengthBytes = lengthBytes;
        Locator = locator;
        Fields = Array.AsReadOnly(_fields);
        Assertions = Array.AsReadOnly(_assertions);
        Relations = Array.AsReadOnly(_relations);
    }

    /// <summary>Family-wide canonical structure identifier.</summary>
    public string StructureId { get; }

    /// <summary>Stable runtime artifact binding used by this structure.</summary>
    public string ArtifactBindingId { get; }

    /// <summary>Exact positive structure length.</summary>
    public long LengthBytes { get; }

    /// <summary>Closed physical locator declaration.</summary>
    public FirmwareMetadataLocator Locator { get; }

    /// <summary>Fields in deterministic structure-relative range order.</summary>
    public IReadOnlyList<FirmwareMetadataField> Fields { get; }

    /// <summary>Assertions in deterministic structure-relative range order.</summary>
    public IReadOnlyList<FirmwareMetadataByteAssertion> Assertions { get; }

    /// <summary>Typed validation relations in deterministic relation-id order.</summary>
    public IReadOnlyList<FirmwareMetadataFieldRelation> Relations { get; }

    /// <summary>Atomically validates and decodes one already-located exact structure slice.</summary>
    public bool TryDecode(
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
                ArtifactBindingId,
                StructureId,
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
            ArtifactBindingId,
            StructureId,
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

    private static void ValidateLocatorShape(
        FirmwareMetadataLocator locator,
        long lengthBytes,
        int assertionCount)
    {
        switch (locator)
        {
            case FirmwareAbsoluteRangeLocator absolute:
                if (absolute.Range.Range.Length != lengthBytes)
                {
                    throw new ArgumentException(
                        "Absolute metadata locator length must equal its structure length.",
                        nameof(locator));
                }

                break;
            case FirmwareRegionRelativeLocator relative:
                _ = checked(relative.Offset + lengthBytes);
                break;
            case FirmwareMarkerRelativeLocator marker:
                _ = checked(marker.ResultOffset + lengthBytes);
                if (assertionCount == 0 &&
                    marker.Selection.Kind != FirmwareMarkerSelectionKind.Unique)
                {
                    throw new ArgumentException(
                        "Non-unique marker-relative metadata structures require an assertion.",
                        nameof(locator));
                }

                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(locator), "Unknown metadata locator type.");
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
