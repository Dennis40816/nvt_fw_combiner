using System.Text;
using NvtFwCombiner.Application.FlashMaps;
using static NvtFwCombiner.Bootstrap.Tests.BootstrapTestData;

namespace NvtFwCombiner.Bootstrap.Tests;

/// <summary>Parity and read-count evidence for the shell firmware inspection facade.</summary>
public sealed class WorkbenchFirmwareInspectionTests
{
    /// <summary>The consolidated snapshot preserves existing metadata and CtrlRAM display projections.</summary>
    [Fact]
    public void InspectionMatchesExistingFirmwareAndCtrlRamDisplayReaders()
    {
        string basePath = GoldenPath("expected/51926/flash.bin");
        var ctrlRamRequest = new WorkbenchCtrlRamInspectionRequest(WorkbenchIcNumberTokens.Cascade);

        WorkbenchFirmwareInspection inspection = WorkbenchCompositionService.InspectFirmware(
            "NT51926",
            basePath,
            basePath,
            ctrlRamRequest);

        Assert.Equal(
            WorkbenchCompositionService.TryReadFirmwareConfigMetadata("NT51926", basePath),
            inspection.FirmwareConfig);
        Assert.Equal(
            WorkbenchCompositionService.TryReadDpVersionMetadata("NT51926", basePath),
            inspection.DpVersion);
        Assert.Equal(
            WorkbenchCompositionService.TryReadCmiDpCodeMetadata("NT51926", basePath, basePath),
            inspection.CmiDpCode);
        Assert.Equal(
            WorkbenchCompositionService.TryReadFirmwareContextSuggestion("NT51926", basePath),
            inspection.ContextSuggestion);

        WorkbenchCtrlRamInspectionDisplay display = Assert.IsType<WorkbenchCtrlRamInspectionDisplay>(inspection.CtrlRamDisplay);
        Assert.Equal(ctrlRamRequest.NumberToken, display.NumberToken);
        Assert.Equal(
            WorkbenchCompositionService.GetCtrlRamRegions("NT51926", ctrlRamRequest.NumberToken, basePath),
            display.Regions);
        Assert.Equal(
            WorkbenchCompositionService.GetReplaceInputSlots(
                "NT51926",
                ctrlRamRequest.NumberToken,
                WorkbenchReplaceModes.CtrlRam,
                basePath),
            display.InputSlots);
        WorkbenchMemoryDisplay expectedMemory = WorkbenchCompositionService.GetReplaceMemoryDisplay(
            "NT51926",
            ctrlRamRequest.NumberToken,
            WorkbenchReplaceModes.CtrlRam,
            ctrlRamBasePath: basePath);
        Assert.Equal(expectedMemory.RangeLabel, display.MemoryDisplay.RangeLabel);
        Assert.Equal(expectedMemory.MemoryMapRows, display.MemoryDisplay.MemoryMapRows);
        Assert.Equal(expectedMemory.CoverageSegments, display.MemoryDisplay.CoverageSegments);
    }

    /// <summary>Each distinct path is read once; a missing primary prevents any secondary read.</summary>
    [Fact]
    public void InspectionReadsEachDistinctPathAtMostOnce()
    {
        byte[] baseBytes = File.ReadAllBytes(GoldenPath("expected/51926/flash.bin"));
        byte[] dpBytes = File.ReadAllBytes(GoldenPath("inputs/51950/dp-256k/dp.bin"));
        byte[] tpBytes = File.ReadAllBytes(GoldenPath("inputs/51950/dp-256k/tp.bin"));
        var artifacts = new Dictionary<string, byte[]>(StringComparer.Ordinal)
        {
            ["base.bin"] = baseBytes,
            ["dp.bin"] = dpBytes,
            ["tp.bin"] = tpBytes,
        };
        var reads = new List<string>();
        byte[]? Read(string path)
        {
            reads.Add(path);
            return artifacts.GetValueOrDefault(path);
        }

        WorkbenchFirmwareInspection samePath = WorkbenchCompositionService.InspectFirmware(
            "NT51926",
            "base.bin",
            "base.bin",
            new WorkbenchCtrlRamInspectionRequest(WorkbenchIcNumberTokens.Cascade),
            readFirmwareImage: Read);
        Assert.NotNull(samePath.FirmwareConfig);
        Assert.Equal(["base.bin"], reads);

        reads.Clear();
        WorkbenchFirmwareInspection distinctPaths = WorkbenchCompositionService.InspectFirmware(
            "NT51950",
            "dp.bin",
            "tp.bin",
            ctrlRamRequest: null,
            readFirmwareImage: Read);
        Assert.Null(distinctPaths.DpVersion);
        _ = Assert.NotNull(distinctPaths.CmiDpCode);
        Assert.Equal(["dp.bin", "tp.bin"], reads);

        reads.Clear();
        WorkbenchFirmwareInspection missingPrimary = WorkbenchCompositionService.InspectFirmware(
            "NT51950",
            "missing.bin",
            "tp.bin",
            ctrlRamRequest: null,
            readFirmwareImage: Read);
        Assert.Null(missingPrimary.FirmwareConfig);
        Assert.Null(missingPrimary.DpVersion);
        Assert.Null(missingPrimary.CmiDpCode);
        Assert.Null(missingPrimary.ContextSuggestion);
        Assert.Null(missingPrimary.CtrlRamDisplay);
        Assert.Equal(["missing.bin"], reads);
    }

    /// <summary>Filename hints win over a bounded 256 KiB header scan without exposing image bytes.</summary>
    [Fact]
    public void InspectionKeepsFilenameFirstBoundedIcHintSemantics()
    {
        byte[] headerBytes = new byte[(256 * 1024) + 32];
        Encoding.ASCII.GetBytes("NT51926").CopyTo(headerBytes, 16);
        Encoding.ASCII.GetBytes("NT51929").CopyTo(headerBytes, (256 * 1024) + 8);

        WorkbenchFirmwareInspection fileNameHint = WorkbenchCompositionService.InspectFirmware(
            "NT51926",
            "NT51927TT_payload.bin",
            tpPath: null,
            ctrlRamRequest: null,
            _ => headerBytes);
        Assert.Equal("NT51927", fileNameHint.DetectedIcId);

        WorkbenchFirmwareInspection unreadableFileNameHint = WorkbenchCompositionService.InspectFirmware(
            "NT51926",
            "NT51927_unreadable.bin",
            tpPath: null,
            ctrlRamRequest: null,
            _ => null);
        Assert.Equal("NT51927", unreadableFileNameHint.DetectedIcId);
        Assert.Null(unreadableFileNameHint.FirmwareConfig);

        WorkbenchFirmwareInspection headerHint = WorkbenchCompositionService.InspectFirmware(
            "NT51926",
            "payload.bin",
            tpPath: null,
            ctrlRamRequest: null,
            _ => headerBytes);
        Assert.Equal("NT51926", headerHint.DetectedIcId);

        Array.Clear(headerBytes, 16, "NT51926".Length);
        WorkbenchFirmwareInspection outOfProbeHint = WorkbenchCompositionService.InspectFirmware(
            "NT51926",
            "payload.bin",
            tpPath: null,
            ctrlRamRequest: null,
            _ => headerBytes);
        Assert.Null(outOfProbeHint.DetectedIcId);

        Assert.DoesNotContain(
            typeof(WorkbenchFirmwareInspection).GetProperties(),
            property => property.PropertyType == typeof(byte[]) ||
                property.PropertyType == typeof(ReadOnlyMemory<byte>));
    }

    /// <summary>Invalid multi-profile firmware metadata cannot select a postbuild display category.</summary>
    [Fact]
    public void InspectionRejectsInvalidVersionBarForMultiProfileDisplaySelection()
    {
        byte[] validBytes = File.ReadAllBytes(GoldenPath("expected/51926/flash.bin"));
        WorkbenchFirmwareInspection validInspection = WorkbenchCompositionService.InspectFirmware(
            "NT51926",
            "base.bin",
            "base.bin",
            new WorkbenchCtrlRamInspectionRequest(WorkbenchIcNumberTokens.Cascade),
            _ => validBytes);
        WorkbenchFirmwareConfigMetadata validMetadata = Assert.IsType<WorkbenchFirmwareConfigMetadata>(
            validInspection.FirmwareConfig);
        Assert.True(validMetadata.IsFirmwareVersionBarValid);
        Assert.NotNull(validMetadata.PostbuildCategory);

        byte[] invalidBytes = (byte[])validBytes.Clone();
        int versionBarOffset = checked(
            (int)validMetadata.FirmwareConfigBackupStart + FirmwareConfigLayout.FirmwareVersionBarOffset);
        invalidBytes[versionBarOffset] ^= 0x01;

        WorkbenchFirmwareInspection invalidInspection = WorkbenchCompositionService.InspectFirmware(
            "NT51926",
            "base.bin",
            "base.bin",
            new WorkbenchCtrlRamInspectionRequest(WorkbenchIcNumberTokens.Cascade),
            _ => invalidBytes);
        WorkbenchFirmwareConfigMetadata invalidMetadata = Assert.IsType<WorkbenchFirmwareConfigMetadata>(
            invalidInspection.FirmwareConfig);
        Assert.False(invalidMetadata.IsFirmwareVersionBarValid);
        Assert.Null(invalidMetadata.PostbuildCategory);
        Assert.Empty(Assert.IsType<WorkbenchCtrlRamInspectionDisplay>(invalidInspection.CtrlRamDisplay).InputSlots);
    }

    /// <summary>Paired DP/TP projections share one physical read for every distinct selected path.</summary>
    [Fact]
    public void InspectionBatchReadsEveryDistinctPathOnce()
    {
        byte[] dpBytes = File.ReadAllBytes(GoldenPath("inputs/51950/dp-256k/dp.bin"));
        byte[] tpBytes = File.ReadAllBytes(GoldenPath("inputs/51950/dp-256k/tp.bin"));
        var artifacts = new Dictionary<string, byte[]>(StringComparer.Ordinal)
        {
            ["dp.bin"] = dpBytes,
            ["tp.bin"] = tpBytes,
        };
        var reads = new List<string>();

        IReadOnlyList<WorkbenchFirmwareInspectionResult> results =
            WorkbenchCompositionService.InspectFirmwareBatch(
                "NT51950",
                [
                    new WorkbenchFirmwareInspectionInput("tp", "tp.bin"),
                    new WorkbenchFirmwareInspectionInput("dp", "dp.bin", "tp.bin"),
                ],
                path =>
                {
                    reads.Add(path);
                    return artifacts.GetValueOrDefault(path);
                });

        Assert.Equal(["tp.bin", "dp.bin"], reads);
        Assert.Equal(2, results.Count);
        Assert.NotNull(results.Single(result => result.InspectionId == "tp").Inspection.FirmwareConfig);
        _ = Assert.NotNull(results.Single(result => result.InspectionId == "dp").Inspection.CmiDpCode);
    }

    /// <summary>Named batch projections reject ambiguous duplicate result identities.</summary>
    [Fact]
    public void InspectionBatchRejectsDuplicateIds()
    {
        int reads = 0;
        ArgumentException exception = Assert.Throws<ArgumentException>(() =>
            WorkbenchCompositionService.InspectFirmwareBatch(
                "NT51926",
                [
                    new WorkbenchFirmwareInspectionInput("slot", "first.bin"),
                    new WorkbenchFirmwareInspectionInput("slot", "second.bin"),
                ],
                _ =>
                {
                    reads++;
                    return null;
                }));

        Assert.Equal("inputs", exception.ParamName);
        Assert.Equal(0, reads);
    }

    /// <summary>A null batch item is rejected before any firmware reader can observe the request.</summary>
    [Fact]
    public void InspectionBatchRejectsNullItemsBeforeReading()
    {
        int reads = 0;

        _ = Assert.Throws<ArgumentNullException>(() =>
            WorkbenchCompositionService.InspectFirmwareBatch(
                "NT51926",
                [
                    new WorkbenchFirmwareInspectionInput("first", "first.bin"),
                    null!,
                ],
                _ =>
                {
                    reads++;
                    return null;
                }));

        Assert.Equal(0, reads);
    }
}
