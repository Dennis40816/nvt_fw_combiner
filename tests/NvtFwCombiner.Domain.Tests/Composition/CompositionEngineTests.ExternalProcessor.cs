using NvtFwCombiner.Domain.Composition;

namespace NvtFwCombiner.Domain.Tests.Composition;

public sealed partial class CompositionEngineTests
{
    /// <summary>Verifies external processor operations use the engine hook and produce normal mutation trace.</summary>
    [Fact]
    public async Task ExternalProcessorOperationMutatesThroughHook()
    {
        CompositionPlan plan = CreateBlankPlan(
            4,
            CreateExternalOperation());

        CompositionExecutionResult result = await CompositionEngine.ExecuteAsync(
            plan,
            EmptyInput(),
            (operation, inputBytes, stagedSources, _) =>
            {
                Assert.Equal("run-crc", operation.OperationId);
                Assert.Equal([0xFF, 0xFF, 0xFF, 0xFF], inputBytes.ToArray());
                Assert.Empty(stagedSources);
                byte[] output = inputBytes.ToArray();
                output[2] = 0x44;
                return ValueTask.FromResult(CompositionExternalProcessorResult.Success(output));
            },
            CancellationToken.None);

        Assert.Equal(CompositionExecutionStatus.Succeeded, result.Status);
        Assert.Equal([0xFF, 0xFF, 0x44, 0xFF], result.OutputBytes.ToArray());
        MutationRecord mutation = Assert.Single(result.Mutations);
        Assert.Equal(CompositionOperationKind.RunExternalProcessor, mutation.OperationKind);
        Assert.Equal([new ByteRange(2, 1)], mutation.ChangedRanges);
    }

    /// <summary>Verifies staged source bytes reach the external hook without first changing the target image.</summary>
    [Fact]
    public async Task ExternalProcessorReceivesStagedSourcesWithoutHostPrePaste()
    {
        AddressSpace[] addressSpaces =
        [
            new("reference-base", 4, AddressSpaceMutability.Immutable),
            new("ctrlram-input", 2, AddressSpaceMutability.Immutable, inputOversizePolicy: InputOversizePolicy.TruncateWithWarning),
            new("output-image", 4, AddressSpaceMutability.Mutable),
        ];
        var plan = new CompositionPlan(
            ImageInitialization.Reference("output-image", "reference-base", 4),
            addressSpaces,
            [
                CompositionOperation.RunExternalProcessor(
                    "run-postbuild",
                    10,
                    "output-image",
                    new ByteRange(0, 4),
                    new ExternalProcessorInvocation(
                        "processor-v1",
                        "tool-v1",
                        [new ByteRange(0, 4)],
                        [new ByteRange(1, 2)],
                        [
                            new ExternalProcessorStagedSourceBinding(
                                "ctrlram-input",
                                new ByteRange(0, 2),
                                new ByteRange(1, 2)),
                        ]),
                    OverlapPolicy.ReplaceExisting,
                    "run combiner pasteback"),
            ]);
        var input = new CompositionExecutionInput(new Dictionary<string, byte[]>
        {
            ["reference-base"] = [0x10, 0x20, 0x30, 0x40],
            ["ctrlram-input"] = [0xAA, 0xBB, 0xCC],
        });

        CompositionExecutionResult result = await CompositionEngine.ExecuteAsync(
            plan,
            input,
            (_, inputBytes, stagedSources, _) =>
            {
                Assert.Equal([0x10, 0x20, 0x30, 0x40], inputBytes.ToArray());
                ExternalProcessorStagedSource stagedSource = Assert.Single(stagedSources);
                Assert.Equal(new ByteRange(1, 2), stagedSource.FirmwareRange);
                Assert.Equal([0xAA, 0xBB], stagedSource.Bytes.ToArray());
                byte[] output = inputBytes.ToArray();
                stagedSource.Bytes.CopyTo(output.AsMemory((int)stagedSource.FirmwareRange.Start));
                return ValueTask.FromResult(CompositionExternalProcessorResult.Success(output));
            },
            CancellationToken.None);

        Assert.Equal(CompositionExecutionStatus.Succeeded, result.Status);
        Assert.Equal([0x10, 0xAA, 0xBB, 0x40], result.OutputBytes.ToArray());
        Assert.Contains(result.Issues, issue => issue.Code == CompositionIssueCodes.InputAddressSpaceTruncated);
    }

    /// <summary>Verifies truncation diagnostics remain visible when a later processor fails.</summary>
    [Fact]
    public async Task ExternalProcessorFailureKeepsPriorTruncationIssue()
    {
        AddressSpace[] addressSpaces =
        [
            new("reference-base", 4, AddressSpaceMutability.Immutable),
            new("ctrlram-input", 2, AddressSpaceMutability.Immutable, inputOversizePolicy: InputOversizePolicy.TruncateWithWarning),
            new("output-image", 4, AddressSpaceMutability.Mutable),
        ];
        var plan = new CompositionPlan(
            ImageInitialization.Reference("output-image", "reference-base", 4),
            addressSpaces,
            [
                CompositionOperation.ReplaceRange(
                    "replace-ctrlram",
                    10,
                    "ctrlram-input",
                    new ByteRange(0, 2),
                    "output-image",
                    new ByteRange(1, 2),
                    OverlapPolicy.Reject,
                    "replace ctrlram"),
                CompositionOperation.RunExternalProcessor(
                    "run-crc",
                    20,
                    "output-image",
                    new ByteRange(0, 4),
                    CreateExternalInvocation(writeRange: new ByteRange(3, 1)),
                    OverlapPolicy.ReplaceExisting,
                    "run crc"),
            ]);
        var input = new CompositionExecutionInput(new Dictionary<string, byte[]>
        {
            ["reference-base"] = [0, 0, 0, 0],
            ["ctrlram-input"] = [0xAA, 0xBB, 0xCC],
        });

        CompositionExecutionResult result = await CompositionEngine.ExecuteAsync(
            plan,
            input,
            (_, _, _, _) => ValueTask.FromResult(CompositionExternalProcessorResult.Failed([
                new CompositionIssue("external-tool.process.failed", "processor failed", "run-crc"),
            ])),
            CancellationToken.None);

        Assert.Equal(CompositionExecutionStatus.Failed, result.Status);
        Assert.Contains(result.Issues, issue => issue.Code == CompositionIssueCodes.InputAddressSpaceTruncated);
        Assert.Contains(result.Issues, issue => issue.Code == "external-tool.process.failed");
    }

    /// <summary>Verifies external processor operations fail closed when no adapter hook is supplied.</summary>
    [Fact]
    public void ExternalProcessorOperationRequiresHook()
    {
        CompositionPlan plan = CreateBlankPlan(
            4,
            CreateExternalOperation());

        CompositionExecutionResult result = CompositionEngine.Execute(plan, EmptyInput());

        Assert.Equal(CompositionExecutionStatus.Failed, result.Status);
        CompositionIssue issue = Assert.Single(result.Issues);
        Assert.Equal(CompositionIssueCodes.ExecutionExternalProcessorUnavailable, issue.Code);
        Assert.Equal("run-crc", issue.OperationId);
    }

    /// <summary>Verifies processor write authority must stay inside the staged target range.</summary>
    [Fact]
    public void ExternalProcessorAllowedWriteRangeMustStayInsideTargetRange()
    {
        ExternalProcessorInvocation invocation = CreateExternalInvocation(writeRange: new ByteRange(3, 2));

        _ = Assert.Throws<ArgumentOutOfRangeException>(() => CreateBlankPlan(
            4,
            CompositionOperation.RunExternalProcessor(
                "run-crc",
                10,
                "output-image",
                new ByteRange(0, 4),
                invocation,
                OverlapPolicy.Reject,
                "run approved fake processor")));
    }

    /// <summary>Verifies staged external processors always receive the full target image coordinate space.</summary>
    [Fact]
    public void ExternalProcessorTargetRangeMustCoverFullTargetSpace()
    {
        ExternalProcessorInvocation invocation = CreateExternalInvocation(writeRange: new ByteRange(1, 1));

        _ = Assert.Throws<ArgumentException>(() => CreateBlankPlan(
            4,
            CompositionOperation.RunExternalProcessor(
                "run-crc",
                10,
                "output-image",
                new ByteRange(1, 2),
                invocation,
                OverlapPolicy.Reject,
                "run approved fake processor")));
    }

    private static CompositionOperation CreateExternalOperation()
    {
        return CompositionOperation.RunExternalProcessor(
            "run-crc",
            10,
            "output-image",
            new ByteRange(0, 4),
            CreateExternalInvocation(),
            OverlapPolicy.Reject,
            "run approved fake processor");
    }

    private static ExternalProcessorInvocation CreateExternalInvocation(ByteRange? writeRange = null)
    {
        return new ExternalProcessorInvocation(
            "processor-v1",
            "tool-v1",
            [new ByteRange(0, 4)],
            [writeRange ?? new ByteRange(2, 1)]);
    }
}
