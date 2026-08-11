using System.Globalization;
using NvtFwCombiner.Application.Authoring;
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

        IReadOnlyList<CompositionIssue> fileIssues = await PrepareRejectedAsync(
            "NT51950",
            "single",
            slots,
            GeneralTestDraftFactory.CreateReplaceDraft([
                GeneralTestDraftFactory.ReplaceFile("file-map", mappingPath, "0x00100", "0x2"),
            ]),
            TestContext.Current.CancellationToken);
        IReadOnlyList<CompositionIssue> patchIssues = await PrepareRejectedAsync(
            "NT51950",
            "single",
            slots,
            GeneralTestDraftFactory.CreateReplaceDraft([GeneralTestDraftFactory.ReplacePatch(
                "hex-patch-1",
                "0x00100",
                "0x2",
                GeneralMappingSourceKind.HexOverwrite,
                patchValue)]),
            TestContext.Current.CancellationToken);

        AssertWorkflowNotSupported(fileIssues, fileMappingOutput);
        AssertWorkflowNotSupported(patchIssues, patchOutput);
        Assert.Equal(originalBaseBytes, await File.ReadAllBytesAsync(basePath, TestContext.Current.CancellationToken));
    }

    /// <summary>Validated fill patches fail closed when no exact V2 General Replace route exists.</summary>
    [Fact]
    public async Task GeneralReplaceVirtualFillFailsClosedWithoutOutput()
    {
        using var workspace = TempWorkspace.Create("nvt-fw-combiner-general-patch-fill");
        string basePath = workspace.Write("base.bin", CreatePattern(0x40000, 0x50));
        string outputPath = workspace.PathFor("fill.bin");

        IReadOnlyList<CompositionIssue> issues = await PrepareRejectedAsync(
            "NT51950",
            "single",
            CreateBaseSlots(basePath),
            GeneralTestDraftFactory.CreateReplaceDraft([GeneralTestDraftFactory.ReplacePatch(
                "hex-fill-1",
                "0x00110",
                "0x3",
                GeneralMappingSourceKind.HexFill,
                "FF")]),
            TestContext.Current.CancellationToken);

        AssertWorkflowNotSupported(issues, outputPath);
    }

    /// <summary>An unavailable workflow rejects malformed and protected inputs before draft validation.</summary>
    [Fact]
    public async Task GeneralReplaceVirtualPatchRejectsMalformedAndProtectedInput()
    {
        using var workspace = TempWorkspace.Create("nvt-fw-combiner-general-patch-invalid");
        byte[] baseBytes = CreatePattern(0x40000, 0x70);
        string basePath = workspace.Write("base.bin", baseBytes);
        string malformedOutput = workspace.PathFor("malformed.bin");

        IReadOnlyList<CompositionIssue> malformedIssues = await PrepareRejectedAsync(
            "NT51950",
            "single",
            CreateBaseSlots(basePath),
            GeneralTestDraftFactory.CreateReplaceDraft([GeneralTestDraftFactory.ReplacePatch(
                "hex-invalid-1",
                "0x00100",
                "0x2",
                GeneralMappingSourceKind.HexOverwrite,
                "ABC")]),
            TestContext.Current.CancellationToken);
        IReadOnlyList<CompositionIssue> protectedIssues = await PrepareRejectedAsync(
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

        Assert.False(File.Exists(malformedOutput));
        AssertHasIssue(malformedIssues, "replace.workflow.not-supported");
        AssertHasIssue(protectedIssues, "replace.workflow.not-supported");
        Assert.Equal(baseBytes, await File.ReadAllBytesAsync(basePath, TestContext.Current.CancellationToken));
    }

    /// <summary>An unavailable workflow rejects before allocating an out-of-base virtual fill artifact.</summary>
    [Fact]
    public async Task GeneralReplaceVirtualFillRejectsTargetOutsideBaseBeforeMaterialization()
    {
        using var workspace = TempWorkspace.Create("nvt-fw-combiner-general-patch-fill-bounds");
        byte[] baseBytes = CreatePattern(0x40000, 0x71);
        string basePath = workspace.Write("base.bin", baseBytes);

        IReadOnlyList<CompositionIssue> issues = await PrepareRejectedAsync(
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

        AssertHasIssue(issues, "replace.workflow.not-supported");
        Assert.Equal(baseBytes, await File.ReadAllBytesAsync(basePath, TestContext.Current.CancellationToken));
    }

    /// <summary>File, overwrite, and fill rows share one pre-compilation occupancy ledger.</summary>
    [Fact]
    public async Task GeneralReplaceRejectsPatchIntersectionBeforeRouteOrPostbuildSelection()
    {
        using var workspace = TempWorkspace.Create("nvt-fw-combiner-general-occupancy");
        string basePath = workspace.Write("base.bin", CreatePattern(0x40000, 0x75));
        string filePath = workspace.Write("mapping.bin", [0x10, 0x11, 0x12, 0x13]);

        IReadOnlyList<CompositionIssue> issues = await PrepareRejectedAsync(
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

        Assert.Equal(2, issues.Count);
        Assert.All(
            issues,
            issue => Assert.Equal(
                "general.admission.target-intersection",
                issue.Code));
        Assert.Contains(
            issues,
            issue => issue.Message.Contains(
                "[0x102, 0x104)",
                StringComparison.Ordinal));
        Assert.Contains(
            issues,
            issue => issue.Message.Contains(
                "[0x104, 0x105)",
                StringComparison.Ordinal));
        Assert.DoesNotContain(
            issues,
            issue => issue.Message.Contains(
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

        IReadOnlyList<CompositionIssue> issues = await PrepareRejectedAsync(
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

        AssertHasIssue(issues, CompositionPlanningIssueCodes.GeneralReplacePatchIdInvalid);
    }

    /// <summary>An unavailable workflow rejects before interpreting an IC-count selection.</summary>
    [Fact]
    public async Task GeneralReplaceDpOnlyPatchStillValidatesIcNumber()
    {
        using var workspace = TempWorkspace.Create("nvt-fw-combiner-general-patch-number");
        string basePath = workspace.Write("base.bin", CreatePattern(0x40000, 0x72));

        IReadOnlyList<CompositionIssue> issues = await PrepareRejectedAsync(
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

        AssertHasIssue(issues, "replace.workflow.not-supported");
    }

    /// <summary>TP virtual patches fail closed when the legacy General Replace compiler is retired.</summary>
    [Fact]
    public async Task GeneralReplaceVirtualTpPatchFailsClosed()
    {
        using var workspace = TempWorkspace.Create("nvt-fw-combiner-workbench-general-tp-patch");
        string basePath = GoldenArtifactPath("51950", "expected-output", "dp-256k");
        byte[] baseBytes = await File.ReadAllBytesAsync(basePath, TestContext.Current.CancellationToken);

        string outputPath = workspace.PathFor("output.bin");
        IReadOnlyList<CompositionIssue> issues = await PrepareRejectedAsync(
            "NT51950",
            "single",
            CreateBaseSlots(basePath),
            GeneralTestDraftFactory.CreateReplaceDraft([GeneralTestDraftFactory.ReplacePatch(
                "hex-tp-1",
                "0x22C00",
                "0x2",
                GeneralMappingSourceKind.HexOverwrite,
                Convert.ToHexString(baseBytes[0x22C00..0x22C02]))]),
            TestContext.Current.CancellationToken);

        AssertWorkflowNotSupported(issues, outputPath);
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

        IReadOnlyList<CompositionIssue> issues = await PrepareRejectedAsync(
            "NT51950",
            "single",
            CreateBaseSlots(basePath),
            GeneralTestDraftFactory.CreateReplaceDraft(patches),
            TestContext.Current.CancellationToken);

        AssertHasIssue(issues, "replace.workflow.not-supported");
    }

    private static Dictionary<string, string> CreateBaseSlots(string basePath)
    {
        return new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["replace-base"] = basePath,
        };
    }

    private static async ValueTask<IReadOnlyList<CompositionIssue>> PrepareRejectedAsync(
        string icId,
        string number,
        IReadOnlyDictionary<string, string> slotPaths,
        GeneralMappingDraftState draft,
        CancellationToken cancellationToken)
    {
        GeneralAuthoringSessionPreparation prepared =
            await GeneralWorkflowTestSupport.PrepareGeneralReplaceAsync(
                BootstrapTestHost.Canonical,
                icId,
                number,
                slotPaths,
                draft,
                savedRulePolicy: null,
                cancellationToken);
        Assert.False(prepared.Succeeded);
        Assert.Null(prepared.AcceptedSession);
        return prepared.Issues;
    }

    private static void AssertHasIssue(IReadOnlyList<CompositionIssue> issues, string code)
    {
        Assert.Contains(issues, issue => issue.Code == code);
    }

    private static void AssertWorkflowNotSupported(
        IReadOnlyList<CompositionIssue> issues,
        string outputPath)
    {
        Assert.False(File.Exists(outputPath));
        AssertHasIssue(issues, "replace.workflow.not-supported");
    }

}
