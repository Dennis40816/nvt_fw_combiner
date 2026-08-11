using NvtFwCombiner.TestSupport;

namespace NvtFwCombiner.Bootstrap.Tests;

public sealed partial class FirmwareInspectionSnapshotTests
{
    /// <summary>The Infrastructure inspection port owns stable file identity and later lease checks.</summary>
    [Fact]
    public void InspectionPortReturnsStableIdentityAndDetectsLaterReplacement()
    {
        using var workspace = TempWorkspace.Create("nfc-firmware-inspection-identity");
        string path = workspace.Write("firmware.bin", new byte[0x40000]);
        IFirmwareInspection inspection = BootstrapTestHost.Services.FirmwareInspectionExperience;

        FirmwareInspectionBatchResult batch = inspection.InspectFirmwareBatch(
            "NT51926",
            [new FirmwareInspectionSnapshotInput("base", path)]);
        FirmwareConfigMetadataReadResult metadata =
            inspection.ReadFirmwareConfigMetadata("NT51926", path);

        Assert.True(batch.IsFileIdentityStable);
        Assert.True(metadata.IsFileIdentityStable);
        Assert.True(batch.FileIdentities[path].Exists);
        Assert.Equal(0x40000, batch.FileIdentities[path].Length);
        Assert.True(inspection.IsFirmwareFileIdentityCurrent(path, metadata.FileIdentity));

        File.WriteAllBytes(path, new byte[0x40001]);

        Assert.False(inspection.IsFirmwareFileIdentityCurrent(path, metadata.FileIdentity));
    }
}
