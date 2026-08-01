using System.Text.Json;
using NvtFwCombiner.Application.ExternalTools;
using NvtFwCombiner.Application.FlashMaps;
using NvtFwCombiner.Application.Ports;
using NvtFwCombiner.TestSupport;

namespace NvtFwCombiner.Bootstrap.Tests;

/// <summary>Regression coverage for physical CtrlRAM Postbuild inputs exposed by the workbench.</summary>
public sealed class WorkbenchCtrlRamPhysicalInputTests
{
    private sealed class InspectingProcessor : IExternalProcessor
    {
        private readonly Func<ExternalProcessorRequest, ExternalProcessorResult> _transform;

        internal InspectingProcessor(Func<ExternalProcessorRequest, ExternalProcessorResult> transform)
        {
            _transform = transform;
        }

        internal int CallCount { get; private set; }

        public ValueTask<ExternalProcessorResult> TransformAsync(
            ExternalProcessorRequest request,
            CancellationToken cancellationToken)
        {
            CallCount++;
            return ValueTask.FromResult(_transform(request));
        }
    }

    /// <summary>NT51927 and its approved aliases expose one slot per physical Postbuild BIN.</summary>
    [Theory]
    [InlineData("NT51917", "single", 4)]
    [InlineData("NT51917", "2", 6)]
    [InlineData("NT51917", "3", 8)]
    [InlineData("NT51927", "single", 4)]
    [InlineData("NT51927", "2", 6)]
    [InlineData("NT51927", "3", 8)]
    [InlineData("NT51928", "single", 4)]
    [InlineData("NT51928", "2", 6)]
    [InlineData("NT51928", "3", 8)]
    public void Nt51927FamilySlotsMatchDistinctPostbuildFiles(
        string icId,
        string number,
        int expectedCount)
    {
        IReadOnlyList<WorkbenchReplaceInputSlot> slots = WorkbenchCompositionService.GetReplaceInputSlots(
            icId,
            number,
            WorkbenchReplaceModes.CtrlRam);

        Assert.Equal(expectedCount, slots.Count);
        Assert.Contains(slots, slot => slot.SlotId == "replace-ctrlram-nf");
        Assert.Contains(slots, slot => slot.SlotId == "replace-ctrlram-vn");
        Assert.DoesNotContain(slots, slot => slot.SlotId.StartsWith("replace-ctrlram-nf-", StringComparison.Ordinal));
        Assert.DoesNotContain(slots, slot => slot.SlotId.StartsWith("replace-ctrlram-vn-", StringComparison.Ordinal));
    }

    /// <summary>Masked NT51929-family Cascade authoring exposes DiffDLM but never an independent NF selector.</summary>
    [Theory]
    [InlineData("NT51919")]
    [InlineData("NT51929")]
    [InlineData("NT51932")]
    public void DynamicDiffDlmCascadeHidesIndependentNfSelector(string icId)
    {
        IReadOnlyList<WorkbenchReplaceInputSlot> slots =
            WorkbenchCompositionService.GetReplaceInputSlots(
                icId,
                WorkbenchIcNumberTokens.CascadeTwoToEight,
                WorkbenchReplaceModes.CtrlRam);

        Assert.Contains(
            slots,
            slot => slot.SlotId == "replace-ctrlram-diff" &&
                    slot.Title == "DiffDLM");
        Assert.DoesNotContain(
            slots,
            slot => slot.SlotId == "replace-ctrlram-nf");
    }

    /// <summary>950-family cascade exposes DiffDLM while its preserved DiffNF never becomes an independent input.</summary>
    [Theory]
    [InlineData("NT51950", "cascade")]
    [InlineData("NT51950", "2")]
    [InlineData("NT51951", "cascade")]
    [InlineData("NT51951", "2")]
    public void Nt51950FamilyCascadeHidesIndependentNfSelector(string icId, string number)
    {
        IReadOnlyList<WorkbenchReplaceInputSlot> slots =
            WorkbenchCompositionService.GetReplaceInputSlots(
                icId,
                number,
                WorkbenchReplaceModes.CtrlRam);

        Assert.Contains(
            slots,
            slot => slot.SlotId == "replace-ctrlram-diff" &&
                    slot.Title == "DiffDLM");
        Assert.DoesNotContain(
            slots,
            slot => slot.SlotId == "replace-ctrlram-nf");
    }

    /// <summary>Single-chip NF is already the physical Postbuild input and does not show a cascade prerequisite.</summary>
    [Theory]
    [InlineData("NT51919")]
    [InlineData("NT51929")]
    [InlineData("NT51932")]
    [InlineData("NT51950")]
    public void DiffNfFamiliesDoNotWarnForSingleChip(string icId)
    {
        WorkbenchReplaceInputSlot nf = WorkbenchCompositionService.GetReplaceInputSlots(
            icId,
            "single",
            WorkbenchReplaceModes.CtrlRam).Single(slot => slot.SlotId == "replace-ctrlram-nf");

        Assert.DoesNotContain("DiffNFMerge.exe", nf.Description, StringComparison.Ordinal);
        Assert.DoesNotContain("DiffNFMerge output", nf.Title, StringComparison.Ordinal);
    }

    /// <summary>Each slot concisely reports its declared maximum and destination.</summary>
    [Fact]
    public void CtrlRamSlotDescriptionsExposeSectionByteLimits()
    {
        WorkbenchReplaceInputSlot nf = WorkbenchCompositionService.GetReplaceInputSlots(
            "NT51950",
            "single",
            WorkbenchReplaceModes.CtrlRam).Single(slot => slot.SlotId == "replace-ctrlram-nf");

        Assert.Equal("NF_Ctrlram.bin · NF CtrlRAM: max 10768 B → 0x22C00", nf.Description);
    }

    /// <summary>One three-chip NF input is sliced into every source-offset mapping declared by Postbuild.</summary>
    [Fact]
    public async Task Nt51927ThreeChipNfInputUsesApprovedSourceOffsets()
    {
        using var workspace = TempWorkspace.Create("nvt-fw-combiner-nt51927-physical-nf");
        string basePath = Nt51927ThreeChipBasePath();
        byte[] nfBytes = [.. Enumerable.Range(0, 0x2F50).Select(index => unchecked((byte)((index * 37) + 11)))];
        string nfPath = workspace.Write("NF_Ctrlram.bin", nfBytes);
        var processor = new InspectingProcessor(request =>
        {
            Assert.Empty(request.StagedSources);
            AssertMappedBytes(request, nfBytes, 0x0000, 0x16800, 0x0010);
            AssertMappedBytes(request, nfBytes, 0x0FD0, 0x16810, 0x0FC0);
            AssertMappedBytes(request, nfBytes, 0x0000, 0x1F800, 0x0010);
            AssertMappedBytes(request, nfBytes, 0x1F90, 0x1F810, 0x0FC0);
            AssertMappedBytes(request, nfBytes, 0x0000, 0x28800, 0x0FD0);
            return ExternalProcessorResult.Success(request.InputBytes, []);
        });
        Dictionary<string, string> slotPaths = new(StringComparer.Ordinal)
        {
            [WorkbenchSlotIds.ReplaceBase] = basePath,
            ["replace-ctrlram-nf"] = nfPath,
        };

        WorkbenchRunResult result = await WorkbenchCompositionService.RunCtrlRamReplaceWithProcessorAsync(
            "NT51927",
            "3",
            slotPaths,
            build: false,
            outputPath: null,
            firmwareVersionEdit: null,
            externalProcessor: processor,
            cancellationToken: TestContext.Current.CancellationToken);

        Assert.True(result.Succeeded, result.ReportJson);
    }

    /// <summary>A zero chip count is warning-only when the selected route does not consume IC Count.</summary>
    [Fact]
    public async Task ZeroFirmwareChipCountWarnsWithoutBlockingCountInvariantPlan()
    {
        using var workspace = TempWorkspace.Create("nvt-fw-combiner-zero-chip-count");
        byte[] baseBytes = File.ReadAllBytes(Nt51927ThreeChipBasePath());
        Assert.True(FirmwareConfigMetadataReader.TryReadBackup(baseBytes, out FirmwareConfigMetadata metadata));
        baseBytes[checked((int)metadata.StructureStart) + FirmwareConfigLayout.ChipNumberOffset] = 0;
        string basePath = workspace.Write("base.bin", baseBytes);
        byte[] nfBytes = [.. Enumerable.Range(0, 0x2F50).Select(index => unchecked((byte)((index * 37) + 11)))];
        string nfPath = workspace.Write("NF_Ctrlram.bin", nfBytes);
        var processor = new InspectingProcessor(request =>
            ExternalProcessorResult.Success(request.InputBytes, []));
        Dictionary<string, string> slotPaths = new(StringComparer.Ordinal)
        {
            [WorkbenchSlotIds.ReplaceBase] = basePath,
            ["replace-ctrlram-nf"] = nfPath,
        };

        WorkbenchRunResult result = await WorkbenchCompositionService.RunCtrlRamReplaceWithProcessorAsync(
            "NT51927",
            "3",
            slotPaths,
            build: false,
            outputPath: null,
            firmwareVersionEdit: null,
            externalProcessor: processor,
            cancellationToken: TestContext.Current.CancellationToken);

        Assert.True(result.Succeeded, result.ReportJson);
        Assert.Equal(1, processor.CallCount);
        Assert.Equal(baseBytes, await File.ReadAllBytesAsync(basePath, TestContext.Current.CancellationToken));
        using var report = JsonDocument.Parse(result.ReportJson);
        JsonElement issue = Assert.Single(
            report.RootElement.GetProperty("Issues").EnumerateArray(),
            candidate => candidate.GetProperty("Code").GetString() ==
                FirmwareConfigChipCountDiagnostics.ZeroWarningIssueCode);
        Assert.Equal("warning", issue.GetProperty("Severity").GetString());
        Assert.Contains(
            "This workflow does not depend on IC Count, so Build may continue.",
            issue.GetProperty("Message").GetString(),
            StringComparison.Ordinal);
    }

    /// <summary>A readable FWConfig chip count that contradicts the selected exact plan fails before processor invocation.</summary>
    [Fact]
    public async Task FirmwareChipCountMismatchFailsBeforeProcessorInvocation()
    {
        using var workspace = TempWorkspace.Create("nvt-fw-combiner-short-ctrlram-authority");
        string basePath = Nt51927ThreeChipBasePath();
        byte[] baseBytes = File.ReadAllBytes(basePath);
        byte[] nfBytes = [0x10, 0x21, 0x32, 0x43, 0x54, 0x65, 0x76, 0x87];
        string nfPath = workspace.Write("NF_Ctrlram.bin", nfBytes);
        var processor = new InspectingProcessor(request =>
            ExternalProcessorResult.Success(request.InputBytes, []));
        Dictionary<string, string> slotPaths = new(StringComparer.Ordinal)
        {
            [WorkbenchSlotIds.ReplaceBase] = basePath,
            ["replace-ctrlram-nf"] = nfPath,
        };

        WorkbenchRunResult result = await WorkbenchCompositionService.RunCtrlRamReplaceWithProcessorAsync(
            "NT51927",
            "single",
            slotPaths,
            build: false,
            outputPath: null,
            firmwareVersionEdit: null,
            externalProcessor: processor,
            cancellationToken: TestContext.Current.CancellationToken);

        Assert.False(result.Succeeded, result.ReportJson);
        Assert.Equal(0, processor.CallCount);
        Assert.Equal(baseBytes, await File.ReadAllBytesAsync(basePath, TestContext.Current.CancellationToken));
        using var document = JsonDocument.Parse(result.ReportJson);
        JsonElement issue = Assert.Single(document.RootElement.GetProperty("Issues").EnumerateArray());
        Assert.Equal(WorkbenchIssueCodes.ReplaceCtrlRamIcNumberMismatch, issue.GetProperty("Code").GetString());
        Assert.Contains("Selected Number is 1 IC", issue.GetProperty("Message").GetString(), StringComparison.Ordinal);
        Assert.Contains("reports 3 IC", issue.GetProperty("Message").GetString(), StringComparison.Ordinal);
        Assert.False(document.RootElement.GetProperty("Output").GetProperty("Committed").GetBoolean());
    }

    /// <summary>An unavailable firmware count may fail other admission checks, but never fabricates a mismatch.</summary>
    [Fact]
    public async Task UnavailableFirmwareChipCountDoesNotCreateMismatchIssue()
    {
        using var workspace = TempWorkspace.Create("nvt-fw-combiner-unavailable-chip-count");
        string basePath = workspace.Write("base.bin", new byte[0x40000]);
        string nfPath = workspace.Write("NF_Ctrlram.bin", [0x10]);
        var processor = new InspectingProcessor(request =>
            ExternalProcessorResult.Success(request.InputBytes, []));

        WorkbenchRunResult result = await WorkbenchCompositionService.RunCtrlRamReplaceWithProcessorAsync(
            "NT51927",
            "single",
            new Dictionary<string, string>(StringComparer.Ordinal)
            {
                [WorkbenchSlotIds.ReplaceBase] = basePath,
                ["replace-ctrlram-nf"] = nfPath,
            },
            build: false,
            outputPath: null,
            firmwareVersionEdit: null,
            externalProcessor: processor,
            cancellationToken: TestContext.Current.CancellationToken);

        Assert.False(result.Succeeded, result.ReportJson);
        using var document = JsonDocument.Parse(result.ReportJson);
        Assert.DoesNotContain(
            document.RootElement.GetProperty("Issues").EnumerateArray(),
            issue => issue.GetProperty("Code").GetString() == WorkbenchIssueCodes.ReplaceCtrlRamIcNumberMismatch);
    }

    private static void AssertMappedBytes(
        ExternalProcessorRequest request,
        byte[] sourceBytes,
        int sourceOffset,
        int firmwareStart,
        int length)
    {
        Assert.Equal(
            sourceBytes[sourceOffset..(sourceOffset + length)],
            request.InputBytes.Slice(firmwareStart, length).ToArray());
    }

    private static string Nt51927ThreeChipBasePath()
    {
        JsonElement goldenCase = CanonicalGoldenTestData.LoadDirectEvidenceCase(
            "ctrlram-replace",
            "nt51927-3chip-self-20260705");
        JsonElement baseArtifact = goldenCase.GetProperty("artifacts").EnumerateArray().Single(item =>
            item.GetProperty("slotId").GetString() == WorkbenchSlotIds.ReplaceBase);
        return CanonicalGoldenTestData.ArtifactPath(baseArtifact);
    }

}
