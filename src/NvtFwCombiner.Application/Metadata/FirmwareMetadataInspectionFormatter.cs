using System.Globalization;
using NvtFwCombiner.Application.Capabilities;
using NvtFwCombiner.Domain.Composition;
using NvtFwCombiner.Domain.Firmware;

namespace NvtFwCombiner.Application.Metadata;

/// <summary>Invariant, source-neutral presentation of one resolved metadata inspection.</summary>
public sealed class FormattedMetadataInspectionSnapshot
{
    internal FormattedMetadataInspectionSnapshot(
        ResolutionToken resolutionToken,
        long authoringRevision,
        IEnumerable<FormattedMetadataStructure> structures)
    {
        ArgumentNullException.ThrowIfNull(structures);
        ResolutionToken = resolutionToken;
        AuthoringRevision = authoringRevision;
        Structures = Array.AsReadOnly([.. structures]);
    }

    /// <summary>Capability publication token used by the source inspection.</summary>
    public ResolutionToken ResolutionToken { get; }

    /// <summary>Authoring revision evaluated by the source inspection.</summary>
    public long AuthoringRevision { get; }

    /// <summary>Formatted entries in canonical metadata-plan order.</summary>
    public IReadOnlyList<FormattedMetadataStructure> Structures { get; }
}

/// <summary>One formatted metadata-plan entry with optional resolved geometry.</summary>
public sealed class FormattedMetadataStructure
{
    internal FormattedMetadataStructure(
        string bindingId,
        string mapId,
        string artifactBindingId,
        string structureId,
        MetadataInspectionState state,
        ResolvedChildReadiness readiness,
        FirmwareMetadataStructureResolutionFailure? failure,
        FirmwareArtifactIdentity? artifactIdentity,
        FirmwareAddressedRange? addressedRange,
        IEnumerable<FormattedMetadataField> fields)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(bindingId);
        ArgumentException.ThrowIfNullOrWhiteSpace(mapId);
        ArgumentException.ThrowIfNullOrWhiteSpace(artifactBindingId);
        ArgumentException.ThrowIfNullOrWhiteSpace(structureId);
        ArgumentNullException.ThrowIfNull(fields);
        BindingId = bindingId;
        MapId = mapId;
        ArtifactBindingId = artifactBindingId;
        StructureId = structureId;
        State = state;
        Readiness = readiness;
        Failure = failure;
        ArtifactIdentity = artifactIdentity;
        AddressedRange = addressedRange;
        Fields = Array.AsReadOnly([.. fields]);
    }

    /// <summary>Stable metadata-plan binding id.</summary>
    public string BindingId { get; }

    /// <summary>Exact resolved map id.</summary>
    public string MapId { get; }

    /// <summary>Exact artifact binding that owns the structure.</summary>
    public string ArtifactBindingId { get; }

    /// <summary>Canonical metadata structure id.</summary>
    public string StructureId { get; }

    /// <summary>Closed inspection state.</summary>
    public MetadataInspectionState State { get; }

    /// <summary>Closed consumer readiness.</summary>
    public ResolvedChildReadiness Readiness { get; }

    /// <summary>Typed resolution failure, when present.</summary>
    public FirmwareMetadataStructureResolutionFailure? Failure { get; }

    /// <summary>Exact immutable artifact identity, only when resolved.</summary>
    public FirmwareArtifactIdentity? ArtifactIdentity { get; }

    /// <summary>Exact resolved structure range, only when resolved.</summary>
    public FirmwareAddressedRange? AddressedRange { get; }

    /// <summary>Resolved fields in canonical definition order.</summary>
    public IReadOnlyList<FormattedMetadataField> Fields { get; }
}

/// <summary>One invariantly formatted resolved field with exact absolute geometry.</summary>
public sealed record FormattedMetadataField(
    string FieldId,
    string DisplayName,
    FirmwareAddressedRange AddressedRange,
    FirmwareMetadataFieldApplicabilityState Applicability,
    FirmwareMetadataValueKind? ValueKind,
    string Value);

/// <summary>Common Application formatter shared by desktop, CLI, and reports.</summary>
public static class FirmwareMetadataInspectionFormatter
{
    /// <summary>Formats only facts already owned by the exact inspection result.</summary>
    public static FormattedMetadataInspectionSnapshot Format(MetadataInspectionSnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        return new FormattedMetadataInspectionSnapshot(
            snapshot.ResolutionToken,
            snapshot.AuthoringRevision,
            snapshot.Results
                .Where(static result => result.PlanEntry.Definition.Purposes.Contains(
                    MetadataReferencePurpose.Formatting))
                .Select(FormatStructure));
    }

    private static FormattedMetadataStructure FormatStructure(MetadataInspectionResult result)
    {
        ArgumentNullException.ThrowIfNull(result);
        MetadataPlanEntry entry = result.PlanEntry.Definition;
        FirmwareMetadataStructureResolution? resolution = result.Resolution;
        FirmwareResolvedMetadataStructure? resolved = resolution?.Resolved;
        FirmwareAddressedRange? structureRange = resolved?.LocatorOutcome.ResolvedRange;
        return new FormattedMetadataStructure(
            entry.BindingId,
            resolution?.MapId ?? entry.ResolvedMap.ImageMap.MapId,
            entry.SpaceId,
            entry.StructureDefinition.StructureId,
            result.State,
            result.Readiness,
            resolution?.Failure,
            resolved?.ArtifactIdentity,
            structureRange,
            resolved is not null && structureRange is not null
                ? resolved.Fields
                    .Where(field => entry.TargetReferences.Any(target =>
                        entry.StructureDefinition.Definition.ReferenceTargetContainsField(
                            target,
                            field.Field)))
                    .Select(field => FormatField(structureRange, field))
                : []);
    }

    private static FormattedMetadataField FormatField(
        FirmwareAddressedRange structureRange,
        FirmwareResolvedMetadataField resolvedField)
    {
        FirmwareMetadataField field = resolvedField.Field;
        var absoluteRange = new ByteRange(
            checked(structureRange.Range.Start + field.Range.Start),
            field.Range.Length);
        return structureRange.Range.Contains(absoluteRange)
            ? new FormattedMetadataField(
                field.FieldId,
                FirmwareMetadataFieldDisplayName.Format(field.FieldId, field.SourceName),
                new FirmwareAddressedRange(structureRange.AddressSpaceId, absoluteRange),
                resolvedField.Applicability,
                resolvedField.Value?.Kind,
                FormatValue(resolvedField.Value))
            : throw new InvalidOperationException(
                $"Resolved field '{field.FieldId}' is outside its structure range.");
    }

    private static string FormatValue(FirmwareMetadataValue? value)
    {
        return value?.Kind switch
        {
            FirmwareMetadataValueKind.SignedInteger =>
                value.SignedIntegerValue!.Value.ToString(CultureInfo.InvariantCulture),
            FirmwareMetadataValueKind.UnsignedInteger =>
                value.UnsignedIntegerValue!.Value.ToString(CultureInfo.InvariantCulture),
            FirmwareMetadataValueKind.Bytes => FormatBytes(value.BytesValue!.Hex),
            FirmwareMetadataValueKind.Text => value.TextValue!,
            null => string.Empty,
            _ => throw new InvalidOperationException("Unknown firmware metadata value kind."),
        };
    }

    private static string FormatBytes(string compactHex)
    {
        return string.Join(
            ' ',
            Enumerable.Range(0, compactHex.Length / 2)
                .Select(index => compactHex.Substring(index * 2, 2).ToUpperInvariant()));
    }
}
