using NvtFwCombiner.Application.ExternalTools;
using NvtFwCombiner.Domain.Composition;

namespace NvtFwCombiner.Application.Tests.ExternalTools;

/// <summary>Tests external processor request invariants.</summary>
public sealed class ExternalProcessorRequestTests
{
    /// <summary>Rejects run ids that could resolve to the staging root or its parent.</summary>
    [Theory]
    [InlineData(".")]
    [InlineData("..")]
    [InlineData("../escape")]
    public void ConstructorRejectsTraversalLikeRunIds(string runId)
    {
        _ = Assert.Throws<ArgumentException>(() =>
        {
            _ = new ExternalProcessorRequest(
                runId,
                "processor",
                "tool",
                new byte[] { 0x01 },
                [new ByteRange(0, 1)]);
        });
    }

    /// <summary>Retains the compiler-resolved IC Count separately from the selector token.</summary>
    [Fact]
    public void ConstructorRetainsResolvedIcCount()
    {
        var request = new ExternalProcessorRequest(
            "run",
            "processor",
            "tool",
            new byte[] { 0x01 },
            [new ByteRange(0, 1)],
            resolvedIcCount: 4);

        Assert.Equal(4, request.ResolvedIcCount);
    }

    /// <summary>Rejects non-positive resolved IC Count values before an adapter can re-plan them.</summary>
    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void ConstructorRejectsNonPositiveResolvedIcCount(int resolvedIcCount)
    {
        _ = Assert.Throws<ArgumentOutOfRangeException>(() => new ExternalProcessorRequest(
            "run",
            "processor",
            "tool",
            new byte[] { 0x01 },
            [new ByteRange(0, 1)],
            resolvedIcCount: resolvedIcCount));
    }
}
