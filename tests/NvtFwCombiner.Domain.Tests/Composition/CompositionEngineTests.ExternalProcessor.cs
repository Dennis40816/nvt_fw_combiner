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
            (operation, inputBytes, stagedSources, stagedArtifacts, _) =>
            {
                Assert.Equal("run-crc", operation.OperationId);
                Assert.Equal([0xFF, 0xFF, 0xFF, 0xFF], inputBytes.ToArray());
                Assert.Empty(stagedSources);
                Assert.Empty(stagedArtifacts);
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

    /// <summary>Verifies a declared external-processor postcondition rejects the staged output before it is imported.</summary>
    [Fact]
    public async Task ExternalProcessorOutputAssertionFailsBeforeImport()
    {
        CompositionPlan plan = CreateBlankPlan(
            4,
            CompositionOperation.RunExternalProcessor(
                "run-crc",
                10,
                "output-image",
                new ByteRange(0, 4),
                new ExternalProcessorInvocation(
                    "processor-v1",
                    "tool-v1",
                    [new ByteRange(0, 4)],
                    [new ByteRange(2, 1)],
                    outputAssertions: [new ExternalProcessorOutputAssertion(new ByteRange(2, 1), [0x44])]),
                OverlapPolicy.Reject,
                "run approved fake processor"));

        CompositionExecutionResult result = await CompositionEngine.ExecuteAsync(
            plan,
            EmptyInput(),
            (_, inputBytes, _, _, _) =>
            {
                byte[] output = inputBytes.ToArray();
                output[2] = 0x43;
                return ValueTask.FromResult(CompositionExternalProcessorResult.Success(output));
            },
            CancellationToken.None);

        Assert.Equal(CompositionExecutionStatus.Failed, result.Status);
        CompositionIssue issue = Assert.Single(result.Issues);
        Assert.Equal(CompositionIssueCodes.ExecutionExternalProcessorPostconditionFailed, issue.Code);
        Assert.Equal("run-crc", issue.OperationId);
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
            (_, inputBytes, stagedSources, stagedArtifacts, _) =>
            {
                Assert.Equal([0x10, 0x20, 0x30, 0x40], inputBytes.ToArray());
                Assert.Empty(stagedArtifacts);
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
            (_, _, _, _, _) => ValueTask.FromResult(CompositionExternalProcessorResult.Failed([
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

    /// <summary>Verifies output assertions cannot inspect bytes outside the processor target image.</summary>
    [Fact]
    public void ExternalProcessorOutputAssertionMustStayInsideTargetRange()
    {
        var invocation = new ExternalProcessorInvocation(
            "processor-v1",
            "tool-v1",
            [new ByteRange(0, 4)],
            [new ByteRange(2, 1)],
            outputAssertions: [new ExternalProcessorOutputAssertion(new ByteRange(4, 1), [0x44])]);

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

    /// <summary>Verifies staged external processors can only target a zero-based image prefix.</summary>
    [Fact]
    public void ExternalProcessorTargetRangeMustStartAtZero()
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

    /// <summary>Verifies prefix processing imports only the transformed prefix and preserves the container tail.</summary>
    [Fact]
    public async Task ExternalProcessorCanTransformPrefixWithoutChangingContainerTailAsync()
    {
        CompositionPlan plan = CreateBlankPlan(
            4,
            CompositionOperation.RunExternalProcessor(
                "run-crc",
                10,
                "output-image",
                new ByteRange(0, 3),
                new ExternalProcessorInvocation(
                    "processor-v1",
                    "tool-v1",
                    [new ByteRange(0, 3)],
                    [new ByteRange(1, 1)]),
                OverlapPolicy.Reject,
                "run approved prefix processor"));

        CompositionExecutionResult result = await CompositionEngine.ExecuteAsync(
            plan,
            EmptyInput(),
            (_, inputBytes, _, _, _) =>
            {
                Assert.Equal([0xFF, 0xFF, 0xFF], inputBytes.ToArray());
                byte[] output = inputBytes.ToArray();
                output[1] = 0x44;
                return ValueTask.FromResult(CompositionExternalProcessorResult.Success(output));
            },
            CancellationToken.None);

        Assert.Equal(CompositionExecutionStatus.Succeeded, result.Status);
        Assert.Equal([0xFF, 0x44, 0xFF, 0xFF], result.OutputBytes.ToArray());
        Assert.Equal(new ByteRange(0, 3), Assert.Single(result.Mutations).TargetRange);
    }

    /// <summary>Verifies an external processor receives snapshots of initialized mutable work buffers as named artifacts.</summary>
    [Fact]
    public async Task ExternalProcessorReceivesNamedSnapshotsOfMutableWorkBuffers()
    {
        AddressSpace[] addressSpaces =
        [
            new("output-image", 4, AddressSpaceMutability.Mutable),
            new("a-work", 2, AddressSpaceMutability.Mutable),
            new("b-work", 2, AddressSpaceMutability.Mutable),
        ];
        var plan = new CompositionPlan(
            [
                ImageInitialization.Blank("a-work", 2, 0),
                ImageInitialization.Blank("b-work", 2, 0),
                ImageInitialization.Blank("output-image", 4, 0),
            ],
            "output-image",
            addressSpaces,
            [
                CompositionOperation.FillRange(
                    "fill-a",
                    10,
                    "a-work",
                    new ByteRange(0, 2),
                    0xA1,
                    OverlapPolicy.Reject,
                    "stage A work buffer"),
                CompositionOperation.FillRange(
                    "fill-b",
                    20,
                    "b-work",
                    new ByteRange(0, 2),
                    0xB2,
                    OverlapPolicy.Reject,
                    "stage B work buffer"),
                CompositionOperation.RunExternalProcessor(
                    "combine-banks",
                    30,
                    "output-image",
                    new ByteRange(0, 4),
                    new ExternalProcessorInvocation(
                        "combiner-v1",
                        "legacy-combiner-1.13.0",
                        [new ByteRange(0, 4)],
                        [new ByteRange(0, 4)],
                        stagedArtifactBindings:
                        [
                            new ExternalProcessorStagedArtifactBinding("a-bank", "a-work", new ByteRange(0, 2)),
                            new ExternalProcessorStagedArtifactBinding("b-bank", "b-work", new ByteRange(0, 2)),
                        ]),
                    OverlapPolicy.Reject,
                    "combine staged banks"),
            ]);

        CompositionExecutionResult result = await CompositionEngine.ExecuteAsync(
            plan,
            EmptyInput(),
            (_, inputBytes, stagedSources, stagedArtifacts, _) =>
            {
                Assert.Empty(stagedSources);
                Assert.Equal(
                    ["a-bank", "b-bank"],
                    stagedArtifacts.Select(static artifact => artifact.ArtifactId));
                Assert.Equal([0xA1, 0xA1], stagedArtifacts[0].Bytes.ToArray());
                Assert.Equal([0xB2, 0xB2], stagedArtifacts[1].Bytes.ToArray());
                Assert.Equal([0, 0, 0, 0], inputBytes.ToArray());
                return ValueTask.FromResult(CompositionExternalProcessorResult.Success(new byte[] { 0xA1, 0xA1, 0xB2, 0xB2 }));
            },
            CancellationToken.None);

        Assert.Equal(CompositionExecutionStatus.Succeeded, result.Status);
        Assert.Equal([0xA1, 0xA1, 0xB2, 0xB2], result.OutputBytes.ToArray());
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
            [writeRange ?? new ByteRange(2, 1)],
            outputAssertions: [new ExternalProcessorOutputAssertion(new ByteRange(2, 1), [0x44])]);
    }
}
