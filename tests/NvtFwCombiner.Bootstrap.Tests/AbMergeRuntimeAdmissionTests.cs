using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Text.Json;
using NvtFwCombiner.Application.FlashMaps;
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
        Assert.True(AbMergeWorkbenchCompositionService.IsAbMergeSupported("51929"));
        Assert.False(AbMergeWorkbenchCompositionService.IsAbMergeSupported("NT51950"));
        Assert.False(AbMergeWorkbenchCompositionService.IsAbMergeSupported("NT51951"));
    }

    /// <summary>The desktop adapter exposes exactly the three compiler-owned pilot slots.</summary>
    [Theory]
    [InlineData("NT51919")]
    [InlineData("NT51929")]
    [InlineData("NT51932")]
    public void WorkbenchInputLayoutComesFromTheCompiledProfile(string icId)
    {
        IReadOnlyList<WorkbenchAbMergeInputSlot> slots = WorkbenchCompositionService.GetAbMergeInputSlots(icId);

        Assert.Collection(
            slots,
            slot => AssertAbSlot(
                slot,
                CompositionAddressSpaceIds.DpAbInput,
                WorkbenchAbMergeInputRole.DpAb,
                DpLength),
            slot => AssertAbSlot(
                slot,
                CompositionAddressSpaceIds.TpAInput,
                WorkbenchAbMergeInputRole.TpA,
                TpLength),
            slot => AssertAbSlot(
                slot,
                CompositionAddressSpaceIds.TpBInput,
                WorkbenchAbMergeInputRole.TpB,
                TpLength));
        Assert.Empty(WorkbenchCompositionService.GetAbMergeInputSlots("NT51950"));
    }

    /// <summary>Load inspection projects independent DP1/DP2 and TPA/TPB values without routing on them.</summary>
    [Theory]
    [InlineData("NT51919")]
    [InlineData("NT51929")]
    [InlineData("NT51932")]
    public void WorkbenchLoadInspectionProjectsHealthAndFourVersionValues(string icId)
    {
        using var workspace = TempWorkspace.Create("nfc-ab-load-inspection");
        byte[] dpAb = new byte[DpLength];
        WriteCmi(dpAb, bankStart: 0, major: 0x06, minor: 0x05, jira: 0x123);
        WriteCmi(dpAb, bankStart: TpLength, major: 0x07, minor: 0x08, jira: 0x456);
        byte[] tpA = CreateTpImage(version: 0x81, subVersion: 0x00);
        byte[] tpB = CreateTpImage(version: 0x82, subVersion: 0x03);

        WorkbenchAbMergeInputInspection dp = WorkbenchCompositionService.InspectAbMergeInput(
            icId,
            CompositionAddressSpaceIds.DpAbInput,
            workspace.Write("dp-ab.bin", dpAb));
        WorkbenchAbMergeInputInspection a = WorkbenchCompositionService.InspectAbMergeInput(
            icId,
            CompositionAddressSpaceIds.TpAInput,
            workspace.Write("tp-a.bin", tpA));
        WorkbenchAbMergeInputInspection b = WorkbenchCompositionService.InspectAbMergeInput(
            icId,
            CompositionAddressSpaceIds.TpBInput,
            workspace.Write("tp-b.bin", tpB));

        Assert.Equal(WorkbenchInputInspectionSeverity.Valid, dp.PrimaryIssue.Severity);
        Assert.Equal(
            [
                new WorkbenchAbVersionValue(WorkbenchAbVersionKind.Dp1, "D0605", "AUTO_PRJ-291", false),
                new WorkbenchAbVersionValue(WorkbenchAbVersionKind.Dp2, "D0708", "AUTO_PRJ-1110", false),
            ],
            dp.Versions);
        Assert.Equal(
            new WorkbenchAbVersionValue(WorkbenchAbVersionKind.TpA, "T81-00", null, false),
            Assert.Single(a.Versions));
        Assert.Equal(
            new WorkbenchAbVersionValue(WorkbenchAbVersionKind.TpB, "T82-03", null, false),
            Assert.Single(b.Versions));
        Assert.False(dp.BlocksBuild);
        Assert.False(a.BlocksBuild);
        Assert.False(b.BlocksBuild);
    }

    /// <summary>Ignored tails warn immediately while metadata remains bounded to the accepted prefix.</summary>
    [Fact]
    public void WorkbenchLoadInspectionBoundsMetadataToAcceptedPrefix()
    {
        using var workspace = TempWorkspace.Create("nfc-ab-load-tail");
        byte[] exact = CreateTpImage(version: 0x81, subVersion: 0x02);
        byte[] oversized = [.. exact, .. CreateTpImage(version: 0x99, subVersion: 0x09)];

        WorkbenchAbMergeInputInspection inspection = WorkbenchCompositionService.InspectAbMergeInput(
            "NT51929",
            CompositionAddressSpaceIds.TpAInput,
            workspace.Write("tp-a-oversized.bin", oversized));

        Assert.Equal(WorkbenchInputInspectionSeverity.Warning, inspection.PrimaryIssue.Severity);
        Assert.Equal("AB_TPA_INPUT_OUTER_LENGTH_UNEXPECTED", inspection.PrimaryIssue.Code);
        Assert.False(inspection.BlocksBuild);
        Assert.Equal(TpLength, inspection.IgnoredTrailingBytes);
        Assert.Equal(
            new WorkbenchAbVersionValue(WorkbenchAbVersionKind.TpA, "T81-02", null, false),
            Assert.Single(inspection.Versions));
    }

    /// <summary>A short selected source immediately blocks and keeps version metadata explicitly Unknown.</summary>
    [Fact]
    public void WorkbenchLoadInspectionBlocksShortSource()
    {
        using var workspace = TempWorkspace.Create("nfc-ab-load-short");
        WorkbenchAbMergeInputInspection inspection = WorkbenchCompositionService.InspectAbMergeInput(
            "NT51929",
            CompositionAddressSpaceIds.DpAbInput,
            workspace.Write("dp-ab-short.bin", new byte[DpLength - 1]));

        Assert.True(inspection.BlocksBuild);
        Assert.Equal(WorkbenchInputInspectionSeverity.Blocking, inspection.PrimaryIssue.Severity);
        Assert.Equal("AB_DP_INPUT_TOO_SHORT", inspection.PrimaryIssue.Code);
        Assert.All(inspection.Versions, static version => Assert.True(version.IsUnknown));
        Assert.DoesNotContain(
            inspection.Issues,
            static issue => issue.Code == WorkbenchIssueCodes.AbInputVersionUnknown);
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

        WorkbenchRunResult result = await AbMergeWorkbenchCompositionService.RunAbMergeAsync(
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
        WorkbenchRunResult exact = await AbMergeWorkbenchCompositionService.RunAbMergeAsync(
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
        WorkbenchRunResult tailed = await AbMergeWorkbenchCompositionService.RunAbMergeAsync(
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
        var originals = paths.ToDictionary(
            static pair => pair.Key,
            static pair => File.ReadAllBytes(pair.Value),
            StringComparer.Ordinal);
        string outputPath = workspace.PathFor("output/nt51929-ab.bin");

        WorkbenchRunResult result = await AbMergeWorkbenchCompositionService.RunAbMergeAsync(
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

    private static void AssertAbSlot(
        WorkbenchAbMergeInputSlot slot,
        string addressSpaceId,
        WorkbenchAbMergeInputRole role,
        int requiredLength)
    {
        Assert.Equal(addressSpaceId, slot.SlotId);
        Assert.Equal(addressSpaceId, slot.AddressSpaceId);
        Assert.Equal(role, slot.Role);
        Assert.Equal(requiredLength, slot.RequiredEndExclusive);
        Assert.Equal([requiredLength], slot.ExpectedOuterLengths);
    }

    private static void WriteCmi(
        byte[] image,
        int bankStart,
        byte major,
        byte minor,
        ushort jira)
    {
        const int register16Offset = 0x401A;
        int start = checked(bankStart + register16Offset);
        image[start] = checked((byte)(jira & 0xFF));
        image[start + 1] = major;
        image[start + 2] = checked((byte)((minor << 4) | ((jira >> 8) & 0x0F)));
    }

    private static byte[] CreateTpImage(byte version, byte subVersion)
    {
        const int backupStart = 0x1000;
        const int markerStart = backupStart + 0xFFC;
        byte[] image = new byte[TpLength];
        image[backupStart + FirmwareConfigLayout.FirmwareVersionOffset] = version;
        image[backupStart + FirmwareConfigLayout.FirmwareVersionBarOffset] = unchecked((byte)~version);
        image[backupStart + FirmwareConfigLayout.FirmwareSubVersionOffset] = subVersion;
        image[markerStart] = 0x00;
        image[markerStart + 1] = (byte)'N';
        image[markerStart + 2] = (byte)'V';
        image[markerStart + 3] = (byte)'T';
        return image;
    }

    private static string Sha256(ReadOnlySpan<byte> bytes)
    {
        return Convert.ToHexStringLower(SHA256.HashData(bytes));
    }
}
