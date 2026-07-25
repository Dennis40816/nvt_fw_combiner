using NvtFwCombiner.Application.Support;

namespace NvtFwCombiner.Bootstrap;

public static partial class WorkbenchCompositionService
{
    private static readonly Lazy<SupportMatrix> s_supportMatrix = new(CurrentSupportMatrixCatalog.Create);

    /// <summary>Gets the read-only support/reporting projection without changing workflow availability or execution admission.</summary>
    public static SupportMatrix GetSupportMatrix()
    {
        return s_supportMatrix.Value;
    }
}
