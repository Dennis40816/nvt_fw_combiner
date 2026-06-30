using NvtFwCombiner.Application.Composition;
using NvtFwCombiner.Application.ExternalTools;
using NvtFwCombiner.Application.Ports;
using NvtFwCombiner.Domain.Composition;
using NvtFwCombiner.Profiles;

namespace NvtFwCombiner.Application.Tests;

/// <summary>Tests standard merge preview/build run orchestration.</summary>
public sealed class CompositionRunServiceTests
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
        Assert.Equal(2, result.Report.Inputs.Count);
        Assert.Equal(2, result.Report.Mutations.Count);
        Assert.Empty(result.Report.Issues);
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

    /// <summary>Verifies missing fixed standard merge input fails before output commit after preview gate passes.</summary>
    [Fact]
    public async Task MissingStandardMergeBindingFailsClosed()
    {
        CompositionProfileDefinition profile = BuiltInStandardMergeProfiles.SyntheticStandardMerge;
        ProfileCompileResult compile = CompositionProfileCompiler.Compile(profile, []);
        var reader = new FakeArtifactReader(new Dictionary<string, byte[]>
        {
            ["dp-artifact"] = [1, 2, 3, 4],
        });
        var writer = new FakeOutputWriter();
        var service = new CompositionRunService(reader, new FakeClock([FirstTimestamp, SecondTimestamp]), writer);
        var request = new CompositionRunRequest(
            "run-missing",
            ToRunProfile(profile),
            compile.Plan!,
            [new InputArtifactBinding("dp-input", "dp-input", "dp-artifact")],
            profile.DefaultOutputFileName,
            approvedPreviewToken: "approved-preview-token");

        CompositionRunResult result = await service.BuildAsync(request, CancellationToken.None);

        Assert.Equal(CompositionExecutionStatus.Failed, result.Status);
        Assert.Null(result.CommittedOutputId);
        Assert.False(writer.WasCalled);
        CompositionIssue issue = Assert.Single(result.Report.Issues);
        Assert.Equal("input.binding.missing", issue.Code);
        Assert.All(result.Report.Operations, operation => Assert.Equal(OperationRunStatus.Skipped, operation.Status));
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
            new InputArtifactBinding("dp-input", "dp-safe", hostLocator),
            new InputArtifactBinding("tp-input", "tp-safe", "tp-artifact"),
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
        InputArtifactSummary input = Assert.Single(result.Report.Inputs);
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
        Assert.Equal("input.address-space.truncated", issue.Code);
        Assert.Contains("from 4 to 2 bytes", issue.Message, StringComparison.Ordinal);
    }

    /// <summary>Verifies seeded mutable address-space bindings are read by run service previews.</summary>
    [Fact]
    public async Task PreviewReadsSeededMutableAddressSpaceBinding()
    {
        var reader = new FakeArtifactReader(new Dictionary<string, byte[]>
        {
            ["scratch-artifact"] = [1, 2, 3, 4],
        });
        var service = new CompositionRunService(reader, new FakeClock([FirstTimestamp, SecondTimestamp]));
        CompositionRunRequest request = CreateScratchRequest();

        CompositionRunResult result = await service.PreviewAsync(request, CancellationToken.None);

        Assert.Equal(CompositionExecutionStatus.Succeeded, result.Status);
        Assert.Equal([0, 3, 0, 0], result.OutputBytes.ToArray());
        InputArtifactSummary input = Assert.Single(result.Report.Inputs);
        Assert.Equal("scratch-safe", input.ArtifactId);
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
            new InputArtifactBinding("dp-input", "dp-safe", "missing-dp"),
            new InputArtifactBinding("tp-input", "tp-safe", "missing-tp"),
        ]);

        CompositionRunResult result = await service.PreviewAsync(request, CancellationToken.None);

        Assert.Equal(CompositionExecutionStatus.Failed, result.Status);
        Assert.Equal(2, result.Report.Issues.Count);
        Assert.All(result.Report.Issues, issue => Assert.Equal("input.artifact.read-failed", issue.Code));
    }

    /// <summary>Verifies preview tokens include overwritten operation details, not only final output bytes.</summary>
    [Fact]
    public async Task PreviewTokenChangesWhenOverwrittenPlanDetailsChange()
    {
        var service = new CompositionRunService(
            new FakeArtifactReader([]),
            new FakeClock([FirstTimestamp, SecondTimestamp, ThirdTimestamp, FourthTimestamp]));
        CompositionRunRequest firstRequest = CreateOverwriteRequest("run-overwrite-a", 0x11);
        CompositionRunRequest secondRequest = CreateOverwriteRequest("run-overwrite-b", 0x12);

        CompositionRunResult first = await service.PreviewAsync(firstRequest, CancellationToken.None);
        CompositionRunResult second = await service.PreviewAsync(secondRequest, CancellationToken.None);

        Assert.Equal(first.OutputBytes.ToArray(), second.OutputBytes.ToArray());
        Assert.NotEqual(first.PreviewToken, second.PreviewToken);
    }

    /// <summary>Verifies preview approval includes address-space padding policy, not only output bytes.</summary>
    [Fact]
    public async Task PreviewTokenChangesWhenInputPaddingPolicyChanges()
    {
        var service = new CompositionRunService(
            new FakeArtifactReader(new Dictionary<string, byte[]>
            {
                ["exact-artifact"] = [0x11, 0x22, 0x33, 0x44],
            }),
            new FakeClock([FirstTimestamp, SecondTimestamp, ThirdTimestamp, FourthTimestamp]));

        CompositionRunResult withoutPadding = await service.PreviewAsync(
            CreatePaddedInputRequest(inputPaddingByte: null, artifactId: "exact-artifact"),
            CancellationToken.None);
        CompositionRunResult withPadding = await service.PreviewAsync(
            CreatePaddedInputRequest(inputPaddingByte: 0xFF, artifactId: "exact-artifact"),
            CancellationToken.None);

        Assert.Equal(withoutPadding.OutputBytes.ToArray(), withPadding.OutputBytes.ToArray());
        Assert.NotEqual(withoutPadding.PreviewToken, withPadding.PreviewToken);
    }

    /// <summary>Verifies preview approval includes address-space truncation policy.</summary>
    [Fact]
    public async Task PreviewTokenChangesWhenInputOversizePolicyChanges()
    {
        var service = new CompositionRunService(
            new FakeArtifactReader(new Dictionary<string, byte[]>
            {
                ["reference-artifact"] = [0, 0, 0, 0],
                ["ctrlram-exact"] = [0xAA, 0xBB],
            }),
            new FakeClock([FirstTimestamp, SecondTimestamp, ThirdTimestamp, FourthTimestamp]));

        CompositionRunResult withoutTruncation = await service.PreviewAsync(
            CreateCtrlRamReplaceRequest(InputOversizePolicy.Reject, "ctrlram-exact"),
            CancellationToken.None);
        CompositionRunResult withTruncation = await service.PreviewAsync(
            CreateCtrlRamReplaceRequest(InputOversizePolicy.TruncateWithWarning, "ctrlram-exact"),
            CancellationToken.None);

        Assert.Equal(withoutTruncation.OutputBytes.ToArray(), withTruncation.OutputBytes.ToArray());
        Assert.NotEqual(withoutTruncation.PreviewToken, withTruncation.PreviewToken);
    }

    /// <summary>Verifies Replace requests require IC number context before execution.</summary>
    [Fact]
    public void ReplaceRunRequestRequiresIcNumberSelection()
    {
        CompositionProfileDefinition profile = BuiltInReplaceProfiles.SyntheticDpReplace;
        ProfileCompileResult compile = CompositionProfileCompiler.Compile(profile, []);

        ArgumentException exception = Assert.Throws<ArgumentException>(() => new CompositionRunRequest(
            "run-missing-ic",
            ToRunProfile(profile),
            compile.Plan!,
            CreateDpReplaceBindings(),
            profile.DefaultOutputFileName));

        Assert.Contains("IC number selection", exception.Message, StringComparison.Ordinal);
    }

    /// <summary>Verifies preview approval binds the selected IC number context.</summary>
    [Fact]
    public async Task PreviewTokenChangesWhenIcNumberChanges()
    {
        var service = new CompositionRunService(
            new FakeArtifactReader(new Dictionary<string, byte[]>
            {
                ["reference-artifact"] = [0, 0, 0, 0, 0, 0, 0, 0],
                ["dp-artifact"] = [1, 2, 3, 4],
            }),
            new FakeClock([FirstTimestamp, SecondTimestamp, ThirdTimestamp, FourthTimestamp]));

        CompositionRunResult first = await service.PreviewAsync(
            CreateDpReplaceRequest("51920"),
            CancellationToken.None);
        CompositionRunResult second = await service.PreviewAsync(
            CreateDpReplaceRequest("51921"),
            CancellationToken.None);

        Assert.Equal(first.OutputBytes.ToArray(), second.OutputBytes.ToArray());
        Assert.NotEqual(first.PreviewToken, second.PreviewToken);
    }


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
        Assert.NotNull(result.PreviewToken);
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

    /// <summary>Verifies output overrides are validated before a writer can see them.</summary>
    [Fact]
    public void RunRequestRejectsOutputFileNameWithPathSyntax()
    {
        ArgumentException exception = Assert.Throws<ArgumentException>(() => CreateRequest(outputFileName: @"..\escape.bin"));

        Assert.Contains("Output file name", exception.Message, StringComparison.Ordinal);
    }

    /// <summary>Verifies run metadata must match compiler-carried plan provenance.</summary>
    [Fact]
    public void RunRequestRejectsProfileMetadataMismatch()
    {
        CompositionProfileDefinition profile = BuiltInStandardMergeProfiles.SyntheticStandardMerge;
        ProfileCompileResult compile = CompositionProfileCompiler.Compile(profile, []);
        var wrongProfile = new CompositionRunProfile(
            "wrong-profile",
            profile.ProfileVersion,
            profile.IcId,
            profile.ModeId,
            profile.ExperienceId,
            profile.CompositionKind);

        ArgumentException exception = Assert.Throws<ArgumentException>(() => new CompositionRunRequest(
            "run-mismatch",
            wrongProfile,
            compile.Plan!,
            DefaultBindings(),
            profile.DefaultOutputFileName));

        Assert.Contains("compiled plan provenance", exception.Message, StringComparison.Ordinal);
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

    private static CompositionRunService CreateService(out FakeOutputWriter writer)
    {
        var reader = new FakeArtifactReader(new Dictionary<string, byte[]>
        {
            ["dp-artifact"] = [1, 2, 3, 4],
            ["tp-artifact"] = [9, 8, 7, 6],
        });
        writer = new FakeOutputWriter();
        return new CompositionRunService(
            reader,
            new FakeClock([FirstTimestamp, SecondTimestamp, ThirdTimestamp, FourthTimestamp]),
            writer);
    }

    private static CompositionRunRequest CreateRequest(
        IReadOnlyList<InputArtifactBinding>? bindings = null,
        string? outputFileName = null)
    {
        CompositionProfileDefinition profile = BuiltInStandardMergeProfiles.SyntheticStandardMerge;
        ProfileCompileResult compile = CompositionProfileCompiler.Compile(profile, []);
        return new CompositionRunRequest(
            "run-standard-synthetic",
            ToRunProfile(profile),
            compile.Plan!,
            bindings ?? DefaultBindings(),
            outputFileName ?? profile.DefaultOutputFileName);
    }

    private static CompositionRunRequest CreateScratchRequest()
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
                CompositionOperation.CopyRange(
                    "copy-scratch",
                    10,
                    "scratch",
                    new ByteRange(2, 1),
                    "output-image",
                    new ByteRange(1, 1),
                    OverlapPolicy.Reject,
                    "copy scratch seed"),
            ]);
        return new CompositionRunRequest(
            "run-scratch",
            new CompositionRunProfile(
                "scratch-profile",
                "1.0.0",
                "NT-SYNTHETIC",
                "scratch",
                "general-merge",
                CompositionKind.Merge),
            plan,
            [new InputArtifactBinding("scratch", "scratch-safe", "scratch-artifact")],
            "scratch.bin");
    }

    private static CompositionRunRequest CreatePaddedInputRequest(byte? inputPaddingByte, string artifactId)
    {
        AddressSpace[] addressSpaces =
        [
            new("output-image", 4, AddressSpaceMutability.Mutable),
            new("short-input", 4, AddressSpaceMutability.Immutable, inputPaddingByte),
        ];
        var plan = new CompositionPlan(
            ImageInitialization.Blank("output-image", 4, 0),
            addressSpaces,
            [
                CompositionOperation.CopyRange(
                    "copy-padded-input",
                    10,
                    "short-input",
                    new ByteRange(0, 4),
                    "output-image",
                    new ByteRange(0, 4),
                    OverlapPolicy.Reject,
                    "copy padded input"),
            ],
            new CompositionPlanProvenance(
                "padded-input-profile",
                "1.0.0",
                "NT-SYNTHETIC",
                "standard-merge",
                "standard-merge",
                CompositionKind.Merge));

        return new CompositionRunRequest(
            "run-padded-input",
            new CompositionRunProfile(
                "padded-input-profile",
                "1.0.0",
                "NT-SYNTHETIC",
                "standard-merge",
                "standard-merge",
                CompositionKind.Merge),
            plan,
            [new InputArtifactBinding("short-input", "short-safe", artifactId)],
            "padded.bin");
    }

    private static CompositionRunRequest CreateCtrlRamReplaceRequest(
        InputOversizePolicy inputOversizePolicy,
        string ctrlRamArtifactId)
    {
        AddressSpace[] addressSpaces =
        [
            new("reference-base", 4, AddressSpaceMutability.Immutable),
            new("ctrlram-input", 2, AddressSpaceMutability.Immutable, inputOversizePolicy: inputOversizePolicy),
            new("output-image", 4, AddressSpaceMutability.Mutable),
        ];
        var provenance = new CompositionPlanProvenance(
            "ctrlram-replace-profile",
            "1.0.0",
            "NT-SYNTHETIC",
            "ctrlram-replace",
            "ctrlram-replace",
            CompositionKind.Replace);
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
            provenance);

        return new CompositionRunRequest(
            "run-ctrlram-replace",
            new CompositionRunProfile(
                provenance.ProfileId,
                provenance.ProfileVersion,
                provenance.IcId,
                provenance.ModeId,
                provenance.ExperienceId,
                provenance.CompositionKind,
                IcNumberInputMode.SingleSelector),
            plan,
            [
                new InputArtifactBinding("reference-base", "reference-safe", "reference-artifact"),
                new InputArtifactBinding("ctrlram-input", "ctrlram-safe", ctrlRamArtifactId),
            ],
            "ctrlram.bin",
            icNumberSelection: new IcNumberSelection(IcNumberInputMode.SingleSelector, ["SYNTHETIC"]));
    }

    private static CompositionRunRequest CreateDpReplaceRequest(string icNumber)
    {
        CompositionProfileDefinition profile = BuiltInReplaceProfiles.SyntheticDpReplace;
        ProfileCompileResult compile = CompositionProfileCompiler.Compile(profile, []);
        return new CompositionRunRequest(
            "run-dp-replace",
            ToRunProfile(profile),
            compile.Plan!,
            CreateDpReplaceBindings(),
            profile.DefaultOutputFileName,
            icNumberSelection: new IcNumberSelection(IcNumberInputMode.SingleSelector, [icNumber]));
    }

    private static IReadOnlyList<InputArtifactBinding> CreateDpReplaceBindings()
    {
        return
        [
            new InputArtifactBinding("reference-base", "reference-safe", "reference-artifact"),
            new InputArtifactBinding("dp-replacement", "dp-safe", "dp-artifact"),
        ];
    }

    private static CompositionRunRequest CreateOverwriteRequest(string runId, byte firstFillByte)
    {
        AddressSpace[] addressSpaces =
        [
            new("output-image", 1, AddressSpaceMutability.Mutable),
        ];
        var plan = new CompositionPlan(
            ImageInitialization.Blank("output-image", 1, 0),
            addressSpaces,
            [
                CompositionOperation.FillRange(
                    "fill-first",
                    10,
                    "output-image",
                    new ByteRange(0, 1),
                    firstFillByte,
                    OverlapPolicy.Reject,
                    "first overwritten fill"),
                CompositionOperation.FillRange(
                    "fill-second",
                    20,
                    "output-image",
                    new ByteRange(0, 1),
                    0x22,
                    OverlapPolicy.ReplaceExisting,
                    "final fill"),
            ]);
        return new CompositionRunRequest(
            runId,
            new CompositionRunProfile(
                "overwrite-profile",
                "1.0.0",
                "NT-SYNTHETIC",
                "overwrite",
                "general-merge",
                CompositionKind.Merge),
            plan,
            [],
            "overwrite.bin");
    }

    private static CompositionRunRequest CreateExternalProcessorRequest()
    {
        AddressSpace[] addressSpaces =
        [
            new("output-image", 4, AddressSpaceMutability.Mutable),
        ];
        var plan = new CompositionPlan(
            ImageInitialization.Blank("output-image", 4, 0),
            addressSpaces,
            [
                CompositionOperation.RunExternalProcessor(
                    "run-crc",
                    10,
                    "output-image",
                    new ByteRange(0, 4),
                    new ExternalProcessorInvocation(
                        "processor-v1",
                        "tool-v1",
                        [new ByteRange(0, 4)],
                        [new ByteRange(1, 1)]),
                    OverlapPolicy.Reject,
                    "run synthetic external processor"),
            ]);
        return new CompositionRunRequest(
            "run-external",
            new CompositionRunProfile(
                "external-profile",
                "1.0.0",
                "NT-SYNTHETIC",
                "external",
                "standard-merge",
                CompositionKind.Merge),
            plan,
            [],
            "external.bin");
    }

    private static IReadOnlyList<InputArtifactBinding> DefaultBindings()
    {
        return
        [
            new InputArtifactBinding("dp-input", "dp-input", "dp-artifact"),
            new InputArtifactBinding("tp-input", "tp-input", "tp-artifact"),
        ];
    }

    private static CompositionRunProfile ToRunProfile(CompositionProfileDefinition profile)
    {
        return new CompositionRunProfile(
            profile.ProfileId,
            profile.ProfileVersion,
            profile.IcId,
            profile.ModeId,
            profile.ExperienceId,
            profile.CompositionKind,
            profile.IcNumberInputMode);
    }

    private sealed class FakeArtifactReader : IArtifactReader
    {
        private readonly Dictionary<string, byte[]> _artifacts;

        internal FakeArtifactReader(Dictionary<string, byte[]> artifacts)
        {
            _artifacts = artifacts;
        }

        public ValueTask<ReadOnlyMemory<byte>> ReadAsync(string artifactId, CancellationToken cancellationToken)
        {
            return ValueTask.FromResult<ReadOnlyMemory<byte>>(_artifacts[artifactId]);
        }
    }

    private sealed class FakeClock : ISystemClock
    {
        private readonly Queue<DateTimeOffset> _timestamps;

        internal FakeClock(IEnumerable<DateTimeOffset> timestamps)
        {
            _timestamps = new Queue<DateTimeOffset>(timestamps);
        }

        public DateTimeOffset UtcNow => _timestamps.Dequeue();
    }

    private sealed class FakeOutputWriter : ICompositionOutputWriter
    {
        internal bool WasCalled { get; private set; }

        internal string? FileName { get; private set; }

        internal byte[] OutputBytes { get; private set; } = [];

        public ValueTask<string> CommitAsync(
            string fileName,
            ReadOnlyMemory<byte> outputBytes,
            CancellationToken cancellationToken)
        {
            WasCalled = true;
            FileName = fileName;
            OutputBytes = outputBytes.ToArray();
            return ValueTask.FromResult($"committed:{fileName}");
        }
    }

    private sealed class FakeExternalProcessor : IExternalProcessor
    {
        private readonly Func<ExternalProcessorRequest, ExternalProcessorResult> _transform;

        internal FakeExternalProcessor(Func<ExternalProcessorRequest, ExternalProcessorResult> transform)
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
}
