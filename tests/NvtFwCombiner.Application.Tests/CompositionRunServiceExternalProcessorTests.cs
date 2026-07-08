using NvtFwCombiner.Application.Composition;
using NvtFwCombiner.Application.ExternalTools;
using NvtFwCombiner.Application.FlashMaps;
using NvtFwCombiner.Contracts.Reports;
using NvtFwCombiner.Domain.Composition;
using NvtFwCombiner.TestSupport;

namespace NvtFwCombiner.Application.Tests;

public sealed partial class CompositionRunServiceTests
{
    /// <summary>Verifies preview runs external processor operations through the application port.</summary>
    [Fact]
    public async Task PreviewRunsExternalProcessorOperationThroughPort()
    {
        var processor = new FakeExternalProcessor(request =>
        {
            Assert.Equal("run-external.run-crc", request.RunId);
            Assert.Equal("processor-v1", request.ProcessorId);
            Assert.Equal("tool-v1", request.ToolBindingId);
            Assert.Equal([new ByteRange(1, 1)], request.AllowedWriteRanges);
            byte[] output = request.InputBytes.ToArray();
            output[1] = 0x7E;
            return ExternalProcessorResult.Success(output, [new ByteRange(1, 1)]);
        });
        var service = new CompositionRunService(
            new FakeArtifactReader([]),
            new FakeClock([FirstTimestamp, SecondTimestamp]),
            null,
            processor);

        CompositionRunResult result = await service.PreviewAsync(
            CreateExternalProcessorRequest(),
            CancellationToken.None);

        Assert.Equal(CompositionExecutionStatus.Succeeded, result.Status);
        Assert.Equal([0, 0x7E, 0, 0], result.OutputBytes.ToArray());
        Assert.Equal(1, processor.CallCount);
        MutationRunSummary mutation = Assert.Single(result.Report.Mutations);
        Assert.Equal(CompositionOperationKind.RunExternalProcessor, mutation.Kind);
        Assert.Equal(1, mutation.ChangedByteCount);
        OperationRunSummary operation = Assert.Single(result.Report.Operations);
        Assert.Equal("processor-v1", operation.ProcessorId);
        Assert.Equal("tool-v1", operation.ToolBindingId);
        Assert.Equal([new ByteRange(0, 4)], operation.ProcessorAllowedReadRanges);
        Assert.Equal([new ByteRange(1, 1)], operation.ProcessorAllowedWriteRanges);
        Assert.Equal("run synthetic external processor", operation.Reason);
        Assert.NotNull(result.PreviewToken);
    }

    /// <summary>Verifies external processor requests receive staged source bytes without pre-writing the work image.</summary>
    [Fact]
    public async Task PreviewPassesExternalProcessorStagedSources()
    {
        var processor = new FakeExternalProcessor(request =>
        {
            Assert.Equal([0x10, 0x20, 0x30, 0x40], request.InputBytes.ToArray());
            ExternalProcessorStagedSource stagedSource = Assert.Single(request.StagedSources);
            Assert.Equal(new ByteRange(1, 2), stagedSource.FirmwareRange);
            Assert.Equal([0xAA, 0xBB], stagedSource.Bytes.ToArray());
            byte[] output = request.InputBytes.ToArray();
            stagedSource.Bytes.CopyTo(output.AsMemory((int)stagedSource.FirmwareRange.Start));
            return ExternalProcessorResult.Success(output, [stagedSource.FirmwareRange]);
        });
        var service = new CompositionRunService(
            new FakeArtifactReader(new Dictionary<string, byte[]>
            {
                ["reference-artifact"] = [0x10, 0x20, 0x30, 0x40],
                ["ctrlram-artifact"] = [0xAA, 0xBB, 0xCC],
            }),
            new FakeClock([FirstTimestamp, SecondTimestamp]),
            null,
            processor);

        CompositionRunResult result = await service.PreviewAsync(
            CreateStagedSourceExternalProcessorRequest(),
            CancellationToken.None);

        Assert.Equal(CompositionExecutionStatus.Succeeded, result.Status);
        Assert.Equal([0x10, 0xAA, 0xBB, 0x40], result.OutputBytes.ToArray());
        Assert.Contains(result.Report.Issues, issue => issue.Code == "input.address-space.truncated");
    }

    /// <summary>Verifies Replace reports classify final output differences against reference base and postbuild write ranges.</summary>
    [Fact]
    public async Task ReplaceReportClassifiesOutputDifferences()
    {
        var processor = new FakeExternalProcessor(request =>
        {
            byte[] output = request.InputBytes.ToArray();
            ExternalProcessorStagedSource stagedSource = Assert.Single(request.StagedSources);
            stagedSource.Bytes.CopyTo(output.AsMemory((int)stagedSource.FirmwareRange.Start));
            output[3] = 0x7E;
            return ExternalProcessorResult.Success(
                output,
                [stagedSource.FirmwareRange, new ByteRange(3, 1)]);
        });
        var service = new CompositionRunService(
            new FakeArtifactReader(new Dictionary<string, byte[]>
            {
                ["reference-artifact"] = [0x10, 0x20, 0x30, 0x40],
                ["ctrlram-artifact"] = [0xAA, 0xBB],
            }),
            new FakeClock([FirstTimestamp, SecondTimestamp]),
            null,
            processor);

        CompositionRunResult result = await service.PreviewAsync(
            CreateStagedSourceExternalProcessorRequest(
                stagedSourceSpaceId: "replace-ctrlram-nf-master",
                writeRangeSections: [
                    new ExternalProcessorWriteRangeSection(TpHeaderSectionIds.FlashHeaderCrc, new ByteRange(3, 1)),
                ]),
            CancellationToken.None);

        Assert.Equal(CompositionExecutionStatus.Succeeded, result.Status);
        Assert.Equal(2, result.Report.OutputDifferences.Count);
        OutputDifferenceSummary replacement = result.Report.OutputDifferences[0];
        Assert.Equal(new ByteRange(1, 2), replacement.Range);
        Assert.True(replacement.IsAccepted);
        Assert.Equal(OutputDifferenceClassifications.DeclaredReplacement, replacement.Classification);
        Assert.Equal("NF CtrlRAM (Master)", replacement.SectionLabel);
        Assert.Contains("staged replacement source", replacement.Explanation, StringComparison.Ordinal);
        Assert.Equal("2030", replacement.BeforeHexPreview);
        Assert.Equal("aabb", replacement.AfterHexPreview);
        Assert.Equal(2, replacement.HexPreviewByteCount);
        Assert.True(replacement.IsHexPreviewComplete);
        OutputDifferenceSummary crcHeader = result.Report.OutputDifferences[1];
        Assert.Equal(new ByteRange(3, 1), crcHeader.Range);
        Assert.True(crcHeader.IsAccepted);
        Assert.Equal(OutputDifferenceClassifications.PostbuildCrcHeader, crcHeader.Classification);
        Assert.Equal("TP flash header / CRC fields", crcHeader.SectionLabel);
        Assert.Contains("approved TP flash header / CRC fields postbuild", crcHeader.Explanation, StringComparison.Ordinal);
        Assert.Equal("40", crcHeader.BeforeHexPreview);
        Assert.Equal("7e", crcHeader.AfterHexPreview);
        Assert.Equal(1, crcHeader.HexPreviewByteCount);
        Assert.True(crcHeader.IsHexPreviewComplete);
        Assert.DoesNotContain(result.Report.Issues, issue => issue.Code == ReportIssueCodes.UnexpectedOutputDifference);
    }

    /// <summary>Verifies preview approval includes staged source-to-firmware mapping details.</summary>
    [Fact]
    public async Task PreviewTokenChangesWhenStagedSourceBindingChanges()
    {
        var processor = new FakeExternalProcessor(request =>
            ExternalProcessorResult.Success(request.InputBytes, []));
        var service = new CompositionRunService(
            new FakeArtifactReader(new Dictionary<string, byte[]>
            {
                ["reference-artifact"] = [0x10, 0x20, 0x30, 0x40],
                ["ctrlram-artifact"] = [0xAA, 0xBB],
            }),
            new FakeClock([FirstTimestamp, SecondTimestamp, ThirdTimestamp, FourthTimestamp]),
            null,
            processor);

        CompositionRunResult first = await service.PreviewAsync(
            CreateStagedSourceExternalProcessorRequest(new ByteRange(1, 2)),
            CancellationToken.None);
        CompositionRunResult second = await service.PreviewAsync(
            CreateStagedSourceExternalProcessorRequest(new ByteRange(2, 2)),
            CancellationToken.None);

        Assert.Equal(first.OutputBytes.ToArray(), second.OutputBytes.ToArray());
        Assert.NotEqual(first.PreviewToken, second.PreviewToken);
    }

    /// <summary>Verifies external processor failures are returned as structured run issues.</summary>
    [Fact]
    public async Task PreviewPropagatesExternalProcessorFailure()
    {
        var processor = new FakeExternalProcessor(_ => ExternalProcessorResult.Failed([
            new CompositionIssue("external-tool.process.failed", "fake processor failed", "run-crc"),
        ]));
        var service = new CompositionRunService(
            new FakeArtifactReader([]),
            new FakeClock([FirstTimestamp, SecondTimestamp]),
            null,
            processor);

        CompositionRunResult result = await service.PreviewAsync(
            CreateExternalProcessorRequest(),
            CancellationToken.None);

        Assert.Equal(CompositionExecutionStatus.Failed, result.Status);
        CompositionIssue issue = Assert.Single(result.Report.Issues);
        Assert.Equal("external-tool.process.failed", issue.Code);
        Assert.Empty(result.OutputBytes.ToArray());
    }

}
