using System.Text.Json;
using NvtFwCombiner.Application.ExternalTools;
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

    /// <summary>932/950 cascade NF clearly identifies the external DiffNFMerge prerequisite.</summary>
    [Theory]
    [InlineData("NT51919", "cascade")]
    [InlineData("NT51929", "cascade")]
    [InlineData("NT51932", "cascade")]
    [InlineData("NT51932", "2")]
    [InlineData("NT51950", "cascade")]
    [InlineData("NT51950", "2")]
    [InlineData("NT51951", "cascade")]
    [InlineData("NT51951", "2")]
    public void DiffNfCascadeFamiliesWarnThatNfMustBePrebuilt(string icId, string number)
    {
        WorkbenchReplaceInputSlot nf = WorkbenchCompositionService.GetReplaceInputSlots(
            icId,
            number,
            WorkbenchReplaceModes.CtrlRam).Single(slot => slot.SlotId == "replace-ctrlram-nf");

        Assert.Contains("DiffNFMerge output", nf.Title, StringComparison.Ordinal);
        Assert.Contains("select an NF_Ctrlram.bin prebuilt by the external DiffNFMerge.exe", nf.Description, StringComparison.Ordinal);
        Assert.Contains("input contract and execution are not yet integrated", nf.Description, StringComparison.Ordinal);
        Assert.DoesNotContain("NF_Diff_", nf.Description, StringComparison.Ordinal);
    }

    /// <summary>Single-chip NF is already the physical Postbuild input and does not show a cascade prerequisite.</summary>
    [Theory]
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

    /// <summary>Each slot reports the declared maximum while explaining Legacy Combiner short-file behavior.</summary>
    [Fact]
    public void CtrlRamSlotDescriptionsExposeSectionByteLimits()
    {
        WorkbenchReplaceInputSlot nf = WorkbenchCompositionService.GetReplaceInputSlots(
            "NT51950",
            "single",
            WorkbenchReplaceModes.CtrlRam).Single(slot => slot.SlotId == "replace-ctrlram-nf");

        Assert.Contains("NF CtrlRAM: max 10768 bytes", nf.Description, StringComparison.Ordinal);
        Assert.Contains("source +0x0 to flash 0x22C00", nf.Description, StringComparison.Ordinal);
        Assert.Contains("Short source files stop at EOF without padding", nf.Description, StringComparison.Ordinal);
        Assert.Contains("bytes beyond each section maximum are not used", nf.Description, StringComparison.Ordinal);
    }

    /// <summary>One three-chip NF input is sliced into every source-offset mapping declared by Postbuild.</summary>
    [Fact]
    public async Task Nt51927ThreeChipNfInputUsesApprovedSourceOffsets()
    {
        using var workspace = TempWorkspace.Create("nvt-fw-combiner-nt51927-physical-nf");
        string basePath = RepositoryPaths.FromRepositoryRoot(
            "testdata",
            "golden",
            "ctrlram-replace",
            "fixtures",
            "20260705",
            "base",
            "nt51927-3ic-tm-tl177xfks03-gm-d08t9b-20260703.bin");
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

    /// <summary>A short source on a non-exact IC-number shape fails before processor invocation.</summary>
    [Fact]
    public async Task ShortCtrlRamSourceOnUnreviewedShapeFailsClosed()
    {
        using var workspace = TempWorkspace.Create("nvt-fw-combiner-short-ctrlram-authority");
        string basePath = RepositoryPaths.FromRepositoryRoot(
            "testdata",
            "golden",
            "ctrlram-replace",
            "fixtures",
            "20260705",
            "base",
            "nt51927-3ic-tm-tl177xfks03-gm-d08t9b-20260703.bin");
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
        Assert.Equal("replace.workflow.not-supported", issue.GetProperty("Code").GetString());
        Assert.False(document.RootElement.GetProperty("Output").GetProperty("Committed").GetBoolean());
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
}
