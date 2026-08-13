namespace NvtFwCombiner.Presentation.Avalonia.ViewModels;

internal sealed partial class ShellTextResources
{
    public string GetReplaceNotSupportedStatus(string icId)
    {
        return SelectLanguage($"{icId} Replace: Not available.", $"{icId} Replace：尚未開放。");
    }
}
