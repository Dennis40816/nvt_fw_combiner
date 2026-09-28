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

    /// <summary>
    /// Only the profile-declared TP overlay is judged, not the entire BIN. NT51950/NT51951 declare the NVT end flag
    /// inside that overlay (NVT-END-FLAG-1113-01): a uniform overlay cannot carry it, so the TP is blocked by its
    /// unreadable IC Count although a Backup outside the overlay is readable; the Backup ending at the declared end
    /// flag is the only one read, and it makes the same overlay non-uniform, so the TP verifies.
    /// </summary>
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
        AuthoringInputSlotStatus blocked = Assert.IsType<AuthoringInputSlotStatus>(
            results.Single(static result => result.InspectionId == "tp").Inspection.InputSlotStatus);
        Assert.Equal(AuthoringSlotLifecycle.Error, blocked.InspectionLifecycle);
        Assert.Equal("firmware-config.chip-count-unreadable", blocked.InspectionIssueCode);
        Assert.True(blocked.BlocksBuild);
        Assert.Equal(AuthoringSlotLifecycle.Verified, results.Single(static result => result.InspectionId == "dp").Inspection.InputSlotStatus!.InspectionLifecycle);

        WritePositiveTpBackup(tp, backupStart: 0x36000);
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

    private static void WritePositiveTpBackup(byte[] tp, int backupStart = 0x1000)
    {
        tp[backupStart] = 0x81;
        tp[backupStart + 1] = 0x7E;
        tp[backupStart + 0x17] = 1;
        "\0NVT"u8.CopyTo(tp.AsSpan(backupStart + 0xFFC));
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
