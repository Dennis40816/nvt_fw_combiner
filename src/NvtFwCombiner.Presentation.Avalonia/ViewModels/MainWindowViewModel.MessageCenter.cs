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
        CapabilityActionBlocker? blocker = availability.PrimaryBlocker;
        // Application owns the order and count; Presentation enriches only the exact inspected slot.
        IssueCardViewModel? input = MessageCenter.IsGlobalBuildBlocked ? null : FindInputCard(blocker, slots);
        int additional = MessageCenter.IsGlobalBuildBlocked ? 0 : Math.Max(0, availability.Blockers.Count - 1);
        string next = additional > 0
            ? FindInputCard(availability.Blockers[1], slots)?.Summary ?? Text.FormatCapabilityActionBlocker(availability.Blockers[1])
            : string.Empty;
        return new IssueCardViewModel(Text.BuildIssueCaption, Text.BuildBlockedTitle,
            input?.Summary ?? FormatBuildBlocker(blocker), string.Empty, input?.Action ?? string.Empty,
            IsError: true, DiagnosticCode: input?.DiagnosticCode ?? (MessageCenter.IsGlobalBuildBlocked ? string.Empty : blocker?.Code ?? string.Empty),
            IsBuildStatus: true, AdditionalBlockerCount: additional,
            AdditionalBlockerText: additional > 0 ? Text.FormatAdditionalBuildBlockers(additional, next) : string.Empty);
    }

    private static IssueCardViewModel? FindInputCard(CapabilityActionBlocker? blocker, IEnumerable<FirmwareSlotViewModel> slots)
    {
        return blocker?.Code == CapabilityActionReadinessIssueCodes.InputBlocked
            ? slots.FirstOrDefault(slot => slot.InspectedSlotId == blocker.SubjectId && slot.BlocksBuild)?.IssueCard
            : null;
    }

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
