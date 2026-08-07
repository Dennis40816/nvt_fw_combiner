using System.Diagnostics.CodeAnalysis;

namespace NvtFwCombiner.Domain.Firmware;

/// <summary>Immutable declaration of one located firmware metadata structure.</summary>
public sealed class FirmwareMetadataStructure
{
    /// <summary>Creates a checked structure declaration without reading artifact bytes.</summary>
    public FirmwareMetadataStructure(
        string structureId,
        string artifactBindingId,
        long lengthBytes,
        FirmwareMetadataLocator locator,
        IEnumerable<FirmwareMetadataField> fields,
        IEnumerable<FirmwareMetadataByteAssertion> assertions,
        IEnumerable<FirmwareMetadataFieldRelation>? relations = null,
        FirmwareMetadataTypedDefinition? typedDefinition = null)
        : this(
            structureId,
            artifactBindingId,
            new FirmwareMetadataStructureDefinition(
                structureId,
                lengthBytes,
                fields,
                assertions,
                relations,
                typedDefinition),
            locator)
    {
    }

    /// <summary>Binds one shared logical definition to an exact artifact and locator.</summary>
    public FirmwareMetadataStructure(
        string structureId,
        string artifactBindingId,
        FirmwareMetadataStructureDefinition definition,
        FirmwareMetadataLocator locator)
    {
        StructureId = RequiredValue.NotBlank(structureId);
        ArtifactBindingId = RequiredValue.NotBlank(artifactBindingId);
        Definition = RequiredValue.NotNull(definition);
        Locator = RequiredValue.NotNull(locator);
        ValidateLocatorShape(
            locator,
            definition.LengthBytes,
            definition.Assertions.Count);

    }

    /// <summary>Family-global binding identifier for this artifact and locator.</summary>
    public string StructureId { get; }

    /// <summary>Stable runtime artifact binding used by this structure.</summary>
    public string ArtifactBindingId { get; }

    /// <summary>Exact shared logical definition referenced by this binding.</summary>
    public FirmwareMetadataStructureDefinition Definition { get; }

    /// <summary>Exact positive structure length.</summary>
    public long LengthBytes => Definition.LengthBytes;

    /// <summary>Closed physical locator declaration.</summary>
    public FirmwareMetadataLocator Locator { get; }

    /// <summary>Fields in deterministic structure-relative range order.</summary>
    public IReadOnlyList<FirmwareMetadataField> Fields => Definition.Fields;

    /// <summary>Typed validation relations in deterministic relation-id order.</summary>
    public IReadOnlyList<FirmwareMetadataFieldRelation> Relations => Definition.Relations;

    /// <summary>Atomically validates and decodes one already-located exact structure slice.</summary>
    public bool TryDecode(
        ReadOnlySpan<byte> bytes,
        [NotNullWhen(true)] out FirmwareDecodedMetadataStructure? result)
    {
        return Definition.TryDecode(
            ArtifactBindingId,
            StructureId,
            bytes,
            out result);
    }

    private static void ValidateLocatorShape(
        FirmwareMetadataLocator locator,
        long lengthBytes,
        int assertionCount)
    {
        switch (locator)
        {
            case FirmwareAbsoluteRangeLocator absolute:
                DomainInvariant.Reject(
                    absolute.Range.Range.Length != lengthBytes,
                    "Absolute metadata locator length must equal its structure length.",
                    nameof(locator));

                break;
            case FirmwareRegionRelativeLocator relative:
                _ = checked(relative.Offset + lengthBytes);
                break;
            case FirmwareMarkerRelativeLocator marker:
                _ = checked(marker.ResultOffset + lengthBytes);
                DomainInvariant.Reject(
                    assertionCount == 0 &&
                    marker.Selection.Kind != FirmwareMarkerSelectionKind.Unique,
                    "Non-unique marker-relative metadata structures require an assertion.",
                    nameof(locator));

                break;
            case FirmwareMetadataFieldSelectedLocator selected:
                _ = checked(selected.ResultOffset + lengthBytes);
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(locator), "Unknown metadata locator type.");
        }
    }
}
