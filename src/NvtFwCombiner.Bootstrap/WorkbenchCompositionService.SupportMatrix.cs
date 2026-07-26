using NvtFwCombiner.Application.Support;

namespace NvtFwCombiner.Bootstrap;

public static partial class WorkbenchCompositionService
{
    /// <summary>
    /// Creates the temporary headless characterization projection pending
    /// CanonicalCapabilityCatalog and Workbench facade retirement.
    /// </summary>
    public static SupportMatrix GetSupportMatrix()
    {
        return CurrentSupportMatrixCatalog.Create();
    }
}
