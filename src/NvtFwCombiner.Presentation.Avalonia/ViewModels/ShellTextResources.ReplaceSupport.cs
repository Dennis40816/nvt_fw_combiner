namespace NvtFwCombiner.Presentation.Avalonia.ViewModels;

public sealed partial class ShellTextResources
{
    /// <summary>Gets the localized support-policy status for a blocked Replace IC.</summary>
    public string GetReplaceNotSupportedStatus(string icId)
    {
        return SelectLanguage($"{icId} Replace: Not Supported.", $"{icId} Replace：不支援。");
    }
}
