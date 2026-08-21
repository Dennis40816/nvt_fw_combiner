using NvtFwCombiner.Application.VersionManagement;

namespace NvtFwCombiner.Presentation.Avalonia.ViewModels;

internal enum SettingsVersionPrimaryAction
{
    None,
    Install,
    Switch,
}

internal sealed record SettingsVersionRowViewModel(
    ManagedAppVersion Version,
    string VersionLabel,
    string StatusLabel,
    string PublishedLabel,
    string ReleaseNotes,
    SettingsVersionPrimaryAction PrimaryAction,
    string PrimaryActionLabel,
    string DeleteActionLabel,
    bool IsActive,
    bool IsInstalled,
    bool IsDamaged,
    bool CanDelete,
    bool IsLastKnownGood)
{
    public bool HasPrimaryAction => PrimaryAction != SettingsVersionPrimaryAction.None;
}
