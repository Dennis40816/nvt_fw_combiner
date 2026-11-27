// Copyright (c) 2026 Dennis Liu. All rights reserved.

using NvtFwCombiner.Profiles.IcFiles;

namespace NvtFwCombiner.ProfileContract.Tests.IcFiles;

/// <summary>Verifies when Evaluator Tests.</summary>
public sealed class WhenEvaluatorTests
{
    private static readonly SourceMark Mark = new(2, 3);

    /// <summary>Verifies interval Includes Its Lower Bound And Excludes Its Upper Bound.</summary>
    [Theory]
    [InlineData(0, false)]
    [InlineData(1, true)]
    [InlineData(3, true)]
    [InlineData(4, false)]
    public void IntervalIncludesItsLowerBoundAndExcludesItsUpperBound(int value, bool expected)
    {
        WhenCondition<int> condition = new WhenCondition<int>.Interval("synthetic-fact", 1, 4, Mark);

        Assert.Equal(expected, IcFileAssertions.Success(WhenEvaluator.Evaluate(
            condition, new Dictionary<string, int> { ["synthetic-fact"] = value }, Comparer<int>.Default)));
    }

    /// <summary>Verifies present Means ANon Null Fact.</summary>
    [Theory]
    [InlineData("value", true)]
    [InlineData(null, false)]
    public void PresentMeansANonNullFact(string? value, bool expected)
    {
        WhenCondition<string?> condition = new WhenCondition<string?>.Present("synthetic-fact", Mark);

        Assert.Equal(expected, IcFileAssertions.Success(WhenEvaluator.Evaluate(
            condition, new Dictionary<string, string?> { ["synthetic-fact"] = value }, StringComparer.Ordinal)));
    }

    /// <summary>Verifies absent Fact Does Not Satisfy Present.</summary>
    [Fact]
    public void AbsentFactDoesNotSatisfyPresent()
    {
        Assert.False(IcFileAssertions.Success(WhenEvaluator.Evaluate(
            new WhenCondition<int>.Present("missing", Mark), new Dictionary<string, int>(), Comparer<int>.Default)));
    }

    /// <summary>Verifies equals Uses The Resolved Value Identity.</summary>
    [Theory]
    [InlineData("value", true)]
    [InlineData("Value", false)]
    public void EqualsUsesTheResolvedValueIdentity(string value, bool expected)
    {
        Assert.Equal(expected, IcFileAssertions.Success(WhenEvaluator.Evaluate(
            new WhenCondition<string>.EqualTo("synthetic-fact", "value", Mark),
            new Dictionary<string, string> { ["synthetic-fact"] = value }, StringComparer.Ordinal)));
    }

    /// <summary>Verifies empty And Reversed Intervals Are Typed Errors.</summary>
    [Theory]
    [InlineData(4, 4)]
    [InlineData(5, 4)]
    public void EmptyAndReversedIntervalsAreTypedErrors(int minimum, int maximum)
    {
        _ = IcFileAssertions.Failure(WhenEvaluator.Evaluate(
            new WhenCondition<int>.Interval("synthetic-fact", minimum, maximum, Mark),
            new Dictionary<string, int> { ["synthetic-fact"] = 4 }, Comparer<int>.Default), IcFileErrorCode.InvalidCondition);
    }

    /// <summary>Verifies aRequired Comparison Fact Cannot Be Silently Absent.</summary>
    [Fact]
    public void ARequiredComparisonFactCannotBeSilentlyAbsent()
    {
        _ = IcFileAssertions.Failure(WhenEvaluator.Evaluate(
            new WhenCondition<int>.EqualTo("missing", 1, Mark), new Dictionary<string, int>(), Comparer<int>.Default),
            IcFileErrorCode.MissingFact);
    }

    /// <summary>Verifies exactly One Variant Returns Its Whole Value.</summary>
    [Fact]
    public void ExactlyOneVariantReturnsItsWholeValue()
    {
        Assert.Equal("chosen", IcFileAssertions.Success(WhenEvaluator.SelectExactlyOne(
            [new WhenVariant<int, string>("first", new WhenCondition<int>.EqualTo("fact", 1, Mark), "chosen"),
             new WhenVariant<int, string>("second", new WhenCondition<int>.EqualTo("fact", 2, Mark), "other")],
            new Dictionary<string, int> { ["fact"] = 1 }, Comparer<int>.Default, Mark)));
    }

    /// <summary>Verifies no Matching Variant Is ATyped Error.</summary>
    [Fact]
    public void NoMatchingVariantIsATypedError()
    {
        _ = IcFileAssertions.Failure(WhenEvaluator.SelectExactlyOne(
            [new WhenVariant<int, string>("first", new WhenCondition<int>.EqualTo("fact", 2, Mark), "other")],
            new Dictionary<string, int> { ["fact"] = 1 }, Comparer<int>.Default, Mark), IcFileErrorCode.NoVariant);
    }

    /// <summary>Verifies multiple Matching Variants Name Both Alternatives.</summary>
    [Fact]
    public void MultipleMatchingVariantsNameBothAlternatives()
    {
        IcFileResult<string> result = WhenEvaluator.SelectExactlyOne(
            [new WhenVariant<int, string>("first", new WhenCondition<int>.Present("fact", Mark), "a"),
             new WhenVariant<int, string>("second", new WhenCondition<int>.EqualTo("fact", 1, Mark), "b")],
            new Dictionary<string, int> { ["fact"] = 1 }, Comparer<int>.Default, Mark);

        Assert.Equal(new IcFileError(IcFileErrorCode.AmbiguousVariant, Mark, "first", "second"),
            IcFileAssertions.Failure(result, IcFileErrorCode.AmbiguousVariant));
    }
}
