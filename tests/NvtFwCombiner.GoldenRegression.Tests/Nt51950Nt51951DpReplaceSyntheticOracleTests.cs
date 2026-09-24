using System.Security.Cryptography;
using System.Text.Json;
using NvtFwCombiner.TestSupport;

namespace NvtFwCombiner.GoldenRegression.Tests;

/// <summary>Archives superseded short-input hashes and their independently declared byte ranges.</summary>
public sealed class Nt51950Nt51951DpReplaceSyntheticOracleTests
{
    private const int TpStart = 0x0A000;
    private const int TpLength = 0x2D000;
    private const int CustomerInfoStart = 0x37000;
    private const int CustomerInfoLength = 0x1000;

    /// <summary>Preserves every historical hash and the reference/replacement byte provenance.</summary>
    [Fact]
    public void HistoricalHashesAndDeclaredRangesRemainImmutable()
    {
        using var document = JsonDocument.Parse(File.ReadAllText(RepositoryPaths.FromRepositoryRoot(
            "testdata",
            "public-synthetic",
            "dp-replace",
            "nt51950-nt51951-dp-replace-oracle-v1.json")));
        JsonElement root = document.RootElement;
        Assert.Equal("public-synthetic", root.GetProperty("classification").GetString());
        Assert.Equal("replacement-dp", root.GetProperty("expectedRule").GetProperty("customerInfoSource").GetString());
        Assert.Equal(
            "dp-replace-owner-approved-legacy-comparison-v1",
            root.GetProperty("ownerApprovedLegacyComparison").GetProperty("evidenceId").GetString());
        Assert.Empty(root.GetProperty("ownerApprovedLegacyComparison").GetProperty("knownDeviations").EnumerateArray());
        Assert.Equal(
            "dp-replace-exact-pair-owner-decision-20260802",
            root.GetProperty("productionAdmissionSupersession").GetProperty("evidenceId").GetString());
        int defaultReplacementLength = root.GetProperty("generator").GetProperty("replacementLengthBytes").GetInt32();

        foreach (JsonElement testCase in root.GetProperty("cases").EnumerateArray())
        {
            int capacity = testCase.GetProperty("capacityBytes").GetInt32();
            int replacementLength = testCase.TryGetProperty("replacementLengthBytes", out JsonElement replacementLengthElement)
                ? replacementLengthElement.GetInt32()
                : defaultReplacementLength;
            byte[] baseBytes = CreatePattern(capacity, testCase.GetProperty("baseSalt").GetByte());
            byte[] replacementBytes = CreatePattern(replacementLength, testCase.GetProperty("replacementSalt").GetByte());
            byte[] expectedBytes = CreateExpectedBytes(capacity, baseBytes, replacementBytes);
            string expectedHash = testCase.GetProperty("expectedSha256").GetString()!;

            Assert.Equal(expectedHash, Sha256Hex(expectedBytes));
            Assert.Equal(replacementBytes[0x09FFF], expectedBytes[0x09FFF]);
            Assert.Equal(baseBytes[TpStart], expectedBytes[TpStart]);
            Assert.Equal(baseBytes[TpStart + TpLength - 1], expectedBytes[TpStart + TpLength - 1]);
            Assert.Equal(ReplacementOrPadding(replacementBytes, CustomerInfoStart), expectedBytes[CustomerInfoStart]);
            Assert.Equal(ReplacementOrPadding(replacementBytes, CustomerInfoStart + CustomerInfoLength - 1), expectedBytes[CustomerInfoStart + CustomerInfoLength - 1]);
            Assert.Equal(0, expectedBytes[replacementLength]);


        }
    }

    private static byte[] CreateExpectedBytes(int capacity, byte[] baseBytes, byte[] replacementBytes)
    {
        Assert.Equal(capacity, baseBytes.Length);
        Assert.InRange(replacementBytes.Length, 1, capacity);

        byte[] expected = new byte[capacity];
        replacementBytes.CopyTo(expected, 0);
        baseBytes.AsSpan(TpStart, TpLength).CopyTo(expected.AsSpan(TpStart, TpLength));
        return expected;
    }

    private static byte[] CreatePattern(int length, byte salt)
    {
        byte[] bytes = new byte[length];
        for (int index = 0; index < bytes.Length; index++)
        {
            bytes[index] = unchecked((byte)(salt + (index * 37)));
        }

        return bytes;
    }

    private static byte ReplacementOrPadding(byte[] replacementBytes, int offset)
    {
        return offset < replacementBytes.Length ? replacementBytes[offset] : (byte)0;
    }

    private static string Sha256Hex(ReadOnlySpan<byte> bytes)
    {
        return Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();
    }
}
