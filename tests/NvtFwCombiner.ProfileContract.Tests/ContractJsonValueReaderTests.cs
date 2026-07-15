using System.Numerics;
using System.Text.Json;
using NvtFwCombiner.Profiles.Normalization;

namespace NvtFwCombiner.ProfileContract.Tests;

/// <summary>Tests shared lossless contract scalar parsing independent of JSON number spelling.</summary>
public sealed class ContractJsonValueReaderTests
{
    /// <summary>Verifies mathematically equal integral representations produce one BigInteger.</summary>
    [Fact]
    public void EquivalentLargeIntegerRepresentationsAreEqual()
    {
        string expanded = $"1{new string('0', 101)}";
        BigInteger literal = ReadInteger(expanded);
        BigInteger exponent = ReadInteger("1e101");
        BigInteger decimalExponent = ReadInteger("1000.0e98");

        Assert.Equal(literal, exponent);
        Assert.Equal(literal, decimalExponent);
    }

    /// <summary>Verifies only mathematically integral JSON numbers enter the shared reader.</summary>
    [Fact]
    public void IntegerReaderRejectsFractionalAndWrongKinds()
    {
        _ = Assert.Throws<ArgumentException>(() => ReadInteger("1.5"));
        using var text = JsonDocument.Parse("\"1\"");
        _ = Assert.Throws<ArgumentException>(() => ContractJsonValueReader.ReadInteger(text.RootElement));
    }

    /// <summary>Verifies zero remains zero independently from an otherwise huge exponent.</summary>
    [Fact]
    public void ZeroDoesNotExpandHugeExponent()
    {
        Assert.Equal(BigInteger.Zero, ReadInteger("0e999999999999999999999999"));
    }

    /// <summary>Verifies the expanded digit ceiling applies equally to literals and exponents.</summary>
    [Fact]
    public void IntegerResourceCeilingIsRepresentationIndependent()
    {
        string maximumLiteral = $"1{new string('0', ContractJsonValueReader.MaximumNormalizedIntegerDigits - 1)}";
        string oversizedLiteral = $"1{new string('0', ContractJsonValueReader.MaximumNormalizedIntegerDigits)}";

        Assert.Equal(ReadInteger(maximumLiteral), ReadInteger("1e4095"));
        _ = Assert.Throws<ArgumentException>(() => ReadInteger(oversizedLiteral));
        _ = Assert.Throws<ArgumentException>(() => ReadInteger("1e4096"));
        _ = Assert.Throws<ArgumentException>(() => ReadInteger("1e1000000000"));
    }

    /// <summary>Verifies string and canonical hex readers do not coerce or normalize input.</summary>
    [Fact]
    public void StringAndHexReadersRemainExact()
    {
        using var text = JsonDocument.Parse("\"A \"");
        Assert.Equal("A ", ContractJsonValueReader.ReadString(text.RootElement));
        Assert.Equal([0xAA, 0x01], ContractJsonValueReader.ParseCanonicalHex("aa01"));
        _ = Assert.Throws<ArgumentException>(() => ContractJsonValueReader.ParseCanonicalHex("AA01"));
        _ = Assert.Throws<ArgumentException>(() => ContractJsonValueReader.ParseCanonicalHex("a"));
    }

    private static BigInteger ReadInteger(string json)
    {
        using var document = JsonDocument.Parse(json);
        return ContractJsonValueReader.ReadInteger(document.RootElement);
    }
}
