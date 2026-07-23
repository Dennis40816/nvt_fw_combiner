using NvtFwCombiner.Application.FlashMaps;
using NvtFwCombiner.Domain.Composition;
using NvtFwCombiner.TestSupport;

namespace NvtFwCombiner.Bootstrap.Tests;

/// <summary>Regression evidence for automatic AB naming across platform-specific input roots.</summary>
public sealed class AbMergeInputRootTests
{
    /// <summary>Automatic naming reads every selected root when case-distinct roots exist.</summary>
    [Fact]
    public async Task AutomaticOutputNameAcceptsCaseDistinctInputRootsAsync()
    {
        using var workspace = TempWorkspace.Create("nfc-ab-case-distinct-roots");
        var slots = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            [CompositionAddressSpaceIds.DpAbInput] = workspace.Write("Foo/dp-ab.bin", new byte[0x80000]),
            [CompositionAddressSpaceIds.TpAInput] = workspace.Write("foo/tp-a.bin", CreateTpImage(0x81, 0x00)),
            [CompositionAddressSpaceIds.TpBInput] = workspace.Write("Foo/tp-b.bin", CreateTpImage(0x82, 0x03)),
        };

        string fileName = await AbMergeWorkbenchCompositionService.ResolveAutomaticOutputFileNameAsync(
            "NT51929",
            slots,
            TestContext.Current.CancellationToken);

        Assert.StartsWith("NT51929_FlashCode_A_", fileName, StringComparison.Ordinal);
        Assert.EndsWith(".bin", fileName, StringComparison.Ordinal);
    }

    private static byte[] CreateTpImage(byte version, byte subVersion)
    {
        const int backupStart = 0x1000;
        const int markerStart = backupStart + 0xFFC;
        byte[] image = new byte[0x40000];
        image[backupStart + FirmwareConfigLayout.FirmwareVersionOffset] = version;
        image[backupStart + FirmwareConfigLayout.FirmwareVersionBarOffset] = unchecked((byte)~version);
        image[backupStart + FirmwareConfigLayout.FirmwareSubVersionOffset] = subVersion;
        image[backupStart + FirmwareConfigLayout.ChipNumberOffset] = 1;
        image[markerStart] = 0x00;
        image[markerStart + 1] = (byte)'N';
        image[markerStart + 2] = (byte)'V';
        image[markerStart + 3] = (byte)'T';
        return image;
    }
}
