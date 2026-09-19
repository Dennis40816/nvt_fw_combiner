using NvtFwCombiner.Application.Configuration;
using NvtFwCombiner.Infrastructure.ExternalTools;

namespace NvtFwCombiner.Bootstrap;

public sealed partial class CompositionHostServices
{
    /// <summary>Gets the host's shared Toolchain selection session and loads it once on first use.</summary>
    public Task<IToolchainRuntimeConfigurationSession> GetToolchainRuntimeConfigurationAsync(
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        lock (_configurationGate)
        {
            if (_toolchainConfiguration is null)
            {
                throw new InvalidOperationException("Toolchain configuration is unavailable in this host graph.");
            }
            if (_toolchainConfigurationLoad is null || _toolchainConfigurationLoad.IsFaulted || _toolchainConfigurationLoad.IsCanceled)
            {
                _toolchainConfigurationLoad = LoadToolchainConfigurationAsync(_toolchainConfiguration);
            }
            return _toolchainConfigurationLoad.WaitAsync(cancellationToken);
        }
    }

    private static async Task<IToolchainRuntimeConfigurationSession> LoadToolchainConfigurationAsync(
        IToolchainRuntimeConfigurationSession session)
    {
        if (session.Current.Status == ToolchainRuntimeConfigurationStatus.NotLoaded)
        {
            _ = await session.ReloadAsync(CancellationToken.None).ConfigureAwait(false);
        }
        return session;
    }

    /// <summary>Handles only the versioned internal runtime trust probe before ordinary host startup.</summary>
    public static bool TryHandleRuntimeTrustProbe(string[] arguments, TextWriter output, out int exitCode)
    {
        ArgumentNullException.ThrowIfNull(arguments);
        ArgumentNullException.ThrowIfNull(output);
        return RuntimeTrustProbeProcess.TryHandle(arguments, output, out exitCode);
    }
}
