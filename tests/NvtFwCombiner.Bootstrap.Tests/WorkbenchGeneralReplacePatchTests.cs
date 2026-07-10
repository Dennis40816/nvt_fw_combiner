using System.Text.Json;
using NvtFwCombiner.Domain.Composition;
using NvtFwCombiner.TestSupport;
using static NvtFwCombiner.Bootstrap.Tests.BootstrapTestData;

namespace NvtFwCombiner.Bootstrap.Tests;

/// <summary>Regression coverage for virtual General Replace hexadecimal patch authoring.</summary>
public sealed class WorkbenchGeneralReplacePatchTests
{
    /// <summary>Virtual overwrite patches must compile to the same output as file-backed mappings.</summary>
    [Fact]
    public async Task GeneralReplaceVirtualOverwriteMatchesEquivalentFileMappingAndKeepsBaseImmutable()
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
                "A5 5A")],
            build: true,
            TestContext.Current.CancellationToken,
            patchOutput);

        Assert.True(fileMapping.Succeeded, fileMapping.ReportJson);
        Assert.True(virtualPatch.Succeeded, virtualPatch.ReportJson);
        Assert.Equal(
            await File.ReadAllBytesAsync(fileMappingOutput, TestContext.Current.CancellationToken),
            await File.ReadAllBytesAsync(patchOutput, TestContext.Current.CancellationToken));
        Assert.Equal(originalBaseBytes, await File.ReadAllBytesAsync(basePath, TestContext.Current.CancellationToken));

        using var document = JsonDocument.Parse(virtualPatch.ReportJson);
        Assert.Contains(
            document.RootElement.GetProperty("Inputs").EnumerateArray(),
            input => input.GetProperty("ArtifactId").GetString() == "hex-patch-1");
        JsonElement operation = Assert.Single(document.RootElement.GetProperty("Operations").EnumerateArray());
        Assert.Equal("hex-patch-1", operation.GetProperty("OperationId").GetString());
        Assert.Equal("ReplaceRange", operation.GetProperty("Kind").GetString());
        JsonElement difference = Assert.Single(document.RootElement.GetProperty("OutputDifferences").EnumerateArray());
        Assert.Equal("Hex patch", difference.GetProperty("SectionLabel").GetString());
    }

    /// <summary>Fill patches materialize a complete equal-length virtual source artifact.</summary>
    [Fact]
    public async Task GeneralReplaceVirtualFillWritesEverySelectedByte()
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

        Assert.True(result.Succeeded, result.ReportJson);
        byte[] output = await File.ReadAllBytesAsync(outputPath, TestContext.Current.CancellationToken);
        Assert.Equal([0xFF, 0xFF, 0xFF], output[0x110..0x113]);
        using var document = JsonDocument.Parse(result.ReportJson);
        JsonElement operation = Assert.Single(document.RootElement.GetProperty("Operations").EnumerateArray());
        Assert.Contains("Fill hexadecimal", operation.GetProperty("Reason").GetString(), StringComparison.Ordinal);
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
        AssertReportHasIssue(protectedRange.ReportJson, "profile.explicit-mapping.region-not-enabled");
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

    /// <summary>TP virtual patches select the existing checked postbuild path rather than a patch-local processor.</summary>
    [Fact]
    public async Task GeneralReplaceVirtualPatchRunsExistingPostbuildForTpRange()
    {
        string basePath = GoldenPath("expected/51950/dp-256k/flash.bin");
        byte[] baseBytes = await File.ReadAllBytesAsync(basePath, TestContext.Current.CancellationToken);

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
            build: false,
            TestContext.Current.CancellationToken);

        Assert.True(result.Succeeded, result.ReportJson);
        using var document = JsonDocument.Parse(result.ReportJson);
        Assert.Collection(
            document.RootElement.GetProperty("Operations").EnumerateArray(),
            operation =>
            {
                Assert.Equal("ReplaceRange", operation.GetProperty("Kind").GetString());
                Assert.Equal("hex-tp-1", operation.GetProperty("OperationId").GetString());
            },
            operation =>
            {
                Assert.Equal("RunExternalProcessor", operation.GetProperty("Kind").GetString());
                Assert.Equal("nfc.nt51950.ctrlram-postbuild-v1", operation.GetProperty("ProcessorId").GetString());
            });
    }

    /// <summary>Hex viewport overlays every non-overlapping staged patch without writing the immutable base BIN.</summary>
    [Fact]
    public void GeneralReplaceHexViewportShowsBaseAndStagedPatchBytes()
    {
        using var workspace = TempWorkspace.Create("nvt-fw-combiner-general-hex-viewport");
        byte[] baseBytes = CreatePattern(0x400, 0x20);
        string basePath = workspace.Write("base.bin", baseBytes);

        WorkbenchGeneralReplaceHexViewport viewport = WorkbenchCompositionService.CreateGeneralReplaceHexViewport(
            basePath,
            0x100,
            [
                new WorkbenchGeneralReplacePatchInput(
                    "hex-overwrite-1",
                    "0x00101",
                    "0x00102",
                    WorkbenchGeneralReplacePatchKind.Overwrite,
                    "A5 5A"),
                new WorkbenchGeneralReplacePatchInput(
                    "hex-fill-1",
                    "0x00110",
                    "0x00111",
                    WorkbenchGeneralReplacePatchKind.Fill,
                    "FF"),
            ]);

        Assert.Empty(viewport.Issues);
        Assert.Equal(0xC0, viewport.ViewportStart);
        Assert.Equal(0x200, viewport.ViewportLength);
        WorkbenchGeneralReplaceHexViewportRow first = viewport.Rows.Single(row => row.Address == 0x100);
        Assert.Equal(0x100, first.Address);
        Assert.Equal(baseBytes[0x100], first.Bytes[0].Before);
        Assert.Equal(baseBytes[0x100], first.Bytes[0].After);
        Assert.Equal((byte)0xA5, first.Bytes[1].After);
        Assert.Equal((byte)0x5A, first.Bytes[2].After);
        Assert.True(first.Bytes[1].IsChanged);
        Assert.True(first.Bytes[2].IsChanged);
        WorkbenchGeneralReplaceHexViewportRow fillRow = viewport.Rows.Single(row => row.Address == 0x110);
        Assert.Equal((byte)0xFF, fillRow.Bytes[0].After);
        Assert.Equal((byte)0xFF, fillRow.Bytes[1].After);
        Assert.Equal(baseBytes, File.ReadAllBytes(basePath));
    }

    /// <summary>Hex viewport reports patch overlap and does not replace the first staged bytes with ambiguous order.</summary>
    [Fact]
    public void GeneralReplaceHexViewportRejectsOverlappingStagedPatches()
    {
        using var workspace = TempWorkspace.Create("nvt-fw-combiner-general-hex-viewport-overlap");
        byte[] baseBytes = CreatePattern(0x400, 0x40);
        string basePath = workspace.Write("base.bin", baseBytes);

        WorkbenchGeneralReplaceHexViewport viewport = WorkbenchCompositionService.CreateGeneralReplaceHexViewport(
            basePath,
            0x100,
            [
                new WorkbenchGeneralReplacePatchInput(
                    "hex-first",
                    "0x00101",
                    "0x00102",
                    WorkbenchGeneralReplacePatchKind.Overwrite,
                    "A5 5A"),
                new WorkbenchGeneralReplacePatchInput(
                    "hex-overlap",
                    "0x00102",
                    "0x00103",
                    WorkbenchGeneralReplacePatchKind.Overwrite,
                    "BB CC"),
            ]);

        CompositionIssue issue = Assert.Single(viewport.Issues);
        Assert.Equal(WorkbenchIssueCodes.GeneralReplacePatchOverlap, issue.Code);
        WorkbenchGeneralReplaceHexViewportRow row = viewport.Rows.Single(row => row.Address == 0x100);
        Assert.Equal((byte)0xA5, row.Bytes[1].After);
        Assert.Equal((byte)0x5A, row.Bytes[2].After);
        Assert.Equal(baseBytes[0x103], row.Bytes[3].After);
        Assert.Equal(baseBytes, File.ReadAllBytes(basePath));
    }

    /// <summary>Go To reports an address outside the BIN instead of silently clamping to the final row.</summary>
    [Fact]
    public void GeneralReplaceHexViewportRejectsAddressOutsideBaseBin()
    {
        using var workspace = TempWorkspace.Create("nvt-fw-combiner-general-hex-viewport-outside");
        string basePath = workspace.Write("base.bin", CreatePattern(0x400, 0x62));

        WorkbenchGeneralReplaceHexViewport viewport = WorkbenchCompositionService.CreateGeneralReplaceHexViewport(
            basePath,
            0x400,
            []);

        CompositionIssue issue = Assert.Single(viewport.Issues);
        Assert.Equal(CompositionIssueCodes.InputAddressSpaceLengthMismatch, issue.Code);
        Assert.Contains("outside", issue.Message, StringComparison.Ordinal);
        Assert.Empty(viewport.Rows);
    }

    /// <summary>Editor range choices are derived from the compiled General Replace profile and exclude protected header bytes.</summary>
    [Fact]
    public void GeneralReplaceEditableRangesExposeOnlyAuthorizedProfileRegions()
    {
        string basePath = GoldenPath("expected/51950/dp-256k/flash.bin");

        IReadOnlyList<WorkbenchGeneralReplaceEditableRange> ranges = WorkbenchCompositionService.GetGeneralReplaceEditableRanges(
            "NT51950",
            "single",
            basePath);

        Assert.NotEmpty(ranges);
        Assert.All(ranges, range => Assert.True(range.Start >= 0x100));
        Assert.DoesNotContain(ranges, range => range.RegionId.Contains("protected", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(ranges, range => range.RequiresPostbuild);
        Assert.Contains(ranges, range => !range.RequiresPostbuild);
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

}
