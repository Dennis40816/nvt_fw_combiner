using NvtFwCombiner.Domain.Composition;
using NvtFwCombiner.Infrastructure.Files;
using NvtFwCombiner.Infrastructure.ExternalTools;
using NvtFwCombiner.Infrastructure.Time;

namespace NvtFwCombiner.Bootstrap.Tests;

internal static class CompositionExecutionTestSupport
{
    internal static ICompositionExecution Create(
        CanonicalTestContext canonical)
    {
        return Create(
            canonical,
            () =>
            {
                ExternalProcessorEnvironmentLease lease =
                    ExternalProcessorEnvironmentTestSupport.AcquireCurrent();
                return new CompositionExternalProcessorLease(
                    lease.Generation,
                    lease.Processor);
            },
            ExternalProcessorEnvironmentTestSupport.IsCurrent);
    }

    internal static ICompositionExecution Create(
        CanonicalTestContext canonical,
        Func<CompositionExternalProcessorLease> acquireExternalProcessor,
        Func<long, bool> generationIsCurrent)
    {
        return new CompositionExecutionExperience(
            canonical.Catalog,
            new ProtectedCompositionDestinationProvider(),
            acquireExternalProcessor,
            generationIsCurrent,
            new SystemClock());
    }

    internal static string FormatIssues(IEnumerable<CompositionIssue> issues)
    {
        return string.Join(
            Environment.NewLine,
            issues.Select(static issue => $"{issue.Code}: {issue.Message}"));
    }
}
