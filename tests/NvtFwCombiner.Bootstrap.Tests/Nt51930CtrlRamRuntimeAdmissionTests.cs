using System.Text.Json;
using NvtFwCombiner.Application.FlashMaps;
using NvtFwCombiner.TestSupport;

namespace NvtFwCombiner.Bootstrap.Tests;

public sealed partial class Nt51930CtrlRamFw130EvidenceTests
{
    /// <summary>A numeric request remains the topology authority when FWConfig does not report a chip count.</summary>
    [Fact]
    public async Task NumericSelectionKeepsRequestedTopologyWhenFirmwareCountIsUnknownAsync()
    {
        OwnerCase evidence = ReadOwnerCase();
        using var workspace = TempWorkspace.Create("nfc-nt51930-numeric-topology");
        byte[] reference = [.. evidence.Expected.Bytes];
        Assert.True(FirmwareConfigMetadataReader.TryReadBackup(reference, out FirmwareConfigMetadata metadata));
        int backupStart = checked((int)metadata.StructureStart);
        reference[backupStart + FirmwareConfigLayout.ChipNumberOffset] = 0;
        string unknownPath = workspace.Write("unknown-count.bin", reference);

        WorkbenchRunResult twoChip = await WorkbenchCompositionService.RunCtrlRamReplaceWithProcessorAsync(
            "NT51930",
            "2",
            CreateSlotPaths(evidence, unknownPath),
            build: true,
            workspace.PathFor("two-chip-output.bin"),
            firmwareVersionEdit: null,
            new PassThroughProcessor(),
            TestContext.Current.CancellationToken);
        WorkbenchRunResult thirteenChip = await WorkbenchCompositionService.RunCtrlRamReplaceWithProcessorAsync(
            "NT51930",
            "13",
            CreateSlotPaths(evidence, unknownPath),
            build: true,
            workspace.PathFor("thirteen-chip-output.bin"),
            firmwareVersionEdit: null,
            new PassThroughProcessor(),
            TestContext.Current.CancellationToken);

        Assert.True(twoChip.Succeeded, twoChip.ReportJson);
        Assert.True(thirteenChip.Succeeded, thirteenChip.ReportJson);
        using var twoChipReport = JsonDocument.Parse(twoChip.ReportJson);
        using var thirteenChipReport = JsonDocument.Parse(thirteenChip.ReportJson);
        Assert.NotEqual(
            twoChipReport.RootElement.GetProperty("CompilationFingerprint").GetString(),
            thirteenChipReport.RootElement.GetProperty("CompilationFingerprint").GetString());
    }

    /// <summary>A readable Common FW version below 1.0.0 is invalid even when the IC has one runtime profile.</summary>
    [Fact]
    public async Task ReadableCommonFwBelowMinimumRejectsSoleRuntimeProfileAsync()
    {
        OwnerCase evidence = ReadOwnerCase();
        using var workspace = TempWorkspace.Create("nfc-nt51930-below-minimum-common-fw");
        byte[] reference = [.. evidence.Expected.Bytes];
        Assert.True(FirmwareConfigMetadataReader.TryReadBackup(reference, out FirmwareConfigMetadata metadata));
        int backupStart = checked((int)metadata.StructureStart);
        reference[backupStart + FirmwareConfigLayout.CommonFwMajorVersionOffset] = 0;
        reference[backupStart + FirmwareConfigLayout.CommonFwMinorVersionOffset] = 255;
        reference[backupStart + FirmwareConfigLayout.CommonFwAdditionalVersionOffset] = 255;
        string referencePath = workspace.Write("below-minimum.bin", reference);
        string referenceSha256 = Hash(reference);
        string outputPath = workspace.PathFor("must-not-exist.bin");

        WorkbenchRunResult result = await WorkbenchCompositionService.RunCtrlRamReplaceWithProcessorAsync(
            "NT51930",
            WorkbenchIcNumberTokens.CascadeTwoToThirteen,
            CreateSlotPaths(evidence, referencePath),
            build: true,
            outputPath,
            firmwareVersionEdit: null,
            new PassThroughProcessor(),
            TestContext.Current.CancellationToken);

        Assert.False(result.Succeeded, result.ReportJson);
        Assert.False(File.Exists(outputPath));
        Assert.Equal(referenceSha256, Hash(File.ReadAllBytes(referencePath)));
        using var report = JsonDocument.Parse(result.ReportJson);
        JsonElement issue = Assert.Single(
            report.RootElement.GetProperty("Issues").EnumerateArray(),
            candidate => candidate.GetProperty("Code").GetString() ==
                WorkbenchIssueCodes.ReplaceCtrlRamPostbuildCategoryUnsupported);
        Assert.Contains(
            "below the minimum supported version 1.0.0",
            issue.GetProperty("Message").GetString(),
            StringComparison.Ordinal);
    }
}
