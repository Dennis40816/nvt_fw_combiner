using System.Runtime.InteropServices;
using NvtFwCombiner.Domain.Composition;

namespace NvtFwCombiner.Domain.Tests.Composition;

/// <summary>External-processor staged-range ownership and allocation tests.</summary>
public sealed class ExternalProcessorStagingOwnershipTests
{
    private const int ArtifactByteCount = 4 * 1024 * 1024;

    /// <summary>An engine-created staged artifact retains its single owned range snapshot.</summary>
    [Fact]
    public void StagedArtifactRetainsOneOwnedRangeSnapshot()
    {
        _ = MeasureStagingAllocation(artifactByteCount: 1);
        StagingObservation baseline = MeasureStagingAllocation(artifactByteCount: 1);
        StagingObservation largeArtifact = MeasureStagingAllocation(ArtifactByteCount);
        long incrementalAllocation = largeArtifact.AllocatedBytes - baseline.AllocatedBytes;

        TestContext.Current.TestOutputHelper?.WriteLine(
            $"STAGED_ARTIFACT_OWNERSHIP artifactBytes={ArtifactByteCount} " +
            $"baseline={baseline.AllocatedBytes} large={largeArtifact.AllocatedBytes} " +
            $"incremental={incrementalAllocation}");
        Assert.Equal(CompositionExecutionStatus.Succeeded, baseline.Status);
        Assert.Equal(CompositionExecutionStatus.Succeeded, largeArtifact.Status);
        Assert.Equal(0, baseline.StagedSourceCount);
        Assert.Equal(0, largeArtifact.StagedSourceCount);
        Assert.Equal(1, baseline.StagedArtifactCount);
        Assert.Equal(1, largeArtifact.StagedArtifactCount);
        Assert.Equal(ArtifactByteCount, largeArtifact.ArtifactLength);
        Assert.Equal(0x5A, largeArtifact.FirstByte);
        Assert.Equal(0x6B, largeArtifact.LastByte);

        // Compare equal execution paths after warm-up. The upper bound rejects a
        // second artifact-sized copy without treating lower runtime allocation as a
        // failure; JIT and GC bookkeeping vary across otherwise equivalent runners.
        Assert.InRange(
            incrementalAllocation,
            0L,
            ArtifactByteCount + (ArtifactByteCount / 2L));
    }

    /// <summary>Public staged values still isolate bytes supplied by arbitrary callers.</summary>
    [Fact]
    public void PublicStagedValuesCopyCallerBytes()
    {
        byte[] artifactCaller = [0x10, 0x20];
        byte[] sourceCaller = [0x30, 0x40];
        var artifact = new ExternalProcessorStagedArtifact(
            "artifact",
            new ReadOnlyMemory<byte>(artifactCaller));
        var source = new ExternalProcessorStagedSource(
            new ByteRange(0, 2),
            new ReadOnlyMemory<byte>(sourceCaller));

        artifactCaller.AsSpan().Fill(0xFF);
        sourceCaller.AsSpan().Fill(0xEE);

        Assert.Equal([0x10, 0x20], artifact.Bytes.ToArray());
        Assert.Equal([0x30, 0x40], source.Bytes.ToArray());
    }

    /// <summary>Public validation retains the original parameter ownership and ordering.</summary>
    [Fact]
    public void PublicStagedValuesPreserveValidationParameters()
    {
        ArgumentException artifactIssue = Assert.Throws<ArgumentException>(() =>
            new ExternalProcessorStagedArtifact("INVALID", ReadOnlyMemory<byte>.Empty));
        ArgumentException sourceIssue = Assert.Throws<ArgumentException>(() =>
            new ExternalProcessorStagedSource(new ByteRange(0, 2), ReadOnlyMemory<byte>.Empty));

        Assert.Equal("artifactId", artifactIssue.ParamName);
        Assert.Equal("bytes", sourceIssue.ParamName);
    }

    /// <summary>Internal constructors adopt only arrays freshly created by the engine.</summary>
    [Fact]
    public void InternalConstructorsRetainEngineCreatedArrays()
    {
        byte[] artifactBytes = [0x10, 0x20];
        byte[] sourceBytes = [0x30, 0x40];
        var artifact = new ExternalProcessorStagedArtifact("artifact", artifactBytes);
        var source = new ExternalProcessorStagedSource(new ByteRange(0, 2), sourceBytes);

        Assert.True(MemoryMarshal.TryGetArray(artifact.Bytes, out ArraySegment<byte> artifactBacking));
        Assert.True(MemoryMarshal.TryGetArray(source.Bytes, out ArraySegment<byte> sourceBacking));
        Assert.Same(artifactBytes, artifactBacking.Array);
        Assert.Same(sourceBytes, sourceBacking.Array);
    }

    private static StagingObservation MeasureStagingAllocation(int artifactByteCount)
    {
        CompositionPlan plan = CreatePlan(artifactByteCount);
        CompositionExecutionInput input = CreateInput(artifactByteCount);
        int stagedSourceCount = -1;
        int stagedArtifactCount = -1;
        int artifactLength = -1;
        byte firstByte = 0;
        byte lastByte = 0;

        ValueTask<CompositionExternalProcessorResult> ObserveProcessor(
            CompositionOperation operation,
            ReadOnlyMemory<byte> processorInput,
            IReadOnlyList<ExternalProcessorStagedSource> stagedSources,
            IReadOnlyList<ExternalProcessorStagedArtifact> stagedArtifacts,
            CancellationToken cancellationToken)
        {
            _ = operation;
            _ = cancellationToken;
            stagedSourceCount = stagedSources.Count;
            stagedArtifactCount = stagedArtifacts.Count;
            if (stagedArtifacts.Count == 1)
            {
                ReadOnlySpan<byte> artifactBytes = stagedArtifacts[0].Bytes.Span;
                artifactLength = artifactBytes.Length;
                firstByte = artifactBytes[0];
                lastByte = artifactBytes[^1];
            }

            return ValueTask.FromResult(CompositionExternalProcessorResult.Success(processorInput));
        }

        CompositionExternalProcessor processor = ObserveProcessor;

        long allocatedBefore = GC.GetAllocatedBytesForCurrentThread();
        CompositionExecutionResult result = CompositionEngine.ExecuteAsync(
                plan,
                input,
                processor,
                CancellationToken.None)
            .AsTask()
            .GetAwaiter()
            .GetResult();
        long allocatedBytes = GC.GetAllocatedBytesForCurrentThread() - allocatedBefore;

        return new StagingObservation(
            allocatedBytes,
            result.Status,
            stagedSourceCount,
            stagedArtifactCount,
            artifactLength,
            firstByte,
            lastByte);
    }

    private readonly record struct StagingObservation(
        long AllocatedBytes,
        CompositionExecutionStatus Status,
        int StagedSourceCount,
        int StagedArtifactCount,
        int ArtifactLength,
        byte FirstByte,
        byte LastByte);

    private static CompositionPlan CreatePlan(int artifactByteCount)
    {
        var invocation = new ExternalProcessorInvocation(
            "processor-v1",
            "tool-v1",
            [new ByteRange(0, 1)],
            [new ByteRange(0, 1)],
            stagedArtifactBindings:
            [
                new ExternalProcessorStagedArtifactBinding(
                    "large-artifact",
                    "artifact-input",
                    new ByteRange(0, artifactByteCount)),
            ]);
        return new CompositionPlan(
            ImageInitialization.Blank("output-image", 1, 0),
            [
                new AddressSpace("artifact-input", artifactByteCount, AddressSpaceMutability.Immutable),
                new AddressSpace("output-image", 1, AddressSpaceMutability.Mutable),
            ],
            [
                CompositionOperation.RunExternalProcessor(
                    "run-processor",
                    10,
                    "output-image",
                    new ByteRange(0, 1),
                    invocation,
                    OverlapPolicy.Reject,
                    "Stage one large immutable artifact."),
            ]);
    }

    private static CompositionExecutionInput CreateInput(int artifactByteCount)
    {
        byte[] bytes = new byte[artifactByteCount];
        bytes[0] = 0x5A;
        bytes[^1] = 0x6B;
        return new CompositionExecutionInput(new Dictionary<string, byte[]>
        {
            ["artifact-input"] = bytes,
        });
    }
}
