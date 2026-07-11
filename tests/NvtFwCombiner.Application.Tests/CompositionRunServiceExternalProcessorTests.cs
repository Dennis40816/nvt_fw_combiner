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
            return ExternalProcessorResult.Success(
                output,
                [new ByteRange(1, 1)],
                [
                    new ExternalProcessInvocation(
                        "C:\\tools\\Combiner.exe",
                        "C:\\staging\\run-external.run-crc",
                        ["CRC_Enable", "C:\\staging\\run-external.run-crc\\output\\firmware.bin"]),
                ]);
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
        ExternalProcessInvocation executedCommand = Assert.Single(operation.ExecutedCommands);
        Assert.Equal("C:\\tools\\Combiner.exe", executedCommand.ExecutablePath);
        Assert.Equal("C:\\staging\\run-external.run-crc", executedCommand.WorkingDirectory);
        Assert.Equal(
            ["CRC_Enable", "C:\\staging\\run-external.run-crc\\output\\firmware.bin"],
            executedCommand.Arguments);
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
        Assert.Contains(result.Report.Issues, issue => issue.Code == CompositionIssueCodes.InputAddressSpaceTruncated);
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
        OutputDifferenceSemantic replacementSemantic = Assert.IsType<OutputDifferenceSemantic>(replacement.Semantic);
        Assert.Equal(TpBinaryCategoryIds.CtrlRam, replacementSemantic.CategoryId);
        Assert.Equal(TpHeaderSectionIds.CtrlRamReplacement, replacementSemantic.SubjectId);
        Assert.Equal("NF CtrlRAM (Master)", replacementSemantic.SubjectLabel);
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

    /// <summary>Verifies a modeled normal-header range reaches the report as a named field, not just a CRC bucket.</summary>
    [Fact]
    public async Task ReplaceReportNamesNt51926DlmCrcZero()
    {
        var processor = new FakeExternalProcessor(request =>
        {
            byte[] output = request.InputBytes.ToArray();
            ExternalProcessorStagedSource stagedSource = Assert.Single(request.StagedSources);
            stagedSource.Bytes.CopyTo(output.AsMemory((int)stagedSource.FirmwareRange.Start));
            output[0x1C] = 0x7E;
            return ExternalProcessorResult.Success(
                output,
                [stagedSource.FirmwareRange, new ByteRange(0x1C, 1)]);
        });
        var service = new CompositionRunService(
            new FakeArtifactReader(new Dictionary<string, byte[]>
            {
                ["reference-artifact"] = new byte[0x100],
                ["ctrlram-artifact"] = [0xAA, 0xBB],
            }),
            new FakeClock([FirstTimestamp, SecondTimestamp]),
            null,
            processor);

        CompositionRunResult result = await service.PreviewAsync(
            CreateNt51926HeaderSemanticRequest(),
            CancellationToken.None);

        OutputDifferenceSummary headerDifference = Assert.Single(
            result.Report.OutputDifferences,
            difference => difference.Range == new ByteRange(0x1C, 1));

        OutputDifferenceSemantic semantic = Assert.IsType<OutputDifferenceSemantic>(headerDifference.Semantic);
        Assert.Equal("tp-flash-header", semantic.CategoryId);
        Assert.Equal("TP Flash Header", semantic.CategoryLabel);
        Assert.Equal("nt51926-header:dlm-crc-0", semantic.SubjectId);
        Assert.Equal("DLM CRC 0", semantic.SubjectLabel);
        Assert.Equal("Expected: postbuild recalculated DLM CRC 0.", semantic.Explanation);
        Assert.Equal("tp-header", semantic.ParentId);
        Assert.Equal("Header", semantic.ParentLabel);
    }

    /// <summary>Verifies copied NT51927 header bytes retain the primary header field identity in the report.</summary>
    [Fact]
    public async Task ReplaceReportNamesNt51927CopiedIlmCrcZero()
    {
        var copiedFieldRange = new ByteRange(0x1E25C, 1);
        CompositionRunRequest request = CreateNt51927CopiedHeaderSemanticRequest();
        ExternalProcessorInvocation invocation = Assert.IsType<ExternalProcessorInvocation>(
            Assert.Single(request.Plan.OrderedOperations).ExternalProcessorInvocation);
        ExternalProcessorWriteRangeSection headerCopySection = Assert.Single(invocation.AllowedWriteRangeSections);
        Assert.Equal(TpHeaderSectionIds.HeaderCopyMaster, headerCopySection.SectionId);
        Assert.Equal(new ByteRange(0x200, 0x190), headerCopySection.SourceRange);
        Assert.True(headerCopySection.TryMapRangeToSourceRange(copiedFieldRange, out ByteRange sourceFieldRange));
        Assert.Equal(new ByteRange(0x22C, 1), sourceFieldRange);
        Assert.True(TpHeaderCatalog.TryFindField("NT51927", sourceFieldRange, out TpHeaderField? sourceField));
        Assert.Equal("header-0-ilm-crc", sourceField!.FieldId);
        var processor = new FakeExternalProcessor(request =>
        {
            byte[] output = request.InputBytes.ToArray();
            output[checked((int)copiedFieldRange.Start)] = 0x7E;
            return ExternalProcessorResult.Success(output, [copiedFieldRange]);
        });
        var service = new CompositionRunService(
            new FakeArtifactReader(new Dictionary<string, byte[]>
            {
                ["reference-artifact"] = new byte[0x1E3C0],
            }),
            new FakeClock([FirstTimestamp, SecondTimestamp]),
            null,
            processor);

        CompositionRunResult result = await service.PreviewAsync(
            request,
            CancellationToken.None);

        OutputDifferenceSummary headerDifference = Assert.Single(result.Report.OutputDifferences);
        Assert.Equal(copiedFieldRange, headerDifference.Range);

        OutputDifferenceSemantic semantic = Assert.IsType<OutputDifferenceSemantic>(headerDifference.Semantic);
        Assert.Equal("tp-flash-header", semantic.CategoryId);
        Assert.Equal("nt51927-header:header-0-ilm-crc", semantic.SubjectId);
        Assert.Equal("ILM CRC 0", semantic.SubjectLabel);
        Assert.Equal(
            "Expected: postbuild refreshed ILM CRC 0 and copied it to Header copy / master.",
            semantic.Explanation);
        Assert.Equal(TpHeaderSectionIds.HeaderCopyMaster, semantic.ParentId);
        Assert.Equal("Header copy / master", semantic.ParentLabel);
    }

    /// <summary>Verifies the verified NT51927 Header #3 continuation also survives copied-header projection.</summary>
    [Fact]
    public async Task ReplaceReportNamesNt51927CopiedHeaderThreeCrc()
    {
        var copiedFieldRange = new ByteRange(0x1E2EC, 4);
        CompositionRunRequest request = CreateNt51927CopiedHeaderSemanticRequest();
        var processor = new FakeExternalProcessor(externalRequest =>
        {
            byte[] output = externalRequest.InputBytes.ToArray();
            output.AsSpan(checked((int)copiedFieldRange.Start), checked((int)copiedFieldRange.Length)).Fill(0x7E);
            return ExternalProcessorResult.Success(output, [copiedFieldRange]);
        });
        var service = new CompositionRunService(
            new FakeArtifactReader(new Dictionary<string, byte[]>
            {
                ["reference-artifact"] = new byte[0x1E3C0],
            }),
            new FakeClock([FirstTimestamp, SecondTimestamp]),
            null,
            processor);

        CompositionRunResult result = await service.PreviewAsync(request, CancellationToken.None);

        OutputDifferenceSummary headerDifference = Assert.Single(result.Report.OutputDifferences);
        OutputDifferenceSemantic semantic = Assert.IsType<OutputDifferenceSemantic>(headerDifference.Semantic);
        Assert.Equal("nt51927-header:header-3-ilm-crc", semantic.SubjectId);
        Assert.Equal("ILM CRC 3", semantic.SubjectLabel);
        Assert.Equal(
            "Expected: postbuild refreshed ILM CRC 3 and copied it to Header copy / master.",
            semantic.Explanation);
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

    /// <summary>Verifies preview approval binds the semantic sections used to explain postbuild writes.</summary>
    [Fact]
    public async Task PreviewTokenChangesWhenWriteSectionProvenanceChanges()
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
            CreateStagedSourceExternalProcessorRequest(
                writeRangeSections:
                [new ExternalProcessorWriteRangeSection("header-a", new ByteRange(0, 2), new ByteRange(0, 2))]),
            CancellationToken.None);
        CompositionRunResult second = await service.PreviewAsync(
            CreateStagedSourceExternalProcessorRequest(
                writeRangeSections:
                [new ExternalProcessorWriteRangeSection("header-b", new ByteRange(0, 2), new ByteRange(2, 2))]),
            CancellationToken.None);

        Assert.Equal(first.OutputBytes.ToArray(), second.OutputBytes.ToArray());
        Assert.NotEqual(first.PreviewToken, second.PreviewToken);
    }

    /// <summary>Verifies external processor failures are returned as structured run issues.</summary>
    [Fact]
    public async Task PreviewPropagatesExternalProcessorFailure()
    {
        var processor = new FakeExternalProcessor(_ => ExternalProcessorResult.Failed(
            [
                new CompositionIssue("external-tool.process.failed", "fake processor failed", "run-crc"),
            ],
            [
                new ExternalProcessInvocation(
                    "C:\\tools\\Combiner.exe",
                    "C:\\staging\\run-external.run-crc",
                    ["CRC_Enable", "C:\\staging\\run-external.run-crc\\output\\firmware.bin"]),
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
        OperationRunSummary operation = Assert.Single(result.Report.Operations);
        Assert.Equal(OperationRunStatus.Failed, operation.Status);
        _ = Assert.Single(operation.ExecutedCommands);
        Assert.Empty(result.OutputBytes.ToArray());
    }

}
