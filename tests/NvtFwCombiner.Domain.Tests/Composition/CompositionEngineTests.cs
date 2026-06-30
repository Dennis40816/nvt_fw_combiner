using NvtFwCombiner.Domain.Composition;

namespace NvtFwCombiner.Domain.Tests.Composition;

/// <summary>Tests shared composition engine execution semantics.</summary>
public sealed class CompositionEngineTests
{
    /// <summary>Verifies blank initialization fills the output before copy operations execute.</summary>
    [Fact]
    public void BlankInitializationFillsOutputBeforeCopyRange()
    {
        CompositionPlan plan = CreateBlankPlan(
            6,
            new AddressSpace("input", 4, AddressSpaceMutability.Immutable),
            CompositionOperation.CopyRange(
                "copy-input",
                10,
                "input",
                new ByteRange(1, 2),
                "output-image",
                new ByteRange(2, 2),
                OverlapPolicy.Reject,
                "copy selected source bytes"));
        var input = new CompositionExecutionInput(new Dictionary<string, byte[]>
        {
            ["input"] = [10, 20, 30, 40],
        });

        CompositionExecutionResult result = CompositionEngine.Execute(plan, input);

        Assert.Equal(CompositionExecutionStatus.Succeeded, result.Status);
        Assert.Equal([0xFF, 0xFF, 20, 30, 0xFF, 0xFF], result.OutputBytes.ToArray());
        MutationRecord mutation = Assert.Single(result.Mutations);
        Assert.Equal([new ByteRange(2, 2)], mutation.ChangedRanges);
    }

    /// <summary>Verifies reference initialization clones the base image and leaves caller-owned input bytes unchanged.</summary>
    [Fact]
    public void ReferenceInitializationClonesBaseBeforeReplaceRange()
    {
        byte[] reference = [1, 2, 3, 4];
        CompositionPlan plan = CreateReferencePlan(
            4,
            new AddressSpace("replacement", 2, AddressSpaceMutability.Immutable),
            CompositionOperation.ReplaceRange(
                "replace-range",
                10,
                "replacement",
                new ByteRange(0, 2),
                "output-image",
                new ByteRange(1, 2),
                OverlapPolicy.Reject,
                "replace declared range"));
        var input = new CompositionExecutionInput(new Dictionary<string, byte[]>
        {
            ["reference-base"] = reference,
            ["replacement"] = [9, 8],
        });

        CompositionExecutionResult result = CompositionEngine.Execute(plan, input);

        Assert.Equal(CompositionExecutionStatus.Succeeded, result.Status);
        Assert.Equal([1, 9, 8, 4], result.OutputBytes.ToArray());
        Assert.Equal([1, 2, 3, 4], reference);
    }

    /// <summary>Verifies profile-declared input padding extends shorter inputs before copy operations read them.</summary>
    [Fact]
    public void ShortInputWithDeclaredPaddingByteIsPaddedBeforeCopyRange()
    {
        CompositionPlan plan = CreateBlankPlan(
            4,
            new AddressSpace("input", 4, AddressSpaceMutability.Immutable, inputPaddingByte: 0xFF),
            CompositionOperation.CopyRange(
                "copy-input",
                10,
                "input",
                new ByteRange(0, 4),
                "output-image",
                new ByteRange(0, 4),
                OverlapPolicy.Reject,
                "copy padded source"));
        var input = new CompositionExecutionInput(new Dictionary<string, byte[]>
        {
            ["input"] = [0x11, 0x22],
        });

        CompositionExecutionResult result = CompositionEngine.Execute(plan, input);

        Assert.Equal(CompositionExecutionStatus.Succeeded, result.Status);
        Assert.Equal([0x11, 0x22, 0xFF, 0xFF], result.OutputBytes.ToArray());
    }

    /// <summary>Verifies longer-than-declared inputs remain rejected even when a padding byte is declared.</summary>
    [Fact]
    public void InputLongerThanDeclaredLengthFailsClosed()
    {
        CompositionPlan plan = CreateBlankPlan(
            2,
            new AddressSpace("input", 2, AddressSpaceMutability.Immutable, inputPaddingByte: 0xFF),
            CompositionOperation.CopyRange(
                "copy-input",
                10,
                "input",
                new ByteRange(0, 2),
                "output-image",
                new ByteRange(0, 2),
                OverlapPolicy.Reject,
                "copy source"));
        var input = new CompositionExecutionInput(new Dictionary<string, byte[]>
        {
            ["input"] = [0x11, 0x22, 0x33],
        });

        CompositionExecutionResult result = CompositionEngine.Execute(plan, input);

        Assert.Equal(CompositionExecutionStatus.Failed, result.Status);
        CompositionIssue issue = Assert.Single(result.Issues);
        Assert.Equal("input.address-space.length-mismatch", issue.Code);
        Assert.Contains("exceed declared length", issue.Message, StringComparison.Ordinal);
    }

    /// <summary>Verifies CtrlRAM replace inputs may truncate oversized source bytes with a run diagnostic.</summary>
    [Fact]
    public void CtrlRamInputWithTruncationPolicyTruncatesBeforeReplaceRange()
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
            ],
            CreateCtrlRamReplaceProvenance());
        var input = new CompositionExecutionInput(new Dictionary<string, byte[]>
        {
            ["reference-base"] = [0, 0, 0, 0],
            ["ctrlram-input"] = [0xAA, 0xBB, 0xCC, 0xDD],
        });

        CompositionExecutionResult result = CompositionEngine.Execute(plan, input);

        Assert.Equal(CompositionExecutionStatus.Succeeded, result.Status);
        Assert.Equal([0, 0xAA, 0xBB, 0], result.OutputBytes.ToArray());
        CompositionIssue issue = Assert.Single(result.Issues);
        Assert.Equal("input.address-space.truncated", issue.Code);
        Assert.Equal("ctrlram-input", issue.OperationId);
        Assert.Contains("from 4 to 2 bytes", issue.Message, StringComparison.Ordinal);
    }

    /// <summary>Verifies caller-supplied initialized target bytes are ignored before padding normalization.</summary>
    [Fact]
    public void InitializedTargetInputBytesAreIgnoredBeforePadding()
    {
        CompositionPlan plan = CreateBlankPlan(3);
        var input = new CompositionExecutionInput(new Dictionary<string, byte[]>
        {
            ["output-image"] = [0x11],
        });

        CompositionExecutionResult result = CompositionEngine.Execute(plan, input);

        Assert.Equal(CompositionExecutionStatus.Succeeded, result.Status);
        Assert.Equal([0xFF, 0xFF, 0xFF], result.OutputBytes.ToArray());
    }

    /// <summary>Verifies padding a source beyond the runtime array limit returns a structured issue.</summary>
    [Fact]
    public void OversizedPaddedInputFailsWithStructuredIssue()
    {
        var plan = new CompositionPlan(
            ImageInitialization.Blank("output-image", 1, 0xFF),
            [
                new AddressSpace("output-image", 1, AddressSpaceMutability.Mutable),
                new AddressSpace("oversized-input", (long)int.MaxValue + 1, AddressSpaceMutability.Immutable, inputPaddingByte: 0xFF),
            ],
            []);
        var input = new CompositionExecutionInput(new Dictionary<string, byte[]>
        {
            ["oversized-input"] = [0x11],
        });

        CompositionExecutionResult result = CompositionEngine.Execute(plan, input);

        Assert.Equal(CompositionExecutionStatus.Failed, result.Status);
        CompositionIssue issue = Assert.Single(result.Issues);
        Assert.Equal("execution.capacity.unsupported", issue.Code);
    }

    /// <summary>Verifies patch-scalar writes exactly the supplied bytes without implicit endian conversion.</summary>
    [Fact]
    public void PatchScalarWritesExactProfileBytes()
    {
        CompositionPlan plan = CreateBlankPlan(
            4,
            CompositionOperation.PatchScalar(
                "patch-scalar",
                10,
                "output-image",
                new ByteRange(1, 2),
                [0xAA, 0xBB],
                OverlapPolicy.Reject,
                "write explicit scalar bytes"));

        CompositionExecutionResult result = CompositionEngine.Execute(plan, EmptyInput());

        Assert.Equal(CompositionExecutionStatus.Succeeded, result.Status);
        Assert.Equal([0xFF, 0xAA, 0xBB, 0xFF], result.OutputBytes.ToArray());
    }

    /// <summary>Verifies mutation hashes use lowercase hex for report contract compatibility.</summary>
    [Fact]
    public void MutationHashesUseLowercaseSha256()
    {
        CompositionPlan plan = CreateBlankPlan(
            1,
            CompositionOperation.PatchScalar(
                "patch-scalar",
                10,
                "output-image",
                new ByteRange(0, 1),
                [0xFF],
                OverlapPolicy.Reject,
                "force non-decimal hash nybbles"));

        CompositionExecutionResult result = CompositionEngine.Execute(plan, EmptyInput());

        MutationRecord mutation = Assert.Single(result.Mutations);
        Assert.Equal(mutation.BeforeSha256, mutation.BeforeSha256.ToLowerInvariant());
        Assert.Equal(mutation.AfterSha256, mutation.AfterSha256.ToLowerInvariant());
    }

    /// <summary>Verifies missing immutable source bytes return a structured issue instead of a partial output.</summary>
    [Fact]
    public void MissingInputAddressSpaceFailsClosed()
    {
        CompositionPlan plan = CreateBlankPlan(
            4,
            new AddressSpace("input", 2, AddressSpaceMutability.Immutable),
            CompositionOperation.CopyRange(
                "copy-input",
                10,
                "input",
                new ByteRange(0, 2),
                "output-image",
                new ByteRange(0, 2),
                OverlapPolicy.Reject,
                "copy source"));

        CompositionExecutionResult result = CompositionEngine.Execute(plan, EmptyInput());

        Assert.Equal(CompositionExecutionStatus.Failed, result.Status);
        CompositionIssue issue = Assert.Single(result.Issues);
        Assert.Equal("input.address-space.missing", issue.Code);
        Assert.Empty(result.OutputBytes.ToArray());
    }

    /// <summary>Verifies mutable address spaces outside initialization must be explicitly seeded.</summary>
    [Fact]
    public void MissingMutableTargetSeedFailsClosed()
    {
        AddressSpace[] addressSpaces =
        [
            new("output-image", 4, AddressSpaceMutability.Mutable),
            new("scratch", 4, AddressSpaceMutability.Mutable),
        ];
        var plan = new CompositionPlan(
            ImageInitialization.Blank("output-image", 4, 0),
            addressSpaces,
            [
                CompositionOperation.FillRange(
                    "fill-scratch",
                    10,
                    "scratch",
                    new ByteRange(0, 2),
                    0x11,
                    OverlapPolicy.Reject,
                    "fill scratch"),
            ]);

        CompositionExecutionResult result = CompositionEngine.Execute(plan, EmptyInput());

        Assert.Equal(CompositionExecutionStatus.Failed, result.Status);
        CompositionIssue issue = Assert.Single(result.Issues);
        Assert.Equal("input.mutable-address-space.missing", issue.Code);
    }

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
            (operation, inputBytes, _) =>
            {
                Assert.Equal("run-crc", operation.OperationId);
                Assert.Equal([0xFF, 0xFF, 0xFF, 0xFF], inputBytes.ToArray());
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
            ],
            CreateCtrlRamReplaceProvenance());
        var input = new CompositionExecutionInput(new Dictionary<string, byte[]>
        {
            ["reference-base"] = [0, 0, 0, 0],
            ["ctrlram-input"] = [0xAA, 0xBB, 0xCC],
        });

        CompositionExecutionResult result = await CompositionEngine.ExecuteAsync(
            plan,
            input,
            (_, _, _) => ValueTask.FromResult(CompositionExternalProcessorResult.Failed([
                new CompositionIssue("external-tool.process.failed", "processor failed", "run-crc"),
            ])),
            CancellationToken.None);

        Assert.Equal(CompositionExecutionStatus.Failed, result.Status);
        Assert.Contains(result.Issues, issue => issue.Code == "input.address-space.truncated");
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
        Assert.Equal("execution.external-processor.unavailable", issue.Code);
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

    private static CompositionExecutionInput EmptyInput()
    {
        return new CompositionExecutionInput(new Dictionary<string, byte[]>());
    }

    private static CompositionPlan CreateBlankPlan(
        long capacity,
        params object[] declarations)
    {
        List<AddressSpace> addressSpaces =
        [
            new("output-image", capacity, AddressSpaceMutability.Mutable),
        ];
        List<CompositionOperation> operations = [];
        foreach (object declaration in declarations)
        {
            if (declaration is AddressSpace addressSpace)
            {
                addressSpaces.Add(addressSpace);
            }
            else if (declaration is CompositionOperation operation)
            {
                operations.Add(operation);
            }
        }

        return new CompositionPlan(ImageInitialization.Blank("output-image", capacity, 0xFF), addressSpaces, operations);
    }

    private static CompositionPlan CreateReferencePlan(
        long capacity,
        AddressSpace sourceSpace,
        CompositionOperation operation)
    {
        AddressSpace[] addressSpaces =
        [
            new("reference-base", capacity, AddressSpaceMutability.Immutable),
            new("output-image", capacity, AddressSpaceMutability.Mutable),
            sourceSpace,
        ];
        return new CompositionPlan(
            ImageInitialization.Reference("output-image", "reference-base", capacity),
            addressSpaces,
            [operation]);
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

    private static CompositionPlanProvenance CreateCtrlRamReplaceProvenance()
    {
        return new CompositionPlanProvenance(
            "ctrlram-replace-profile",
            "1.0.0",
            "NT-SYNTHETIC",
            "ctrlram-replace",
            "ctrlram-replace",
            CompositionKind.Replace);
    }
}
