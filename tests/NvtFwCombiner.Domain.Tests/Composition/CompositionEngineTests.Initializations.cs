using NvtFwCombiner.Domain.Composition;

namespace NvtFwCombiner.Domain.Tests.Composition;

public sealed partial class CompositionEngineTests
{
    /// <summary>Verifies clone work buffers copy immutable input without mutating caller bytes.</summary>
    [Fact]
    public void ClonedWorkBufferUsesImmutableSourceSnapshot()
    {
        byte[] source = [0x12, 0x34];
        var plan = new CompositionPlan(
            [
                ImageInitialization.Blank("output-image", 2, 0),
                ImageInitialization.Reference("scratch", "source", 2),
            ],
            "output-image",
            [
                new AddressSpace("source", 2, AddressSpaceMutability.Immutable),
                new AddressSpace("output-image", 2, AddressSpaceMutability.Mutable),
                new AddressSpace("scratch", 2, AddressSpaceMutability.Mutable),
            ],
            [
                CompositionOperation.CopyRange(
                    "copy-scratch",
                    10,
                    "scratch",
                    new ByteRange(0, 2),
                    "output-image",
                    new ByteRange(0, 2),
                    OverlapPolicy.Reject,
                    "copy cloned scratch"),
            ]);
        var input = new CompositionExecutionInput(new Dictionary<string, byte[]>
        {
            ["source"] = source,
        });
        source.AsSpan().Fill(0xFF);

        CompositionExecutionResult result = CompositionEngine.Execute(plan, input);

        Assert.Equal(CompositionExecutionStatus.Succeeded, result.Status);
        Assert.Equal([0x12, 0x34], result.OutputBytes.ToArray());
        Assert.Equal([0xFF, 0xFF], source);
    }

    /// <summary>Verifies a work-buffer clone consumes only the normalized declared prefix and reports the outer tail.</summary>
    [Fact]
    public void ClonedWorkBufferUsesDeclaredPrefixSnapshot()
    {
        byte[] source = [0x12, 0x34, 0x99];
        var plan = new CompositionPlan(
            [
                ImageInitialization.Blank("output-image", 2, 0),
                ImageInitialization.Reference("scratch", "source", 2),
            ],
            "output-image",
            [
                new AddressSpace(
                    "source",
                    2,
                    AddressSpaceMutability.Immutable,
                    inputOversizePolicy: InputOversizePolicy.ExtractDeclaredRange,
                    expectedInputLengths: [2],
                    unexpectedInputLengthIssueCode: "INPUT_OUTER_LENGTH"),
                new AddressSpace("output-image", 2, AddressSpaceMutability.Mutable),
                new AddressSpace("scratch", 2, AddressSpaceMutability.Mutable),
            ],
            [
                CompositionOperation.CopyRange(
                    "copy-scratch",
                    10,
                    "scratch",
                    new ByteRange(0, 2),
                    "output-image",
                    new ByteRange(0, 2),
                    OverlapPolicy.Reject,
                    "copy declared prefix"),
            ]);

        CompositionExecutionResult result = CompositionEngine.Execute(
            plan,
            new CompositionExecutionInput(new Dictionary<string, byte[]> { ["source"] = source }));

        Assert.Equal(CompositionExecutionStatus.Succeeded, result.Status);
        Assert.Equal([0x12, 0x34], result.OutputBytes.ToArray());
        CompositionIssue warning = Assert.Single(result.Issues);
        Assert.Equal("INPUT_OUTER_LENGTH", warning.Code);
        Assert.Equal(CompositionIssueSeverity.Warning, warning.Severity);
        Assert.Equal([0x12, 0x34, 0x99], source);
    }

    /// <summary>Verifies a work-buffer clone can consume a checked source view while ignoring a non-authoritative tail.</summary>
    [Fact]
    public void ClonedWorkBufferUsesSourceViewSnapshot()
    {
        byte[] source = [0x12, 0x34, 0x99];
        var plan = new CompositionPlan(
            [
                ImageInitialization.Blank("output-image", 2, 0),
                ImageInitialization.Reference("scratch", "source", 2),
            ],
            "output-image",
            [
                new AddressSpace(
                    "source",
                    2,
                    AddressSpaceMutability.Immutable,
                    inputOversizePolicy: InputOversizePolicy.ExtractDeclaredRange),
                new AddressSpace("output-image", 2, AddressSpaceMutability.Mutable),
                new AddressSpace("scratch", 2, AddressSpaceMutability.Mutable),
            ],
            [
                CompositionOperation.CopyRange(
                    "copy-scratch",
                    10,
                    "scratch",
                    new ByteRange(0, 2),
                    "output-image",
                    new ByteRange(0, 2),
                    OverlapPolicy.Reject,
                    "copy source view"),
            ]);

        CompositionExecutionResult result = CompositionEngine.Execute(
            plan,
            new CompositionExecutionInput(new Dictionary<string, byte[]> { ["source"] = source }));

        Assert.Equal(CompositionExecutionStatus.Succeeded, result.Status);
        Assert.Equal([0x12, 0x34], result.OutputBytes.ToArray());
        Assert.Empty(result.Issues);
        Assert.Equal([0x12, 0x34, 0x99], source);
    }

    /// <summary>Verifies caller bytes cannot seed any engine-owned work buffer.</summary>
    [Fact]
    public void CallerSuppliedWorkBufferBytesAreRejected()
    {
        CompositionPlan plan = MultiBlankPlan("output-image");
        var input = new CompositionExecutionInput(new Dictionary<string, byte[]>
        {
            ["scratch"] = [0x99],
        });

        CompositionExecutionResult result = CompositionEngine.Execute(plan, input);

        Assert.Equal(CompositionExecutionStatus.Failed, result.Status);
        CompositionIssue issue = Assert.Single(result.Issues);
        Assert.Equal(CompositionIssueCodes.InputMutableAddressSpaceNotAllowed, issue.Code);
        Assert.Equal("scratch", issue.OperationId);
    }

    /// <summary>Verifies caller input cannot introduce an undeclared address space.</summary>
    [Fact]
    public void CallerSuppliedUnknownAddressSpaceIsRejected()
    {
        CompositionPlan plan = MultiBlankPlan("output-image");
        var input = new CompositionExecutionInput(new Dictionary<string, byte[]>
        {
            ["unknown"] = [0x99],
        });

        CompositionExecutionResult result = CompositionEngine.Execute(plan, input);

        Assert.Equal(CompositionExecutionStatus.Failed, result.Status);
        CompositionIssue issue = Assert.Single(result.Issues);
        Assert.Equal(CompositionIssueCodes.InputAddressSpaceUnknown, issue.Code);
        Assert.Equal("unknown", issue.OperationId);
    }

    /// <summary>Verifies the selected output need not be the first initializer in canonical order.</summary>
    [Fact]
    public void ExecuteReturnsExplicitlySelectedOutputSpace()
    {
        CompositionPlan plan = MultiBlankPlan("z-output");

        CompositionExecutionResult result = CompositionEngine.Execute(plan, EmptyInput());

        Assert.Equal(CompositionExecutionStatus.Succeeded, result.Status);
        Assert.Equal([0x22], result.OutputBytes.ToArray());
    }

    /// <summary>Verifies every mutable buffer capacity is checked before allocation.</summary>
    [Fact]
    public void OversizedWorkBufferFailsBeforeAllocation()
    {
        long unsupportedLength = (long)int.MaxValue + 1;
        var plan = new CompositionPlan(
            [
                ImageInitialization.Blank("output-image", 1, 0),
                ImageInitialization.Blank("scratch", unsupportedLength, 0),
            ],
            "output-image",
            [
                new AddressSpace("output-image", 1, AddressSpaceMutability.Mutable),
                new AddressSpace("scratch", unsupportedLength, AddressSpaceMutability.Mutable),
            ],
            []);

        CompositionExecutionResult result = CompositionEngine.Execute(plan, EmptyInput());

        Assert.Equal(CompositionExecutionStatus.Failed, result.Status);
        CompositionIssue issue = Assert.Single(result.Issues);
        Assert.Equal(CompositionIssueCodes.ExecutionCapacityUnsupported, issue.Code);
        Assert.Equal("scratch", issue.OperationId);
    }

    private static CompositionPlan MultiBlankPlan(string outputSpaceId)
    {
        return new CompositionPlan(
            [
                ImageInitialization.Blank("a-scratch", 1, 0x11),
                ImageInitialization.Blank("output-image", 1, 0x33),
                ImageInitialization.Blank("scratch", 1, 0x44),
                ImageInitialization.Blank("z-output", 1, 0x22),
            ],
            outputSpaceId,
            [
                new AddressSpace("a-scratch", 1, AddressSpaceMutability.Mutable),
                new AddressSpace("output-image", 1, AddressSpaceMutability.Mutable),
                new AddressSpace("scratch", 1, AddressSpaceMutability.Mutable),
                new AddressSpace("z-output", 1, AddressSpaceMutability.Mutable),
            ],
            []);
    }
}
