using NvtFwCombiner.Application.Composition;
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
            profile.CompositionKind);
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
}
