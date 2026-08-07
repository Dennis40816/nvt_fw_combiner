using NvtFwCombiner.Domain.Composition;

namespace NvtFwCombiner.Domain.Firmware;

/// <summary>Closed canonical metadata-structure kind.</summary>
public enum FirmwareMetadataStructureKind
{
    /// <summary>Legacy/common metadata without a typed specialization.</summary>
    Generic,

    /// <summary>One TP Flash Header definition owned by the TP artifact.</summary>
    TpFlashHeader,
}

/// <summary>Resolved applicability of one physical metadata field.</summary>
public enum FirmwareMetadataFieldApplicabilityState
{
    /// <summary>The selected topology actively uses the field.</summary>
    Active,

    /// <summary>The field physically exists but is unused by the selected topology.</summary>
    Unused,

    /// <summary>No exact owner-declared applicability row can be selected.</summary>
    Unknown,
}

/// <summary>Closed semantic target kind for a metadata-definition reference.</summary>
public enum FirmwareMetadataReferenceTargetKind
{
    /// <summary>One named span including reserved bytes.</summary>
    Span,

    /// <summary>One exact physical field.</summary>
    Field,

    /// <summary>One explicit repeated-field series.</summary>
    Series,

    /// <summary>One semantic field/series group.</summary>
    Group,
}

/// <summary>
/// One exact semantic target identity without copied geometry or execution
/// authority.
/// </summary>
public sealed record FirmwareMetadataReferenceTarget
{
    /// <summary>Creates one checked semantic target reference.</summary>
    public FirmwareMetadataReferenceTarget(
        FirmwareMetadataReferenceTargetKind kind,
        string targetId)
    {
        ClosedEnum.ThrowIfUndefined(kind, "Unknown metadata reference target kind.");

        TargetId = RequiredValue.NotBlank(targetId);
        Kind = kind;
    }

    /// <summary>Closed semantic target kind.</summary>
    public FirmwareMetadataReferenceTargetKind Kind { get; }

    /// <summary>Exact id inside one canonical metadata definition.</summary>
    public string TargetId { get; }
}

/// <summary>Base for an exact typed specialization of common firmware metadata.</summary>
internal abstract class FirmwareMetadataTypedDefinition
{
    internal abstract FirmwareMetadataStructureKind StructureKind { get; }

    internal abstract void Validate(
        IReadOnlyList<FirmwareMetadataField> fields,
        long definitionLength);

    internal abstract IReadOnlyList<FirmwareResolvedMetadataField> ResolveFields(
        IReadOnlyList<FirmwareMetadataField> fields,
        TopologySelection? topology);
}

/// <summary>One named structure-relative span, including reserved bytes.</summary>
internal sealed record FirmwareMetadataNamedSpan
{
    internal FirmwareMetadataNamedSpan(string spanId, ByteRange range)
    {
        SpanId = RequiredValue.NotBlank(spanId);
        Range = range;
    }

    internal string SpanId { get; }

    internal ByteRange Range { get; }
}

internal enum TpFlashHeaderFieldSubject
{
    Header,
    Ilm,
    Dlm,
    DlmDifference,
    Data,
    FirmwareConfig,
    CtrlRam,
    MpCtrlRam,
}

internal enum TpFlashHeaderFieldRole
{
    IntegrityValue,
    DestinationAddress,
    Size,
    TpBinStartAddress,
    Option,
}

internal enum TpFlashHeaderStoredAddressBasis
{
    Absolute,
    TpBinOffset,
}

/// <summary>
/// Meaning of an address integer stored in one Header field. This describes
/// the value and never the byte position of the field itself.
/// </summary>
internal sealed record FirmwareTpFlashHeaderStoredAddressSemantics
{
    internal FirmwareTpFlashHeaderStoredAddressSemantics(
        string addressSpaceId,
        TpFlashHeaderStoredAddressBasis basis)
    {
        AddressSpaceId = RequiredValue.NotBlank(addressSpaceId);
        ClosedEnum.ThrowIfUndefined(basis, "Unknown TP Header stored-address basis.");

        Basis = basis;
    }

    internal string AddressSpaceId { get; }

    internal TpFlashHeaderStoredAddressBasis Basis { get; }
}

/// <summary>TP-specific meaning attached to one already declared physical field.</summary>
internal sealed record FirmwareTpFlashHeaderFieldSemantics
{
    internal FirmwareTpFlashHeaderFieldSemantics(
        string fieldId,
        string spanId,
        TpFlashHeaderFieldSubject subject,
        TpFlashHeaderFieldRole role,
        int? logicalIndex = null,
        FirmwareTpFlashHeaderStoredAddressSemantics? storedAddress = null)
    {
        FieldId = RequiredValue.NotBlank(fieldId);
        SpanId = RequiredValue.NotBlank(spanId);
        ClosedEnum.ThrowIfUndefined(subject, "Unknown TP Header field subject.");
        ClosedEnum.ThrowIfUndefined(role, "Unknown TP Header field role.");

        if (logicalIndex < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(logicalIndex), logicalIndex, "Logical index cannot be negative.");
        }

        Subject = subject;
        Role = role;
        LogicalIndex = logicalIndex;
        StoredAddress = storedAddress;
    }

    internal string FieldId { get; }

    internal string SpanId { get; }

    internal TpFlashHeaderFieldSubject Subject { get; }

    internal TpFlashHeaderFieldRole Role { get; }

    internal int? LogicalIndex { get; }

    internal FirmwareTpFlashHeaderStoredAddressSemantics? StoredAddress { get; }
}

/// <summary>Typed TP Flash Header payload attached to one common metadata definition.</summary>
internal sealed class FirmwareTpFlashHeaderDefinition : FirmwareMetadataTypedDefinition
{
    private readonly FirmwareMetadataNamedSpan[] _spans;
    private readonly FirmwareTpFlashHeaderFieldSemantics[] _fieldSemantics;
    private readonly FirmwareMetadataFieldSeries[] _fieldSeries;
    private readonly FirmwareMetadataFieldGroup[] _fieldGroups;

    internal FirmwareTpFlashHeaderDefinition(
        IEnumerable<FirmwareMetadataNamedSpan> spans,
        IEnumerable<FirmwareTpFlashHeaderFieldSemantics> fieldSemantics,
        IEnumerable<FirmwareMetadataFieldSeries> fieldSeries,
        IEnumerable<FirmwareMetadataFieldGroup> fieldGroups)
    {
        _spans = SnapshotUnique(
            spans,
            static span => span.SpanId,
            nameof(spans),
            "TP Header span");
        _fieldSemantics = SnapshotUnique(
            fieldSemantics,
            static semantics => semantics.FieldId,
            nameof(fieldSemantics),
            "TP Header field semantics");
        _fieldSeries = SnapshotUnique(
            fieldSeries,
            static series => series.SeriesId,
            nameof(fieldSeries),
            "TP Header field series");
        _fieldGroups = SnapshotUnique(
            fieldGroups,
            static group => group.GroupId,
            nameof(fieldGroups),
            "TP Header field group");
        DomainInvariant.Reject(
            _spans.Length == 0 || _fieldSemantics.Length == 0,
            "TP Flash Header definitions require named spans and field semantics.");

        Array.Sort(_spans, static (left, right) =>
        {
            int range = FirmwareRangeOrdering.Compare(left.Range, right.Range);
            return range != 0
                ? range
                : StringComparer.Ordinal.Compare(left.SpanId, right.SpanId);
        });
        Array.Sort(_fieldSemantics, static (left, right) =>
            StringComparer.Ordinal.Compare(left.FieldId, right.FieldId));
        Array.Sort(_fieldSeries, static (left, right) =>
            StringComparer.Ordinal.Compare(left.SeriesId, right.SeriesId));
        Array.Sort(_fieldGroups, static (left, right) =>
            StringComparer.Ordinal.Compare(left.GroupId, right.GroupId));
        Spans = Array.AsReadOnly(_spans);
        FieldSemantics = Array.AsReadOnly(_fieldSemantics);
        FieldSeries = Array.AsReadOnly(_fieldSeries);
        FieldGroups = Array.AsReadOnly(_fieldGroups);
    }

    internal override FirmwareMetadataStructureKind StructureKind =>
        FirmwareMetadataStructureKind.TpFlashHeader;

    internal IReadOnlyList<FirmwareMetadataNamedSpan> Spans { get; }

    internal IReadOnlyList<FirmwareTpFlashHeaderFieldSemantics> FieldSemantics { get; }

    internal IReadOnlyList<FirmwareMetadataFieldSeries> FieldSeries { get; }

    internal IReadOnlyList<FirmwareMetadataFieldGroup> FieldGroups { get; }

    internal override void Validate(
        IReadOnlyList<FirmwareMetadataField> fields,
        long definitionLength)
    {
        var fieldsById =
            fields.ToDictionary(static field => field.FieldId, StringComparer.Ordinal);
        DomainInvariant.Reject(
            _fieldSemantics.Length != fields.Count ||
            _fieldSemantics.Any(semantics => !fieldsById.ContainsKey(semantics.FieldId)),
            "TP Header field semantics must reference every physical field exactly once.");

        Dictionary<string, FirmwareMetadataNamedSpan> spansById =
            _spans.ToDictionary(static span => span.SpanId, StringComparer.Ordinal);
        DomainInvariant.Reject(
            _spans.Any(span => span.Range.EndExclusive > definitionLength),
            "TP Header named spans must remain inside the common structure length.");

        foreach (FirmwareTpFlashHeaderFieldSemantics semantics in _fieldSemantics)
        {
            DomainInvariant.Reject(
                !spansById.TryGetValue(semantics.SpanId, out FirmwareMetadataNamedSpan? span) ||
                !span.Range.Contains(fieldsById[semantics.FieldId].Range),
                $"TP Header field '{semantics.FieldId}' is outside its named span.");

            bool storesAddress = semantics.Role is
                TpFlashHeaderFieldRole.DestinationAddress or
                TpFlashHeaderFieldRole.TpBinStartAddress;
            DomainInvariant.Reject(
                storesAddress != (semantics.StoredAddress is not null),
                $"TP Header field '{semantics.FieldId}' stored-address semantics do not match its role.");

            DomainInvariant.Reject(
                semantics.StoredAddress is { } storedAddress &&
                ((semantics.Role == TpFlashHeaderFieldRole.DestinationAddress &&
                  storedAddress.Basis != TpFlashHeaderStoredAddressBasis.Absolute) ||
                 (semantics.Role == TpFlashHeaderFieldRole.TpBinStartAddress &&
                  storedAddress.Basis != TpFlashHeaderStoredAddressBasis.TpBinOffset)),
                $"TP Header field '{semantics.FieldId}' uses an incompatible stored-address basis.");
        }

        FirmwareMetadataField[] orderedFields = [.. fields];
        Array.Sort(orderedFields, static (left, right) =>
            FirmwareRangeOrdering.Compare(left.Range, right.Range));
        for (int index = 1; index < orderedFields.Length; index++)
        {
            DomainInvariant.Reject(
                orderedFields[index - 1].Range.Overlaps(orderedFields[index].Range),
                "TP Header physical fields cannot overlap.");
        }

        HashSet<string> seriesFieldIds = new(StringComparer.Ordinal);
        foreach (FirmwareMetadataFieldSeries series in _fieldSeries)
        {
            foreach (FirmwareMetadataFieldSeriesMember member in series.Members)
            {
                DomainInvariant.Reject(
                    !fieldsById.ContainsKey(member.FieldId) ||
                    !seriesFieldIds.Add(member.FieldId),
                    $"TP Header series '{series.SeriesId}' has a dangling or repeated field reference.");

                FirmwareTpFlashHeaderFieldSemantics semantics =
                    _fieldSemantics.Single(candidate =>
                        StringComparer.Ordinal.Equals(candidate.FieldId, member.FieldId));
                DomainInvariant.Reject(
                    semantics.LogicalIndex != member.Index,
                    $"TP Header series '{series.SeriesId}' index does not match field semantics.");
            }
        }

        Dictionary<string, FirmwareMetadataFieldSeries> seriesById =
            _fieldSeries.ToDictionary(static series => series.SeriesId, StringComparer.Ordinal);
        foreach (FirmwareMetadataFieldGroup group in _fieldGroups)
        {
            DomainInvariant.Reject(
                group.FieldIds.Any(fieldId => !fieldsById.ContainsKey(fieldId)) ||
                group.SeriesIds.Any(seriesId => !seriesById.ContainsKey(seriesId)),
                $"TP Header group '{group.GroupId}' has a dangling reference.");

            HashSet<string> effectiveFields = [.. group.FieldIds];
            foreach (string seriesId in group.SeriesIds)
            {
                foreach (FirmwareMetadataFieldSeriesMember member in seriesById[seriesId].Members)
                {
                    DomainInvariant.Reject(
                        !effectiveFields.Add(member.FieldId),
                        $"TP Header group '{group.GroupId}' repeats an effective field.");
                }
            }
        }
    }

    internal override IReadOnlyList<FirmwareResolvedMetadataField> ResolveFields(
        IReadOnlyList<FirmwareMetadataField> fields,
        TopologySelection? topology)
    {
        var seriesByField =
            _fieldSeries
                .SelectMany(series => series.Members.Select(member => (member.FieldId, Series: series)))
                .ToDictionary(static pair => pair.FieldId, static pair => pair.Series, StringComparer.Ordinal);
        return
        [
            .. fields.Select(field =>
                new FirmwareResolvedMetadataField(
                    field,
                    seriesByField.TryGetValue(field.FieldId, out FirmwareMetadataFieldSeries? series)
                        ? series.Resolve(field.FieldId, topology)
                        : FirmwareMetadataFieldApplicabilityState.Active)),
        ];
    }

    private static T[] SnapshotUnique<T>(
        IEnumerable<T> values,
        Func<T, string> idSelector,
        string parameterName,
        string label)
        where T : class
    {
        T[] snapshot = ImmutableReferenceSnapshot.Create(
            values,
            $"{label} declarations cannot contain null values.");
        return snapshot.Select(idSelector).Distinct(StringComparer.Ordinal).Count() ==
               snapshot.Length
            ? snapshot
            : throw new ArgumentException($"{label} ids must be unique.", parameterName);
    }
}

/// <summary>One exact physical field plus its resolution-scoped applicability.</summary>
public sealed record FirmwareResolvedMetadataField
{
    /// <summary>Creates one immutable resolved field reference.</summary>
    public FirmwareResolvedMetadataField(
        FirmwareMetadataField field,
        FirmwareMetadataFieldApplicabilityState applicability,
        FirmwareMetadataValue? value = null)
    {
        Field = RequiredValue.NotNull(field);
        ClosedEnum.ThrowIfUndefined(applicability, "Unknown metadata field applicability state.");

        Applicability = applicability;
        Value = value;
    }

    /// <summary>Exact canonical physical field definition reference.</summary>
    public FirmwareMetadataField Field { get; }

    /// <summary>Resolution-scoped applicability without write authority.</summary>
    public FirmwareMetadataFieldApplicabilityState Applicability { get; }

    /// <summary>
    /// Decoded value for a successful structure resolution; null only for a
    /// definition-only applicability projection.
    /// </summary>
    public FirmwareMetadataValue? Value { get; }
}
