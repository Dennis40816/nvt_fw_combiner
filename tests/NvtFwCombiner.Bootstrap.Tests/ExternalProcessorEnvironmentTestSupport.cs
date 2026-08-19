using NvtFwCombiner.Infrastructure.ExternalTools;

namespace NvtFwCombiner.Bootstrap.Tests;

internal static class ExternalProcessorEnvironmentTestSupport
{
    private static readonly Lazy<ExternalProcessorEnvironmentLoader> Loaded = new(CreateLoaded);

    internal static ExternalProcessorEnvironmentLease AcquireCurrent()
    {
        return Loaded.Value.AcquireCurrent();
    }

    internal static bool IsCurrent(long generation)
    {
        return Loaded.Value.IsCurrent(generation);
    }

    private static ExternalProcessorEnvironmentLoader CreateLoaded()
    {
        var loader = new ExternalProcessorEnvironmentLoader();
        return ((Application.ExternalTools.IExternalProcessorEnvironmentLoader)loader)
            .LoadToCompletionAsync(null, CancellationToken.None)
            .GetAwaiter().GetResult().Succeeded
            ? loader
            : throw new InvalidOperationException("The test external environment did not load.");
    }
}
