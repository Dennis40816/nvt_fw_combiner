using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Text.Json;
using NvtFwCombiner.Domain.Composition;
using NvtFwCombiner.TestSupport;

namespace NvtFwCombiner.Bootstrap.Tests;

/// <summary>Application/Bootstrap runtime evidence for the owner-approved three-IC AB pilot.</summary>
public sealed class AbMergeRuntimeAdmissionTests
{
    private const int DpLength = 0x80000;
    private const int TpLength = 0x40000;

    /// <summary>Only the owner-approved perfect family is exposed by the runtime registry.</summary>
    [Fact]
    public void RuntimeCatalogContainsOnlyTheApprovedPilot()
    {
        Assert.Equal(
            ["NT51919", "NT51929", "NT51932"],
            WorkbenchCompositionService.GetAbMergeProfileSummaries().Select(static profile => profile.IcId));
        Assert.All(
            WorkbenchCompositionService.GetAbMergeProfileSummaries(),
            static profile => Assert.True(profile.CompileSucceeded, string.Join(',', profile.IssueCodes)));
        Assert.True(WorkbenchCompositionService.IsAbMergeSupported("51929"));
        Assert.False(WorkbenchCompositionService.IsAbMergeSupported("NT51950"));
        Assert.False(WorkbenchCompositionService.IsAbMergeSupported("NT51951"));
    }

    /// <summary>Each selected source that ends one byte early blocks before execution with its profile issue code.</summary>
    [Theory]
    [InlineData(CompositionAddressSpaceIds.DpAbInput, DpLength - 1, "AB_DP_INPUT_TOO_SHORT")]
    [InlineData(CompositionAddressSpaceIds.TpAInput, TpLength - 1, "AB_TPA_INPUT_TOO_SHORT")]
    [InlineData(CompositionAddressSpaceIds.TpBInput, TpLength - 1, "AB_TPB_INPUT_TOO_SHORT")]
    public async Task OneByteShortInputBlocksWithoutOutputAsync(
        string shortAddressSpaceId,
        int shortLength,
        string expectedIssueCode)
    {
        using var workspace = TempWorkspace.Create("nfc-ab-short");
        Dictionary<string, string> paths = WriteInputs(workspace);
        byte[] shortBytes = CreatePattern(shortLength, 0xA1);
        paths[shortAddressSpaceId] = workspace.Write($"short/{shortAddressSpaceId}.bin", shortBytes);
        string outputPath = workspace.PathFor($"output/{shortAddressSpaceId}.bin");

        WorkbenchRunResult result = await WorkbenchCompositionService.RunAbMergeAsync(
            "NT51929",
            paths,
            build: true,
            TestContext.Current.CancellationToken,
            outputPath);

        Assert.False(result.Succeeded);
        Assert.Equal(0, result.OutputSize);
        Assert.False(File.Exists(outputPath));
        using var report = JsonDocument.Parse(result.ReportJson);
        JsonElement issue = Assert.Single(
            report.RootElement.GetProperty("Issues").EnumerateArray(),
            issue => issue.GetProperty("Code").GetString() == expectedIssueCode);
        Assert.Equal(shortAddressSpaceId, issue.GetProperty("OperationId").GetString());
        Assert.Equal("error", issue.GetProperty("Severity").GetString());
        Assert.Equal(shortBytes, await File.ReadAllBytesAsync(
            paths[shortAddressSpaceId],
            TestContext.Current.CancellationToken));
    }

    /// <summary>Each accepted oversized source warns, preserves full identity, and produces exact-prefix output.</summary>
    [Theory]
    [InlineData(CompositionAddressSpaceIds.DpAbInput, "AB_DP_INPUT_OUTER_LENGTH_UNEXPECTED")]
    [InlineData(CompositionAddressSpaceIds.TpAInput, "AB_TPA_INPUT_OUTER_LENGTH_UNEXPECTED")]
    [InlineData(CompositionAddressSpaceIds.TpBInput, "AB_TPB_INPUT_OUTER_LENGTH_UNEXPECTED")]
    public async Task OversizedInputWarnsWithoutChangingOutputAsync(
        string oversizedAddressSpaceId,
        string expectedIssueCode)
    {
        using var workspace = TempWorkspace.Create("nfc-ab-oversize");
        Dictionary<string, string> paths = WriteInputs(workspace);
        WorkbenchRunResult exact = await WorkbenchCompositionService.RunAbMergeAsync(
            "NT51929",
            paths,
            build: false,
            TestContext.Current.CancellationToken);
        Assert.True(exact.Succeeded, exact.ReportJson);

        byte[] original = await File.ReadAllBytesAsync(
            paths[oversizedAddressSpaceId],
            TestContext.Current.CancellationToken);
        byte[] oversized = [.. original, 0xD7];
        paths[oversizedAddressSpaceId] = workspace.Write($"oversized/{oversizedAddressSpaceId}.bin", oversized);
        WorkbenchRunResult tailed = await WorkbenchCompositionService.RunAbMergeAsync(
            "NT51929",
            paths,
            build: false,
            TestContext.Current.CancellationToken);

        Assert.True(tailed.Succeeded, tailed.ReportJson);
        Assert.Equal(exact.OutputSha256, tailed.OutputSha256);
        using var report = JsonDocument.Parse(tailed.ReportJson);
        JsonElement issue = Assert.Single(
            report.RootElement.GetProperty("Issues").EnumerateArray(),
            issue => issue.GetProperty("Code").GetString() == expectedIssueCode);
        Assert.Equal("warning", issue.GetProperty("Severity").GetString());
        JsonElement input = Assert.Single(
            report.RootElement.GetProperty("Inputs").EnumerateArray(),
            input => input.GetProperty("AddressSpaceId").GetString() == oversizedAddressSpaceId);
        Assert.Equal(oversized.LongLength, input.GetProperty("Size").GetInt64());
        Assert.Equal(Sha256(oversized), input.GetProperty("Sha256").GetString());
        JsonElement snapshot = input.GetProperty("ExecutionSnapshot");
        Assert.Equal(original.LongLength, snapshot.GetProperty("AcceptedSize").GetInt64());
        Assert.Equal(Sha256(original), snapshot.GetProperty("AcceptedSha256").GetString());
        Assert.Equal(0, snapshot.GetProperty("AcceptedRange").GetProperty("Start").GetInt64());
        Assert.Equal(original.LongLength, snapshot.GetProperty("AcceptedRange").GetProperty("EndExclusive").GetInt64());
        Assert.Equal(1, snapshot.GetProperty("IgnoredTrailingBytes").GetInt64());
        Assert.Equal(original.LongLength, snapshot.GetProperty("IgnoredTrailingRange").GetProperty("Start").GetInt64());
        Assert.Equal(oversized.LongLength, snapshot.GetProperty("IgnoredTrailingRange").GetProperty("EndExclusive").GetInt64());
        Assert.Equal(oversized, await File.ReadAllBytesAsync(
            paths[oversizedAddressSpaceId],
            TestContext.Current.CancellationToken));
    }

    /// <summary>Build commits the exact supported output while preserving every selected source.</summary>
    [Fact]
    public async Task ExactBuildCommitsOutputAndPreservesSourcesAsync()
    {
        using var workspace = TempWorkspace.Create("nfc-ab-build");
        Dictionary<string, string> paths = WriteInputs(workspace);
        Dictionary<string, byte[]> originals = paths.ToDictionary(
            static pair => pair.Key,
            static pair => File.ReadAllBytes(pair.Value),
            StringComparer.Ordinal);
        string outputPath = workspace.PathFor("output/nt51929-ab.bin");

        WorkbenchRunResult result = await WorkbenchCompositionService.RunAbMergeAsync(
            "NT51929",
            paths,
            build: true,
            TestContext.Current.CancellationToken,
            outputPath);

        Assert.True(result.Succeeded, result.ReportJson);
        Assert.Equal(DpLength, result.OutputSize);
        byte[] output = await File.ReadAllBytesAsync(outputPath, TestContext.Current.CancellationToken);
        Assert.Equal(result.OutputSha256, Sha256(output));
        foreach ((string addressSpaceId, string path) in paths)
        {
            Assert.Equal(originals[addressSpaceId], await File.ReadAllBytesAsync(
                path,
                TestContext.Current.CancellationToken));
        }
    }

    private static Dictionary<string, string> WriteInputs(TempWorkspace workspace)
    {
        byte[] tpB = CreatePattern(TpLength, 0x83);
        BinaryPrimitives.WriteUInt32LittleEndian(tpB.AsSpan(0x7164, sizeof(uint)), 0x00123456);
        BinaryPrimitives.WriteUInt32LittleEndian(tpB.AsSpan(0x7168, sizeof(uint)), 0x00ABCDEF);
        BinaryPrimitives.WriteUInt32LittleEndian(tpB.AsSpan(0x716C, sizeof(uint)), 0x0000C0DE);
        return new Dictionary<string, string>(StringComparer.Ordinal)
        {
            [CompositionAddressSpaceIds.DpAbInput] = workspace.Write("inputs/dp-ab.bin", CreatePattern(DpLength, 0x31)),
            [CompositionAddressSpaceIds.TpAInput] = workspace.Write("inputs/tp-a.bin", CreatePattern(TpLength, 0x57)),
            [CompositionAddressSpaceIds.TpBInput] = workspace.Write("inputs/tp-b.bin", tpB),
        };
    }

    private static byte[] CreatePattern(int length, byte salt)
    {
        byte[] bytes = new byte[length];
        for (int index = 0; index < length; index++)
        {
            bytes[index] = unchecked((byte)(salt + (index * 37)));
        }

        return bytes;
    }

    private static string Sha256(ReadOnlySpan<byte> bytes)
    {
        return Convert.ToHexStringLower(SHA256.HashData(bytes));
    }
}
