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
        if (!Enum.IsDefined(kind))
        {
            throw new ArgumentOutOfRangeException(
                nameof(kind),
                kind,
                "Unknown metadata reference target kind.");
        }

        ArgumentException.ThrowIfNullOrWhiteSpace(targetId);
        Kind = kind;
        TargetId = targetId;
    }

    /// <summary>Closed semantic target kind.</summary>
    public FirmwareMetadataReferenceTargetKind Kind { get; }

    /// <summary>Exact id inside one canonical metadata definition.</summary>
    public string TargetId { get; }
}

/// <summary>Base for an exact typed specialization of common firmware metadata.</summary>
public abstract class FirmwareMetadataTypedDefinition
{
    /// <summary>Closed specialization discriminator.</summary>
    public abstract FirmwareMetadataStructureKind StructureKind { get; }

    internal abstract void Validate(
        IReadOnlyList<FirmwareMetadataField> fields,
        long definitionLength);

    internal abstract IReadOnlyList<FirmwareResolvedMetadataField> ResolveFields(
        IReadOnlyList<FirmwareMetadataField> fields,
        TopologySelection? topology);
}

/// <summary>One named structure-relative span, including reserved bytes.</summary>
public sealed record FirmwareMetadataNamedSpan
{
    /// <summary>Creates one checked named half-open span.</summary>
    public FirmwareMetadataNamedSpan(string spanId, ByteRange range)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(spanId);
        SpanId = spanId;
        Range = range;
    }

    /// <summary>Stable span identity inside one structure definition.</summary>
    public string SpanId { get; }

    /// <summary>Checked structure-relative range.</summary>
    public ByteRange Range { get; }
}

/// <summary>Semantic subject carried by one TP Flash Header field.</summary>
public enum TpFlashHeaderFieldSubject
{
    /// <summary>Whole-header or build-option state.</summary>
    Header,

    /// <summary>Instruction local memory payload.</summary>
    Ilm,

    /// <summary>Data local memory payload.</summary>
    Dlm,

    /// <summary>Difference DLM payload.</summary>
    DlmDifference,
}

/// <summary>Closed value role carried by one TP Flash Header field.</summary>
public enum TpFlashHeaderFieldRole
{
    /// <summary>CRC or another integrity result value.</summary>
    IntegrityValue,

    /// <summary>Runtime destination address.</summary>
    DestinationAddress,

    /// <summary>Declared payload byte size.</summary>
    Size,

    /// <summary>Stored TP-BIN-relative payload start address.</summary>
    TpBinStartAddress,

    /// <summary>Build or transport option value.</summary>
    Option,
}

/// <summary>Closed basis used to interpret one address value stored in a Header field.</summary>
public enum TpFlashHeaderStoredAddressBasis
{
    /// <summary>The encoded value is absolute in its declared value address space.</summary>
    Absolute,

    /// <summary>The encoded value is relative to the start of the immutable TP BIN.</summary>
    TpBinOffset,
}

/// <summary>
/// Meaning of an address integer stored in one Header field. This describes
/// the value and never the byte position of the field itself.
/// </summary>
public sealed record FirmwareTpFlashHeaderStoredAddressSemantics
{
    /// <summary>Creates one checked stored-address value declaration.</summary>
    public FirmwareTpFlashHeaderStoredAddressSemantics(
        string addressSpaceId,
        TpFlashHeaderStoredAddressBasis basis)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(addressSpaceId);
        if (!Enum.IsDefined(basis))
        {
            throw new ArgumentOutOfRangeException(
                nameof(basis),
                basis,
                "Unknown TP Header stored-address basis.");
        }

        AddressSpaceId = addressSpaceId;
        Basis = basis;
    }

    /// <summary>Address space named by the encoded value.</summary>
    public string AddressSpaceId { get; }

    /// <summary>Origin/basis used to interpret the encoded value.</summary>
    public TpFlashHeaderStoredAddressBasis Basis { get; }
}

/// <summary>TP-specific meaning attached to one already declared physical field.</summary>
public sealed record FirmwareTpFlashHeaderFieldSemantics
{
    /// <summary>Creates one exact field-to-span semantic binding.</summary>
    public FirmwareTpFlashHeaderFieldSemantics(
        string fieldId,
        string spanId,
        TpFlashHeaderFieldSubject subject,
        TpFlashHeaderFieldRole role,
        int? logicalIndex = null,
        FirmwareTpFlashHeaderStoredAddressSemantics? storedAddress = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(fieldId);
        ArgumentException.ThrowIfNullOrWhiteSpace(spanId);
        if (!Enum.IsDefined(subject))
        {
            throw new ArgumentOutOfRangeException(nameof(subject), subject, "Unknown TP Header field subject.");
        }

        if (!Enum.IsDefined(role))
        {
            throw new ArgumentOutOfRangeException(nameof(role), role, "Unknown TP Header field role.");
        }

        if (logicalIndex < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(logicalIndex), logicalIndex, "Logical index cannot be negative.");
        }

        FieldId = fieldId;
        SpanId = spanId;
        Subject = subject;
        Role = role;
        LogicalIndex = logicalIndex;
        StoredAddress = storedAddress;
    }

    /// <summary>Exact physical field identity.</summary>
    public string FieldId { get; }

    /// <summary>Named span that contains the field.</summary>
    public string SpanId { get; }

    /// <summary>Typed firmware subject.</summary>
    public TpFlashHeaderFieldSubject Subject { get; }

    /// <summary>Typed value role.</summary>
    public TpFlashHeaderFieldRole Role { get; }

    /// <summary>Optional logical record index.</summary>
    public int? LogicalIndex { get; }

    /// <summary>
    /// Value address space/basis when this field stores an address; null for
    /// non-address values.
    /// </summary>
    public FirmwareTpFlashHeaderStoredAddressSemantics? StoredAddress { get; }
}

/// <summary>Typed TP Flash Header payload attached to one common metadata definition.</summary>
public sealed class FirmwareTpFlashHeaderDefinition : FirmwareMetadataTypedDefinition
{
    private readonly FirmwareMetadataNamedSpan[] _spans;
    private readonly FirmwareTpFlashHeaderFieldSemantics[] _fieldSemantics;
    private readonly FirmwareMetadataFieldSeries[] _fieldSeries;
    private readonly FirmwareMetadataFieldGroup[] _fieldGroups;

    /// <summary>Creates one immutable typed TP Flash Header payload.</summary>
    public FirmwareTpFlashHeaderDefinition(
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
        if (_spans.Length == 0 || _fieldSemantics.Length == 0)
        {
            throw new ArgumentException(
                "TP Flash Header definitions require named spans and field semantics.");
        }

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

    /// <inheritdoc />
    public override FirmwareMetadataStructureKind StructureKind =>
        FirmwareMetadataStructureKind.TpFlashHeader;

    /// <summary>Named structure-relative spans.</summary>
    public IReadOnlyList<FirmwareMetadataNamedSpan> Spans { get; }

    /// <summary>Semantic binding for every physical field.</summary>
    public IReadOnlyList<FirmwareTpFlashHeaderFieldSemantics> FieldSemantics { get; }

    /// <summary>Explicit repeated-field series.</summary>
    public IReadOnlyList<FirmwareMetadataFieldSeries> FieldSeries { get; }

    /// <summary>Reference-only semantic field groups.</summary>
    public IReadOnlyList<FirmwareMetadataFieldGroup> FieldGroups { get; }

    internal override void Validate(
        IReadOnlyList<FirmwareMetadataField> fields,
        long definitionLength)
    {
        var fieldsById =
            fields.ToDictionary(static field => field.FieldId, StringComparer.Ordinal);
        if (_fieldSemantics.Length != fields.Count ||
            _fieldSemantics.Any(semantics => !fieldsById.ContainsKey(semantics.FieldId)))
        {
            throw new ArgumentException(
                "TP Header field semantics must reference every physical field exactly once.");
        }

        Dictionary<string, FirmwareMetadataNamedSpan> spansById =
            _spans.ToDictionary(static span => span.SpanId, StringComparer.Ordinal);
        if (_spans.Any(span => span.Range.EndExclusive > definitionLength))
        {
            throw new ArgumentException(
                "TP Header named spans must remain inside the common structure length.");
        }

        foreach (FirmwareTpFlashHeaderFieldSemantics semantics in _fieldSemantics)
        {
            if (!spansById.TryGetValue(semantics.SpanId, out FirmwareMetadataNamedSpan? span) ||
                !span.Range.Contains(fieldsById[semantics.FieldId].Range))
            {
                throw new ArgumentException(
                    $"TP Header field '{semantics.FieldId}' is outside its named span.");
            }

            bool storesAddress = semantics.Role is
                TpFlashHeaderFieldRole.DestinationAddress or
                TpFlashHeaderFieldRole.TpBinStartAddress;
            if (storesAddress != (semantics.StoredAddress is not null))
            {
                throw new ArgumentException(
                    $"TP Header field '{semantics.FieldId}' stored-address semantics do not match its role.");
            }

            if (semantics.StoredAddress is { } storedAddress &&
                ((semantics.Role == TpFlashHeaderFieldRole.DestinationAddress &&
                  storedAddress.Basis != TpFlashHeaderStoredAddressBasis.Absolute) ||
                 (semantics.Role == TpFlashHeaderFieldRole.TpBinStartAddress &&
                  storedAddress.Basis != TpFlashHeaderStoredAddressBasis.TpBinOffset)))
            {
                throw new ArgumentException(
                    $"TP Header field '{semantics.FieldId}' uses an incompatible stored-address basis.");
            }
        }

        FirmwareMetadataField[] orderedFields = [.. fields];
        Array.Sort(orderedFields, static (left, right) =>
            FirmwareRangeOrdering.Compare(left.Range, right.Range));
        for (int index = 1; index < orderedFields.Length; index++)
        {
            if (orderedFields[index - 1].Range.Overlaps(orderedFields[index].Range))
            {
                throw new ArgumentException(
                    "TP Header physical fields cannot overlap.");
            }
        }

        HashSet<string> seriesFieldIds = new(StringComparer.Ordinal);
        foreach (FirmwareMetadataFieldSeries series in _fieldSeries)
        {
            foreach (FirmwareMetadataFieldSeriesMember member in series.Members)
            {
                if (!fieldsById.ContainsKey(member.FieldId) ||
                    !seriesFieldIds.Add(member.FieldId))
                {
                    throw new ArgumentException(
                        $"TP Header series '{series.SeriesId}' has a dangling or repeated field reference.");
                }

                FirmwareTpFlashHeaderFieldSemantics semantics =
                    _fieldSemantics.Single(candidate =>
                        StringComparer.Ordinal.Equals(candidate.FieldId, member.FieldId));
                if (semantics.LogicalIndex != member.Index)
                {
                    throw new ArgumentException(
                        $"TP Header series '{series.SeriesId}' index does not match field semantics.");
                }
            }
        }

        Dictionary<string, FirmwareMetadataFieldSeries> seriesById =
            _fieldSeries.ToDictionary(static series => series.SeriesId, StringComparer.Ordinal);
        foreach (FirmwareMetadataFieldGroup group in _fieldGroups)
        {
            if (group.FieldIds.Any(fieldId => !fieldsById.ContainsKey(fieldId)) ||
                group.SeriesIds.Any(seriesId => !seriesById.ContainsKey(seriesId)))
            {
                throw new ArgumentException(
                    $"TP Header group '{group.GroupId}' has a dangling reference.");
            }

            HashSet<string> effectiveFields = [.. group.FieldIds];
            foreach (string seriesId in group.SeriesIds)
            {
                foreach (FirmwareMetadataFieldSeriesMember member in seriesById[seriesId].Members)
                {
                    if (!effectiveFields.Add(member.FieldId))
                    {
                        throw new ArgumentException(
                            $"TP Header group '{group.GroupId}' repeats an effective field.");
                    }
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
        ArgumentNullException.ThrowIfNull(field);
        if (!Enum.IsDefined(applicability))
        {
            throw new ArgumentOutOfRangeException(
                nameof(applicability),
                applicability,
                "Unknown metadata field applicability state.");
        }

        Field = field;
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
