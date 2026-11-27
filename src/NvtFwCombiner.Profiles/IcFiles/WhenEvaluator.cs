// Copyright (c) 2026 Dennis Liu. All rights reserved.

namespace NvtFwCombiner.Profiles.IcFiles;

// Object conditions only. The future binder owns conversion of document scalars to T.
internal abstract record WhenCondition<T>(string FactId, SourceMark Mark)
{
    internal sealed record Present(string FactId, SourceMark Mark) : WhenCondition<T>(FactId, Mark);

    internal sealed record EqualTo(string FactId, T Expected, SourceMark Mark) : WhenCondition<T>(FactId, Mark);

    internal sealed record Interval(string FactId, T MinimumInclusive, T MaximumExclusive, SourceMark Mark)
        : WhenCondition<T>(FactId, Mark);
}

internal sealed record WhenVariant<T, TResult>(string Id, WhenCondition<T> Condition, TResult Value);

internal static class WhenEvaluator
{
    // An explicit comparer keeps value ordering with the resolved fact's semantic owner.
    internal static IcFileResult<bool> Evaluate<T>(
        WhenCondition<T> condition, IReadOnlyDictionary<string, T> facts, IComparer<T> comparer)
    {
        bool present = facts.TryGetValue(condition.FactId, out T? value) && value is not null;
        return condition switch
        {
            WhenCondition<T>.Present => new IcFileResult<bool>.Success(present),
            WhenCondition<T>.Interval interval
                when comparer.Compare(interval.MinimumInclusive, interval.MaximumExclusive) >= 0 =>
                Failure<bool>(IcFileErrorCode.InvalidCondition, condition.Mark, condition.FactId),
            _ when !present => Failure<bool>(IcFileErrorCode.MissingFact, condition.Mark, condition.FactId),
            WhenCondition<T>.EqualTo equal => new IcFileResult<bool>.Success(EqualityComparer<T>.Default.Equals(value, equal.Expected)),
            WhenCondition<T>.Interval bounds => new IcFileResult<bool>.Success(
                comparer.Compare(value, bounds.MinimumInclusive) >= 0 && comparer.Compare(value, bounds.MaximumExclusive) < 0),
            _ => Failure<bool>(IcFileErrorCode.InvalidCondition, condition.Mark, condition.FactId),
        };
    }

    internal static IcFileResult<TResult> SelectExactlyOne<T, TResult>(
        IReadOnlyList<WhenVariant<T, TResult>> variants,
        IReadOnlyDictionary<string, T> facts,
        IComparer<T> comparer,
        SourceMark mark)
    {
        List<WhenVariant<T, TResult>> matches = [];
        foreach (WhenVariant<T, TResult> variant in variants)
        {
            IcFileResult<bool> evaluated = Evaluate(variant.Condition, facts, comparer);
            if (evaluated is IcFileResult<bool>.Failure failure)
            {
                return new IcFileResult<TResult>.Failure(failure.Error);
            }

            if (((IcFileResult<bool>.Success)evaluated).Value)
            {
                matches.Add(variant);
            }
        }

        return matches.Count switch
        {
            0 => Failure<TResult>(IcFileErrorCode.NoVariant, mark),
            1 => new IcFileResult<TResult>.Success(matches[0].Value),
            _ => new IcFileResult<TResult>.Failure(new IcFileError(
                IcFileErrorCode.AmbiguousVariant, mark, matches[0].Id, matches[1].Id)),
        };
    }

    private static IcFileResult<T>.Failure Failure<T>(IcFileErrorCode code, SourceMark mark, string? subject = null)
    {
        return new(new IcFileError(code, mark, subject));
    }
}
