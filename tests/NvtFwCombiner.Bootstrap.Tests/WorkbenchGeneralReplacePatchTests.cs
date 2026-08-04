using System.Globalization;
using System.Text.Json;
using NvtFwCombiner.Application.Authoring;
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

        WorkbenchRunResult fileMapping = await WorkbenchCompositionService.BuildGeneralReplaceEphemeralDraftAsync(
            "NT51950",
            "single",
            slots,
            GeneralTestDraftFactory.CreateReplaceDraft([
                GeneralTestDraftFactory.ReplaceFile("file-map", mappingPath, "0x00100", "0x2"),
            ]),
            fileMappingOutput,
            TestContext.Current.CancellationToken);
        WorkbenchRunResult virtualPatch = await WorkbenchCompositionService.BuildGeneralReplaceEphemeralDraftAsync(
            "NT51950",
            "single",
            slots,
            GeneralTestDraftFactory.CreateReplaceDraft([GeneralTestDraftFactory.ReplacePatch(
                "hex-patch-1",
                "0x00100",
                "0x2",
                GeneralMappingSourceKind.HexOverwrite,
                patchValue)]),
            patchOutput,
            TestContext.Current.CancellationToken);

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

        WorkbenchRunResult result = await WorkbenchCompositionService.BuildGeneralReplaceEphemeralDraftAsync(
            "NT51950",
            "single",
            CreateBaseSlots(basePath),
            GeneralTestDraftFactory.CreateReplaceDraft([GeneralTestDraftFactory.ReplacePatch(
                "hex-fill-1",
                "0x00110",
                "0x3",
                GeneralMappingSourceKind.HexFill,
                "FF")]),
            outputPath,
            TestContext.Current.CancellationToken);

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

        WorkbenchRunResult malformed = await WorkbenchCompositionService.BuildGeneralReplaceEphemeralDraftAsync(
            "NT51950",
            "single",
            CreateBaseSlots(basePath),
            GeneralTestDraftFactory.CreateReplaceDraft([GeneralTestDraftFactory.ReplacePatch(
                "hex-invalid-1",
                "0x00100",
                "0x2",
                GeneralMappingSourceKind.HexOverwrite,
                "ABC")]),
            malformedOutput,
            TestContext.Current.CancellationToken);
        WorkbenchRunResult protectedRange = await WorkbenchCompositionService.PreviewGeneralReplaceEphemeralDraftAsync(
            "NT51950",
            "single",
            CreateBaseSlots(basePath),
            GeneralTestDraftFactory.CreateReplaceDraft([GeneralTestDraftFactory.ReplacePatch(
                "hex-protected-1",
                "0x36000",
                "0x1",
                GeneralMappingSourceKind.HexOverwrite,
                "A5")]),
            TestContext.Current.CancellationToken);

        Assert.False(malformed.Succeeded);
        Assert.False(File.Exists(malformedOutput));
        AssertReportHasIssue(malformed.ReportJson, GeneralAuthoringIssueCodes.InlineHexInvalid);
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

        WorkbenchRunResult result = await WorkbenchCompositionService.PreviewGeneralReplaceEphemeralDraftAsync(
            "NT51950",
            "single",
            CreateBaseSlots(basePath),
            GeneralTestDraftFactory.CreateReplaceDraft([GeneralTestDraftFactory.ReplacePatch(
                "hex-fill-outside-base",
                "0x00100",
                "0x7FFFFF00",
                GeneralMappingSourceKind.HexFill,
                "FF")]),
            TestContext.Current.CancellationToken);

        Assert.False(result.Succeeded);
        AssertReportHasIssue(result.ReportJson, "general.admission.target-out-of-bounds");
        AssertReportHasIssue(result.ReportJson, "general.admission.inline-materialization-exceeded");
        Assert.Equal(baseBytes, await File.ReadAllBytesAsync(basePath, TestContext.Current.CancellationToken));
    }

    /// <summary>File, overwrite, and fill rows share one pre-compilation occupancy ledger.</summary>
    [Fact]
    public async Task GeneralReplaceRejectsPatchIntersectionBeforeRouteOrPostbuildSelection()
    {
        using var workspace = TempWorkspace.Create("nvt-fw-combiner-general-occupancy");
        string basePath = workspace.Write("base.bin", CreatePattern(0x40000, 0x75));
        string filePath = workspace.Write("mapping.bin", [0x10, 0x11, 0x12, 0x13]);

        WorkbenchRunResult result = await WorkbenchCompositionService.PreviewGeneralReplaceEphemeralDraftAsync(
            "NT51926",
            "single",
            CreateBaseSlots(basePath),
            GeneralTestDraftFactory.CreateReplaceDraft([
                GeneralTestDraftFactory.ReplaceFile("file", filePath, "0x100", "0x4"),
                GeneralTestDraftFactory.ReplacePatch(
                    "overwrite",
                    "0x102",
                    "0x3",
                    GeneralMappingSourceKind.HexOverwrite,
                    "AABBCC"),
                GeneralTestDraftFactory.ReplacePatch(
                    "fill",
                    "0x104",
                    "0x2",
                    GeneralMappingSourceKind.HexFill,
                    "FF"),
            ]),
            TestContext.Current.CancellationToken);

        Assert.False(result.Succeeded);
        using var report = JsonDocument.Parse(result.ReportJson);
        JsonElement[] issues =
        [
            .. report.RootElement.GetProperty("Issues").EnumerateArray(),
        ];
        Assert.Equal(2, issues.Length);
        Assert.All(
            issues,
            issue => Assert.Equal(
                "general.admission.target-intersection",
                issue.GetProperty("Code").GetString()));
        Assert.Contains(
            issues,
            issue => issue.GetProperty("Message").GetString()!.Contains(
                "[0x102, 0x104)",
                StringComparison.Ordinal));
        Assert.Contains(
            issues,
            issue => issue.GetProperty("Message").GetString()!.Contains(
                "[0x104, 0x105)",
                StringComparison.Ordinal));
        Assert.DoesNotContain(
            issues,
            issue => issue.GetProperty("Message").GetString()!.Contains(
                "postbuild",
                StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>The canonical Base slot id cannot be reused as a General mapping id.</summary>
    [Fact]
    public async Task GeneralReplaceRejectsReservedReferenceSlotIdWithTypedIssue()
    {
        using var workspace = TempWorkspace.Create("nvt-fw-combiner-general-reference-id");
        string basePath = workspace.Write("base.bin", CreatePattern(0x40000, 0x76));
        string sourcePath = workspace.Write("mapping.bin", [0xA5, 0x5A]);

        WorkbenchRunResult result = await WorkbenchCompositionService
            .PreviewGeneralReplaceEphemeralDraftAsync(
                "NT51926",
                "single",
                CreateBaseSlots(basePath),
                GeneralTestDraftFactory.CreateReplaceDraft([
                    GeneralTestDraftFactory.ReplaceFile(
                        "reference-image",
                        sourcePath,
                        "0x3E020",
                        "0x2"),
                ]),
                TestContext.Current.CancellationToken);

        Assert.False(result.Succeeded);
        AssertReportHasIssue(
            result.ReportJson,
            WorkbenchIssueCodes.GeneralReplacePatchIdInvalid);
    }

    /// <summary>Rejects counts outside an owner-declared range before evaluating DP-only route support.</summary>
    [Fact]
    public async Task GeneralReplaceDpOnlyPatchStillValidatesIcNumber()
    {
        using var workspace = TempWorkspace.Create("nvt-fw-combiner-general-patch-number");
        string basePath = workspace.Write("base.bin", CreatePattern(0x40000, 0x72));

        WorkbenchRunResult result = await WorkbenchCompositionService.PreviewGeneralReplaceEphemeralDraftAsync(
            "NT51929",
            "9",
            CreateBaseSlots(basePath),
            GeneralTestDraftFactory.CreateReplaceDraft([GeneralTestDraftFactory.ReplacePatch(
                "hex-dp-number",
                "0x00100",
                "0x1",
                GeneralMappingSourceKind.HexOverwrite,
                "A5")]),
            TestContext.Current.CancellationToken);

        Assert.False(result.Succeeded);
        AssertReportHasIssue(result.ReportJson, WorkbenchIssueCodes.ReplaceGeneralIcNumberUnsupported);
    }

    /// <summary>TP virtual patches fail closed when the legacy General Replace compiler is retired.</summary>
    [Fact]
    public async Task GeneralReplaceVirtualTpPatchFailsClosed()
    {
        using var workspace = TempWorkspace.Create("nvt-fw-combiner-workbench-general-tp-patch");
        string basePath = GoldenArtifactPath("51950", "expected-output", "dp-256k");
        byte[] baseBytes = await File.ReadAllBytesAsync(basePath, TestContext.Current.CancellationToken);

        string outputPath = workspace.PathFor("output.bin");
        WorkbenchRunResult result = await WorkbenchCompositionService.BuildGeneralReplaceEphemeralDraftAsync(
            "NT51950",
            "single",
            CreateBaseSlots(basePath),
            GeneralTestDraftFactory.CreateReplaceDraft([GeneralTestDraftFactory.ReplacePatch(
                "hex-tp-1",
                "0x22C00",
                "0x2",
                GeneralMappingSourceKind.HexOverwrite,
                Convert.ToHexString(baseBytes[0x22C00..0x22C02]))]),
            outputPath,
            TestContext.Current.CancellationToken);

        AssertWorkflowNotSupported(result, outputPath);
    }

    /// <summary>Many valid mappings still fail closed without a legacy General Replace compiler.</summary>
    [Fact]
    public async Task GeneralReplaceManyMappingsFailClosed()
    {
        string basePath = GoldenArtifactPath("51950", "expected-output", "dp-256k");
        byte[] baseBytes = await File.ReadAllBytesAsync(basePath, TestContext.Current.CancellationToken);
        AuthoringMappingState[] patches =
        [
            .. Enumerable.Range(0, 80).Select(index => GeneralTestDraftFactory.ReplacePatch(
                $"hex-dp-{index:D2}",
                $"0x{0x100 + index:X6}",
                "0x1",
                GeneralMappingSourceKind.HexOverwrite,
                "A5")),
            GeneralTestDraftFactory.ReplacePatch(
                "hex-tp-final",
                "0x022C00",
                "0x1",
                GeneralMappingSourceKind.HexOverwrite,
                baseBytes[0x22C00].ToString("X2", CultureInfo.InvariantCulture)),
        ];

        WorkbenchRunResult result = await WorkbenchCompositionService.PreviewGeneralReplaceEphemeralDraftAsync(
            "NT51950",
            "single",
            CreateBaseSlots(basePath),
            GeneralTestDraftFactory.CreateReplaceDraft(patches),
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
