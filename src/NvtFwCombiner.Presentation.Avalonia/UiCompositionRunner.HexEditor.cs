using NvtFwCombiner.Bootstrap;

namespace NvtFwCombiner.Presentation.Avalonia;

public static partial class UiCompositionRunner
{
    /// <summary>Creates one isolated raw-BIN Hex Editor session for one shell window.</summary>
    public static WorkbenchRawBinaryEditorSession CreateRawBinaryEditorSession()
    {
        return new WorkbenchRawBinaryEditorSession();
    }
}
