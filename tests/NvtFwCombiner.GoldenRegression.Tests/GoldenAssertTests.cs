// Copyright (c) 2026 Dennis Liu. All rights reserved.
using NvtFwCombiner.TestSupport;
using Xunit.Sdk;

namespace NvtFwCombiner.GoldenRegression.Tests;

[Collection(nameof(GoldenHostCatalogReloadSerialGroup))]
public sealed class GoldenAssertTests
{
    [Fact]
    public void Contains_ExplicitComparison_PreservesMarkerSemantics()
    {
        Assert.True(GoldenDiagnostics.Contains("marker", "prefix marker suffix"));
        Assert.False(GoldenDiagnostics.Contains("MARKER", "prefix marker suffix"));
        Assert.True(GoldenDiagnostics.Contains("MARKER", "prefix marker suffix", StringComparison.OrdinalIgnoreCase));
        Assert.False(GoldenDiagnostics.Contains("marker", null));
    }

    [Fact]
    public void AssertEqualsGolden_EqualArraysAndSpans_Passes()
    {
        byte[] expected = [1, 2, 3];
        byte[] actual = [1, 2, 3];

        GoldenAssert.AssertEqualsGolden(expected, actual, "equal image");
        GoldenAssert.AssertEqualsGolden(expected.AsSpan(), actual.AsSpan(), "equal image");
        GoldenAssert.AssertEqualsGolden(ReadOnlySpan<byte>.Empty, ReadOnlySpan<byte>.Empty, "empty image");
    }

    [Fact]
    public void AssertEqualsGolden_OneByteDifference_ReportsFirstOffset()
    {
        XunitException failure = Assert.Throws<XunitException>(() =>
            GoldenAssert.AssertEqualsGolden(new byte[] { 1, 2, 3 }, new byte[] { 1, 9, 3 }, "header magic"));

        Assert.StartsWith("Golden mismatch [header magic]: expected 3 bytes, actual 3 bytes, first difference at offset 0x1, expected sha256 039058c6f2c0, actual sha256 ", failure.Message, StringComparison.Ordinal);
        Assert.Matches("actual sha256 [0-9a-f]{12}\\.$", failure.Message);
    }

    [Fact]
    public void AssertEqualsGolden_DistinctiveBytes_DoesNotExposeByteValues()
    {
        XunitException failure = Assert.Throws<XunitException>(() =>
            GoldenAssert.AssertEqualsGolden(new byte[] { 0xDE, 0xAD, 0xBE, 0xEF },
                new byte[] { 0xDE, 0xAD, 0xBE, 0x01 }, "marker"));

        Assert.DoesNotContain("DEADBEEF", failure.Message, StringComparison.Ordinal);
        Assert.DoesNotContain("deadbeef", failure.Message, StringComparison.Ordinal);
        Assert.DoesNotContain("EF", failure.Message, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("01", failure.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [InlineData(0, 1)]
    [InlineData(1, 0)]
    [InlineData(2, 3)]
    [InlineData(3, 2)]
    public void AssertEqualsGolden_DifferentLengths_ReportsLengthsAndShorterOffset(int expectedLength, int actualLength)
    {
        XunitException failure = Assert.Throws<XunitException>(() =>
            GoldenAssert.AssertEqualsGolden(new byte[expectedLength], new byte[actualLength], "image length"));

        Assert.Contains($"expected {expectedLength} bytes, actual {actualLength} bytes", failure.Message, StringComparison.Ordinal);
        Assert.Contains($"first difference at offset 0x{Math.Min(expectedLength, actualLength):X},", failure.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void AssertEqualsGolden_DifferentLengthsWithEarlierMismatch_ReportsEarlierOffset()
    {
        XunitException failure = Assert.Throws<XunitException>(() =>
            GoldenAssert.AssertEqualsGolden(new byte[] { 1, 2 }, new byte[] { 9, 2, 3 }, "image length"));

        Assert.Contains("first difference at offset 0x0,", failure.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void AssertFileEqualsGolden_EqualFile_Passes()
    {
        using var workspace = TempWorkspace.Create("golden-assert");
        string expectedPath = workspace.Write("expected.bin", [1, 2, 3]);

        GoldenAssert.AssertFileEqualsGolden(expectedPath, new byte[] { 1, 2, 3 }, "complete image");
    }

    [Fact]
    public void AssertFileEqualsGolden_DifferentFile_DoesNotExposePath()
    {
        using var workspace = TempWorkspace.Create("golden-assert");
        string expectedPath = workspace.Write("private-customer-deadbeef.bin", [1, 2, 3]);

        XunitException failure = Assert.Throws<XunitException>(() =>
            GoldenAssert.AssertFileEqualsGolden(expectedPath, new byte[] { 1, 9, 3 }, "complete image"));

        Assert.DoesNotContain(expectedPath, failure.Message, StringComparison.Ordinal);
        Assert.DoesNotContain("private-customer", failure.Message, StringComparison.Ordinal);
        Assert.Contains("first difference at offset 0x1,", failure.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void AssertFileEqualsGolden_MissingFile_DoesNotExposePath()
    {
        using var workspace = TempWorkspace.Create("golden-assert");
        string expectedPath = workspace.PathFor("private-customer-missing.bin");

        XunitException failure = Assert.Throws<XunitException>(() =>
            GoldenAssert.AssertFileEqualsGolden(expectedPath, new byte[] { 1 }, "complete image"));

        Assert.Equal("Golden expected file could not be read.", failure.Message);
    }

    [Fact]
    public void AssertEqualsGolden_NullArrays_Passes()
    {
        GoldenAssert.AssertEqualsGolden((byte[]?)null, (byte[]?)null, "optional image");
    }

    [Fact]
    public void AssertEqualsGolden_NullAndEmptyArrays_ThrowsPrivateMismatch()
    {
        XunitException failure = Assert.Throws<XunitException>(() =>
            GoldenAssert.AssertEqualsGolden((byte[]?)null, Array.Empty<byte>(), "optional image"));

        Assert.Equal("Golden mismatch [optional image]: expected 0 bytes, actual 0 bytes, first difference at offset 0x0, expected sha256 e3b0c44298fc, actual sha256 e3b0c44298fc.", failure.Message);
    }

    [Fact]
    public void TextSummary_PrivateOutput_ReportsOnlyLengthAndHash()
    {
        const string output = "secret-DEADBEEF/customer.bin";

        string summary = GoldenDiagnostics.TextSummary(output);

        Assert.Equal("length 28, sha256 f8a4979243a8", summary);
        Assert.DoesNotContain("DEADBEEF", summary, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("customer.bin", summary, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("0")]
    [InlineData("true")]
    [InlineData(" 1 ")]
    [InlineData("1")]
    public void Label_DefaultAndVerbose_HidesIdUnlessExplicitlyEnabled(string? verbose)
    {
        string? previous = Environment.GetEnvironmentVariable("NFC_GOLDEN_VERBOSE");
        string expected = verbose == "1" ? "customer-private-case" : "case-ae9de91e8329";
        try
        {
            Environment.SetEnvironmentVariable("NFC_GOLDEN_VERBOSE", verbose);

            Assert.Equal(expected, GoldenDiagnostics.Label("customer-private-case"));
        }
        finally
        {
            Environment.SetEnvironmentVariable("NFC_GOLDEN_VERBOSE", previous);
        }
    }

    [Fact]
    public void Label_ControlledChildProcess_ReturnsExpectedLabel()
    {
        string? previous = Environment.GetEnvironmentVariable("NFC_GOLDEN_VERBOSE");
        try
        {
            Environment.SetEnvironmentVariable("NFC_GOLDEN_VERBOSE", "1");

            Assert.Equal("customer-private-case", GoldenDiagnostics.Label("customer-private-case"));
        }
        finally
        {
            Environment.SetEnvironmentVariable("NFC_GOLDEN_VERBOSE", previous);
        }
    }

    [Fact]
    public void AssertAllGolden_FirstMismatch_ContinuesNextItemWithoutExposingBytes()
    {
        int visited = 0;
        int[] items = [0, 1];
        XunitException failure = Assert.Throws<XunitException>(() =>
            GoldenAssert.AssertAllGolden(items, item =>
            {
                visited++;
                GoldenAssert.AssertEqualsGolden(new byte[] { 0xDE, 0xAD, 0xBE, 0xEF },
                    item == 0 ? new byte[] { 0xDE, 0xAD, 0xBE, 0x01 } : new byte[] { 0xDE, 0xAD, 0xBE, 0xEF }, "marker");
            }));

        Assert.Equal(2, visited);
        Assert.StartsWith("Golden mismatch [marker]:", failure.Message, StringComparison.Ordinal);
        Assert.DoesNotContain("DEADBEEF", failure.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void AssertAllGolden_IoFailure_ContinuesWithoutExposingPath()
    {
        int visited = 0;
        int[] items = [0, 1];
        XunitException failure = Assert.Throws<XunitException>(() =>
            GoldenAssert.AssertAllGolden(items, item =>
            {
                visited++;
                if (item == 0)
                {
                    throw new IOException("private-customer/DEADBEEF.bin");
                }
            }));

        Assert.Equal(2, visited);
        Assert.Equal("Golden sequence check could not complete.", failure.Message);
    }

    [Fact]
    public void AssertAllGolden_UntrustedXunitFailure_DoesNotExposeDiagnostic()
    {
        int[] items = [0];
        XunitException failure = Assert.Throws<XunitException>(() =>
            GoldenAssert.AssertAllGolden(items, _ => throw new XunitException("private-customer/DEADBEEF.bin")));

        Assert.Equal("Golden sequence check could not complete.", failure.Message);
    }
}
