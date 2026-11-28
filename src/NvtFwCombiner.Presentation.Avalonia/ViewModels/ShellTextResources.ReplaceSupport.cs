namespace NvtFwCombiner.Presentation.Avalonia.ViewModels;

internal sealed partial class ShellTextResources
{
    public string GetReplaceNotSupportedStatus(string icId)
    {
        return SelectLanguage(
            "Replace is unavailable for the selected IC and mode; choose another IC or mode.",
            "所選 IC 與模式無法使用 Replace，請選擇其他 IC 或模式。") + Environment.NewLine + icId;
    }
}
