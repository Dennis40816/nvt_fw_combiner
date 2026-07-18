using NvtFwCombiner.Application.Composition;

namespace NvtFwCombiner.Bootstrap;

internal sealed class CompositionRunExecutionOutcome
{
    internal CompositionRunExecutionOutcome(
        CompositionRunResult result,
        CompositionRunExecutionMetrics metrics)
    {
        ArgumentNullException.ThrowIfNull(result);

        Result = result;
        Metrics = metrics;
    }

    internal CompositionRunResult Result { get; }

    internal CompositionRunExecutionMetrics Metrics { get; }
}
