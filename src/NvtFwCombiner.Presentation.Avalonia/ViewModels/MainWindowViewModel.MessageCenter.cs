using NvtFwCombiner.Application.Capabilities;
using System.ComponentModel;

namespace NvtFwCombiner.Presentation.Avalonia.ViewModels;

internal sealed partial class MainWindowViewModel
{
    public MessageCenterViewModel MessageCenter { get; }

    public bool HasMergeBuildBlocker => !Merge.CanBuildMerge &&
        (MessageCenter.IsGlobalBuildBlocked || Merge.PrimaryBuildBlocker is not null);

    public string MergeBuildBlockerText => HasMergeBuildBlocker ? MergeBuildBlockerCard.AutomationText : string.Empty;

    public IssueCardViewModel MergeBuildBlockerCard => CreateBuildBlockerCard(Merge.BuildAvailability, Merge.MergeSlots);

    public bool HasReplaceBuildBlocker => !Replace.CanBuildReplace &&
        (MessageCenter.IsGlobalBuildBlocked || Replace.PrimaryBuildBlocker is not null);

    public string ReplaceBuildBlockerText => HasReplaceBuildBlocker ? ReplaceBuildBlockerCard.AutomationText : string.Empty;

    public IssueCardViewModel ReplaceBuildBlockerCard => CreateBuildBlockerCard(Replace.BuildAvailability, Replace.ReplaceSlots);

    private IssueCardViewModel CreateBuildBlockerCard(CapabilityActionAvailability availability, IEnumerable<FirmwareSlotViewModel> slots)
    {
        return IssueCardViewModel.FromBuildAvailability(availability, slots, Text,
            MessageCenter.IsGlobalBuildBlocked, MessageCenter.GlobalBuildBlockerText);
    }

    private bool IsGlobalBuildBlocked()
    {
        return MessageCenter.IsGlobalBuildBlocked;
    }

    private void MessageCenterDiagnosticsChanged(bool catalogPublicationChanged)
    {
        if (catalogPublicationChanged)
        {
            WorkflowSession.RefreshCanonicalCatalogState();
            NotifyCatalogWorkflowCommandStateChanged();
        }

        PresentationObserver.Invoke(() => RefreshCommandState(refreshReplaceReadiness: false));
        PresentationObserver.Invoke(() => OnPropertyChanged(nameof(HasMergeBuildBlocker)));
        PresentationObserver.Invoke(() => OnPropertyChanged(nameof(MergeBuildBlockerText)));
        PresentationObserver.Invoke(() => OnPropertyChanged(nameof(MergeBuildBlockerCard)));
        PresentationObserver.Invoke(() => OnPropertyChanged(nameof(HasReplaceBuildBlocker)));
        PresentationObserver.Invoke(() => OnPropertyChanged(nameof(ReplaceBuildBlockerText)));
        PresentationObserver.Invoke(() => OnPropertyChanged(nameof(ReplaceBuildBlockerCard)));
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
            OnPropertyChanged(nameof(MergeBuildBlockerCard));
            OnPropertyChanged(nameof(ReplaceBuildBlockerCard));
        }
    }
}
