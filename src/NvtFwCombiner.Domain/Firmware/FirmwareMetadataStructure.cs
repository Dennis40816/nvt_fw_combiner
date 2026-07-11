using System.Diagnostics.CodeAnalysis;

namespace NvtFwCombiner.Domain.Firmware;

/// <summary>Immutable declaration of one located and asserted firmware metadata structure.</summary>
public sealed class FirmwareMetadataStructure
{
    private readonly FirmwareMetadataField[] _fields;
    private readonly FirmwareMetadataByteAssertion[] _assertions;

    /// <summary>Creates a checked structure declaration without reading artifact bytes.</summary>
    public FirmwareMetadataStructure(
        string structureId,
        string artifactBindingId,
        long lengthBytes,
        FirmwareMetadataLocator locator,
        IEnumerable<FirmwareMetadataField> fields,
        IEnumerable<FirmwareMetadataByteAssertion> assertions)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(structureId);
        ArgumentException.ThrowIfNullOrWhiteSpace(artifactBindingId);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(lengthBytes);
        ArgumentNullException.ThrowIfNull(locator);

        ArgumentNullException.ThrowIfNull(fields);
        _fields = [.. fields];
        if (_fields.Any(static field => field is null))
        {
            throw new ArgumentException("Metadata structures cannot contain null fields.", nameof(fields));
        }

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

        ArgumentNullException.ThrowIfNull(assertions);
        _assertions = [.. assertions];
        if (_assertions.Any(static assertion => assertion is null))
        {
            throw new ArgumentException("Metadata structures cannot contain null assertions.", nameof(assertions));
        }

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
        ValidateLocatorShape(locator, lengthBytes, _assertions.Length);

        StructureId = structureId;
        ArtifactBindingId = artifactBindingId;
        LengthBytes = lengthBytes;
        Locator = locator;
        Fields = Array.AsReadOnly(_fields);
        Assertions = Array.AsReadOnly(_assertions);
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
        }

        result = new FirmwareDecodedMetadataStructure(ArtifactBindingId, StructureId, facts);
        return true;
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
                if (assertionCount == 0)
                {
                    throw new ArgumentException(
                        "Marker-relative metadata structures require an assertion.",
                        nameof(locator));
                }

                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(locator), "Unknown metadata locator type.");
        }
    }

    private static int CompareFields(FirmwareMetadataField left, FirmwareMetadataField right)
    {
        int startComparison = left.Range.Start.CompareTo(right.Range.Start);
        if (startComparison != 0)
        {
            return startComparison;
        }

        int lengthComparison = right.Range.Length.CompareTo(left.Range.Length);
        return lengthComparison != 0
            ? lengthComparison
            : StringComparer.Ordinal.Compare(left.FieldId, right.FieldId);
    }

    private static int CompareAssertions(
        FirmwareMetadataByteAssertion left,
        FirmwareMetadataByteAssertion right)
    {
        int startComparison = left.Range.Start.CompareTo(right.Range.Start);
        if (startComparison != 0)
        {
            return startComparison;
        }

        int lengthComparison = right.Range.Length.CompareTo(left.Range.Length);
        if (lengthComparison != 0)
        {
            return lengthComparison;
        }

        int expectedComparison = StringComparer.Ordinal.Compare(
            left.ExpectedBytes.Hex,
            right.ExpectedBytes.Hex);
        return expectedComparison != 0
            ? expectedComparison
            : StringComparer.Ordinal.Compare(left.MaskBytes.Hex, right.MaskBytes.Hex);
    }
}
