using NvtFwCombiner.Infrastructure.ExternalTools;
using NvtFwCombiner.TestSupport;

namespace NvtFwCombiner.Infrastructure.Tests.ExternalTools;

/// <summary>Locks bounded full-content verification for immutable external-processor artifacts.</summary>
public sealed class StagedArtifactFileVerifierTests
{
    private const int MultiBufferLength = (128 * 1024 * 2) + 17;

    /// <summary>Verifies equality is preserved across more than two comparison-buffer windows.</summary>
    [Fact]
    public async Task MatchesExactArtifactAcrossMultipleReads()
    {
        using var workspace = TempWorkspace.Create("nvt-staged-artifact-verify");
        byte[] expected = CreateBytes(MultiBufferLength);
        string path = workspace.Write("artifact.bin", expected);

        bool matches = await StagedArtifactFileVerifier.MatchesAsync(
            path,
            expected,
            TestContext.Current.CancellationToken);

        Assert.True(matches);
    }

    /// <summary>Verifies first, boundary, and final-byte mutations fail the immutable-artifact gate.</summary>
    [Theory]
    [InlineData(0)]
    [InlineData(128 * 1024)]
    [InlineData(MultiBufferLength - 1)]
    public async Task RejectsSameLengthMutationAtAnyReadBoundary(int changedOffset)
    {
        using var workspace = TempWorkspace.Create("nvt-staged-artifact-mutation");
        byte[] expected = CreateBytes(MultiBufferLength);
        byte[] actual = [.. expected];
        actual[changedOffset] ^= 0xFF;
        string path = workspace.Write("artifact.bin", actual);

        bool matches = await StagedArtifactFileVerifier.MatchesAsync(
            path,
            expected,
            TestContext.Current.CancellationToken);

        Assert.False(matches);
    }

    /// <summary>Verifies both truncation and extension fail before content can be accepted.</summary>
    [Theory]
    [InlineData(-1)]
    [InlineData(1)]
    public async Task RejectsChangedArtifactLength(int lengthDelta)
    {
        using var workspace = TempWorkspace.Create("nvt-staged-artifact-length");
        byte[] expected = CreateBytes((128 * 1024) + 1);
        byte[] actual = new byte[expected.Length + lengthDelta];
        expected.AsSpan(0, Math.Min(expected.Length, actual.Length)).CopyTo(actual);
        string path = workspace.Write("artifact.bin", actual);

        bool matches = await StagedArtifactFileVerifier.MatchesAsync(
            path,
            expected,
            TestContext.Current.CancellationToken);

        Assert.False(matches);
    }

    /// <summary>Verifies cancellation wins before any filesystem access is attempted.</summary>
    [Fact]
    public async Task RejectsPreCancelledVerificationBeforeOpeningFile()
    {
        using var cancellationSource = new CancellationTokenSource();
        cancellationSource.Cancel();

        _ = await Assert.ThrowsAsync<OperationCanceledException>(() =>
            StagedArtifactFileVerifier.MatchesAsync(
                "missing-artifact.bin",
                new byte[] { 0x01 },
                cancellationSource.Token).AsTask());
    }

    private static byte[] CreateBytes(int length)
    {
        byte[] bytes = new byte[length];
        for (int index = 0; index < bytes.Length; index++)
        {
            bytes[index] = checked((byte)(index % 251));
        }

        return bytes;
    }
}
