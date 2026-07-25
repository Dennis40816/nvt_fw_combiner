using NvtFwCombiner.Application.Support;

namespace NvtFwCombiner.Bootstrap;

public static partial class WorkbenchCompositionService
{
    /// <summary>Creates the current headless support/reporting projection.</summary>
    public static SupportMatrix GetSupportMatrix()
    {
        return CurrentSupportMatrixCatalog.Create();
    }
}
