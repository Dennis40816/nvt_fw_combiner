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
        Assert.Equal(CompositionIssueCodes.InputAddressSpaceLengthMismatch, issue.Code);
        Assert.Contains("0x2, 0x4", issue.Message, StringComparison.Ordinal);
        Assert.Contains("actual length is 3 bytes", issue.Message, StringComparison.Ordinal);
    }

    /// <summary>Verifies one exact immutable input length rejects shorter and longer artifacts without normalization.</summary>
    [Theory]
    [InlineData(3)]
    [InlineData(5)]
    public void ExactImmutableInputLengthRejectsNonExactArtifacts(int artifactLength)
    {
        CompositionPlan plan = CreateBlankPlan(
            4,
            new AddressSpace("input", 4, AddressSpaceMutability.Immutable, allowedInputLengths: [4]),
            CompositionOperation.CopyRange(
                "copy-input",
                10,
                "input",
                new ByteRange(0, 4),
                "output-image",
                new ByteRange(0, 4),
                OverlapPolicy.Reject,
                "copy exact source"));
        var input = new CompositionExecutionInput(new Dictionary<string, byte[]>
        {
            ["input"] = new byte[artifactLength],
        });

        CompositionExecutionResult result = CompositionEngine.Execute(plan, input);

        Assert.Equal(CompositionExecutionStatus.Failed, result.Status);
        Assert.Equal(CompositionIssueCodes.InputAddressSpaceLengthMismatch, Assert.Single(result.Issues).Code);
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
        Assert.Equal(CompositionIssueCodes.InputAddressSpaceLengthMismatch, issue.Code);
        Assert.Contains("exceed declared length", issue.Message, StringComparison.Ordinal);
        Assert.Contains("actual 3 bytes", issue.Message, StringComparison.Ordinal);
        Assert.Contains("declared 2 bytes", issue.Message, StringComparison.Ordinal);
    }

    /// <summary>Verifies approved DP range extraction accepts a sufficient nonstandard artifact and reports a warning.</summary>
    [Fact]
    public void DeclaredRangeExtractionUsesOnlyRequiredBytesAndWarnsForUnexpectedLength()
    {
        CompositionPlan plan = CreateBlankPlan(
            2,
            new AddressSpace(
                "input",
                2,
                AddressSpaceMutability.Immutable,
                inputOversizePolicy: InputOversizePolicy.ExtractDeclaredRange,
                expectedInputLengths: [4]),
            CompositionOperation.CopyRange(
                "copy-input",
                10,
                "input",
                new ByteRange(0, 2),
                "output-image",
                new ByteRange(0, 2),
                OverlapPolicy.Reject,
                "copy declared source range"));
        var input = new CompositionExecutionInput(new Dictionary<string, byte[]>
        {
            ["input"] = [0x11, 0x22, 0x33],
        });

        CompositionExecutionResult result = CompositionEngine.Execute(plan, input);

        Assert.Equal(CompositionExecutionStatus.Succeeded, result.Status);
        Assert.Equal([0x11, 0x22], result.OutputBytes.ToArray());
        CompositionIssue issue = Assert.Single(result.Issues);
        Assert.Equal(CompositionIssueCodes.InputAddressSpaceLengthUnexpected, issue.Code);
        Assert.Equal(CompositionIssueSeverity.Warning, issue.Severity);
        Assert.Contains("unexpected length 3 bytes", issue.Message, StringComparison.Ordinal);
    }

    /// <summary>Verifies declared-range extraction accepts an expected outer container and rejects a source shorter than its declared span.</summary>
    [Fact]
    public void DeclaredRangeExtractionAcceptsExpectedContainerAndRejectsTooShortInput()
    {
        CompositionPlan plan = CreateBlankPlan(
            2,
            new AddressSpace(
                "input",
                2,
                AddressSpaceMutability.Immutable,
                inputOversizePolicy: InputOversizePolicy.ExtractDeclaredRange,
                expectedInputLengths: [4]),
            CompositionOperation.CopyRange(
                "copy-input",
                10,
                "input",
                new ByteRange(0, 2),
                "output-image",
                new ByteRange(0, 2),
                OverlapPolicy.Reject,
                "copy declared source range"));

        CompositionExecutionResult expected = CompositionEngine.Execute(
            plan,
            new CompositionExecutionInput(new Dictionary<string, byte[]> { ["input"] = [0x11, 0x22, 0x33, 0x44] }));
        CompositionExecutionResult tooShort = CompositionEngine.Execute(
            plan,
            new CompositionExecutionInput(new Dictionary<string, byte[]> { ["input"] = [0x11] }));

        Assert.Equal(CompositionExecutionStatus.Succeeded, expected.Status);
        Assert.Equal([0x11, 0x22], expected.OutputBytes.ToArray());
        Assert.Empty(expected.Issues);
        Assert.Equal(CompositionExecutionStatus.Failed, tooShort.Status);
        Assert.Equal(CompositionIssueCodes.InputAddressSpaceLengthMismatch, Assert.Single(tooShort.Issues).Code);
    }

    /// <summary>Verifies a profile-owned extraction warning code replaces only the generic unexpected-length diagnostic.</summary>
    [Fact]
    public void DeclaredRangeExtractionUsesConfiguredUnexpectedLengthWarningCode()
    {
        CompositionPlan plan = CreateBlankPlan(
            2,
            new AddressSpace(
                "input",
                2,
                AddressSpaceMutability.Immutable,
                inputOversizePolicy: InputOversizePolicy.ExtractDeclaredRange,
                expectedInputLengths: [4],
                unexpectedInputLengthIssueCode: "DP_SIZE_WARNING"),
            CompositionOperation.CopyRange(
                "copy-input",
                10,
                "input",
                new ByteRange(0, 2),
                "output-image",
                new ByteRange(0, 2),
                OverlapPolicy.Reject,
                "copy declared source range"));

        CompositionExecutionResult result = CompositionEngine.Execute(
            plan,
            new CompositionExecutionInput(new Dictionary<string, byte[]> { ["input"] = [0x11, 0x22, 0x33] }));

        Assert.Equal(CompositionExecutionStatus.Succeeded, result.Status);
        Assert.Equal([0x11, 0x22], result.OutputBytes.ToArray());
        CompositionIssue issue = Assert.Single(result.Issues);
        Assert.Equal("DP_SIZE_WARNING", issue.Code);
        Assert.Equal(CompositionIssueSeverity.Warning, issue.Severity);
    }

    /// <summary>Verifies unexpected-length warning codes are restricted to immutable declared-range extraction inputs.</summary>
    [Fact]
    public void UnexpectedLengthWarningCodeRequiresImmutableDeclaredRangeExtractionInput()
    {
        _ = Assert.Throws<ArgumentException>(() => new AddressSpace(
            "mutable",
            2,
            AddressSpaceMutability.Mutable,
            inputOversizePolicy: InputOversizePolicy.ExtractDeclaredRange,
            expectedInputLengths: [4],
            unexpectedInputLengthIssueCode: "DP_SIZE_WARNING"));
        _ = Assert.Throws<ArgumentException>(() => new AddressSpace(
            "rejecting-input",
            2,
            AddressSpaceMutability.Immutable,
            expectedInputLengths: [4],
            unexpectedInputLengthIssueCode: "DP_SIZE_WARNING"));
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
            ]);
        var input = new CompositionExecutionInput(new Dictionary<string, byte[]>
        {
            ["reference-base"] = [0, 0, 0, 0],
            ["ctrlram-input"] = [0xAA, 0xBB, 0xCC, 0xDD],
        });

        CompositionExecutionResult result = CompositionEngine.Execute(plan, input);

        Assert.Equal(CompositionExecutionStatus.Succeeded, result.Status);
        Assert.Equal([0, 0xAA, 0xBB, 0], result.OutputBytes.ToArray());
        CompositionIssue issue = Assert.Single(result.Issues);
        Assert.Equal(CompositionIssueCodes.InputAddressSpaceTruncated, issue.Code);
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

    /// <summary>Verifies caller-supplied output bytes are rejected as unauthorized mutable input.</summary>
    [Fact]
    public void CallerSuppliedOutputBytesAreRejected()
    {
        CompositionPlan plan = CreateBlankPlan(3);
        var input = new CompositionExecutionInput(new Dictionary<string, byte[]>
        {
            ["output-image"] = [0x11],
        });

        CompositionExecutionResult result = CompositionEngine.Execute(plan, input);

        Assert.Equal(CompositionExecutionStatus.Failed, result.Status);
        CompositionIssue issue = Assert.Single(result.Issues);
        Assert.Equal(CompositionIssueCodes.InputMutableAddressSpaceNotAllowed, issue.Code);
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
        Assert.Equal(CompositionIssueCodes.ExecutionCapacityUnsupported, issue.Code);
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
        Assert.Equal(CompositionIssueCodes.InputAddressSpaceMissing, issue.Code);
        Assert.Empty(result.OutputBytes.ToArray());
    }

    /// <summary>Verifies non-output mutable spaces are initialized by the engine before operations.</summary>
    [Fact]
    public void BlankWorkBufferIsInitializedBeforeOperations()
    {
        AddressSpace[] addressSpaces =
        [
            new("output-image", 4, AddressSpaceMutability.Mutable),
            new("scratch", 4, AddressSpaceMutability.Mutable),
        ];
        var plan = new CompositionPlan(
            [
                ImageInitialization.Blank("output-image", 4, 0),
                ImageInitialization.Blank("scratch", 4, 0x5A),
            ],
            "output-image",
            addressSpaces,
            [
                CompositionOperation.CopyRange(
                    "copy-scratch",
                    10,
                    "scratch",
                    new ByteRange(0, 2),
                    "output-image",
                    new ByteRange(0, 2),
                    OverlapPolicy.Reject,
                    "copy initialized scratch"),
                CompositionOperation.FillRange(
                    "mutate-scratch",
                    20,
                    "scratch",
                    new ByteRange(0, 2),
                    0x11,
                    OverlapPolicy.Reject,
                    "fill scratch"),
            ]);

        CompositionExecutionResult result = CompositionEngine.Execute(plan, EmptyInput());

        Assert.Equal(CompositionExecutionStatus.Succeeded, result.Status);
        Assert.Equal([0x5A, 0x5A, 0, 0], result.OutputBytes.ToArray());
        Assert.Equal(["output-image", "scratch"], result.Mutations.Select(item => item.TargetSpaceId));
    }

}
