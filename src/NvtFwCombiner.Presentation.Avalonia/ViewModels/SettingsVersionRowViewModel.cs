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
    public bool HasReleaseNotes => !string.IsNullOrWhiteSpace(ReleaseNotes);

    // Only the row's disclosure writes this transient state. New projections start collapsed.
    public bool IsReleaseNotesExpanded { get; set; }

    public bool HasPrimaryAction => PrimaryAction != SettingsVersionPrimaryAction.None;

    public bool IsAvailable => PrimaryAction == SettingsVersionPrimaryAction.Install;

    public bool IsHealthyInstalled => IsInstalled && !IsDamaged;
}
