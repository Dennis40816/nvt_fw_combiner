using System.Globalization;
using System.Text.Json;
using NvtFwCombiner.Domain.Composition;
using NvtFwCombiner.TestSupport;
using static NvtFwCombiner.Bootstrap.Tests.BootstrapTestData;

namespace NvtFwCombiner.Bootstrap.Tests;

/// <summary>Regression coverage for virtual General Replace hexadecimal patch authoring.</summary>
public sealed class WorkbenchGeneralReplacePatchTests
{
    /// <summary>Retired General Replace mappings fail closed after both input forms validate.</summary>
    [Theory]
    [InlineData("A5 5A")]
    [InlineData("A5-5A")]
    [InlineData("A5,5A")]
    [InlineData("A5_5A")]
    public async Task GeneralReplaceVirtualAndFileMappingsFailClosedAndKeepBaseImmutable(string patchValue)
    {
        using var workspace = TempWorkspace.Create("nvt-fw-combiner-general-patch-equivalence");
        byte[] baseBytes = CreatePattern(0x40000, 0x20);
        byte[] originalBaseBytes = [.. baseBytes];
        string basePath = workspace.Write("base.bin", baseBytes);
        string mappingPath = workspace.Write("mapping.bin", [0xA5, 0x5A]);
        string fileMappingOutput = workspace.PathFor("file-mapping.bin");
        string patchOutput = workspace.PathFor("patch.bin");
        Dictionary<string, string> slots = CreateBaseSlots(basePath);

        WorkbenchRunResult fileMapping = await WorkbenchCompositionService.RunReplaceAsync(
            "NT51950",
            "single",
            "General",
            slots,
            [new WorkbenchGeneralReplaceMappingInput("file-map", mappingPath, "0x00100", "0x00101")],
            build: true,
            TestContext.Current.CancellationToken,
            fileMappingOutput);
        WorkbenchRunResult virtualPatch = await WorkbenchCompositionService.RunReplaceAsync(
            "NT51950",
            "single",
            "General",
            slots,
            [],
            [new WorkbenchGeneralReplacePatchInput(
                "hex-patch-1",
                "0x00100",
                "0x00101",
                WorkbenchGeneralReplacePatchKind.Overwrite,
                patchValue)],
            build: true,
            TestContext.Current.CancellationToken,
            patchOutput);

        AssertWorkflowNotSupported(fileMapping, fileMappingOutput);
        AssertWorkflowNotSupported(virtualPatch, patchOutput);
        Assert.Equal(originalBaseBytes, await File.ReadAllBytesAsync(basePath, TestContext.Current.CancellationToken));
    }

    /// <summary>Validated fill patches fail closed when no exact V2 General Replace route exists.</summary>
    [Fact]
    public async Task GeneralReplaceVirtualFillFailsClosedWithoutOutput()
    {
        using var workspace = TempWorkspace.Create("nvt-fw-combiner-general-patch-fill");
        string basePath = workspace.Write("base.bin", CreatePattern(0x40000, 0x50));
        string outputPath = workspace.PathFor("fill.bin");

        WorkbenchRunResult result = await WorkbenchCompositionService.RunReplaceAsync(
            "NT51950",
            "single",
            "General",
            CreateBaseSlots(basePath),
            [],
            [new WorkbenchGeneralReplacePatchInput(
                "hex-fill-1",
                "0x00110",
                "0x00112",
                WorkbenchGeneralReplacePatchKind.Fill,
                "FF")],
            build: true,
            TestContext.Current.CancellationToken,
            outputPath);

        AssertWorkflowNotSupported(result, outputPath);
    }

    /// <summary>Malformed bytes and protected ranges are rejected before any output is committed.</summary>
    [Fact]
    public async Task GeneralReplaceVirtualPatchRejectsMalformedAndProtectedInput()
    {
        using var workspace = TempWorkspace.Create("nvt-fw-combiner-general-patch-invalid");
        byte[] baseBytes = CreatePattern(0x40000, 0x70);
        string basePath = workspace.Write("base.bin", baseBytes);
        string malformedOutput = workspace.PathFor("malformed.bin");

        WorkbenchRunResult malformed = await WorkbenchCompositionService.RunReplaceAsync(
            "NT51950",
            "single",
            "General",
            CreateBaseSlots(basePath),
            [],
            [new WorkbenchGeneralReplacePatchInput(
                "hex-invalid-1",
                "0x00100",
                "0x00101",
                WorkbenchGeneralReplacePatchKind.Overwrite,
                "ABC")],
            build: true,
            TestContext.Current.CancellationToken,
            malformedOutput);
        WorkbenchRunResult protectedRange = await WorkbenchCompositionService.RunReplaceAsync(
            "NT51950",
            "single",
            "General",
            CreateBaseSlots(basePath),
            [],
            [new WorkbenchGeneralReplacePatchInput(
                "hex-protected-1",
                "0x36000",
                "0x36000",
                WorkbenchGeneralReplacePatchKind.Overwrite,
                "A5")],
            build: false,
            TestContext.Current.CancellationToken);

        Assert.False(malformed.Succeeded);
        Assert.False(File.Exists(malformedOutput));
        AssertReportHasIssue(malformed.ReportJson, "ui.general-replace.patch-hex-invalid");
        Assert.False(protectedRange.Succeeded);
        AssertReportHasIssue(protectedRange.ReportJson, "replace.workflow.not-supported");
        Assert.Equal(baseBytes, await File.ReadAllBytesAsync(basePath, TestContext.Current.CancellationToken));
    }

    /// <summary>Rejects an out-of-base fill range before allocating the virtual fill artifact.</summary>
    [Fact]
    public async Task GeneralReplaceVirtualFillRejectsTargetOutsideBaseBeforeMaterialization()
    {
        using var workspace = TempWorkspace.Create("nvt-fw-combiner-general-patch-fill-bounds");
        byte[] baseBytes = CreatePattern(0x40000, 0x71);
        string basePath = workspace.Write("base.bin", baseBytes);

        WorkbenchRunResult result = await WorkbenchCompositionService.RunReplaceAsync(
            "NT51950",
            "single",
            "General",
            CreateBaseSlots(basePath),
            [],
            [new WorkbenchGeneralReplacePatchInput(
                "hex-fill-outside-base",
                "0x00100",
                "0x7FFFFFFF",
                WorkbenchGeneralReplacePatchKind.Fill,
                "FF")],
            build: false,
            TestContext.Current.CancellationToken);

        Assert.False(result.Succeeded);
        AssertReportHasIssue(result.ReportJson, CompositionIssueCodes.InputAddressSpaceLengthMismatch);
        Assert.Equal(baseBytes, await File.ReadAllBytesAsync(basePath, TestContext.Current.CancellationToken));
    }

    /// <summary>Rejects unsupported IC numbers even when mappings touch only DP bytes.</summary>
    [Fact]
    public async Task GeneralReplaceDpOnlyPatchStillValidatesIcNumber()
    {
        using var workspace = TempWorkspace.Create("nvt-fw-combiner-general-patch-number");
        string basePath = workspace.Write("base.bin", CreatePattern(0x40000, 0x72));

        WorkbenchRunResult result = await WorkbenchCompositionService.RunReplaceAsync(
            "NT51950",
            "999",
            "General",
            CreateBaseSlots(basePath),
            [],
            [new WorkbenchGeneralReplacePatchInput(
                "hex-dp-number",
                "0x00100",
                "0x00100",
                WorkbenchGeneralReplacePatchKind.Overwrite,
                "A5")],
            build: false,
            TestContext.Current.CancellationToken);

        Assert.False(result.Succeeded);
        AssertReportHasIssue(result.ReportJson, WorkbenchIssueCodes.ReplaceGeneralIcNumberUnsupported);
    }

    /// <summary>TP virtual patches fail closed when the legacy General Replace compiler is retired.</summary>
    [Fact]
    public async Task GeneralReplaceVirtualTpPatchFailsClosed()
    {
        using var workspace = TempWorkspace.Create("nvt-fw-combiner-workbench-general-tp-patch");
        string basePath = GoldenPath("expected/51950/dp-256k/flash.bin");
        byte[] baseBytes = await File.ReadAllBytesAsync(basePath, TestContext.Current.CancellationToken);

        string outputPath = workspace.PathFor("output.bin");
        WorkbenchRunResult result = await WorkbenchCompositionService.RunReplaceAsync(
            "NT51950",
            "single",
            "General",
            CreateBaseSlots(basePath),
            [],
            [new WorkbenchGeneralReplacePatchInput(
                "hex-tp-1",
                "0x22C00",
                "0x22C01",
                WorkbenchGeneralReplacePatchKind.Overwrite,
                Convert.ToHexString(baseBytes[0x22C00..0x22C02]))],
            build: true,
            TestContext.Current.CancellationToken,
            outputPath);

        AssertWorkflowNotSupported(result, outputPath);
    }

    /// <summary>Many valid mappings still fail closed without a legacy General Replace compiler.</summary>
    [Fact]
    public async Task GeneralReplaceManyMappingsFailClosed()
    {
        string basePath = GoldenPath("expected/51950/dp-256k/flash.bin");
        byte[] baseBytes = await File.ReadAllBytesAsync(basePath, TestContext.Current.CancellationToken);
        WorkbenchGeneralReplacePatchInput[] patches =
        [
            .. Enumerable.Range(0, 80).Select(index => new WorkbenchGeneralReplacePatchInput(
                $"hex-dp-{index:D2}",
                $"0x{0x100 + index:X6}",
                $"0x{0x100 + index:X6}",
                WorkbenchGeneralReplacePatchKind.Overwrite,
                "A5")),
            new WorkbenchGeneralReplacePatchInput(
                "hex-tp-final",
                "0x022C00",
                "0x022C00",
                WorkbenchGeneralReplacePatchKind.Overwrite,
                baseBytes[0x22C00].ToString("X2", CultureInfo.InvariantCulture)),
        ];

        WorkbenchRunResult result = await WorkbenchCompositionService.RunReplaceAsync(
            "NT51950",
            "single",
            "General",
            CreateBaseSlots(basePath),
            [],
            patches,
            build: false,
            TestContext.Current.CancellationToken);

        Assert.False(result.Succeeded, result.ReportJson);
        AssertReportHasIssue(result.ReportJson, "replace.workflow.not-supported");
    }

    private static Dictionary<string, string> CreateBaseSlots(string basePath)
    {
        return new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["replace-base"] = basePath,
        };
    }

    private static void AssertReportHasIssue(string reportJson, string code)
    {
        using var document = JsonDocument.Parse(reportJson);
        Assert.Contains(
            document.RootElement.GetProperty("Issues").EnumerateArray(),
            issue => issue.GetProperty("Code").GetString() == code);
    }

    private static void AssertWorkflowNotSupported(WorkbenchRunResult result, string outputPath)
    {
        Assert.False(result.Succeeded, result.ReportJson);
        Assert.False(File.Exists(outputPath));
        AssertReportHasIssue(result.ReportJson, "replace.workflow.not-supported");
        using var document = JsonDocument.Parse(result.ReportJson);
        Assert.False(document.RootElement.GetProperty("Output").GetProperty("Committed").GetBoolean());
    }

}
