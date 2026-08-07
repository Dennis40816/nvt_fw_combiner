namespace NvtFwCombiner.Domain.Tests;

/// <summary>Protects the shared invariant guard's exception and lazy-message contracts.</summary>
public sealed class DomainInvariantTests
{
    /// <summary>Requires retain exact argument metadata.</summary>
    [Fact]
    public void RequirePreservesArgumentExceptionContract()
    {
        ArgumentException exception = Assert.Throws<ArgumentException>(() =>
            DomainInvariant.Require(false, "invalid value", "candidate"));

        Assert.Equal("candidate", exception.ParamName);
        Assert.StartsWith("invalid value", exception.Message, StringComparison.Ordinal);
    }

    /// <summary>Passing interpolated guards do not evaluate formatted values.</summary>
    [Fact]
    public void RejectSkipsInterpolatedValuesWhenInvariantPasses()
    {
        int evaluations = 0;

        DomainInvariant.Reject(
            false,
            $"first={Observe(ref evaluations)}, second={Observe(ref evaluations)}");

        Assert.Equal(0, evaluations);
    }

    /// <summary>Failing interpolated guards format once and preserve argument metadata.</summary>
    [Fact]
    public void RejectFormatsInterpolatedMessageWhenInvariantFails()
    {
        int evaluations = 0;

        ArgumentException exception = Assert.Throws<ArgumentException>(() =>
            DomainInvariant.Reject(
                true,
                $"first={Observe(ref evaluations)}, second={Observe(ref evaluations)}",
                "candidate"));

        Assert.Equal(2, evaluations);
        Assert.Equal("candidate", exception.ParamName);
        Assert.StartsWith("first=7, second=7", exception.Message, StringComparison.Ordinal);
    }

    private static int Observe(ref int evaluations)
    {
        evaluations++;
        return 7;
    }
}
