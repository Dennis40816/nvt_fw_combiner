using NvtFwCombiner.Application.Composition;
using NvtFwCombiner.Domain.Composition;

namespace NvtFwCombiner.Application.Tests.Composition;

/// <summary>Verifies the immutable typed boundary for refusals raised before execution.</summary>
public sealed class CompositionPreRunRefusalExceptionTests
{
    /// <summary>The exception snapshots its issues and retains deterministic legacy-compatible text.</summary>
    [Fact]
    public void ConstructorCopiesIssuesAndPreservesDeterministicMessage()
    {
        var source = new List<CompositionIssue>
        {
            new("FORMAT_A", "First refusal."),
            new("FORMAT_B", "Second refusal."),
        };

        var exception = new CompositionPreRunRefusalException(source);
        source.Clear();

        Assert.Equal(2, exception.Issues.Count);
        Assert.Equal("FORMAT_A: First refusal. | FORMAT_B: Second refusal.", exception.Message);
        _ = Assert.Throws<NotSupportedException>(() => ((IList<CompositionIssue>)exception.Issues).Clear());
    }

    /// <summary>A refusal must always contain at least one structured issue.</summary>
    [Fact]
    public void ConstructorRejectsNullOrEmptyIssues()
    {
        _ = Assert.Throws<ArgumentNullException>(() => new CompositionPreRunRefusalException(null!));
        _ = Assert.Throws<ArgumentException>(() => new CompositionPreRunRefusalException([]));
    }
}
