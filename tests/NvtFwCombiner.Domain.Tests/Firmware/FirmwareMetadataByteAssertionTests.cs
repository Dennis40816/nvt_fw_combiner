using NvtFwCombiner.Domain.Composition;
using NvtFwCombiner.Domain.Firmware;

namespace NvtFwCombiner.Domain.Tests.Firmware;

/// <summary>Tests canonical metadata byte assertion declarations and matching.</summary>
public sealed class FirmwareMetadataByteAssertionTests
{
    /// <summary>Verifies exact assertions snapshot bytes and normalize an all-ff runtime mask.</summary>
    [Fact]
    public void ExactCreatesCanonicalImmutableAssertion()
    {
        byte[] source = [0x12, 0x34];

        var assertion = FirmwareMetadataByteAssertion.Exact(4, source);
        source[0] = 0xFF;

        Assert.Equal(new ByteRange(4, 2), assertion.Range);
        Assert.Equal("1234", assertion.ExpectedBytes.Hex);
        Assert.Equal("ffff", assertion.MaskBytes.Hex);
        Assert.True(assertion.Matches([0x12, 0x34]));
        Assert.False(assertion.Matches([0x12, 0x35]));
        Assert.False(assertion.Matches([0x12]));
        Assert.False(assertion.Matches([0x12, 0x34, 0x00]));
    }

    /// <summary>Verifies partial masks ignore cleared bits and compare every selected bit.</summary>
    [Fact]
    public void MaskedMatchesCanonicalSelectedBits()
    {
        byte[] expected = [0x10, 0x04];
        byte[] mask = [0xF0, 0x0F];

        var assertion = FirmwareMetadataByteAssertion.Masked(
            8,
            expected,
            mask);
        expected[0] = 0;
        mask[0] = 0;

        Assert.Equal("1004", assertion.ExpectedBytes.Hex);
        Assert.Equal("f00f", assertion.MaskBytes.Hex);
        Assert.True(assertion.Matches([0x1F, 0xA4]));
        Assert.False(assertion.Matches([0x2F, 0xA4]));
        Assert.False(assertion.Matches([0x1F, 0xA5]));
    }

    /// <summary>Verifies zero, exact, wrong-length, and noncanonical masks are rejected.</summary>
    [Fact]
    public void MaskedRejectsInvalidMaskBoundaries()
    {
        _ = Assert.Throws<ArgumentException>(() => FirmwareMetadataByteAssertion.Masked(
            0,
            [0],
            [0]));
        _ = Assert.Throws<ArgumentException>(() => FirmwareMetadataByteAssertion.Masked(
            0,
            [1],
            [0xFF]));
        _ = Assert.Throws<ArgumentException>(() => FirmwareMetadataByteAssertion.Masked(
            0,
            [0, 0],
            [0x0F]));
        _ = Assert.Throws<ArgumentException>(() => FirmwareMetadataByteAssertion.Masked(
            0,
            [0x10],
            [0x0F]));
    }

    /// <summary>Verifies empty and invalid structure-relative ranges fail closed.</summary>
    [Fact]
    public void FactoriesRejectInvalidRangeBoundaries()
    {
        _ = Assert.Throws<ArgumentException>(() => FirmwareMetadataByteAssertion.Exact(0, []));
        _ = Assert.Throws<ArgumentException>(() => FirmwareMetadataByteAssertion.Masked(0, [], []));
        _ = Assert.Throws<ArgumentOutOfRangeException>(() => FirmwareMetadataByteAssertion.Exact(-1, [1]));
        _ = Assert.Throws<OverflowException>(() => FirmwareMetadataByteAssertion.Exact(long.MaxValue, [1]));
    }
}
