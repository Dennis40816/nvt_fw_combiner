using NvtFwCombiner.Application.Authoring;
using NvtFwCombiner.Application.Composition;
using NvtFwCombiner.Domain.Composition;
using NvtFwCombiner.TestSupport;

namespace NvtFwCombiner.Application.Tests;

/// <summary>Tests standard merge preview/build run orchestration.</summary>
public sealed partial class CompositionRunServiceTests
{
    private static readonly DateTimeOffset FirstTimestamp = new(2026, 6, 28, 12, 0, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset SecondTimestamp = new(2026, 6, 28, 12, 0, 1, TimeSpan.Zero);
    private static readonly DateTimeOffset ThirdTimestamp = new(2026, 6, 28, 12, 0, 2, TimeSpan.Zero);
    private static readonly DateTimeOffset FourthTimestamp = new(2026, 6, 28, 12, 0, 3, TimeSpan.Zero);

    /// <summary>Verifies synthetic standard merge preview returns output, token, hash, operations, and report metadata.</summary>
    [Fact]
    public async Task PreviewRunsSyntheticStandardMergeWithoutCommittingOutput()
    {
        CompositionRunService service = CreateService(out _);
        CompositionRunRequest request = CreateRequest();

        CompositionRunResult result = await service.PreviewAsync(request, CancellationToken.None);

        Assert.Equal(CompositionExecutionStatus.Succeeded, result.Status);
        Assert.Equal([1, 2, 3, 4, 9, 8, 7, 6], result.OutputBytes.ToArray());
        Assert.Null(result.CommittedOutputId);
        Assert.NotNull(result.PreviewToken);
        Assert.False(result.Report.Output.Committed);
        Assert.Equal("synthetic-standard-merge", result.Report.ProfileId);
        Assert.Equal("NT-SYNTHETIC", result.Report.IcId);
        Assert.Equal("standard-merge", result.Report.ExperienceId);
        Assert.Equal(FirstTimestamp, result.Report.StartedAtUtc);
        Assert.Equal(SecondTimestamp, result.Report.CompletedAtUtc);
        Assert.Equal(["copy-dp", "copy-tp"], result.Report.Operations.Select(operation => operation.OperationId));
        OperationRunSummary copyDp = result.Report.Operations[0];
        Assert.Equal("dp-input", copyDp.SourceSpaceId);
        Assert.Equal(new ByteRange(0, 4), copyDp.SourceRange);
        Assert.Equal("output-image", copyDp.TargetSpaceId);
        Assert.Equal(new ByteRange(0, 4), copyDp.TargetRange);
        Assert.Equal(OverlapPolicy.Reject, copyDp.OverlapPolicy);
        Assert.Null(copyDp.ProcessorId);
        Assert.Empty(copyDp.ProcessorAllowedWriteRanges);
        Assert.Equal("Copy synthetic DP input into the output DP range.", copyDp.Reason);
        Assert.Equal("built-in-profile", copyDp.Provenance.Kind);
        Assert.Null(copyDp.Provenance.SourceId);
        Assert.Equal(2, result.Report.Inputs.Count);
        Assert.Equal(2, result.Report.Mutations.Count);
        Assert.Empty(result.Report.OutputDifferences);
        Assert.Empty(result.Report.Issues);
    }

    /// <summary>Preview identity and report preserve the exact General admission snapshot.</summary>
    [Fact]
    public async Task PreviewTokenChangesWhenGeneralAdmissionObservationChanges()
    {
        CompositionRunService service = CreateService(out _);
        CompositionRunRequest request = CreateRequest();
        GeneralResourceLimits limits = new(
            maximumMappingCount: 1,
            maximumTotalWriteBytes: 4,
            maximumFileBytes: 8,
            maximumSafeMaterializationBytes: 8,
            [new GeneralSlotLengthLimits("source", 1, 8)]);
        var firstAdmission = new GeneralAuthoringAdmissionSummary(
            "trusted-parent",
            "saved-rule",
            limits,
            [new GeneralInputResource("source", 4)],
            [],
            []);
        var secondAdmission = new GeneralAuthoringAdmissionSummary(
            "trusted-parent",
            "saved-rule",
            limits,
            [new GeneralInputResource("source", 5)],
            [],
            []);

        CompositionRunResult first = await service.PreviewAsync(
            WithGeneralAdmission(request, firstAdmission),
            CancellationToken.None);
        CompositionRunResult second = await service.PreviewAsync(
            WithGeneralAdmission(request, secondAdmission),
            CancellationToken.None);

        Assert.NotEqual(first.PreviewToken, second.PreviewToken);
        Assert.Equal(
            4,
            Assert.Single(
                first.Report.GeneralAdmission!.InputResources).LengthBytes);
        Assert.Equal(
            5,
            Assert.Single(
                second.Report.GeneralAdmission!.InputResources).LengthBytes);
    }

    /// <summary>Verifies synthetic standard merge build commits only after an approved preview token is supplied.</summary>
    [Fact]
    public async Task BuildCommitsSyntheticStandardMergeOutputAfterPreviewApproval()
    {
        CompositionRunService service = CreateService(out FakeOutputWriter writer);
        CompositionRunRequest request = CreateRequest();
        CompositionRunResult preview = await service.PreviewAsync(request, CancellationToken.None);

        CompositionRunResult result = await service.BuildAsync(
            request.WithApprovedPreviewToken(preview.PreviewToken!),
            CancellationToken.None);

        Assert.Equal(CompositionExecutionStatus.Succeeded, result.Status);
        Assert.Equal("committed:synthetic-standard-merge.bin", result.CommittedOutputId);
        Assert.True(result.Report.Output.Committed);
        Assert.Equal("synthetic-standard-merge.bin", writer.FileName);
        Assert.Equal([1, 2, 3, 4, 9, 8, 7, 6], writer.OutputBytes);
    }

    /// <summary>Verifies automatic build commits the exact output from one authoritative execution.</summary>
    [Fact]
    public async Task AutomaticBuildCommitsSyntheticStandardMergeOutputFromOneRun()
    {
        CompositionRunService service = CreateService(out FakeOutputWriter writer);

        CompositionRunResult result = await service.PreviewOrBuildAsync(
            CreateRequest(),
            build: true,
            CancellationToken.None);

        Assert.Equal(CompositionExecutionStatus.Succeeded, result.Status);
        Assert.Equal("committed:synthetic-standard-merge.bin", result.CommittedOutputId);
        Assert.Null(result.PreviewToken);
        Assert.True(result.Report.Output.Committed);
        Assert.Equal("synthetic-standard-merge.bin", writer.FileName);
        Assert.Equal([1, 2, 3, 4, 9, 8, 7, 6], writer.OutputBytes);
    }

    /// <summary>Verifies build fails before reading or committing when no preview token is approved.</summary>
    [Fact]
    public async Task BuildRequiresApprovedPreviewTokenBeforeCommit()
    {
        CompositionRunService service = CreateService(out FakeOutputWriter writer);
        CompositionRunRequest request = CreateRequest();

        CompositionRunResult result = await service.BuildAsync(request, CancellationToken.None);

        Assert.Equal(CompositionExecutionStatus.Failed, result.Status);
        Assert.False(writer.WasCalled);
        CompositionIssue issue = Assert.Single(result.Report.Issues);
        Assert.Equal("build.preview-token.required", issue.Code);
    }

    /// <summary>Verifies a mismatched preview token prevents output commit.</summary>
    [Fact]
    public async Task BuildRejectsPreviewTokenMismatch()
    {
        CompositionRunService service = CreateService(out FakeOutputWriter writer);
        CompositionRunRequest request = CreateRequest().WithApprovedPreviewToken("not-the-preview-token");

        CompositionRunResult result = await service.BuildAsync(request, CancellationToken.None);

        Assert.Equal(CompositionExecutionStatus.Failed, result.Status);
        Assert.False(writer.WasCalled);
        CompositionIssue issue = Assert.Single(result.Report.Issues);
        Assert.Equal("build.preview-token.mismatch", issue.Code);
    }

    /// <summary>Verifies approved numeric IC number selections can be bound to Replace run profiles.</summary>
    [Fact]
    public void NumericIcNumberSelectionIsAcceptedForReplaceRunProfile()
    {
        CompositionRunRequest request = CreateNumericReplaceRequest("2");

        Assert.Equal(IcNumberInputMode.NumericSelector, request.IcNumberSelection!.Mode);
        Assert.Equal("2", Assert.Single(request.IcNumberSelection.Parts));
    }

    private static CompositionRunRequest WithGeneralAdmission(
        CompositionRunRequest request,
        GeneralAuthoringAdmissionSummary admission)
    {
        return new CompositionRunRequest(
            request.RunId,
            request.CompiledComposition,
            request.ArtifactBindings.Values,
            request.OutputFileName,
            request.ApprovedPreviewToken,
            request.IcNumberSelection,
            request.IsOutputFileNameOverride,
            request.AbMergeTopologySelection,
            request.AdvisoryIssues,
            admission);
    }

    /// <summary>Verifies numeric IC number selections reject non-integer values before execution.</summary>
    [Fact]
    public void NumericIcNumberSelectionRejectsNonIntegerValue()
    {
        ArgumentException exception = Assert.Throws<ArgumentException>(() => CreateNumericReplaceRequest("cascade"));

        Assert.Contains("positive integer", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>Verifies missing fixed standard merge input fails before output commit after preview gate passes.</summary>
    [Fact]
    public void MissingStandardMergeBindingFailsClosed()
    {
        ArgumentException exception = Assert.Throws<ArgumentException>(() => CreateRequest(
            [new InputArtifactBinding(
                "dp-input",
                "dp-input",
                "dp-artifact",
                "dp-input.bin",
                CompiledInputArtifactClass.TpFirmware)],
            approvedPreviewToken: "approved-preview-token"));

        Assert.Contains("exactly match", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>Verifies reports use safe binding ids instead of host artifact locators.</summary>
    [Fact]
    public async Task PreviewReportKeepsHostPathsOutOfInputSummaries()
    {
        string hostLocator = @"C:\private\dp.bin";
        var reader = new FakeArtifactReader(new Dictionary<string, byte[]>
        {
            [hostLocator] = [1, 2, 3, 4],
            ["tp-artifact"] = [9, 8, 7, 6],
        });
        var service = new CompositionRunService(
            reader,
            new FakeClock([FirstTimestamp, SecondTimestamp]));
        CompositionRunRequest request = CreateRequest(bindings:
        [
            new InputArtifactBinding(
                "dp-input", "dp-safe", hostLocator, "dp-input.bin",
                CompiledInputArtifactClass.TpFirmware),
            new InputArtifactBinding(
                "tp-input", "tp-safe", "tp-artifact", "tp-input.bin",
                CompiledInputArtifactClass.TpFirmware),
        ]);

        CompositionRunResult result = await service.PreviewAsync(request, CancellationToken.None);

        Assert.Equal(CompositionExecutionStatus.Succeeded, result.Status);
        Assert.Contains(result.Report.Inputs, input => input.ArtifactId == "dp-safe");
        Assert.DoesNotContain(result.Report.Inputs, input => input.ArtifactId == hostLocator);
    }

    /// <summary>Verifies previews report actual input size while execution uses profile-declared padding.</summary>
    [Fact]
    public async Task PreviewPadsShortInputButReportsActualInputSize()
    {
        var reader = new FakeArtifactReader(new Dictionary<string, byte[]>
        {
            ["padding-reference-artifact"] = [0, 0, 0, 0],
            ["short-artifact"] = [0x11, 0x22],
        });
        var service = new CompositionRunService(
            reader,
            new FakeClock([FirstTimestamp, SecondTimestamp]));

        CompositionRunResult result = await service.PreviewAsync(
            CreatePaddedInputRequest(inputPaddingByte: 0xFF, artifactId: "short-artifact"),
            CancellationToken.None);

        Assert.Equal(CompositionExecutionStatus.Succeeded, result.Status);
        Assert.Equal([0x11, 0x22, 0xFF, 0xFF], result.OutputBytes.ToArray());
        InputArtifactSummary input = Assert.Single(
            result.Report.Inputs,
            static input => input.AddressSpaceId == "short-input");
        Assert.Equal(2, input.Size);
    }

    /// <summary>Verifies CtrlRAM replace previews truncate oversized inputs and report the prompt diagnostic.</summary>
    [Fact]
    public async Task PreviewTruncatesOversizedCtrlRamInputAndReportsIssue()
    {
        var reader = new FakeArtifactReader(new Dictionary<string, byte[]>
        {
            ["reference-artifact"] = [0, 0, 0, 0],
            ["ctrlram-artifact"] = [0xAA, 0xBB, 0xCC, 0xDD],
        });
        var service = new CompositionRunService(
            reader,
            new FakeClock([FirstTimestamp, SecondTimestamp]));

        CompositionRunResult result = await service.PreviewAsync(
            CreateCtrlRamReplaceRequest(InputOversizePolicy.TruncateWithWarning, "ctrlram-artifact"),
            CancellationToken.None);

        Assert.Equal(CompositionExecutionStatus.Succeeded, result.Status);
        Assert.Equal([0, 0xAA, 0xBB, 0], result.OutputBytes.ToArray());
        Assert.Contains(result.Report.Inputs, input => input.AddressSpaceId == "ctrlram-input" && input.Size == 4);
        CompositionIssue issue = Assert.Single(result.Report.Issues);
        Assert.Equal(CompositionIssueCodes.InputAddressSpaceTruncated, issue.Code);
        Assert.Equal(CompositionIssueSeverity.Warning, issue.Severity);
        Assert.Contains("from 4 to 2 bytes", issue.Message, StringComparison.Ordinal);
    }

    /// <summary>Verifies previews use engine-initialized mutable buffers without artifact bindings.</summary>
    [Fact]
    public async Task PreviewUsesEngineInitializedMutableAddressSpace()
    {
        var reader = new FakeArtifactReader(new Dictionary<string, byte[]>
        {
            ["v2-test-input-artifact"] = [0],
        });
        var service = new CompositionRunService(reader, new FakeClock([FirstTimestamp, SecondTimestamp]));
        CompositionRunRequest request = CreateScratchRequest();

        CompositionRunResult result = await service.PreviewAsync(request, CancellationToken.None);

        Assert.Equal(CompositionExecutionStatus.Succeeded, result.Status);
        Assert.Equal([0, 3, 0, 0], result.OutputBytes.ToArray());
        Assert.Equal("v2-test-input", Assert.Single(result.Report.Inputs).AddressSpaceId);
    }

    /// <summary>Verifies preview approval fingerprints every initializer and explicit output selection.</summary>
    [Fact]
    public async Task PreviewTokenFingerprintsAllInitializersAndOutputSelection()
    {
        var service = new CompositionRunService(
            new FakeArtifactReader(new Dictionary<string, byte[]>
            {
                ["v2-test-input-artifact"] = [0],
            }),
            new FakeClock(Enumerable.Range(0, 6).Select(offset => FirstTimestamp.AddSeconds(offset))));

        CompositionRunResult baseline = await service.PreviewAsync(
            CreateInitializerFingerprintRequest(scratchFillByte: 0, outputSpaceId: "output-image"),
            CancellationToken.None);
        CompositionRunResult changedInitializer = await service.PreviewAsync(
            CreateInitializerFingerprintRequest(scratchFillByte: 1, outputSpaceId: "output-image"),
            CancellationToken.None);
        CompositionRunResult changedOutput = await service.PreviewAsync(
            CreateInitializerFingerprintRequest(scratchFillByte: 0, outputSpaceId: "scratch"),
            CancellationToken.None);

        Assert.Equal(baseline.OutputBytes.ToArray(), changedInitializer.OutputBytes.ToArray());
        Assert.Equal(baseline.OutputBytes.ToArray(), changedOutput.OutputBytes.ToArray());
        Assert.NotEqual(baseline.PreviewToken, changedInitializer.PreviewToken);
        Assert.NotEqual(baseline.PreviewToken, changedOutput.PreviewToken);
    }

    /// <summary>Verifies artifact read failures are returned as structured run issues.</summary>
    [Fact]
    public async Task PreviewConvertsArtifactReadFailuresIntoRunIssues()
    {
        var service = new CompositionRunService(
            new FakeArtifactReader([]),
            new FakeClock([FirstTimestamp, SecondTimestamp]));
        CompositionRunRequest request = CreateRequest(bindings:
        [
            new InputArtifactBinding(
                "dp-input", "dp-safe", "missing-dp", "dp-input.bin",
                CompiledInputArtifactClass.TpFirmware),
            new InputArtifactBinding(
                "tp-input", "tp-safe", "missing-tp", "tp-input.bin",
                CompiledInputArtifactClass.TpFirmware),
        ]);

        CompositionRunResult result = await service.PreviewAsync(request, CancellationToken.None);

        Assert.Equal(CompositionExecutionStatus.Failed, result.Status);
        Assert.Equal(2, result.Report.Issues.Count);
        Assert.All(result.Report.Issues, issue => Assert.Equal("input.artifact.read-failed", issue.Code));
        Assert.All(result.Report.Issues, issue => Assert.Equal(CompositionIssueSeverity.Error, issue.Severity));
    }

    /// <summary>One immutable artifact is read and hashed once while each address space keeps its own buffer.</summary>
    [Fact]
    public async Task PreviewSnapshotsSharedArtifactOncePerRun()
    {
        byte[] sharedBytes = [0x11, 0x22, 0x33, 0x44];
        var reader = new CountingArtifactReader(sharedBytes);
        var service = new CompositionRunService(
            reader,
            new FakeClock([FirstTimestamp, SecondTimestamp]));
        CompositionRunRequest request = CreateRequest(bindings:
        [
            new InputArtifactBinding(
                "dp-input", "dp-safe", "shared-artifact", "dp-input.bin",
                CompiledInputArtifactClass.TpFirmware),
            new InputArtifactBinding(
                "tp-input", "tp-safe", "shared-artifact", "tp-input.bin",
                CompiledInputArtifactClass.TpFirmware),
        ]);

        CompositionRunResult result = await service.PreviewAsync(request, CancellationToken.None);

        Assert.Equal(CompositionExecutionStatus.Succeeded, result.Status);
        Assert.Equal(1, reader.ReadCount);
        Assert.Equal([.. sharedBytes, .. sharedBytes], result.OutputBytes.ToArray());
        Assert.Equal(["dp-input", "tp-input"], result.Report.Inputs.Select(input => input.AddressSpaceId));
        _ = Assert.Single(result.Report.Inputs.Select(input => input.Sha256).Distinct(StringComparer.Ordinal));
    }

    /// <summary>A failed shared artifact read is retried so each missing binding keeps its own issue.</summary>
    [Fact]
    public async Task PreviewDoesNotCacheSharedArtifactReadFailure()
    {
        var reader = new CountingArtifactReader(bytes: null);
        var service = new CompositionRunService(
            reader,
            new FakeClock([FirstTimestamp, SecondTimestamp]));
        CompositionRunRequest request = CreateRequest(bindings:
        [
            new InputArtifactBinding(
                "dp-input", "dp-safe", "missing-shared-artifact", "dp-input.bin",
                CompiledInputArtifactClass.TpFirmware),
            new InputArtifactBinding(
                "tp-input", "tp-safe", "missing-shared-artifact", "tp-input.bin",
                CompiledInputArtifactClass.TpFirmware),
        ]);

        CompositionRunResult result = await service.PreviewAsync(request, CancellationToken.None);

        Assert.Equal(CompositionExecutionStatus.Failed, result.Status);
        Assert.Equal(2, reader.ReadCount);
        Assert.Equal(2, result.Report.Issues.Count);
        Assert.All(result.Report.Issues, issue => Assert.Equal("input.artifact.read-failed", issue.Code));
    }

    /// <summary>Verifies Replace requests require IC number context before execution.</summary>
    [Fact]
    public void ReplaceRunRequestRequiresIcNumberSelection()
    {
        ArgumentException exception = Assert.Throws<ArgumentException>(() => new CompositionRunRequest(
            "run-missing-ic",
            CreateDpReplaceCompiledComposition(),
            CreateDpReplaceBindings(),
            "synthetic-dp-replace.bin"));

        Assert.Contains("IC number selection", exception.Message, StringComparison.Ordinal);
    }

    /// <summary>Verifies output overrides are validated before a writer can see them.</summary>
    [Fact]
    public void RunRequestRejectsOutputFileNameWithPathSyntax()
    {
        ArgumentException exception = Assert.Throws<ArgumentException>(() => CreateRequest(outputFileName: @"..\escape.bin"));

        Assert.Contains("Output file name", exception.Message, StringComparison.Ordinal);
    }

    /// <summary>Verifies preview approval retains the exact compiled composition artifact.</summary>
    [Fact]
    public void ApprovedPreviewTokenPreservesCompiledComposition()
    {
        CompositionRunRequest request = CreateRequest();

        CompositionRunRequest approved = request.WithApprovedPreviewToken("approved-preview-token");

        Assert.Same(request.CompiledComposition, approved.CompiledComposition);
    }

    /// <summary>Verifies run requests snapshot caller-provided binding collections.</summary>
    [Fact]
    public void RunRequestSnapshotsArtifactBindings()
    {
        List<InputArtifactBinding> bindings = [.. DefaultBindings()];

        CompositionRunRequest request = CreateRequest(bindings: bindings);
        bindings.Clear();

        Assert.Equal(2, request.ArtifactBindings.Count);
    }

}
