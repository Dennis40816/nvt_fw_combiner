using NvtFwCombiner.Domain.Composition;

namespace NvtFwCombiner.Domain.Tests.Composition;

/// <summary>Tests shared composition engine execution semantics.</summary>
public sealed partial class CompositionEngineTests
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

    /// <summary>Verifies profile-declared allowed input lengths reject unexpected source artifact sizes.</summary>
    [Fact]
    public void InputOutsideAllowedLengthSetFailsClosed()
    {
        CompositionPlan plan = CreateBlankPlan(
            4,
            new AddressSpace(
                "input",
                4,
                AddressSpaceMutability.Immutable,
                inputPaddingByte: 0xFF,
                allowedInputLengths: [2, 4]),
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
            ["input"] = [0x11, 0x22, 0x33],
        });

        CompositionExecutionResult result = CompositionEngine.Execute(plan, input);

        Assert.Equal(CompositionExecutionStatus.Failed, result.Status);
        CompositionIssue issue = Assert.Single(result.Issues);
        Assert.Equal("input.address-space.length-mismatch", issue.Code);
        Assert.Contains("0x2, 0x4", issue.Message, StringComparison.Ordinal);
        Assert.Contains("actual length is 3 bytes", issue.Message, StringComparison.Ordinal);
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
        Assert.Contains("actual 3 bytes", issue.Message, StringComparison.Ordinal);
        Assert.Contains("declared 2 bytes", issue.Message, StringComparison.Ordinal);
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
        Assert.Equal(CompositionIssueSeverity.Warning, issue.Severity);
        Assert.Equal("ctrlram-input", issue.OperationId);
        Assert.Contains("from 4 to 2 bytes", issue.Message, StringComparison.Ordinal);
    }

    /// <summary>Verifies report issue severity is constrained to the contract vocabulary.</summary>
    [Fact]
    public void CompositionIssueRejectsUnknownSeverity()
    {
        ArgumentException exception = Assert.Throws<ArgumentException>(() => new CompositionIssue(
            "issue.test",
            "test message",
            severity: "fatal"));

        Assert.Contains("Unsupported issue severity", exception.Message, StringComparison.Ordinal);
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

}
