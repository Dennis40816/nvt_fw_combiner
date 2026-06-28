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

    /// <summary>Verifies synthetic standard merge preview returns output, hash, operations, and report metadata.</summary>
    [Fact]
    public async Task PreviewRunsSyntheticStandardMergeWithoutCommittingOutput()
    {
        CompositionRunService service = CreateService(out _);
        CompositionRunRequest request = CreateRequest();

        CompositionRunResult result = await service.PreviewAsync(request, CancellationToken.None);

        Assert.Equal(CompositionExecutionStatus.Succeeded, result.Status);
        Assert.Equal([1, 2, 3, 4, 9, 8, 7, 6], result.OutputBytes.ToArray());
        Assert.Null(result.CommittedOutputId);
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

    /// <summary>Verifies synthetic standard merge build commits successful output through the output writer port.</summary>
    [Fact]
    public async Task BuildCommitsSyntheticStandardMergeOutput()
    {
        CompositionRunService service = CreateService(out FakeOutputWriter writer);
        CompositionRunRequest request = CreateRequest();

        CompositionRunResult result = await service.BuildAsync(request, CancellationToken.None);

        Assert.Equal(CompositionExecutionStatus.Succeeded, result.Status);
        Assert.Equal("committed:synthetic-standard-merge.bin", result.CommittedOutputId);
        Assert.True(result.Report.Output.Committed);
        Assert.Equal("synthetic-standard-merge.bin", writer.FileName);
        Assert.Equal([1, 2, 3, 4, 9, 8, 7, 6], writer.OutputBytes);
    }

    /// <summary>Verifies missing fixed standard merge input fails before output commit.</summary>
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
            new Dictionary<string, string>
            {
                ["dp-input"] = "dp-artifact",
            },
            profile.DefaultOutputFileName);

        CompositionRunResult result = await service.BuildAsync(request, CancellationToken.None);

        Assert.Equal(CompositionExecutionStatus.Failed, result.Status);
        Assert.Null(result.CommittedOutputId);
        Assert.False(writer.WasCalled);
        CompositionIssue issue = Assert.Single(result.Report.Issues);
        Assert.Equal("input.binding.missing", issue.Code);
        Assert.All(result.Report.Operations, operation => Assert.Equal(OperationRunStatus.Skipped, operation.Status));
    }

    private static CompositionRunService CreateService(out FakeOutputWriter writer)
    {
        var reader = new FakeArtifactReader(new Dictionary<string, byte[]>
        {
            ["dp-artifact"] = [1, 2, 3, 4],
            ["tp-artifact"] = [9, 8, 7, 6],
        });
        writer = new FakeOutputWriter();
        return new CompositionRunService(reader, new FakeClock([FirstTimestamp, SecondTimestamp]), writer);
    }

    private static CompositionRunRequest CreateRequest()
    {
        CompositionProfileDefinition profile = BuiltInStandardMergeProfiles.SyntheticStandardMerge;
        ProfileCompileResult compile = CompositionProfileCompiler.Compile(profile, []);
        return new CompositionRunRequest(
            "run-standard-synthetic",
            ToRunProfile(profile),
            compile.Plan!,
            new Dictionary<string, string>
            {
                ["dp-input"] = "dp-artifact",
                ["tp-input"] = "tp-artifact",
            },
            profile.DefaultOutputFileName);
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
