using NvtFwCombiner.Application.Capabilities;
using System.ComponentModel;

namespace NvtFwCombiner.Presentation.Avalonia.ViewModels;

public sealed partial class MainWindowViewModel
{
    /// <summary>Compact reports and refreshable System Information entry.</summary>
    public MessageCenterViewModel MessageCenter { get; }

    /// <summary>True when a typed Merge blocker should own the focusable warning affordance.</summary>
    public bool HasMergeBuildBlocker => !Merge.CanBuildMerge &&
        (MessageCenter.IsGlobalBuildBlocked || Merge.PrimaryBuildBlocker is not null);

    /// <summary>Highest-priority global or Merge-local actionable blocker text.</summary>
    public string MergeBuildBlockerText => FormatBuildBlocker(Merge.PrimaryBuildBlocker);

    /// <summary>True when a typed Replace blocker should own the focusable warning affordance.</summary>
    public bool HasReplaceBuildBlocker => !Replace.CanBuildReplace &&
        (MessageCenter.IsGlobalBuildBlocked || Replace.PrimaryBuildBlocker is not null);

    /// <summary>Highest-priority global or Replace-local actionable blocker text.</summary>
    public string ReplaceBuildBlockerText => FormatBuildBlocker(Replace.PrimaryBuildBlocker);

    private string FormatBuildBlocker(CapabilityActionBlocker? local)
    {
        return MessageCenter.IsGlobalBuildBlocked
            ? MessageCenter.GlobalBuildBlockerText
            : local is null
                ? string.Empty
                : Text.FormatCapabilityActionBlocker(local);
    }

    private bool IsGlobalBuildBlocked()
    {
        return MessageCenter.IsGlobalBuildBlocked;
    }

    private void MessageCenterDiagnosticsChanged(bool catalogPublicationChanged)
    {
        if (catalogPublicationChanged && WorkflowSession.IsWorkflowLoaded)
        {
            WorkflowSession.RefreshCanonicalCatalogState();
        }

        RefreshCommandState();
        OnPropertyChanged(nameof(HasMergeBuildBlocker));
        OnPropertyChanged(nameof(MergeBuildBlockerText));
        OnPropertyChanged(nameof(HasReplaceBuildBlocker));
        OnPropertyChanged(nameof(ReplaceBuildBlockerText));
    }

    private void MessageCenter_OnPropertyChanged(
        object? sender,
        PropertyChangedEventArgs e)
    {
        OnPropertyChanged(nameof(MessageCenter));
        if (e.PropertyName == nameof(MessageCenterViewModel.IsOpen))
        {
            NotifyCompositionActionRailVisibilityChanged();
        }

        if (e.PropertyName is nameof(MessageCenterViewModel.Text) or
            nameof(MessageCenterViewModel.GlobalBuildBlockerText))
        {
            OnPropertyChanged(nameof(MergeBuildBlockerText));
            OnPropertyChanged(nameof(ReplaceBuildBlockerText));
        }
    }
}
