using System.Security.Cryptography;
using System.Text.Json;
using NvtFwCombiner.Bootstrap;
using NvtFwCombiner.Domain.Composition;
using NvtFwCombiner.TestSupport;

namespace NvtFwCombiner.GoldenRegression.Tests;

/// <summary>Archives superseded short-input hashes and verifies exact-pair production admission rejects them.</summary>
public sealed class Nt51950Nt51951DpReplaceSyntheticOracleTests
{
    private const int TpStart = 0x0A000;
    private const int TpLength = 0x2D000;
    private const int CustomerInfoStart = 0x37000;
    private const int CustomerInfoLength = 0x1000;

    /// <summary>Preserves every historical hash without allowing the superseded short inputs into production.</summary>
    [Fact]
    public async Task HistoricalHashesRemainImmutableAndProductionRejectsShortInputs()
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
            string icId = testCase.GetProperty("icId").GetString()!;
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

            using var workspace = TempWorkspace.Create($"nfc-dp-replace-oracle-{icId}-{capacity:X}");
            string basePath = workspace.Write("base.bin", baseBytes);
            string replacementPath = workspace.Write("replacement-dp.bin", replacementBytes);
            string outputPath = workspace.PathFor($"{icId.ToLowerInvariant()}-dp-replace.bin");
            WorkbenchRunResult result = await WorkbenchCompositionService.RunReplaceAsync(
                icId,
                "single",
                "DP",
                new Dictionary<string, string>(StringComparer.Ordinal)
                {
                    ["replace-base"] = basePath,
                    ["replace-dp"] = replacementPath,
                },
                build: true,
                TestContext.Current.CancellationToken,
                outputPath);

            Assert.False(result.Succeeded, $"{icId} 0x{capacity:X}: superseded short input was accepted");
            Assert.False(File.Exists(outputPath), outputPath);
            Assert.Contains(CompositionIssueCodes.InputAddressSpaceLengthMismatch, result.ReportJson, StringComparison.Ordinal);
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
