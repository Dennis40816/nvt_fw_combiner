// Resource bags intentionally expose many concise bindable labels; XML comments on each label add noise.
#pragma warning disable CS1591

namespace NvtFwCombiner.Presentation.Avalonia.ViewModels;

public sealed partial class ShellTextResources
{
    public string SettingsOverviewTitle { get; private init; } = string.Empty;

    public string SettingsOverviewSubtitle { get; private init; } = string.Empty;

    public string SettingsCapabilitiesTitle { get; private init; } = string.Empty;

    public string SettingsCapabilitiesSubtitle { get; private init; } = string.Empty;

    public string SettingsPreferencesTitle { get; private init; } = string.Empty;

    public string SettingsPreferencesSubtitle { get; private init; } = string.Empty;

    public string ThemeLabel { get; private init; } = string.Empty;

    public string LanguageLabel { get; private init; } = string.Empty;
}

#pragma warning restore CS1591
