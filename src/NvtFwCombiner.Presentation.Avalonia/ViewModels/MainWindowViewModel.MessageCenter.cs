using NvtFwCombiner.Application.Capabilities;
using System.ComponentModel;

namespace NvtFwCombiner.Presentation.Avalonia.ViewModels;

internal sealed partial class MainWindowViewModel
{
    public MessageCenterViewModel MessageCenter { get; }

    public bool HasMergeBuildBlocker => !Merge.CanBuildMerge &&
        (MessageCenter.IsGlobalBuildBlocked || Merge.PrimaryBuildBlocker is not null);

    public string MergeBuildBlockerText => FormatBuildBlocker(Merge.PrimaryBuildBlocker);

    public bool HasReplaceBuildBlocker => !Replace.CanBuildReplace &&
        (MessageCenter.IsGlobalBuildBlocked || Replace.PrimaryBuildBlocker is not null);

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

        PresentationObserver.Invoke(RefreshCommandState);
        PresentationObserver.Invoke(() => OnPropertyChanged(nameof(HasMergeBuildBlocker)));
        PresentationObserver.Invoke(() => OnPropertyChanged(nameof(MergeBuildBlockerText)));
        PresentationObserver.Invoke(() => OnPropertyChanged(nameof(HasReplaceBuildBlocker)));
        PresentationObserver.Invoke(() => OnPropertyChanged(nameof(ReplaceBuildBlockerText)));
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
