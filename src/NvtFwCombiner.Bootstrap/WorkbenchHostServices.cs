using NvtFwCombiner.Application.Ports;
using NvtFwCombiner.Infrastructure.Shell;

namespace NvtFwCombiner.Bootstrap;

/// <summary>Creates host adapters used by the desktop composition root.</summary>
public static class WorkbenchHostServices
{
    /// <summary>Creates the constrained Windows file-reveal adapter.</summary>
    public static IFileRevealService CreateFileRevealService()
    {
        return new WindowsExplorerFileRevealService();
    }
}
