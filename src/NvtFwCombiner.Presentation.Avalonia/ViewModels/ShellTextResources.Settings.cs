// Resource bags intentionally expose many concise bindable labels; XML comments on each label add noise.
#pragma warning disable CS1591

namespace NvtFwCombiner.Presentation.Avalonia.ViewModels;

public sealed partial class ShellTextResources
{
    public string SettingsCatalogTitle { get; private init; } = string.Empty;

    public string SettingsCatalogSubtitle { get; private init; } = string.Empty;

    public string SettingsRuntimeChecksTitle { get; private init; } = string.Empty;

    public string SettingsRuntimeChecksSubtitle { get; private init; } = string.Empty;

    public string SettingsDiagnosticsTitle { get; private init; } = string.Empty;

    public string SettingsDiagnosticsSubtitle { get; private init; } = string.Empty;

    public string SettingsPreferencesTitle { get; private init; } = string.Empty;

    public string SettingsPreferencesSubtitle { get; private init; } = string.Empty;

    public string ThemeLabel { get; private init; } = string.Empty;

    public string StrictnessLabel { get; private init; } = string.Empty;

    public string LanguageLabel { get; private init; } = string.Empty;

    public string SettingsInspectorKicker { get; private init; } = string.Empty;

    public string SettingsReadinessTitle { get; private init; } = string.Empty;
}

#pragma warning restore CS1591
