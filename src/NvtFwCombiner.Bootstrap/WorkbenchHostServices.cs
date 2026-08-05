using NvtFwCombiner.Application.Capabilities;
using NvtFwCombiner.Application.Ports;
using NvtFwCombiner.Infrastructure.Shell;

namespace NvtFwCombiner.Bootstrap;

/// <summary>Creates host adapters used by the desktop composition root.</summary>
public static class WorkbenchHostServices
{
    internal static CanonicalCapabilityCatalogHost CanonicalCapabilities { get; } =
        new(new CanonicalCapabilityCatalogMigrationSource());

    /// <summary>Creates the focused query over the host's single canonical catalog publication.</summary>
    public static ICanonicalSupportMatrixQuery CreateCanonicalSupportMatrixQuery()
    {
        return new CanonicalSupportMatrixQuery(
            static () => CanonicalCapabilities.LatestReload);
    }

    /// <summary>Warms the canonical catalog on the caller-owned background worker.</summary>
    public static void WarmCanonicalCapabilities(CancellationToken cancellationToken)
    {
        CanonicalCapabilities.Warm(cancellationToken);
    }

    /// <summary>Creates the constrained Windows file-reveal adapter.</summary>
    public static IFileRevealService CreateFileRevealService()
    {
        return new WindowsExplorerFileRevealService();
    }
}
