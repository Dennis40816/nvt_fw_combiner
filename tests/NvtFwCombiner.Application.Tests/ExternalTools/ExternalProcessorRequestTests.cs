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
    [InlineData(@"..\escape")]
    [InlineData(@"C:\escape")]
    [InlineData(@"\escape")]
    [InlineData("run:child")]
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
}
