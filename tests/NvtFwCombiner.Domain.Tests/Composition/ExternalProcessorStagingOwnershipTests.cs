using System.Runtime.InteropServices;
using NvtFwCombiner.Domain.Composition;

namespace NvtFwCombiner.Domain.Tests.Composition;

/// <summary>External-processor staged-range ownership and allocation tests.</summary>
public sealed class ExternalProcessorStagingOwnershipTests
{
    private const int ArtifactByteCount = 4 * 1024 * 1024;

    /// <summary>An engine-created staged artifact retains its single owned range snapshot.</summary>
    [Fact]
    public async Task StagedArtifactRetainsOneOwnedRangeSnapshot()
    {
        _ = await ExecuteAsync(artifactByteCount: 1);
        CompositionPlan plan = CreatePlan(ArtifactByteCount);
        CompositionExecutionInput input = CreateInput(ArtifactByteCount);

        long allocatedBefore = GC.GetAllocatedBytesForCurrentThread();
        CompositionExecutionResult result = await CompositionEngine.ExecuteAsync(
            plan,
            input,
            (_, processorInput, stagedSources, stagedArtifacts, _) =>
            {
                Assert.Empty(stagedSources);
                ExternalProcessorStagedArtifact artifact = Assert.Single(stagedArtifacts);
                Assert.Equal(ArtifactByteCount, artifact.Bytes.Length);
                Assert.Equal(0x5A, artifact.Bytes.Span[0]);
                Assert.Equal(0x6B, artifact.Bytes.Span[^1]);
                return ValueTask.FromResult(CompositionExternalProcessorResult.Success(processorInput));
            },
            TestContext.Current.CancellationToken);
        long allocated = GC.GetAllocatedBytesForCurrentThread() - allocatedBefore;

        TestContext.Current.TestOutputHelper?.WriteLine(
            $"STAGED_ARTIFACT_OWNERSHIP artifactBytes={ArtifactByteCount} allocated={allocated}");
        Assert.Equal(CompositionExecutionStatus.Succeeded, result.Status);
        Assert.InRange(allocated, 0, ArtifactByteCount + 32_768L);
    }

    /// <summary>Public staged values still isolate bytes supplied by arbitrary callers.</summary>
    [Fact]
    public void PublicStagedValuesCopyCallerBytes()
    {
        byte[] artifactCaller = [0x10, 0x20];
        byte[] sourceCaller = [0x30, 0x40];
        var artifact = new ExternalProcessorStagedArtifact("artifact", artifactCaller);
        var source = new ExternalProcessorStagedSource(new ByteRange(0, 2), sourceCaller);

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

    /// <summary>Internal factories adopt only arrays freshly created by the engine.</summary>
    [Fact]
    public void OwnedFactoriesRetainEngineCreatedArrays()
    {
        byte[] artifactBytes = [0x10, 0x20];
        byte[] sourceBytes = [0x30, 0x40];
        var artifact =
            ExternalProcessorStagedArtifact.FromOwnedBytes("artifact", artifactBytes);
        var source =
            ExternalProcessorStagedSource.FromOwnedBytes(new ByteRange(0, 2), sourceBytes);

        Assert.True(MemoryMarshal.TryGetArray(artifact.Bytes, out ArraySegment<byte> artifactBacking));
        Assert.True(MemoryMarshal.TryGetArray(source.Bytes, out ArraySegment<byte> sourceBacking));
        Assert.Same(artifactBytes, artifactBacking.Array);
        Assert.Same(sourceBytes, sourceBacking.Array);
    }

    private static ValueTask<CompositionExecutionResult> ExecuteAsync(int artifactByteCount)
    {
        return CompositionEngine.ExecuteAsync(
            CreatePlan(artifactByteCount),
            CreateInput(artifactByteCount),
            (_, processorInput, _, _, _) =>
                ValueTask.FromResult(CompositionExternalProcessorResult.Success(processorInput)),
            CancellationToken.None);
    }

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
