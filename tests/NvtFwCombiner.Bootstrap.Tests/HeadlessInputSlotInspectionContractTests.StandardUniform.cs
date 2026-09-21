using NvtFwCombiner.Application.Authoring;
using NvtFwCombiner.Domain.Composition;

namespace NvtFwCombiner.Bootstrap.Tests;

public sealed partial class HeadlessInputSlotInspectionContractTests
{
    /// <summary>Real compiled DP and TP ranges accept uniform snapshots with warning-only health.</summary>
    [Theory]
    [InlineData(0x00)]
    [InlineData(0xFF)]
    [InlineData(0xA5)]
    public void StandardUniformSnapshotsRetainWarningAndBytes(int fill)
    {
        ReloadCatalog();
        byte[] dp = new byte[0x6000];
        byte[] tp = new byte[0x40000];
        Array.Fill(dp, (byte)fill);
        Array.Fill(tp, (byte)fill);
        WritePositiveTpBackup(tp);
        IReadOnlyList<FirmwareInspectionSnapshotResult> results = StandardBatch("NT51929", dp, tp);
        AssertWarning(results, "dp", "DP_UNIFORM_CONTENT_WARNING", dp);
        AssertWarning(results, "tp", "TP_UNIFORM_CONTENT_WARNING", tp);
    }

    /// <summary>Only the profile-declared TP overlay must be uniform, not the entire BIN.</summary>
    [Theory]
    [InlineData("NT51950")]
    [InlineData("NT51951")]
    public void StandardUniformOverlayIsNotAWholeFileClaim(string ic)
    {
        ReloadCatalog();
        byte[] dp = new byte[0x40000];
        dp[1] = 1;
        byte[] tp = new byte[0x37000];
        tp[1] = 1;
        tp.AsSpan(0xA000, 0x2D000).Fill(0xA5);
        WritePositiveTpBackup(tp);
        IReadOnlyList<FirmwareInspectionSnapshotResult> results = StandardBatch(ic, dp, tp);
        AssertWarning(results, "tp", "TP_UNIFORM_CONTENT_WARNING", tp);
        Assert.Equal(AuthoringSlotLifecycle.Verified, results.Single(static result => result.InspectionId == "dp").Inspection.InputSlotStatus!.InspectionLifecycle);

        // Change only one byte in the inspected overlay: the same profile now verifies TP.
        tp[0xA001] = 0x5A;
        results = StandardBatch(ic, dp, tp);
        Assert.Equal(AuthoringSlotLifecycle.Verified, results.Single(static result => result.InspectionId == "tp").Inspection.InputSlotStatus!.InspectionLifecycle);
    }

    /// <summary>NT51928's existing optional LDC warning uses the same non-blocking contract.</summary>
    [Fact]
    public void StandardUniformLdcKeepsItsOwnDiagnostic()
    {
        ReloadCatalog();
        byte[] dp = new byte[0x40000];
        byte[] tp = new byte[0x35000];
        byte[] ldc = new byte[0x80000];
        Array.Fill(ldc, (byte)0xFF);
        IReadOnlyList<FirmwareInspectionSnapshotResult> results = BuiltInFirmwareInspection.InspectFirmwareBatch(
            _host.Canonical, "NT51928",
            [new("dp", "dp.bin", StandardMergeAddressSpaceId: CompositionAddressSpaceIds.DpInput),
             new("tp", "tp.bin", StandardMergeAddressSpaceId: CompositionAddressSpaceIds.TpInput),
             new("ldc", "ldc.bin", StandardMergeAddressSpaceId: CompositionAddressSpaceIds.LdcInput)],
            path => path == "dp.bin" ? dp : path == "tp.bin" ? tp : ldc);
        AssertWarning(results, "ldc", "LDC_UNIFORM_CONTENT_WARNING", ldc);
    }

    private IReadOnlyList<FirmwareInspectionSnapshotResult> StandardBatch(string ic, byte[] dp, byte[] tp)
    {
        return BuiltInFirmwareInspection.InspectFirmwareBatch(_host.Canonical, ic,
            [new("dp", "dp.bin", StandardMergeAddressSpaceId: CompositionAddressSpaceIds.DpInput),
             new("tp", "tp.bin", StandardMergeAddressSpaceId: CompositionAddressSpaceIds.TpInput)],
            path => path == "dp.bin" ? dp : tp);
    }

    private static void WritePositiveTpBackup(byte[] tp)
    {
        tp[0x1000] = 0x81;
        tp[0x1001] = 0x7E;
        tp[0x1017] = 1;
        "\0NVT"u8.CopyTo(tp.AsSpan(0x1FFC));
    }

    private static void AssertWarning(IReadOnlyList<FirmwareInspectionSnapshotResult> results, string id, string code, byte[] source)
    {
        AuthoringInputSlotStatus status = Assert.IsType<AuthoringInputSlotStatus>(results.Single(result => result.InspectionId == id).Inspection.InputSlotStatus);
        Assert.Equal(AuthoringSlotLifecycle.Warning, status.InspectionLifecycle);
        Assert.Equal(code, status.InspectionIssueCode);
        Assert.False(status.BlocksBuild);
        Assert.Equal(FileStamp.FromBytes(source), status.FileStamp);
        Assert.Equal(source, status.AcceptedBytes!.Value.ToArray());
        InputDiagnosticEvidence evidence = Assert.IsType<InputDiagnosticEvidence>(status.Inspection!.DiagnosticEvidence);
        Assert.Equal(status.Inspection.AddressSpaceId, evidence.AddressSpaceId);
        ByteRange sourceRange = Assert.IsType<ByteRange>(evidence.SourceRange);
        Assert.Equal(source[checked((int)sourceRange.Start)], evidence.RepeatedByte);
    }
}
