using NvtFwCombiner.Application.Ports;

namespace NvtFwCombiner.Bootstrap;

/// <summary>Warms immutable Replace runtime state without reading user firmware or starting a processor.</summary>
public static class WorkbenchRuntimePrewarmer
{
    private const string HotCtrlRamBundleId = "nt51927-ctrlram-replace-candidate";
    private static readonly Lazy<Task> ProcessLifetime = new(
        PrewarmCoreAsync,
        LazyThreadSafetyMode.ExecutionAndPublication);

    /// <summary>Initializes immutable Replace catalogs and processor discovery outside the caller thread.</summary>
    public static Task PrewarmAsync(CancellationToken cancellationToken = default)
    {
        return ProcessLifetime.Value.WaitAsync(cancellationToken);
    }

    private static async Task PrewarmCoreAsync()
    {
        Task<IExternalProcessor?> externalProcessor = ExternalProcessorFactory.PrewarmAsync(CancellationToken.None);
        var profiles = Task.Run(PrewarmProfiles);
        await Task.WhenAll(externalProcessor, profiles).ConfigureAwait(false);
    }

    private static void PrewarmProfiles()
    {
        BuiltInV2Bundle hotBundle = BuiltInV2BundleRegistry.All[HotCtrlRamBundleId];
        hotBundle.PrewarmCatalog();
        hotBundle.PrewarmRuntimeReferenceReplaceCompiler();
        _ = BuiltInTpFlashMapCatalog.IcIds.Count;
        foreach (KeyValuePair<string, BuiltInV2Bundle> entry in BuiltInV2BundleRegistry.All
                     .Where(static entry => entry.Key.Contains("-ctrlram-replace-", StringComparison.Ordinal))
                     .Where(static entry => !StringComparer.Ordinal.Equals(entry.Key, HotCtrlRamBundleId))
                     .OrderBy(static entry => entry.Key, StringComparer.Ordinal))
        {
            entry.Value.PrewarmCatalog();
        }
    }
}
