using NvtFwCombiner.Domain.Composition;
using NvtFwCombiner.TestSupport;

namespace NvtFwCombiner.Bootstrap.Tests;

public sealed partial class AbMergeRuntimeAdmissionTests
{
    /// <summary>Selector-free NT51951 records valid TP counts but never treats them as a topology gate.</summary>
    [Fact]
    public async Task Nt51951DoesNotGateTheDeclaredMapOnTpCountClassificationAsync()
    {
        using var workspace = TempWorkspace.Create("nfc-nt51951-ab-observed-topology");
        var paths = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            [CompositionAddressSpaceIds.DpAbInput] = workspace.Write("inputs/dp-ab.bin", new byte[0x100000]),
            [CompositionAddressSpaceIds.TpAInput] = workspace.Write(
                "inputs/tp-a.bin",
                CreateTpImage(0x81, 0x00, chipCount: 1, length: 0x37000)),
            [CompositionAddressSpaceIds.TpBInput] = workspace.Write(
                "inputs/tp-b.bin",
                CreateTpImage(0x82, 0x01, chipCount: 2, length: 0x37000)),
        };

        WorkbenchRunResult result = await AbMergeWorkbenchCompositionService.RunAbMergeAsync(
            "NT51951",
            paths,
            build: false,
            TestContext.Current.CancellationToken);

        Assert.True(result.Succeeded, result.ReportJson);
        Assert.DoesNotContain("AB_TP_TOPOLOGY", result.ReportJson, StringComparison.Ordinal);
    }

    /// <summary>Selector-free NT51951 retains non-blocking unknown metadata rather than requiring FWConfig Backup topology facts.</summary>
    [Fact]
    public async Task Nt51951DoesNotBlockWhenTpFirmwareConfigBackupIsUnreadableAsync()
    {
        using var workspace = TempWorkspace.Create("nfc-nt51951-ab-unreadable-topology");
        var paths = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            [CompositionAddressSpaceIds.DpAbInput] = workspace.Write("inputs/dp-ab.bin", new byte[0x100000]),
            [CompositionAddressSpaceIds.TpAInput] = workspace.Write("inputs/tp-a.bin", new byte[0x37000]),
            [CompositionAddressSpaceIds.TpBInput] = workspace.Write("inputs/tp-b.bin", new byte[0x37000]),
        };

        WorkbenchRunResult result = await AbMergeWorkbenchCompositionService.RunAbMergeAsync(
            "NT51951",
            paths,
            build: false,
            TestContext.Current.CancellationToken);

        Assert.True(result.Succeeded, result.ReportJson);
        Assert.DoesNotContain("AB_TP_FIRMWARE_CONFIG_BACKUP_INVALID", result.ReportJson, StringComparison.Ordinal);
        Assert.Contains("output-naming.metadata-unknown", result.ReportJson, StringComparison.Ordinal);
    }
}
