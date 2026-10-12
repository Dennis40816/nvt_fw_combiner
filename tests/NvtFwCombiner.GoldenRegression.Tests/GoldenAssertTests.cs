// Copyright (c) 2026 Dennis Liu. All rights reserved.
using System.Security.Cryptography;
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

        Assert.Equal("Golden mismatch [header magic]: expected 3 bytes, actual 3 bytes, first difference at offset 0x1, expected sha256 omitted, actual sha256 omitted.", failure.Message);
        GoldenAssert.AssertValueEqualsGolden(256, 256, "field");
        GoldenAssert.AssertValueEqualsGolden((int?)256, (int?)256, "field");
        XunitException fieldFailure = Assert.Throws<XunitException>(() =>
            GoldenAssert.AssertValueEqualsGolden(256, 257, "field"));
        Assert.Equal("Golden value mismatch [field]: values differ.", fieldFailure.Message);
    }

    [Fact]
    public void AssertEqualsGolden_LongImageDifference_ReportsHashPrefixOfBothSides()
    {
        byte[] expected = new byte[GoldenAssert.MinimumHashedLength];
        byte[] actual = new byte[GoldenAssert.MinimumHashedLength];
        actual[7] = 1;

        XunitException failure = Assert.Throws<XunitException>(() =>
            GoldenAssert.AssertEqualsGolden(expected, actual, "long image"));

        string expectedHash = Convert.ToHexStringLower(SHA256.HashData(expected))[..12];
        string actualHash = Convert.ToHexStringLower(SHA256.HashData(actual))[..12];
        Assert.Equal($"Golden mismatch [long image]: expected 32 bytes, actual 32 bytes, first difference at offset 0x7, expected sha256 {expectedHash}, actual sha256 {actualHash}.", failure.Message);
        string expectedDigest = Convert.ToHexStringLower(SHA256.HashData(expected));
        string actualDigest = Convert.ToHexStringLower(SHA256.HashData(actual));
        GoldenAssert.AssertHashEqualsGolden(expectedDigest, expectedDigest, "hash");
        GoldenAssert.AssertHashEqualsGolden(expectedDigest, expectedDigest.ToUpperInvariant(), "hash", ignoreCase: true);
        GoldenAssert.AssertHashEqualsGolden((string?)null, (string?)null, "hash");
        XunitException hashFailure = Assert.Throws<XunitException>(() =>
            GoldenAssert.AssertHashEqualsGolden(expectedDigest, actualDigest, "hash"));
        Assert.Equal($"Golden hash mismatch [hash]: expected sha256 {expectedHash}, actual sha256 {actualHash}.", hashFailure.Message);
        Assert.False(GoldenDiagnostics.Contains(expectedDigest, hashFailure.Message, StringComparison.Ordinal));
        Assert.False(GoldenDiagnostics.Contains(actualDigest, hashFailure.Message, StringComparison.Ordinal));
        XunitException caseFailure = Assert.Throws<XunitException>(() =>
            GoldenAssert.AssertHashEqualsGolden(expectedDigest, expectedDigest.ToUpperInvariant(), "hash"));
        Assert.StartsWith("Golden hash mismatch [hash]:", caseFailure.Message, StringComparison.Ordinal);
        XunitException invalidHashFailure = Assert.Throws<XunitException>(() =>
            GoldenAssert.AssertHashEqualsGolden("invalid", null, "hash"));
        Assert.Equal("Golden hash mismatch [hash]: expected sha256 omitted, actual sha256 omitted.", invalidHashFailure.Message);
        GoldenAssert.AssertHashSequenceEqualsGolden([expectedDigest, actualDigest], [expectedDigest, actualDigest], "hashes");
        XunitException sequenceFailure = Assert.Throws<XunitException>(() =>
            GoldenAssert.AssertHashSequenceEqualsGolden([expectedDigest, actualDigest], [actualDigest, expectedDigest], "hashes"));
        Assert.Equal("Golden hash sequence mismatch [hashes]: values differ.", sequenceFailure.Message);
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
        FileNotFoundException missingBytes = Assert.Throws<FileNotFoundException>(() => GoldenFileIo.ReadAllBytes(expectedPath));
        Assert.True(string.Equals("Golden file was not found.", missingBytes.Message, StringComparison.Ordinal), "Missing-file diagnostic was not redacted.");
        Assert.True(missingBytes.FileName is null && missingBytes.InnerException is null, "Missing-file metadata was not redacted.");
        FileNotFoundException missingText = Assert.Throws<FileNotFoundException>(() => GoldenFileIo.ReadAllText(expectedPath));
        Assert.True(string.Equals(missingBytes.Message, missingText.Message, StringComparison.Ordinal), "Missing-file diagnostics differed.");
        Task<byte[]> missingBytesTask = GoldenFileIo.ReadAllBytesAsync(expectedPath, TestContext.Current.CancellationToken);
        FileNotFoundException missingAsyncBytes = Assert.IsType<FileNotFoundException>(missingBytesTask.Exception!.InnerException);
        Assert.True(missingAsyncBytes.FileName is null && missingAsyncBytes.InnerException is null, "Missing-file metadata was not redacted.");
        Task<string> missingTextTask = GoldenFileIo.ReadAllTextAsync(expectedPath, TestContext.Current.CancellationToken);
        FileNotFoundException missingAsyncText = Assert.IsType<FileNotFoundException>(missingTextTask.Exception!.InnerException);
        Assert.True(string.Equals(missingBytes.Message, missingAsyncText.Message, StringComparison.Ordinal), "Missing-file diagnostics differed.");
        string missingDirectory = workspace.PathFor(Path.Combine("missing-parent", "missing.bin"));
        DirectoryNotFoundException missingParent = Assert.Throws<DirectoryNotFoundException>(() => GoldenFileIo.ReadAllBytes(missingDirectory));
        Assert.True(string.Equals("Golden directory was not found.", missingParent.Message, StringComparison.Ordinal), "Missing-directory diagnostic was not redacted.");
        Assert.Null(missingParent.InnerException);
        FileNotFoundException missingManifest = Assert.Throws<FileNotFoundException>(() =>
            CanonicalGoldenTestData.LoadDirectWorkflowManifest(nameof(GoldenAssertTests), workspace.Root));
        Assert.True(string.Equals(missingBytes.Message, missingManifest.Message, StringComparison.Ordinal), "Missing-manifest diagnostic was not redacted.");
        Assert.True(missingManifest.FileName is null && missingManifest.InnerException is null, "Missing-manifest metadata was not redacted.");
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

        Assert.Equal("Golden mismatch [optional image]: expected 0 bytes, actual 0 bytes, first difference at offset 0x0, expected sha256 omitted, actual sha256 omitted.", failure.Message);
    }

    [Fact]
    public void TextSummary_PrivateOutput_ReportsOnlyLengthAndHash()
    {
        const string output = "secret-DEADBEEF/customer.bin/segment-two";

        string summary = GoldenDiagnostics.TextSummary(output);

        Assert.Equal("length 40, sha256 e945324154a5", summary);
        Assert.Equal("length 4, sha256 omitted", GoldenDiagnostics.TextSummary("abcd"));
        Assert.DoesNotContain("DEADBEEF", summary, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("customer.bin", summary, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("0")]
    [InlineData("true")]
    [InlineData(" 1 ")]
    [InlineData("%NFC_GOLDEN_VERBOSE%")]
    [InlineData("1")]
    public void Label_DefaultAndVerbose_HidesIdUnlessExplicitlyEnabled(string? setting)
    {
        string expected = setting == "1" ? "customer-private-case" : "case-ae9de91e8329";

        Assert.Equal(expected, GoldenDiagnostics.Label("customer-private-case", GoldenDiagnostics.IsVerbose(setting)));
    }

    [Fact]
    public void Label_EnvironmentSetting_UsesTheSameRuleAsTheExplicitOverload()
    {
        string setting = Environment.ExpandEnvironmentVariables("%NFC_GOLDEN_VERBOSE%");

        Assert.Equal(
            GoldenDiagnostics.Label("customer-private-case", GoldenDiagnostics.IsVerbose(setting)),
            GoldenDiagnostics.Label("customer-private-case"));
        var identity = new GoldenCaseId(nameof(GoldenAssertTests));
        Assert.Equal(GoldenDiagnostics.Label(identity.Value), identity.ToString());
        string serialized = SerializationHelper.Instance.Serialize(identity);
        GoldenCaseId restored = Assert.IsType<GoldenCaseId>(SerializationHelper.Instance.Deserialize(serialized));
        Assert.Equal(identity.Value, restored.Value);
        Assert.Equal(identity.ToString(), restored.ToString());
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
