using System.Buffers.Binary;
using System.Text.Json;
using NvtFwCombiner.Application.Authoring;
using NvtFwCombiner.Application.FlashMaps;
using NvtFwCombiner.Domain.Composition;
using NvtFwCombiner.Domain.Firmware;
using NvtFwCombiner.TestSupport;

namespace NvtFwCombiner.Bootstrap.Tests;

/// <summary>
/// NVT-END-FLAG-1113-01 (owner decisions 30/37): AB TP A and TP B inputs are read only at the NVT end flag the AB layout
/// declares in TP-input coordinates [0x36FFC, 0x37000). A marker elsewhere is neither counted nor rejected; an end flag
/// without the marker is rejected for the existing IC Count reason of that slot.
/// </summary>
public sealed partial class AbMergeGoldenRegressionTests
{
    private const int TpEndFlagStart = 0x36FFC;
    private const int TpBackupStart = 0x36000;

    // TP A/B bytes below the copied TP code (0xA000): never part of the output.
    private const int UncopiedTpMarker = 0x1FFC;

    /// <summary>
    /// Off-end-flag markers in TP A and TP B keep the owner-certified NT51950 AB output byte-for-byte, with the same T
    /// tokens and only the end-flag marker counted.
    /// </summary>
    [Fact(
        Skip = "Requires the packaged Windows legacy Combiner processor.",
        SkipUnless = nameof(IsWindows))]
    public async Task Nt51950TpMarkerAwayFromTheEndFlagKeepsTheOwnerAbGoldenAsync()
    {
        JsonElement goldenCase = ReadGoldenCase("nt51950-ab-boe-d82t80");
        Dictionary<string, byte[]> inputs = ReadInputs(goldenCase);
        byte[] tpWithMarker = [.. inputs[CompositionAddressSpaceIds.TpAInput]];
        FirmwareNvtEndFlag.MarkerBytes.CopyTo(tpWithMarker.AsSpan(UncopiedTpMarker));
        var single = new TopologySelection(1, "1 IC", TopologySelectionSource.Requested, "test");
        using var workspace = TempWorkspace.Create("nfc-nt51950-ab-end-flag");
        CompositionHostServices host = await CreateFormatGoldenHostAsync(workspace);

        EndFlagBuild baseline = await BuildAbAsync(host, workspace, "NT51950", inputs[CompositionAddressSpaceIds.DpAbInput],
            inputs[CompositionAddressSpaceIds.TpAInput], inputs[CompositionAddressSpaceIds.TpBInput], "baseline", single);
        EndFlagBuild markers = await BuildAbAsync(host, workspace, "NT51950", inputs[CompositionAddressSpaceIds.DpAbInput],
            tpWithMarker, tpWithMarker, "markers", single);

        byte[] expected = ReadExpected(goldenCase);
        Assert.Equal(expected, baseline.Output);
        Assert.Equal(expected, markers.Output);
        Assert.Equal(ExpectedArtifact(goldenCase).GetProperty("sha256").GetString(), Hash(markers.Output));
        Assert.Equal(AbNamingTokens(baseline.Result), AbNamingTokens(markers.Result));
        AssertOnlyTheEndFlagCounts(markers.Composition, tpWithMarker, inputs[CompositionAddressSpaceIds.TpAInput]);
    }

    /// <summary>
    /// Selector-free NT51951: an off-end-flag marker in the shared TP keeps the complete output equal to the pinned
    /// Python reference for the unmodified inputs, with the same T tokens and only the end-flag marker counted.
    /// </summary>
    [Fact(
        Skip = "Requires the packaged Windows legacy Combiner processor.",
        SkipUnless = nameof(IsWindows))]
    public async Task Nt51951TpMarkerAwayFromTheEndFlagKeepsTheReferenceOutputAsync()
    {
        byte[] dp = CreatePattern(0x100000, 37, 11);
        byte[] tp = CreateNt51951ValidPrimaryTp();
        byte[] tpWithMarker = [.. tp];
        FirmwareNvtEndFlag.MarkerBytes.CopyTo(tpWithMarker.AsSpan(UncopiedTpMarker));
        using var workspace = TempWorkspace.Create("nfc-nt51951-ab-end-flag");
        using var referenceWorkspace = TempWorkspace.Create("nfc-nt51951-ab-end-flag-reference");
        CompositionHostServices host = await CreateFormatGoldenHostAsync(workspace);
        byte[] expected = await RunPythonReferenceAsync(
            referenceWorkspace, "51951", dp, tp, tp, TestContext.Current.CancellationToken);

        EndFlagBuild baseline = await BuildAbAsync(host, workspace, "NT51951", dp, tp, tp, "baseline", topology: null);
        EndFlagBuild markers = await BuildAbAsync(host, workspace, "NT51951", dp, tpWithMarker, tpWithMarker, "markers",
            topology: null);

        Assert.Equal(expected, baseline.Output);
        Assert.Equal(expected, markers.Output);
        Assert.Equal(AbNamingTokens(baseline.Result), AbNamingTokens(markers.Result));
        AssertOnlyTheEndFlagCounts(markers.Composition, tpWithMarker, tp);
    }

    /// <summary>
    /// A TP A or TP B whose end flag lacks the marker blocks with the existing IC Count reason for that slot only,
    /// the same issues as without the marker, although a complete Backup ends at 0x1FFC.
    /// </summary>
    [Theory]
    [InlineData("NT51950", CompositionAddressSpaceIds.TpAInput)]
    [InlineData("NT51950", CompositionAddressSpaceIds.TpBInput)]
    [InlineData("NT51951", CompositionAddressSpaceIds.TpAInput)]
    [InlineData("NT51951", CompositionAddressSpaceIds.TpBInput)]
    public async Task AbTpWithoutTheEndFlagMarkerIsRejectedEvenWithABackupElsewhereAsync(string icId, string slot)
    {
        (byte[] dp, byte[] tp, TopologySelection? topology) = icId == "NT51950"
            ? Nt51950GoldenAbInputs()
            : (CreatePattern(0x100000, 37, 11), CreateNt51951ValidPrimaryTp(), null);
        byte[] missing = [.. tp];
        missing[TpEndFlagStart + 1] ^= 0xFF;
        byte[] moved = [.. missing];
        tp.AsSpan(TpBackupStart, 0x1000).CopyTo(moved.AsSpan(0x1000));
        string otherSlot = slot == CompositionAddressSpaceIds.TpAInput
            ? CompositionAddressSpaceIds.TpBInput
            : CompositionAddressSpaceIds.TpAInput;
        using var workspace = TempWorkspace.Create("nfc-ab-end-flag-missing");
        CompositionHostServices host = await CreateFormatGoldenHostAsync(workspace);

        CompiledAuthoringSessionPreparation movedResult = await PrepareAbAsync(host, workspace, icId, dp,
            slot == CompositionAddressSpaceIds.TpAInput ? moved : tp,
            slot == CompositionAddressSpaceIds.TpBInput ? moved : tp, "moved", topology);
        CompiledAuthoringSessionPreparation missingResult = await PrepareAbAsync(host, workspace, icId, dp,
            slot == CompositionAddressSpaceIds.TpAInput ? missing : tp,
            slot == CompositionAddressSpaceIds.TpBInput ? missing : tp, "missing", topology);

        Assert.False(movedResult.Succeeded);
        Assert.False(missingResult.Succeeded);
        Assert.Contains(movedResult.Issues, issue =>
            issue.Code == FirmwareConfigChipCountDiagnostics.UnreadableIssueCode && issue.OperationId == slot);
        Assert.DoesNotContain(movedResult.Issues, issue =>
            issue.Code == FirmwareConfigChipCountDiagnostics.UnreadableIssueCode && issue.OperationId == otherSlot);
        Assert.Equal(
            missingResult.Issues.Select(static issue => issue.Code).Order(StringComparer.Ordinal),
            movedResult.Issues.Select(static issue => issue.Code).Order(StringComparer.Ordinal));
    }

    private static void AssertOnlyTheEndFlagCounts(CompiledComposition composition, byte[] tpWithMarker, byte[] original)
    {
        FirmwareNvtEndFlagResolution endFlag = composition.V2Details.Provenance.ResolvedMap.NvtEndFlagResolution;
        Assert.Equal(new FirmwareAddressedRange("flash", new ByteRange(TpEndFlagStart, 4)),
            Assert.IsType<FirmwareNvtEndFlag>(endFlag.EndFlag).Position);
        Assert.True(FirmwareConfigMetadataReader.TryReadBackup(tpWithMarker, endFlag, out FirmwareConfigMetadata metadata,
            out int markerCount));
        Assert.Equal(1, markerCount);
        Assert.Equal(TpBackupStart, metadata.StructureStart);
        Assert.True(FirmwareConfigMetadataReader.TryReadBackup(original, endFlag, out FirmwareConfigMetadata expected, out _));
        Assert.Equal(expected, metadata);
        // The previous whole-image rule counted both markers and rejected the TP.
        Assert.False(FirmwareConfigMetadataReader.TryReadBackup(tpWithMarker, out _, out int wholeImageCount));
        Assert.Equal(2, wholeImageCount);
    }

    private static (byte[] Dp, byte[] Tp, TopologySelection Topology) Nt51950GoldenAbInputs()
    {
        Dictionary<string, byte[]> inputs = ReadInputs(ReadGoldenCase("nt51950-ab-boe-d82t80"));
        return (inputs[CompositionAddressSpaceIds.DpAbInput], inputs[CompositionAddressSpaceIds.TpAInput],
            new TopologySelection(1, "1 IC", TopologySelectionSource.Requested, "test"));
    }

    private static byte[] CreateNt51951ValidPrimaryTp()
    {
        // The runtime-admissible valid-primary pattern of Nt51951SharedTpPublicHostEnforcesPrimaryValidityAsync.
        byte[] tp = CreatePattern(0x37000, 19, 23);
        WriteHeaderPointers(tp);
        BinaryPrimitives.WriteUInt32LittleEndian(tp.AsSpan(0xA130, sizeof(uint)), 0x1F6CF3EC);
        tp[0x22201] = (byte)~tp[0x22200];
        tp[0x36001] = (byte)~tp[0x36000];
        tp[0x36017] = 1;
        FirmwareNvtEndFlag.MarkerBytes.CopyTo(tp.AsSpan(TpEndFlagStart));
        return tp;
    }

    private static async Task<EndFlagBuild> BuildAbAsync(
        CompositionHostServices host,
        TempWorkspace workspace,
        string icId,
        byte[] dp,
        byte[] tpA,
        byte[] tpB,
        string name,
        TopologySelection? topology)
    {
        Dictionary<string, string> paths = WriteAbInputs(workspace, dp, tpA, tpB, name);
        CompiledAuthoringSessionPreparation prepared = await AbMergeTestSupport.PrepareAsync(
            host, icId, paths, TestContext.Current.CancellationToken, topology);
        Assert.True(prepared.Succeeded, CompositionExecutionTestSupport.FormatIssues(prepared.Issues));
        string outputPath = workspace.PathFor($"{name}-output.bin");
        CompositionRunResult result = await AbMergeTestSupport.RunAsync(
            host, icId, paths, build: true, TestContext.Current.CancellationToken, outputPath, topology);
        Assert.True(result.Succeeded, CompositionRunReportJson.Serialize(result));
        return new EndFlagBuild(result, File.ReadAllBytes(outputPath),
            prepared.Snapshot!.ExactCapability!.CompiledComposition);
    }

    private static async Task<CompiledAuthoringSessionPreparation> PrepareAbAsync(
        CompositionHostServices host,
        TempWorkspace workspace,
        string icId,
        byte[] dp,
        byte[] tpA,
        byte[] tpB,
        string name,
        TopologySelection? topology)
    {
        return await AbMergeTestSupport.PrepareAsync(host, icId, WriteAbInputs(workspace, dp, tpA, tpB, name),
            TestContext.Current.CancellationToken, topology);
    }

    private static Dictionary<string, string> WriteAbInputs(
        TempWorkspace workspace, byte[] dp, byte[] tpA, byte[] tpB, string name)
    {
        return new Dictionary<string, string>(StringComparer.Ordinal)
        {
            [CompositionAddressSpaceIds.DpAbInput] = workspace.Write($"{name}-dp-ab.bin", dp),
            [CompositionAddressSpaceIds.TpAInput] = workspace.Write($"{name}-tp-a.bin", tpA),
            [CompositionAddressSpaceIds.TpBInput] = workspace.Write($"{name}-tp-b.bin", tpB),
        };
    }

    private static (string TokenId, string Value, bool IsKnown)[] AbNamingTokens(CompositionRunResult result)
    {
        return
        [
            .. Assert.IsType<OutputNamingSummary>(result.Report.OutputNaming).Tokens
                .Where(static token => token.TokenId != "date")
                .Select(static token => (token.TokenId, token.Value, token.IsKnown)),
        ];
    }

    private sealed record EndFlagBuild(CompositionRunResult Result, byte[] Output, CompiledComposition Composition);
}
