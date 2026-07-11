using NvtFwCombiner.Domain.Composition;
using NvtFwCombiner.Domain.Firmware;

namespace NvtFwCombiner.Domain.Tests.Firmware;

/// <summary>Tests assertion-first atomic decoding of already-located metadata structures.</summary>
public sealed class FirmwareMetadataStructureDecodingTests
{
    /// <summary>Verifies all field kinds decode as deterministic full-key immutable facts.</summary>
    [Fact]
    public void TryDecodeCreatesCanonicalTypedFacts()
    {
        FirmwareMetadataField[] fields = Fields();
        FirmwareMetadataStructure structure = Structure(fields: fields, assertions: Assertions());
        byte[] source = SourceBytes();

        Assert.True(structure.TryDecode(source, out FirmwareDecodedMetadataStructure? decoded));
        source[0] = 0xFF;
        fields[0] = BytesField("changed", 0, 1);

        Assert.Equal("tp-firmware", decoded.ArtifactBindingId);
        Assert.Equal("firmware-config", decoded.MetadataStructureId);
        Assert.Equal(
            ["raw", "label", "high-nibble", "low-nibble", "signed-offset"],
            decoded.Facts.Select(static fact => fact.FieldId));
        Assert.All(decoded.Facts, fact =>
        {
            Assert.Equal("tp-firmware", fact.ArtifactBindingId);
            Assert.Equal("firmware-config", fact.MetadataStructureId);
        });
        Assert.Equal("aa2f", decoded.Facts[0].Value.BytesValue?.Hex);
        Assert.Equal(" A", decoded.Facts[1].Value.TextValue);
        Assert.Equal(0x0BUL, decoded.Facts[2].Value.UnsignedIntegerValue);
        Assert.Equal(0x05UL, decoded.Facts[3].Value.UnsignedIntegerValue);
        Assert.Equal(-2, decoded.Facts[4].Value.SignedIntegerValue);

        IList<FirmwareDecodedMetadataFact> factView = Assert.IsType<IList<FirmwareDecodedMetadataFact>>(
            decoded.Facts,
            exactMatch: false);
        Assert.True(factView.IsReadOnly);
        _ = Assert.Throws<NotSupportedException>(() => factView[0] = factView[1]);
    }

    /// <summary>Verifies declaration input order cannot change canonical decoded fact order.</summary>
    [Fact]
    public void TryDecodeUsesCanonicalDeclarationOrder()
    {
        FirmwareMetadataField[] fields = Fields();
        FirmwareMetadataStructure forward = Structure(fields: fields, assertions: Assertions());
        FirmwareMetadataStructure reverse = Structure(
            fields: fields.Reverse(),
            assertions: Assertions().Reverse());

        Assert.True(forward.TryDecode(SourceBytes(), out FirmwareDecodedMetadataStructure? first));
        Assert.True(reverse.TryDecode(SourceBytes(), out FirmwareDecodedMetadataStructure? second));

        Assert.Equal(
            first.Facts.Select(static fact => (fact.FieldId, fact.Value)),
            second.Facts.Select(static fact => (fact.FieldId, fact.Value)));
    }

    /// <summary>Verifies exact and masked assertions form one all-pass conjunction.</summary>
    [Fact]
    public void TryDecodeRejectsAnyFailedAssertionWithoutFacts()
    {
        FirmwareMetadataStructure structure = Structure(fields: Fields(), assertions: Assertions());
        byte[] exactFailure = SourceBytes();
        exactFailure[0] = 0xAB;
        byte[] maskedFailure = SourceBytes();
        maskedFailure[1] = 0x3F;

        Assert.False(structure.TryDecode(exactFailure, out FirmwareDecodedMetadataStructure? exactResult));
        Assert.Null(exactResult);
        Assert.False(structure.TryDecode(maskedFailure, out FirmwareDecodedMetadataStructure? maskedResult));
        Assert.Null(maskedResult);
    }

    /// <summary>Verifies a later field failure discards every earlier temporary fact.</summary>
    [Fact]
    public void TryDecodeRejectsInvalidFieldWithoutPartialFacts()
    {
        FirmwareMetadataStructure structure = Structure(
            lengthBytes: 3,
            fields:
            [
                BytesField("first", 0, 1),
                new FirmwareMetadataField("invalid-text", 1, 2, FirmwareMetadataEncoding.PrintableAscii),
            ]);

        Assert.False(structure.TryDecode(
            [0xAA, (byte)'A', 0x80],
            out FirmwareDecodedMetadataStructure? result));
        Assert.Null(result);
    }

    /// <summary>Verifies structure decoding requires the exact declared byte length.</summary>
    [Fact]
    public void TryDecodeRejectsWrongStructureLength()
    {
        FirmwareMetadataStructure structure = Structure();

        Assert.False(structure.TryDecode([], out FirmwareDecodedMetadataStructure? empty));
        Assert.Null(empty);
        Assert.False(structure.TryDecode(new byte[7], out FirmwareDecodedMetadataStructure? shortResult));
        Assert.Null(shortResult);
        Assert.False(structure.TryDecode(new byte[9], out FirmwareDecodedMetadataStructure? longResult));
        Assert.Null(longResult);
    }

    /// <summary>Verifies assertion-only structures retain top-level identity with no forged facts.</summary>
    [Fact]
    public void TryDecodeAcceptsAssertionOnlyStructure()
    {
        FirmwareMetadataStructure structure = Structure(
            lengthBytes: 1,
            fields: [],
            assertions: [FirmwareMetadataByteAssertion.Exact(0, [0xAA])]);

        Assert.True(structure.TryDecode([0xAA], out FirmwareDecodedMetadataStructure? decoded));
        Assert.Equal("tp-firmware", decoded.ArtifactBindingId);
        Assert.Equal("firmware-config", decoded.MetadataStructureId);
        Assert.Empty(decoded.Facts);
    }

    /// <summary>Verifies non-marker structures may decode fields without byte assertions.</summary>
    [Fact]
    public void TryDecodeAcceptsFieldOnlyStructureWithoutAssertions()
    {
        FirmwareMetadataStructure structure = Structure(
            lengthBytes: 2,
            fields: [BytesField("raw", 0, 2)],
            assertions: []);

        Assert.True(structure.TryDecode([0x00, 0x01], out FirmwareDecodedMetadataStructure? decoded));
        FirmwareDecodedMetadataFact fact = Assert.Single(decoded.Facts);
        Assert.Equal("raw", fact.FieldId);
        Assert.Equal("0001", fact.Value.BytesValue?.Hex);
    }

    /// <summary>Verifies decoded values cannot be directly forged outside the Domain assembly.</summary>
    [Fact]
    public void DecodedResultConstructorsAreNotPublic()
    {
        Assert.Empty(typeof(FirmwareDecodedMetadataStructure).GetConstructors());
        Assert.Empty(typeof(FirmwareDecodedMetadataFact).GetConstructors());
    }

    private static FirmwareMetadataStructure Structure(
        long lengthBytes = 8,
        IEnumerable<FirmwareMetadataField>? fields = null,
        IEnumerable<FirmwareMetadataByteAssertion>? assertions = null)
    {
        return new FirmwareMetadataStructure(
            "firmware-config",
            "tp-firmware",
            lengthBytes,
            new FirmwareAbsoluteRangeLocator(
                new FirmwareAddressedRange("flash", new ByteRange(0, lengthBytes)),
                "root"),
            fields ?? Fields(),
            assertions ?? []);
    }

    private static FirmwareMetadataField[] Fields()
    {
        return
        [
            BytesField("raw", 0, 2),
            new FirmwareMetadataField("label", 2, 2, FirmwareMetadataEncoding.PrintableAscii),
            UnsignedSlice("high-nibble", 4, 4, 4),
            UnsignedSlice("low-nibble", 4, 0, 4),
            new FirmwareMetadataField(
                "signed-offset",
                5,
                2,
                FirmwareMetadataEncoding.SignedInteger,
                FirmwareMetadataByteOrder.LittleEndian),
        ];
    }

    private static FirmwareMetadataByteAssertion[] Assertions()
    {
        return
        [
            FirmwareMetadataByteAssertion.Exact(0, [0xAA]),
            FirmwareMetadataByteAssertion.Masked(1, [0x20], [0xF0]),
        ];
    }

    private static byte[] SourceBytes()
    {
        return [0xAA, 0x2F, (byte)' ', (byte)'A', 0xB5, 0xFE, 0xFF, 0x00];
    }

    private static FirmwareMetadataField BytesField(string fieldId, long offset, int widthBytes)
    {
        return new FirmwareMetadataField(fieldId, offset, widthBytes, FirmwareMetadataEncoding.Bytes);
    }

    private static FirmwareMetadataField UnsignedSlice(
        string fieldId,
        long offset,
        int leastSignificantBit,
        int bitCount)
    {
        return new FirmwareMetadataField(
            fieldId,
            offset,
            1,
            FirmwareMetadataEncoding.UnsignedInteger,
            FirmwareMetadataByteOrder.LittleEndian,
            new FirmwareMetadataBitSlice(leastSignificantBit, bitCount));
    }
}
