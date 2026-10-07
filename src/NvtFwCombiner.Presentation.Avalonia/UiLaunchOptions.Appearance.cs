namespace NvtFwCombiner.Presentation.Avalonia;

internal sealed partial class UiLaunchOptions
{
    static partial void AppendAppearanceHelp(List<string> entries)
    {
        entries.Add("""
          --theme light|dark|system             Temporary appearance for this launch.
          --language en|zh-Hant                 Temporary UI language for this launch.
          --motion full|reduced                 Temporary motion preference for this launch.
        Appearance overrides are excluded from saved preferences, including later unrelated edits.
        """);
    }
}
